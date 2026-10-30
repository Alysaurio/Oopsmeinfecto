using System;
using System.Collections.Generic;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Constructive level generator. It first plans a winning contact, then builds geometry around
    /// the mandatory trajectories, places hazards and validates the finished map.
    /// </summary>
    public static class GeneradorNivel
    {
        /// <summary>Optional diagnostic hook used by the external harness; null in the game.</summary>
        public static Action<string> Log;

        public static GenerationResult Generate(int levelIndex, int seed, GenerationSettings settingsInput)
        {
            var settings = settingsInput.Clone();
            settings.Sanitize();

            LevelDefinition fallback = null;
            int attempts = 0;

            for (int attempt = 0; attempt < settings.maxGenerationAttempts; attempt++)
            {
                attempts++;
                var rng = new SeededRandom(HashSeed(seed, attempt, levelIndex));

                int width = rng.Range(settings.minWidth, settings.maxWidth);
                int height = rng.Range(settings.minHeight, settings.maxHeight);
                var placement = GeneradorPatronMovimiento.Generate(width, height, rng);
                if (placement == null) continue;

                int zombieConciencia = rng.Range(settings.minZombieConciencia, settings.maxZombieConciencia);
                int maxEnergy = rng.Range(settings.minEnergy, settings.maxEnergy);
                var openBoard = new BoardData(width, height, TileType.Floor);
                Log?.Invoke($"attempt {attempt} {width}x{height} pattern={placement.TemplateName} zc={zombieConciencia} energy={maxEnergy}");

                var starts = BuildStartCandidates(openBoard, placement.Route, rng, 12, maxEnergy);
                for (int s = 0; s < starts.Count; s++)
                {
                    var start = starts[s];
                    var plans = PlanificadorSolucion.FindCandidates(
                        openBoard, start, placement, zombieConciencia,
                        settings.playerConciencia, maxEnergy, settings);
                    if (plans.Count == 0) continue;
                    Log?.Invoke($"  start {start}: {plans.Count} plans, best contactTurn={plans[0].ContactTurn} switch={plans[0].SwitchPosition} score={plans[0].Score}");

                    int planLimit = Math.Min(4, plans.Count);
                    for (int p = 0; p < planLimit; p++)
                    {
                        var plan = plans[p];
                        var core = BuildCore(plan, placement.Route, start);
                        if (core == null) continue;

                        int maxHazards = HazardPlacer.GetMaxHazards(width, height, settings.maxHazardsCap);
                        var radii = BuildRadii(settings.maxLateralExpansion);

                        for (int r = 0; r < radii.Count; r++)
                        {
                            var geometry = GeneradorGeometria.Build(
                                width, height, core, radii[r], settings.expansionDensity,
                                rng, maxHazards);
                            if (geometry == null) continue;

                            geometry.Board.SetTile(plan.SwitchPosition, TileType.Switch);

                            var protectedCells = new HashSet<GridPos>(core);
                            protectedCells.Add(plan.SwitchPosition);
                            protectedCells.Add(start);
                            protectedCells.Add(placement.Route[0]);
                            for (int i = 0; i < placement.Route.Length; i++) protectedCells.Add(placement.Route[i]);

                            var hazards = HazardPlacer.Place(
                                geometry.Board, geometry.OptionalCells, protectedCells,
                                settings.maxHazardsCap, rng);
                            if (hazards == null) continue;

                            var level = BuildDefinition(
                                levelIndex, seed, width, height, geometry.Board,
                                start, placement, zombieConciencia, maxEnergy,
                                settings, plan, hazards, attempts);

                            var structural = ValidadorNivel.Validate(level, settings, false);
                            if (!structural.Valid)
                            {
                                Log?.Invoke($"    radius {radii[r]}: structural invalid: {structural}");
                                continue;
                            }
                            Log?.Invoke($"    radius {radii[r]}: structural valid, checking uniqueness");

                            if (!settings.requireUniqueSolution)
                            {
                                level.UniqueConfirmed = false;
                                level.SolutionCount = -1;
                                level.GenerationMessage = "valid level; uniqueness not required";
                                return new GenerationResult
                                {
                                    Level = level,
                                    Success = true,
                                    UniqueConfirmed = false,
                                    Attempts = attempts,
                                    Message = level.GenerationMessage
                                };
                            }

                            var full = ValidadorNivel.Validate(level, settings, true);
                            Log?.Invoke($"    uniqueness: valid={full.Valid} count={full.SolutionCount} states={full.StatesExplored} msg={full}");
                            if (full.Valid && full.SolutionCount == 1)
                            {
                                level.UniqueConfirmed = true;
                                level.SolutionCount = 1;
                                level.GenerationMessage = "valid unique level";
                                return new GenerationResult
                                {
                                    Level = level,
                                    Success = true,
                                    UniqueConfirmed = true,
                                    Attempts = attempts,
                                    Message = level.GenerationMessage
                                };
                            }

                            if (full.Valid && fallback == null)
                            {
                                level.UniqueConfirmed = false;
                                level.SolutionCount = full.SolutionCount;
                                level.GenerationMessage = "valid fallback; " + full.Message;
                                fallback = level;
                            }
                        }
                    }
                }
            }

            if (fallback != null)
            {
                fallback.UsedFallback = true;
                fallback.GenerationAttemptsUsed = attempts;
                fallback.GenerationMessage += " | uniqueness not confirmed after " + attempts + " attempts";
                return new GenerationResult
                {
                    Level = fallback,
                    Success = true,
                    UniqueConfirmed = false,
                    Attempts = attempts,
                    Message = fallback.GenerationMessage
                };
            }

            var emergency = CreateEmergencyLevel(levelIndex, seed, settings, attempts);
            return new GenerationResult
            {
                Level = emergency,
                Success = true,
                UniqueConfirmed = false,
                Attempts = attempts,
                Message = emergency.GenerationMessage
            };
        }

        private static HashSet<GridPos> BuildCore(SolutionPlan plan, GridPos[] route, GridPos start)
        {
            var core = new HashSet<GridPos>();
            for (int i = 0; i < plan.ManualPositions.Count; i++) core.Add(plan.ManualPositions[i]);
            for (int i = 0; i < route.Length; i++) core.Add(route[i]);
            for (int i = 0; i < plan.AutoPositions.Count; i++) core.Add(plan.AutoPositions[i]);
            core.Add(start);
            core.Add(plan.SwitchPosition);
            return core;
        }

        private static List<GridPos> BuildStartCandidates(
            BoardData board, GridPos[] route, SeededRandom rng, int maxStarts, int maxEnergy)
        {
            var routeSet = new HashSet<GridPos>(route);
            var nearRoute = new List<GridPos>();
            var relaxed = new List<GridPos>();

            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var p = new GridPos(x, y);
                    if (routeSet.Contains(p)) continue;
                    relaxed.Add(p);
                    if (MinDistanceToRoute(p, route) <= maxEnergy) nearRoute.Add(p);
                }
            }

            rng.Shuffle(nearRoute);
            rng.Shuffle(relaxed);

            var result = new List<GridPos>();
            var seen = new HashSet<GridPos>();
            for (int i = 0; i < nearRoute.Count && result.Count < maxStarts; i++)
            {
                if (seen.Add(nearRoute[i])) result.Add(nearRoute[i]);
            }
            for (int i = 0; i < relaxed.Count && result.Count < maxStarts; i++)
            {
                if (seen.Add(relaxed[i])) result.Add(relaxed[i]);
            }
            return result;
        }

        private static int MinDistanceToRoute(GridPos p, GridPos[] route)
        {
            int best = int.MaxValue;
            for (int i = 0; i < route.Length; i++)
            {
                int d = p.ManhattanDistance(route[i]);
                if (d < best) best = d;
            }
            return best;
        }

        private static bool IsAdjacentToRoute(GridPos p, HashSet<GridPos> routeSet)
        {
            for (int i = 0; i < Directions.All.Length; i++)
            {
                if (routeSet.Contains(p + Directions.Delta(Directions.All[i]))) return true;
            }
            return false;
        }

        private static List<int> BuildRadii(int maxRadius)
        {
            var radii = new List<int>();
            void Add(int value)
            {
                if (value < 0) return;
                if (!radii.Contains(value)) radii.Add(value);
            }

            Add(maxRadius);
            Add(Math.Max(0, maxRadius / 2));
            Add(0);
            return radii;
        }

        private static LevelDefinition BuildDefinition(
            int levelIndex, int seed, int width, int height, BoardData board,
            GridPos start, PatternPlacement placement, int zombieConciencia, int maxEnergy,
            GenerationSettings settings, SolutionPlan plan, List<GridPos> hazards, int attempts)
        {
            return new LevelDefinition
            {
                Seed = seed,
                LevelIndex = levelIndex,
                Width = width,
                Height = height,
                Board = board,
                PlayerStart = start,
                ZombieStart = placement.Route[0],
                SwitchPosition = plan.SwitchPosition,
                NpcRoute = (GridPos[])placement.Route.Clone(),
                ZombiePhaseStart = 0,
                ZombieConciencia = zombieConciencia,
                PlayerConciencia = settings.playerConciencia,
                MaxEnergy = maxEnergy,
                HistoryCapacity = settings.historyCapacity,
                PlannedManualDirections = plan.ManualDirections.ToArray(),
                PlannedManualPositions = plan.ManualPositions.ToArray(),
                PlannedSequence = plan.Sequence.ToArray(),
                PlannedAutoPositions = plan.AutoPositions.ToArray(),
                HazardPositions = hazards.ToArray(),
                PatternName = placement.TemplateName,
                PatternRotation = placement.RotationDegrees,
                PatternMirrorH = placement.MirrorHorizontal,
                PatternMirrorV = placement.MirrorVertical,
                PatternPhase = placement.Phase,
                PatternOrigin = placement.RegionOrigin,
                UniqueConfirmed = false,
                SolutionCount = 0,
                GenerationAttemptsUsed = attempts,
                UsedFallback = false
            };
        }

        private static int HashSeed(int seed, int attempt, int levelIndex)
        {
            unchecked
            {
                uint x = (uint)seed;
                x ^= (uint)attempt * 0x9E3779B9u;
                x ^= (uint)levelIndex * 0x85EBCA6Bu;
                x ^= x >> 16;
                x *= 0x7FEB352Du;
                x ^= x >> 15;
                x *= 0x846CA68Bu;
                x ^= x >> 16;
                return (int)x;
            }
        }

        private static LevelDefinition CreateEmergencyLevel(
            int levelIndex, int seed, GenerationSettings settings, int attempts)
        {
            int width = 6;
            int height = 6;
            var board = new BoardData(width, height, TileType.Floor);
            var start = new GridPos(0, 0);
            var switchPosition = new GridPos(1, 0);
            var hazard = new GridPos(0, 5);
            board.SetTile(switchPosition, TileType.Switch);
            board.SetTile(hazard, TileType.Damage);

            // A small, valid perimeter route away from the debug switch.
            var route = new[]
            {
                new GridPos(3, 5), new GridPos(4, 5), new GridPos(5, 5), new GridPos(5, 4),
                new GridPos(5, 3), new GridPos(4, 3), new GridPos(3, 3), new GridPos(3, 4)
            };

            return new LevelDefinition
            {
                Seed = seed,
                LevelIndex = levelIndex,
                Width = width,
                Height = height,
                Board = board,
                PlayerStart = start,
                ZombieStart = route[0],
                SwitchPosition = switchPosition,
                NpcRoute = route,
                ZombiePhaseStart = 0,
                ZombieConciencia = 3,
                PlayerConciencia = settings.playerConciencia,
                MaxEnergy = 5,
                HistoryCapacity = settings.historyCapacity,
                PlannedManualDirections = new[] { Direction.Right },
                PlannedManualPositions = new[] { start, switchPosition },
                PlannedSequence = new Direction[0],
                PlannedAutoPositions = new[] { start, switchPosition },
                HazardPositions = new[] { hazard },
                PatternName = "Emergency perimeter",
                PatternRotation = 0,
                PatternMirrorH = false,
                PatternMirrorV = false,
                PatternPhase = 0,
                PatternOrigin = new GridPos(3, 3),
                UniqueConfirmed = false,
                SolutionCount = 0,
                GenerationAttemptsUsed = attempts,
                UsedFallback = true,
                GenerationMessage = "EMERGENCY debug level: no valid level was found after " + attempts + " attempts"
            };
        }
    }
}
