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
            GenerationResult result = null;
            var routine = GenerateCoroutine(levelIndex, seed, settingsInput, r => result = r, allowEmergency);
            while (routine.MoveNext()) { }
            return result;
        }

        public static System.Collections.IEnumerator GenerateCoroutine(
            int levelIndex, int seed, GenerationSettings settingsInput, Action<GenerationResult> onComplete, bool allowEmergency = true)
        {
            var settings = settingsInput.Clone();
            settings.Sanitize();

            LevelDefinition fallback = null;
            int attempts = 0;
            var profile = DifficultyProfile.ForLevel(levelIndex);

            for (int attempt = 0; attempt < settings.maxGenerationAttempts; attempt++)
            {
                attempts++;
                yield return null;

                var rng = new SeededRandom(SeedUtility.DeriveAttemptSeed(seed, attempt, levelIndex));

                int widthMin = levelIndex <= 2 ? 6 : settings.minWidth;
                int widthMax = levelIndex <= 2 ? 8 : settings.maxWidth;
                int heightMin = levelIndex <= 2 ? 6 : settings.minHeight;
                int heightMax = levelIndex <= 2 ? 8 : settings.maxHeight;
                int width = rng.Range(widthMin, widthMax);
                int height = rng.Range(heightMin, heightMax);
                var placement = GeneradorPatronMovimiento.Generate(width, height, rng);
                if (placement == null) continue;
                if (levelIndex <= 2 && placement.Route.Length > 4) continue;

                int zombieConciencia;
                int playerConciencia;
                if (!TryPickConsciousness(levelIndex, settings, rng, out zombieConciencia, out playerConciencia))
                    continue;

                int planningEnergy = settings.maxEnergy;
                var openBoard = new BoardData(width, height, TileType.Floor);
                Log?.Invoke($"attempt {attempt} {width}x{height} pattern={placement.TemplateName} " +
                            $"zc={zombieConciencia} pc={playerConciencia} tier={profile.Tier}");

                var starts = BuildStartCandidates(
                    openBoard, placement.Route, rng, 12, planningEnergy, profile, zombieConciencia, playerConciencia);
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

                        if (levelIndex <= 2)
                        {
                            // Ruta Jugador -> Zombi: distancia estrictamente entre 3 y 4 casillas (al menos un giro, no linea recta)
                            if (plan.ManualDirections.Count < 3 || plan.ManualDirections.Count > 4) continue;
                            if (plan.ManualTurnCount < 1) continue;
                            if (plan.MaxStraightRun >= plan.ManualDirections.Count) continue;

                            // Ruta Jugador Infectado -> Meta: maximo 5 casillas de distancia
                            if (plan.AutomaticSteps > 5) continue;
                        }

                        int energy;
                        if (!TryChooseEnergy(plan, start, settings, rng, out energy))
                        {
                            Log?.Invoke("    plan rejected: switch is too close for the minimum energy");
                            continue;
                        }

                        var core = BuildCore(plan, placement.Route, start, levelIndex);
                        if (core == null) continue;

                        int maxHazards = HazardPlacer.GetMaxHazards(width, height, settings.maxHazardsCap);
                        if (levelIndex <= 1) maxHazards = 1;
                        else if (levelIndex == 2) maxHazards = Math.Min(2, maxHazards);

                        int minOptional = levelIndex <= 1 ? 2 : (levelIndex == 2 ? 3 : Math.Max(1, maxHazards) + 2);
                        var radii = BuildRadii(settings.maxLateralExpansion, levelIndex);

                        for (int r = 0; r < radii.Count; r++)
                        {
                            var geometry = GeneradorGeometria.Build(
                                width, height, core, radii[r], settings.expansionDensity,
                                rng, minOptional);
                            if (geometry == null) continue;

                            // Limite Total de Baldosas: N=1 -> max 12, N=2 -> max 15, N>2 -> +2 por nivel hasta original
                            int maxWalkable = GetMaxWalkableTiles(levelIndex);
                            if (geometry.Board.CountWalkable() > maxWalkable) continue;

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

                            // Complejidad Topológica: asegurar que existan al menos 2 posibles secuencias espaciales al zombi
                            if (levelIndex <= 2)
                            {
                                int pathsToZombie = CountManualPathsToContact(level);
                                if (pathsToZombie < 2)
                                {
                                    Log?.Invoke($"    rejected: paths to zombie ({pathsToZombie}) < 2 for level <= 2");
                                    continue;
                                }
                            }

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
                                onComplete?.Invoke(new GenerationResult
                                {
                                    Level = level,
                                    Success = true,
                                    UniqueConfirmed = false,
                                    Attempts = attempts,
                                    Message = level.GenerationMessage
                                });
                                yield break;
                            }

                            ValidationReport full = null;
                            var valRoutine = ValidadorNivel.ValidateRoutine(level, settings, true, rep => full = rep);
                            while (valRoutine.MoveNext())
                            {
                                yield return valRoutine.Current;
                            }

                            Log?.Invoke($"    uniqueness: valid={full.Valid} count={full.SolutionCount} states={full.StatesExplored} msg={full}");
                            if (full.Valid && full.SolutionCount == 1)
                            {
                                level.UniqueConfirmed = true;
                                level.SolutionCount = 1;
                                level.GenerationMessage = "valid unique level";
                                onComplete?.Invoke(new GenerationResult
                                {
                                    Level = level,
                                    Success = true,
                                    UniqueConfirmed = true,
                                    Attempts = attempts,
                                    Message = level.GenerationMessage
                                });
                                yield break;
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
                onComplete?.Invoke(new GenerationResult
                {
                    Level = fallback,
                    Success = true,
                    UniqueConfirmed = false,
                    Attempts = attempts,
                    Message = fallback.GenerationMessage
                });
                yield break;
            }

            if (!allowEmergency)
            {
                onComplete?.Invoke(new GenerationResult
                {
                    Level = null,
                    Success = false,
                    UniqueConfirmed = false,
                    Attempts = attempts,
                    Message = "no valid level found after " + attempts + " attempts"
                });
                yield break;
            }

            // Last resort: retry with relaxed uniqueness and a deterministic offset seed. The
            // relaxed level is still structurally valid and uses the real turn engine, so it
            // never replaces gameplay with a broken hand-written board.
            var relaxed = settings.Clone();
            relaxed.requireUniqueSolution = false;
            relaxed.maxGenerationAttempts = Math.Min(40, Math.Max(10, settings.maxGenerationAttempts));
            for (int retry = 1; retry <= 4; retry++)
            {
                yield return null;
                int retrySeed = unchecked(seed + retry * 0x5EED);
                GenerationResult retryResult = null;
                var retryRoutine = GenerateCoroutine(levelIndex, retrySeed, relaxed, r => retryResult = r, false);
                while (retryRoutine.MoveNext())
                {
                    yield return retryRoutine.Current;
                }

                if (retryResult != null && retryResult.Success && retryResult.Level != null)
                {
                    retryResult.Level.UsedFallback = true;
                    retryResult.Level.GenerationMessage =
                        "relaxed fallback after " + attempts + " attempts; " + retryResult.Message;
                    retryResult.Message = retryResult.Level.GenerationMessage;
                    onComplete?.Invoke(retryResult);
                    yield break;
                }
            }

            onComplete?.Invoke(new GenerationResult
            {
                Level = null,
                Success = false,
                UniqueConfirmed = false,
                Attempts = attempts,
                Message = "no valid level found after " + attempts + " attempts"
            });
        }

        public static int GetMaxWalkableTiles(int levelIndex)
        {
            if (levelIndex <= 1) return 12;
            if (levelIndex == 2) return 15;
            return Math.Min(48, 15 + 2 * (levelIndex - 2));
        }

        private static bool TryPickConsciousness(
            int levelIndex, GenerationSettings settings, SeededRandom rng, out int zombie, out int player)
        {
            // Conciencia Zombi: Para N=1 debe ser 1. Para N=2 debe ser 2. Para niveles superiores aumenta en +1 por nivel hasta su limite original (5).
            zombie = Math.Min(settings.maxZombieConciencia, Math.Max(1, levelIndex));

            // Conciencia Player: Para N=1 y N=2, debe ser aleatorio entre 2 y 3. A partir del nivel 3, aumenta en +1 por nivel hasta su tope original (5).
            if (levelIndex <= 2)
            {
                player = rng.Range(2, 3);
            }
            else
            {
                int minP = Math.Min(settings.maxPlayerConciencia, 2 + (levelIndex - 2));
                int maxP = Math.Min(settings.maxPlayerConciencia, 3 + (levelIndex - 2));
                player = rng.Range(minP, maxP);
            }

            return true;
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

        private static HashSet<GridPos> BuildCore(SolutionPlan plan, GridPos[] route, GridPos start, int levelIndex)
        {
            var core = new HashSet<GridPos>();
            for (int i = 0; i < plan.ManualPositions.Count; i++) core.Add(plan.ManualPositions[i]);
            for (int i = 0; i < route.Length; i++) core.Add(route[i]);
            for (int i = 0; i < plan.AutoPositions.Count; i++) core.Add(plan.AutoPositions[i]);
            core.Add(start);
            core.Add(plan.SwitchPosition);

            // Para los primeros niveles (N <= 2), ensanchar el camino creando zonas 2x2 o una bifurcación adyacente
            if (levelIndex <= 2 && plan.ManualPositions.Count >= 3)
            {
                bool widened = false;
                for (int i = 0; i < plan.ManualDirections.Count - 1; i++)
                {
                    var d1 = plan.ManualDirections[i];
                    var d2 = plan.ManualDirections[i + 1];
                    if (d1 != d2)
                    {
                        var pTurn = plan.ManualPositions[i];
                        var pDiag = pTurn + Directions.Delta(d2);
                        core.Add(pDiag);
                        widened = true;
                        break;
                    }
                }

                if (!widened)
                {
                    for (int d = 0; d < Directions.All.Length; d++)
                    {
                        var branch = start + Directions.Delta(Directions.All[d]);
                        if (!core.Contains(branch))
                        {
                            core.Add(branch);
                            break;
                        }
                    }
                }
            }

            return core;
        }

        public static int CountManualPathsToContact(LevelDefinition level)
        {
            if (level == null || level.PlannedManualDirections == null) return 0;
            var state = level.CreateGameState();
            int targetTurn = level.PlannedManualDirections.Length;
            int paths = 0;

            void Dfs(GameState current, int turn)
            {
                if (turn == targetTurn)
                {
                    if (current.Player.Position == current.Zombie.Position)
                    {
                        paths++;
                    }
                    return;
                }

                for (int i = 0; i < Directions.All.Length; i++)
                {
                    var nextState = current.Clone();
                    var outcome = nextState.TryManualMove(Directions.All[i]);
                    if (turn + 1 < targetTurn)
                    {
                        if (outcome.Outcome == TurnOutcome.Moved)
                        {
                            Dfs(nextState, turn + 1);
                        }
                    }
                    else
                    {
                        if (outcome.Outcome == TurnOutcome.Infected)
                        {
                            paths++;
                        }
                    }
                }
            }

            Dfs(state, 0);
            return paths;
        }

        private static List<GridPos> BuildStartCandidates(
            BoardData board, GridPos[] route, SeededRandom rng, int maxStarts,
            int maxEnergy, DifficultyProfile profile, int zombieConciencia, int playerConciencia)
        {
            var routeSet = new HashSet<GridPos>(route);
            var preferred = new List<GridPos>();
            var fallback = new List<GridPos>();

            int minDepth = Math.Max(Math.Max(zombieConciencia, playerConciencia), profile.MinManualMoves);
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

        private static List<int> BuildRadii(int maxRadius, int levelIndex)
        {
            var radii = new List<int>();
            void Add(int value)
            {
                if (value < 0) return;
                if (!radii.Contains(value)) radii.Add(value);
            }

            if (levelIndex <= 1)
            {
                Add(0);
                Add(1);
            }
            else if (levelIndex == 2)
            {
                Add(0);
                Add(1);
                Add(2);
            }
            else
            {
                Add(maxRadius);
                Add(maxRadius + 1);
                Add(Math.Max(0, maxRadius / 2));
                Add(0);
            }
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
