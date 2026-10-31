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

        public static GenerationResult Generate(
            int levelIndex, int seed, GenerationSettings settingsInput, bool allowEmergency = true)
        {
            var settings = settingsInput.Clone();
            settings.Sanitize();

            LevelDefinition fallback = null;
            int attempts = 0;
            var profile = DifficultyProfile.ForLevel(levelIndex);

            for (int attempt = 0; attempt < settings.maxGenerationAttempts; attempt++)
            {
                attempts++;
                var rng = new SeededRandom(SeedUtility.DeriveAttemptSeed(seed, attempt, levelIndex));

                int width = rng.Range(settings.minWidth, settings.maxWidth);
                int height = rng.Range(settings.minHeight, settings.maxHeight);
                var placement = GeneradorPatronMovimiento.Generate(width, height, rng);
                if (placement == null) continue;

                int zombieConciencia;
                int playerConciencia;
                if (!TryPickConsciousness(settings, rng, out zombieConciencia, out playerConciencia))
                    continue;

                int planningEnergy = settings.maxEnergy;
                var openBoard = new BoardData(width, height, TileType.Floor);
                Log?.Invoke($"attempt {attempt} {width}x{height} pattern={placement.TemplateName} " +
                            $"zc={zombieConciencia} pc={playerConciencia} tier={profile.Tier}");

                var starts = BuildStartCandidates(
                    openBoard, placement.Route, rng, 12, planningEnergy, profile, playerConciencia);
                for (int s = 0; s < starts.Count; s++)
                {
                    var start = starts[s];
                    var plans = PlanificadorSolucion.FindCandidates(
                        openBoard, start, placement, zombieConciencia, playerConciencia,
                        planningEnergy, settings, levelIndex, rng);
                    if (plans.Count == 0) continue;
                    Log?.Invoke($"  start {start}: {plans.Count} plans, best length={plans[0].ManualDirections.Count} " +
                                $"turns={plans[0].ManualTurnCount} switch={plans[0].SwitchPosition} score={plans[0].Score}");

                    int planLimit = Math.Min(8, plans.Count);
                    for (int p = 0; p < planLimit; p++)
                    {
                        var plan = plans[p];
                        if (!plan.MeetsDifficulty)
                        {
                            Log?.Invoke($"    plan rejected: does not meet {profile}");
                            continue;
                        }

                        int energy;
                        if (!TryChooseEnergy(plan, start, settings, rng, out energy))
                        {
                            Log?.Invoke("    plan rejected: switch is too close for the minimum energy");
                            continue;
                        }

                        var core = BuildCore(plan, placement.Route, start);
                        if (core == null) continue;

                        int maxHazards = HazardPlacer.GetMaxHazards(width, height, settings.maxHazardsCap);
                        var radii = BuildRadii(settings.maxLateralExpansion);

                        for (int r = 0; r < radii.Count; r++)
                        {
                            var geometry = GeneradorGeometria.Build(
                                width, height, core, radii[r], settings.expansionDensity,
                                rng, Math.Max(1, maxHazards) + 2);
                            if (geometry == null) continue;

                            geometry.Board.SetTile(plan.SwitchPosition, TileType.Switch);

                            var protectedCells = new HashSet<GridPos>(core);
                            protectedCells.Add(plan.SwitchPosition);
                            protectedCells.Add(start);
                            protectedCells.Add(placement.Route[0]);
                            for (int i = 0; i < placement.Route.Length; i++) protectedCells.Add(placement.Route[i]);

                            var hazards = HazardPlacer.Place(
                                geometry.Board, geometry.OptionalCells, protectedCells,
                                plan.ManualPositions, plan.AutoPositions, placement.Route,
                                settings.maxHazardsCap, rng);
                            if (hazards == null)
                            {
                                Log?.Invoke($"    radius {radii[r]}: not enough useful hazard cells");
                                continue;
                            }

                            var level = BuildDefinition(
                                levelIndex, seed, width, height, geometry.Board,
                                start, placement, zombieConciencia, playerConciencia, energy,
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

            if (!allowEmergency)
            {
                return new GenerationResult
                {
                    Level = null,
                    Success = false,
                    UniqueConfirmed = false,
                    Attempts = attempts,
                    Message = "no valid level found after " + attempts + " attempts"
                };
            }

            // Last resort: retry with relaxed uniqueness and a deterministic offset seed. The
            // relaxed level is still structurally valid and uses the real turn engine, so it
            // never replaces gameplay with a broken hand-written board.
            var relaxed = settings.Clone();
            relaxed.requireUniqueSolution = false;
            relaxed.maxGenerationAttempts = Math.Min(40, Math.Max(10, settings.maxGenerationAttempts));
            for (int retry = 1; retry <= 4; retry++)
            {
                int retrySeed = unchecked(seed + retry * 0x5EED);
                var retryResult = Generate(levelIndex, retrySeed, relaxed, false);
                if (retryResult.Success && retryResult.Level != null)
                {
                    retryResult.Level.UsedFallback = true;
                    retryResult.Level.GenerationMessage =
                        "relaxed fallback after " + attempts + " attempts; " + retryResult.Message;
                    retryResult.Message = retryResult.Level.GenerationMessage;
                    return retryResult;
                }
            }

            return new GenerationResult
            {
                Level = null,
                Success = false,
                UniqueConfirmed = false,
                Attempts = attempts,
                Message = "no valid level found after " + attempts + " attempts"
            };
        }

        private static bool TryPickConsciousness(
            GenerationSettings settings, SeededRandom rng, out int zombie, out int player)
        {
            zombie = 0;
            player = 0;

            var zombies = new List<int>();
            for (int z = settings.minZombieConciencia; z <= settings.maxZombieConciencia; z++)
                zombies.Add(z);
            rng.Shuffle(zombies);

            for (int i = 0; i < zombies.Count; i++)
            {
                int z = zombies[i];
                int minPlayer = Math.Max(settings.minPlayerConciencia, z - 1);
                if (minPlayer > settings.maxPlayerConciencia) continue;
                zombie = z;
                player = rng.Range(minPlayer, settings.maxPlayerConciencia);
                return true;
            }

            return false;
        }

        private static bool TryChooseEnergy(
            SolutionPlan plan, GridPos start, GenerationSettings settings, SeededRandom rng, out int energy)
        {
            energy = 0;
            if (plan == null) return false;

            int manualLength = plan.ManualDirections.Count;
            int switchDistance = start.ManhattanDistance(plan.SwitchPosition);

            // The manual budget must at least cover the planned contact route, and it must stay
            // below the Manhattan distance to the switch so the switch cannot be walked to
            // before contact.
            int minEnergy = Math.Max(settings.minEnergy, manualLength);
            int maxEnergy = Math.Min(settings.maxEnergy, switchDistance - 1);
            if (minEnergy > maxEnergy) return false;

            energy = rng.Range(minEnergy, maxEnergy);
            return true;
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
            BoardData board, GridPos[] route, SeededRandom rng, int maxStarts,
            int maxEnergy, DifficultyProfile profile, int playerConciencia)
        {
            var routeSet = new HashSet<GridPos>(route);
            var preferred = new List<GridPos>();
            var fallback = new List<GridPos>();

            int minDepth = Math.Max(3, Math.Max(profile.MinManualMoves, playerConciencia));
            int maxDepth = Math.Min(maxEnergy, profile.MaxManualMoves);
            if (minDepth > maxDepth) return new List<GridPos>();

            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var p = new GridPos(x, y);
                    if (routeSet.Contains(p)) continue;

                    int options = CountInitialOptions(board, p, routeSet);
                    if (options < profile.MinInitialOptions) continue;

                    // The zombie reaches route[d] after exactly d manual moves, so the player
                    // start has to be able to reach one of those contact cells in d steps.
                    int bestContactDistance = int.MaxValue;
                    int bestContactDepth = -1;
                    for (int depth = minDepth; depth <= maxDepth; depth++)
                    {
                        if (depth >= route.Length) break;
                        int distance = p.ManhattanDistance(route[depth]);
                        if (distance < 2 || distance > depth) continue;
                        if (((depth - distance) & 1) != 0) continue;
                        if (distance < bestContactDistance)
                        {
                            bestContactDistance = distance;
                            bestContactDepth = depth;
                        }
                    }

                    bool interior = x > 0 && y > 0 && x < board.Width - 1 && y < board.Height - 1;
                    if (bestContactDepth >= 0 && interior &&
                        bestContactDistance >= 2 && bestContactDistance <= Math.Max(2, maxEnergy))
                        preferred.Add(p);
                    else
                        fallback.Add(p);
                }
            }

            rng.Shuffle(preferred);
            rng.Shuffle(fallback);

            var result = new List<GridPos>();
            var seen = new HashSet<GridPos>();
            for (int i = 0; i < preferred.Count && result.Count < maxStarts; i++)
            {
                if (seen.Add(preferred[i])) result.Add(preferred[i]);
            }
            for (int i = 0; i < fallback.Count && result.Count < maxStarts; i++)
            {
                if (seen.Add(fallback[i])) result.Add(fallback[i]);
            }
            return result;
        }

        private static int CountInitialOptions(BoardData board, GridPos p, HashSet<GridPos> routeSet)
        {
            int count = 0;
            for (int i = 0; i < Directions.All.Length; i++)
            {
                var next = p + Directions.Delta(Directions.All[i]);
                if (!board.IsWalkable(next)) continue;
                if (routeSet.Contains(next)) continue;
                count++;
            }
            return count;
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

        private static List<int> BuildRadii(int maxRadius)
        {
            var radii = new List<int>();
            void Add(int value)
            {
                if (value < 0) return;
                if (!radii.Contains(value)) radii.Add(value);
            }

            Add(maxRadius);
            Add(maxRadius + 1);
            Add(Math.Max(0, maxRadius / 2));
            Add(0);
            return radii;
        }

        private static LevelDefinition BuildDefinition(
            int levelIndex, int seed, int width, int height, BoardData board,
            GridPos start, PatternPlacement placement, int zombieConciencia, int playerConciencia,
            int maxEnergy, GenerationSettings settings, SolutionPlan plan,
            List<GridPos> hazards, int attempts)
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
                PlayerConciencia = playerConciencia,
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
                DifficultyTier = DifficultyProfile.TierForLevel(levelIndex),
                MeetsDifficulty = plan.MeetsDifficulty,
                ManualTurnCount = plan.ManualTurnCount,
                DistinctDirections = plan.DistinctDirections,
                MaxStraightRun = plan.MaxStraightRun,
                ImmediateReversals = plan.ImmediateReversals,
                RevisitedCells = plan.RevisitedCells,
                InitialOptions = plan.InitialOptions,
                AutomaticStepCount = plan.AutomaticSteps,
                Bounds = PlayableBounds.FromBoard(board),
                UniqueConfirmed = false,
                SolutionCount = 0,
                GenerationAttemptsUsed = attempts,
                UsedFallback = false
            };
        }

    }
}
