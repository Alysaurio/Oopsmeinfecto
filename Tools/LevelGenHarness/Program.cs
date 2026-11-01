using System;
using System.Collections.Generic;
using System.Diagnostics;
using Oopsmeinfecto;

internal static class Program
{
    private static int _failures;

    private static void Main(string[] args)
    {
        Console.WriteLine("== Oopsmeinfecto core harness ==");
        if (args.Length > 0 && args[0] == "debug")
        {
            GeneradorNivel.Log = Console.WriteLine;
            PlanificadorSolucion.Log = Console.WriteLine;
            var debugSettings = new GenerationSettings
            {
                maxGenerationAttempts = 4,
                maxPlannerStates = 60000,
                maxValidatorStates = 60000,
                requireUniqueSolution = true
            };
            debugSettings.Sanitize();
            var debugResult = GeneradorNivel.Generate(1, 1000 + 1 * 7919, debugSettings);
            Console.WriteLine("DEBUG RESULT: " + debugResult.Message + " attempts=" + debugResult.Attempts);
            return;
        }
        TestPatternLibrary();
        TestHistoryAndSequence();
        TestTurnRules();
        TestPlaybackLastStep();
        TestZombieConcienciaDiscrepancies();
        bool quick = args.Length > 0 && args[0] == "quick";
        TestGeneration(seedCount: quick ? 2 : 20, requireUnique: true, attempts: quick ? 12 : 80);
        TestGeneration(seedCount: quick ? 1 : 5, requireUnique: false, attempts: quick ? 8 : 40);
        TestDeterminism();
        TestInitialLevelsConstraints();
        Console.WriteLine(_failures == 0 ? "ALL CHECKS PASSED" : $"{_failures} CHECK(S) FAILED");
        Environment.ExitCode = _failures == 0 ? 0 : 1;
    }

    private static void Check(bool condition, string message)
    {
        if (condition) return;
        _failures++;
        Console.WriteLine("FAIL: " + message);
    }

    private static void TestPatternLibrary()
    {
        Console.WriteLine("\n-- Pattern library --");
        foreach (var template in PatternLibrary.Templates)
        {
            bool ok = PatternLibrary.ValidateTemplate(template.Cells, out var error);
            Check(ok, $"template {template.Name}: {error}");
            for (int rotation = 0; rotation < 360; rotation += 90)
            {
                var transformed = PatternLibrary.TransformCells(template.Cells, rotation, false, false);
                for (int i = 0; i < transformed.Length; i++)
                {
                    Check(transformed[i].X >= 0 && transformed[i].X <= 2 && transformed[i].Y >= 0 && transformed[i].Y <= 2,
                        $"{template.Name} rotation {rotation} outside 3x3");
                    Check(transformed[i].IsOrthogonallyAdjacent(transformed[(i + 1) % transformed.Length]),
                        $"{template.Name} rotation {rotation} non-orthogonal transition {i}");
                }
            }
        }
        Console.WriteLine($"Templates checked: {PatternLibrary.Templates.Length}");
    }

    private static void TestHistoryAndSequence()
    {
        Console.WriteLine("\n-- History and infection sequence --");
        var zombie = new EntityState(1, EntityKind.Zombie, new GridPos(5, 5), 5, 7);
        var player = new EntityState(0, EntityKind.Player, new GridPos(0, 0), 2, 7);
        for (int i = 0; i < 9; i++)
        {
            zombie.RecordMove(Directions.All[i % 4]);
            player.RecordMove(Directions.All[(i + 1) % 4]);
        }
        Check(zombie.HistoryCount == 7, "zombie history cap is not 7");
        Check(player.HistoryCount == 7, "player history cap is not 7");

        // The infected entity contributes its own Conciencia, and the infector contributes its own Conciencia.
        var sequence = InfectionSystem.BuildSequence(zombie, player);
        Check(sequence.Count == zombie.Conciencia + player.Conciencia,
            $"sequence should be {zombie.Conciencia} + {player.Conciencia} = {zombie.Conciencia + player.Conciencia}, got {sequence.Count}");
        var expectedZombie = zombie.GetLastDirections(zombie.Conciencia);
        var expectedPlayer = player.GetLastDirections(player.Conciencia);
        for (int i = 0; i < zombie.Conciencia; i++)
            Check(sequence[i] == expectedZombie[i], "zombie part of sequence mismatch");
        for (int i = 0; i < player.Conciencia; i++)
            Check(sequence[zombie.Conciencia + i] == expectedPlayer[i], "infected part of sequence mismatch");

        // Same infector, different infected conciencia: the length must change accordingly.
        var player2 = new EntityState(0, EntityKind.Player, new GridPos(0, 0), 4, 7);
        for (int i = 0; i < 6; i++) player2.RecordMove(Directions.All[i % 4]);
        var sequence2 = InfectionSystem.BuildSequence(zombie, player2);
        Check(sequence2.Count == zombie.Conciencia + player2.Conciencia,
            "sequence should follow both entities' conciencia");

        // Chronological order inside each group: oldest first.
        Check(sequence2[0] == zombie.GetLastDirections(zombie.Conciencia)[0], "zombie group is not chronological");
        Check(sequence2[zombie.Conciencia] == player2.GetLastDirections(4)[0], "infected group is not chronological");

        var shortEntity = new EntityState(0, EntityKind.Player, new GridPos(0, 0), 3, 7);
        shortEntity.RecordMove(Direction.Right);
        Check(!InfectionSystem.HasRequiredHistory(zombie, shortEntity),
            "history check accepted an infected entity with too few moves");
        Console.WriteLine("History cap and sequence composition OK");
    }

    private static void TestTurnRules()
    {
        Console.WriteLine("\n-- Turn rules --");
        // 5x3 room. Player at (0,0), zombie route around (3,0)-(4,0).
        var board = new BoardData(5, 3, TileType.Floor);
        var route = new[] { new GridPos(3, 0), new GridPos(4, 0) };
        var state = new GameState(board, new GridPos(0, 0), route, 2, 3, 5, 7);

        var blocked = state.TryManualMove(Direction.Left);
        Check(blocked.Outcome == TurnOutcome.Blocked, "wall/boundary move should be blocked");
        Check(state.CurrentEnergy == 5, "blocked move spent energy");
        Check(state.Zombie.HistoryCount == 0, "blocked move advanced NPC");

        var moved = state.TryManualMove(Direction.Right);
        Check(moved.Outcome == TurnOutcome.Moved, "valid move failed");
        Check(state.CurrentEnergy == 4, "valid move did not spend exactly one energy");
        Check(state.Player.HistoryCount == 1, "player history did not record move");
        Check(state.Zombie.HistoryCount == 1, "NPC did not move after valid move");

        // Damage tile: immediate defeat, NPC does not move.
        var damageBoard = new BoardData(5, 3, TileType.Floor);
        damageBoard.SetTile(new GridPos(1, 0), TileType.Damage);
        var damageState = new GameState(damageBoard, new GridPos(0, 0), route, 2, 3, 5, 7);
        var damage = damageState.TryManualMove(Direction.Right);
        Check(damage.Outcome == TurnOutcome.DamageDefeat, "damage tile did not defeat");
        Check(damageState.Zombie.HistoryCount == 0, "NPC moved after damage defeat");
        Check(damageState.Phase == GamePhase.Defeat, "damage did not set Defeat phase");

        // The switch only activates during the inherited playback. Standing on it manually is
        // an ordinary move and must not win.
        var switchBoard = new BoardData(5, 3, TileType.Floor);
        switchBoard.SetTile(new GridPos(1, 0), TileType.Switch);
        var switchState = new GameState(switchBoard, new GridPos(0, 0), route, 2, 3, 5, 7);
        var switchResult = switchState.TryManualMove(Direction.Right);
        Check(switchResult.Outcome == TurnOutcome.Moved, "manual switch step should be an ordinary move");
        Check(switchState.Phase == GamePhase.HumanControlled, "manual switch step must not win");
        Check(switchState.Zombie.HistoryCount == 1, "NPC did not move after a manual switch step");

        // Energy exhaustion without contact enters the environmental transformation phase.
        var exhaustBoard = new BoardData(8, 3, TileType.Floor);
        var exhaustRoute = new[] { new GridPos(7, 2), new GridPos(7, 1) };
        var exhaust = new GameState(exhaustBoard, new GridPos(0, 0), exhaustRoute, 2, 3, 2, 7);
        var first = exhaust.TryManualMove(Direction.Right);
        Check(first.Outcome == TurnOutcome.Moved, "exhaust first move failed");
        var second = exhaust.TryManualMove(Direction.Right);
        Check(second.Outcome == TurnOutcome.EnergyDefeat, "energy exhaustion did not defeat");
        Check(exhaust.Phase == GamePhase.EnvironmentalTransformation,
            "energy exhaustion did not enter EnvironmentalTransformation");
        Check(exhaust.DefeatReason == DefeatReason.EnergyDepleted, "wrong energy defeat reason");
        var afterExhaust = exhaust.TryManualMove(Direction.Right);
        Check(afterExhaust.Outcome == TurnOutcome.Blocked, "transformation phase accepted manual input");

        // Contact on the last energy unit has priority over exhaustion.
        var contactBoard = new BoardData(5, 3, TileType.Floor);
        var contactRoute = new[] { new GridPos(2, 0), new GridPos(1, 0) };
        var contact = new GameState(contactBoard, new GridPos(0, 0), contactRoute, 2, 3, 1, 7);
        var contactResult = contact.TryManualMove(Direction.Right);
        Check(contactResult.Outcome == TurnOutcome.Infected, "contact at zero energy did not infect");
        Check(contact.Phase == GamePhase.InfectedPlayback, "contact did not enter playback");

        Console.WriteLine("Blocked movement, energy, damage, switch and contact priority OK");
    }

    private static void TestPlaybackLastStep()
    {
        Console.WriteLine("\n-- Playback last step & order of operations --");
        var board = new BoardData(10, 5, TileType.Floor);
        var route = new[] { new GridPos(2, 0), new GridPos(1, 0) };
        var state = new GameState(board, new GridPos(0, 0), route, 3, 1, 10, 7);

        var contactRes = state.TryManualMove(Direction.Right);
        Check(contactRes.Outcome == TurnOutcome.Infected, "Failed to infect in test setup: " + contactRes.Outcome);
        Check(state.Phase == GamePhase.InfectedPlayback, "State did not enter InfectedPlayback");

        int totalMoves = state.PlaybackSequence.Count;
        Check(totalMoves > 0, "Playback sequence is empty");

        GridPos posBeforeLast = default;
        for (int i = 0; i < totalMoves - 1; i++)
        {
            var stepRes = state.AdvancePlaybackStep();
            Check(stepRes.Outcome == TurnOutcome.PlaybackStep, $"Step {i} was not PlaybackStep, got {stepRes.Outcome}");
            Check(state.Phase == GamePhase.InfectedPlayback, $"Phase changed prematurely to {state.Phase} on step {i}");
        }

        posBeforeLast = state.Player.Position;
        var lastStepRes = state.AdvancePlaybackStep();
        Check(lastStepRes.Outcome == TurnOutcome.PlaybackStep,
            $"Final step should be PlaybackStep so visual interpolation completes, but was {lastStepRes.Outcome}");
        Check(state.Phase == GamePhase.InfectedPlayback,
            $"Final step should remain InfectedPlayback until next tick, but was {state.Phase}");

        GridPos finalPosA = state.Player.Position;
        Check(finalPosA != posBeforeLast || lastStepRes.PlaybackStepBlocked,
            "Player did not move on the final step");

        var exhaustRes = state.AdvancePlaybackStep();
        Check(exhaustRes.Outcome == TurnOutcome.PlaybackExhausted,
            $"Post-final tick should be PlaybackExhausted, got {exhaustRes.Outcome}");
        Check(state.Phase == GamePhase.Defeat,
            $"Post-final phase should be Defeat, got {state.Phase}");
        Check(state.DefeatReason == DefeatReason.PlaybackExhausted,
            $"DefeatReason should be PlaybackExhausted, got {state.DefeatReason}");
        Check(state.Player.Position == finalPosA,
            $"Player position changed after exhaustion! Expected {finalPosA}, got {state.Player.Position}");

        // Switch scenario: Switch is placed at finalPosA
        var switchBoard = new BoardData(10, 5, TileType.Floor);
        switchBoard.SetTile(finalPosA, TileType.Switch);
        var switchState = new GameState(switchBoard, new GridPos(0, 0), route, 3, 1, 10, 7);
        switchState.TryManualMove(Direction.Right);

        for (int i = 0; i < totalMoves - 1; i++)
        {
            var s = switchState.AdvancePlaybackStep();
            Check(s.Outcome == TurnOutcome.PlaybackStep, "Switch scenario intermediate step failed");
        }
        var winStep = switchState.AdvancePlaybackStep();
        Check(winStep.Outcome == TurnOutcome.SwitchVictory,
            $"Reaching switch on final step should return SwitchVictory, got {winStep.Outcome}");
        Check(switchState.Phase == GamePhase.Victory,
            $"Phase should be Victory, got {switchState.Phase}");
        Check(switchState.Player.Position == finalPosA,
            $"Player should end on switch position {finalPosA}, got {switchState.Player.Position}");

        Console.WriteLine("Playback last step & victory/exhaustion order verified successfully");
    }

    private static void TestZombieConcienciaDiscrepancies()
    {
        Console.WriteLine("\n-- Zombie Conciencia Discrepancies & Single Source of Truth --");

        int[] testConciencias = { 1, 2, 3, 4, 5 };
        foreach (int zc in testConciencias)
        {
            int pc = 2;
            var board = new BoardData(35, 5, TileType.Floor);
            var route = new[] { new GridPos(20, 2), new GridPos(21, 2) };

            // T is even and strictly greater than both zc and pc
            int T = 2 * (Math.Max(zc, pc) + 1);
            var playerStart = new GridPos(20 - T, 2);
            var state = new GameState(board, playerStart, route, zc, pc, T + 10, 7);

            for (int step = 0; step < T; step++)
            {
                var res = state.TryManualMove(Direction.Right);
                if (step == T - 1)
                {
                    Check(res.Outcome == TurnOutcome.Infected, $"ZC={zc}: expected contact at step {step}, got {res.Outcome}");
                    Check(state.Phase == GamePhase.InfectedPlayback, $"ZC={zc}: expected InfectedPlayback");
                }
                else
                {
                    Check(res.Outcome == TurnOutcome.Moved, $"ZC={zc}: step {step} failed with {res.Outcome}");
                }
            }

            Check(state.Zombie.Conciencia == zc, $"ZC={zc}: Zombie.Conciencia is {state.Zombie.Conciencia}");
            Check(state.Player.Conciencia == pc, $"ZC={zc}: Player.Conciencia is {state.Player.Conciencia}");

            var selectedZombie = InfectionSystem.GetSelectedMoves(state.Zombie);
            var selectedPlayer = InfectionSystem.GetSelectedMoves(state.Player);
            Check(selectedZombie.Count == zc, $"ZC={zc}: selected zombie moves count={selectedZombie.Count}, expected {zc}");
            Check(selectedPlayer.Count == pc, $"ZC={zc}: selected player moves count={selectedPlayer.Count}, expected {pc}");

            Check(state.PlaybackSequence.Count == zc + pc,
                $"ZC={zc}: sequence count {state.PlaybackSequence.Count} != {zc} + {pc}");

            for (int i = 0; i < zc; i++)
            {
                Check(state.PlaybackSequence[i] == selectedZombie[i],
                    $"ZC={zc}: zombie move {i} mismatch: {state.PlaybackSequence[i]} vs {selectedZombie[i]}");
            }
            for (int i = 0; i < pc; i++)
            {
                Check(state.PlaybackSequence[zc + i] == selectedPlayer[i],
                    $"ZC={zc}: player move {i} mismatch: {state.PlaybackSequence[zc + i]} vs {selectedPlayer[i]}");
            }

            var expectedLastZombieMove = state.Zombie.History[state.Zombie.HistoryCount - 1];
            Check(selectedZombie[selectedZombie.Count - 1] == expectedLastZombieMove,
                $"ZC={zc}: last selected zombie move is not the contact move");

            int zombieHistoryBeforePlayback = state.Zombie.HistoryCount;
            int playerHistoryBeforePlayback = state.Player.HistoryCount;
            var zombieHistorySnapshot = new List<Direction>(state.Zombie.History);
            var playerHistorySnapshot = new List<Direction>(state.Player.History);

            while (state.Phase == GamePhase.InfectedPlayback)
            {
                state.AdvancePlaybackStep();
                Check(state.Zombie.HistoryCount == zombieHistoryBeforePlayback,
                    $"ZC={zc}: Zombie history grew during playback from {zombieHistoryBeforePlayback} to {state.Zombie.HistoryCount}!");
                Check(state.Player.HistoryCount == playerHistoryBeforePlayback,
                    $"ZC={zc}: Player history grew during playback from {playerHistoryBeforePlayback} to {state.Player.HistoryCount}!");
            }

            for (int i = 0; i < zombieHistorySnapshot.Count; i++)
                Check(state.Zombie.History[i] == zombieHistorySnapshot[i], $"ZC={zc}: Zombie history mutated during playback at {i}");
            for (int i = 0; i < playerHistorySnapshot.Count; i++)
                Check(state.Player.History[i] == playerHistorySnapshot[i], $"ZC={zc}: Player history mutated during playback at {i}");
        }

        // 2. Cyclic repetition: zombie completes 6 full loops before contact
        {
            var board = new BoardData(35, 5, TileType.Floor);
            var route = new[] { new GridPos(20, 2), new GridPos(21, 2) };
            int zc = 3;
            int pc = 2;
            int T = 12; // 6 round trips
            var playerStart = new GridPos(20 - T, 2);
            var state = new GameState(board, playerStart, route, zc, pc, 25, 7);

            for (int s = 0; s < T; s++)
            {
                state.TryManualMove(Direction.Right);
            }
            Check(state.Phase == GamePhase.InfectedPlayback, "Loop test failed to infect");
            Check(state.PlaybackSequence.Count == zc + pc,
                $"Loop test sequence count {state.PlaybackSequence.Count} != {zc + pc}");
            var selZ = InfectionSystem.GetSelectedMoves(state.Zombie);
            Check(selZ.Count == zc, $"Loop test selected zombie count={selZ.Count}, expected {zc}");
            for (int i = 0; i < zc; i++)
                Check(state.PlaybackSequence[i] == selZ[i], $"Loop test move {i} mismatch");
        }

        // 3. Different initial phases (4-cycle route)
        for (int initialPhase = 0; initialPhase < 4; initialPhase++)
        {
            var board = new BoardData(35, 10, TileType.Floor);
            var route = new[] { new GridPos(20, 5), new GridPos(21, 5), new GridPos(21, 6), new GridPos(20, 6) };
            int zc = 3;
            int pc = 3;
            int steps = 8; // two full cycles
            var pattern = new CyclicRoutePattern(route);
            var contactPos = pattern.PositionAt(initialPhase + steps);
            var playerStart = new GridPos(contactPos.X - steps, contactPos.Y);
            var state = new GameState(board, playerStart, route, zc, pc, 15, 7, initialZombiePhase: initialPhase);

            for (int s = 0; s < steps; s++)
                state.TryManualMove(Direction.Right);

            Check(state.Phase == GamePhase.InfectedPlayback, $"Phase {initialPhase}: failed to infect");
            Check(state.PlaybackSequence.Count == zc + pc, $"Phase {initialPhase}: sequence count mismatch");
            var selZ = InfectionSystem.GetSelectedMoves(state.Zombie);
            Check(selZ.Count == zc, $"Phase {initialPhase}: selected zombie count mismatch");
            for (int i = 0; i < zc; i++)
                Check(state.PlaybackSequence[i] == selZ[i], $"Phase {initialPhase}: move {i} mismatch");
        }

        Console.WriteLine("Zombie Conciencia discrepancies and single source of truth OK");
    }

    private static void TestGeneration(int seedCount, bool requireUnique, int attempts)
    {
        Console.WriteLine($"\n-- Generation ({seedCount} seeds, unique={requireUnique}, attempts={attempts}) --");
        var settings = new GenerationSettings
        {
            requireUniqueSolution = requireUnique,
            maxGenerationAttempts = attempts,
            maxPlannerStates = 120000,
            maxValidatorStates = 120000
        };
        settings.Sanitize();

        int success = 0;
        int unique = 0;
        int fallback = 0;
        int emergency = 0;
        double totalMs = 0;
        for (int i = 1; i <= seedCount; i++)
        {
            var sw = Stopwatch.StartNew();
            var result = GeneradorNivel.Generate(i, 1000 + i * 7919, settings);
            sw.Stop();
            totalMs += sw.Elapsed.TotalMilliseconds;
            Check(result != null && result.Level != null, $"seed {i}: null result");
            if (result == null || result.Level == null) continue;
            var level = result.Level;
            success++;
            if (result.UniqueConfirmed) unique++;
            if (level.UsedFallback) fallback++;
            if (level.GenerationMessage != null && level.GenerationMessage.StartsWith("EMERGENCY")) emergency++;

            var report = ValidadorNivel.Validate(level, settings, true);
            if (!level.GenerationMessage.StartsWith("EMERGENCY"))
            {
                Check(report.Valid, $"seed {i}: validator rejected generated level: {report}");
                if (requireUnique) Check(report.SolutionCount == 1, $"seed {i}: expected one solution, got {report.SolutionCount}");
                Check(level.HazardPositions != null && level.HazardPositions.Length >= 1, $"seed {i}: no hazard");
                Check(level.Width >= 6 && level.Width <= 16 && level.Height >= 6 && level.Height <= 16, $"seed {i}: bad dimensions");
                Check(level.MaxEnergy >= 5 && level.MaxEnergy <= 10, $"seed {i}: bad energy");
                Check(level.ZombieConciencia >= 2 && level.ZombieConciencia <= 5, $"seed {i}: bad zombie conciencia");
                Check(level.PlayerConciencia >= 2 && level.PlayerConciencia <= 5, $"seed {i}: bad player conciencia");
                Check(level.PlannedSequence.Length == level.ZombieConciencia + level.PlayerConciencia, $"seed {i}: wrong sequence length");
                Check(level.PlannedAutoPositions.Length - 1 == level.PlannedSequence.Length,
                    $"seed {i}: automatic trajectory does not consume the whole sequence");
                Check(level.PlannedAutoPositions.Length - 1 >= level.ZombieConciencia + 2,
                    $"seed {i}: automatic trajectory shorter than zombie conciencia + 2");
                Check(level.PlayerStart.ManhattanDistance(level.SwitchPosition) > level.MaxEnergy,
                    $"seed {i}: switch is within the manual energy budget");
                Check(level.HazardPositions.Length <= HazardPlacer.GetMaxHazards(level.Width, level.Height, settings.maxHazardsCap),
                    $"seed {i}: too many hazards");
                Check(level.Bounds.IsValid, $"seed {i}: invalid playable bounds");
                Check(level.Bounds.MinX >= 0 && level.Bounds.MinY >= 0 &&
                      level.Bounds.MaxX < level.Width && level.Bounds.MaxY < level.Height,
                    $"seed {i}: playable bounds outside the board");

                var profile = DifficultyProfile.ForLevel(level.LevelIndex);
                Check(level.DifficultyTier == profile.Tier, $"seed {i}: difficulty tier mismatch");
                Check(level.ManualTurnCount >= profile.MinTurns, $"seed {i}: too few turns for tier");
                Check(level.DistinctDirections >= profile.MinDistinctDirections, $"seed {i}: too few directions for tier");
                Check(level.MaxStraightRun <= profile.MaxStraightRun, $"seed {i}: straight run too long for tier");
                Check(level.ImmediateReversals <= profile.MaxImmediateReversals, $"seed {i}: too many reversals for tier");
                Check(level.RevisitedCells <= profile.MaxRevisitedCells, $"seed {i}: too many revisits for tier");
                Check(level.InitialOptions >= profile.MinInitialOptions, $"seed {i}: too few initial options");
                Check(PlannedSolutionReachesSwitch(level), $"seed {i}: independent planned solution simulation failed");
            }

            Console.WriteLine(
                $"seed {i,3}: {level.Width}x{level.Height} energy={level.MaxEnergy} " +
                $"zc={level.ZombieConciencia} pc={level.PlayerConciencia} tier={level.DifficultyTier} " +
                $"hazards={level.HazardPositions?.Length ?? 0} sol={report.SolutionCount} unique={result.UniqueConfirmed} " +
                $"attempts={result.Attempts} pattern={level.PatternName} ms={sw.Elapsed.TotalMilliseconds:F0}");
        }

        Console.WriteLine($"success={success}/{seedCount} unique={unique} fallback={fallback} emergency={emergency} avgMs={totalMs / seedCount:F0}");
        Check(success == seedCount, "not every generation returned a level");
    }

    private static bool PlannedSolutionReachesSwitch(LevelDefinition level)
    {
        var state = level.CreateGameState();
        for (int i = 0; i < level.PlannedManualDirections.Length; i++)
        {
            var result = state.TryManualMove(level.PlannedManualDirections[i]);
            if (i < level.PlannedManualDirections.Length - 1 && result.Outcome != TurnOutcome.Moved) return false;
            if (i == level.PlannedManualDirections.Length - 1 && result.Outcome != TurnOutcome.Infected) return false;
        }
        if (state.Phase != GamePhase.InfectedPlayback) return false;
        if (state.PlaybackSequence.Count != level.ZombieConciencia + level.PlayerConciencia) return false;
        int guard = 0;
        while (state.Phase == GamePhase.InfectedPlayback && guard++ < 256)
        {
            var step = state.AdvancePlaybackStep();
            if (step.PlaybackStepBlocked) return false;
            if (step.Outcome == TurnOutcome.DamageDefeat) return false;
            if (step.Outcome == TurnOutcome.SwitchVictory) return state.Player.Position == level.SwitchPosition;
        }
        return false;
    }

    private static void TestDeterminism()
    {
        Console.WriteLine("\n-- Determinism --");
        var settings = new GenerationSettings { maxGenerationAttempts = 40, requireUniqueSolution = true };
        settings.Sanitize();
        var a = GeneradorNivel.Generate(7, 424242, settings);
        var b = GeneradorNivel.Generate(7, 424242, settings);
        Check(a.Level.Width == b.Level.Width && a.Level.Height == b.Level.Height, "dimensions not deterministic");
        Check(a.Level.PlayerStart == b.Level.PlayerStart, "start not deterministic");
        Check(a.Level.SwitchPosition == b.Level.SwitchPosition, "switch not deterministic");
        Check(a.Level.MaxEnergy == b.Level.MaxEnergy, "energy not deterministic");
        Check(a.Level.ZombieConciencia == b.Level.ZombieConciencia, "zombie conciencia not deterministic");
        Check(a.Level.PlayerConciencia == b.Level.PlayerConciencia, "player conciencia not deterministic");
        Check(a.Level.NpcRoute.Length == b.Level.NpcRoute.Length, "route length not deterministic");
        for (int i = 0; i < a.Level.NpcRoute.Length; i++)
            Check(a.Level.NpcRoute[i] == b.Level.NpcRoute[i], "route not deterministic");
        for (int y = 0; y < a.Level.Height; y++)
            for (int x = 0; x < a.Level.Width; x++)
                Check(a.Level.Board.GetTile(x, y) == b.Level.Board.GetTile(x, y), "board not deterministic");
        Check(a.Level.Bounds.MinX == b.Level.Bounds.MinX && a.Level.Bounds.MaxY == b.Level.Bounds.MaxY,
            "bounds not deterministic");
        Console.WriteLine("Same seed reproduces the same level");
    }

    private static void TestInitialLevelsConstraints()
    {
        Console.WriteLine("\n-- Initial Levels Constraints (Levels 1 & 2) & Hazard Reachability --");
        var settings = new GenerationSettings { maxGenerationAttempts = 40, requireUniqueSolution = false };
        settings.Sanitize();

        for (int lvl = 1; lvl <= 2; lvl++)
        {
            for (int s = 1; s <= 4; s++)
            {
                int seed = 1000 + lvl * 100 + s * 17;
                var res = GeneradorNivel.Generate(lvl, seed, settings, allowEmergency: true);
                Check(res != null && res.Success && res.Level != null, $"Lvl {lvl} seed {seed}: failed generation");
                var level = res?.Level;
                if (level == null) continue;

                // 1. Player -> Zombie route: strictly 3 to 4 tiles
                int manualLen = level.PlannedManualDirections.Length;
                Check(manualLen >= 3 && manualLen <= 4,
                    $"Lvl {lvl} seed {seed}: manual route length {manualLen} not in [3, 4]");

                // 2. Requires at least one turn (avoiding straight lines)
                Check(level.ManualTurnCount >= 1,
                    $"Lvl {lvl} seed {seed}: manual turn count {level.ManualTurnCount} < 1");

                // 3. Zombie's own route: max 4 tiles
                Check(level.NpcRoute.Length <= 4,
                    $"Lvl {lvl} seed {seed}: zombie route length {level.NpcRoute.Length} > 4");

                // 4. Infected Player -> Goal: max 5 tiles distance
                int autoSteps = level.PlannedAutoPositions.Length - 1;
                Check(autoSteps <= 5,
                    $"Lvl {lvl} seed {seed}: auto steps {autoSteps} > 5");

                // 5. Total walkable tiles: max 22
                int walkable = level.Board.CountWalkable();
                Check(walkable <= 22,
                    $"Lvl {lvl} seed {seed}: walkable tiles {walkable} > 22");

                // 6. Hazard accessibility: reachable from start without crossing switch, and not encapsulated
                if (level.HazardPositions != null)
                {
                    var reachable = new HashSet<GridPos>();
                    var q = new Queue<GridPos>();
                    if (level.Board.IsWalkable(level.PlayerStart) && !level.Board.IsSwitch(level.PlayerStart))
                    {
                        reachable.Add(level.PlayerStart);
                        q.Enqueue(level.PlayerStart);
                    }
                    while (q.Count > 0)
                    {
                        var curr = q.Dequeue();
                        for (int d = 0; d < Directions.All.Length; d++)
                        {
                            var n = curr + Directions.Delta(Directions.All[d]);
                            if (!level.Board.IsWalkable(n) || level.Board.IsSwitch(n)) continue;
                            if (reachable.Add(n)) q.Enqueue(n);
                        }
                    }

                    for (int h = 0; h < level.HazardPositions.Length; h++)
                    {
                        var hz = level.HazardPositions[h];
                        Check(reachable.Contains(hz),
                            $"Lvl {lvl} seed {seed}: hazard at {hz} not reachable from start without crossing goal");

                        int nonSwitchNeighbors = 0;
                        for (int d = 0; d < Directions.All.Length; d++)
                        {
                            var n = hz + Directions.Delta(Directions.All[d]);
                            if (level.Board.IsWalkable(n) && !level.Board.IsSwitch(n)) nonSwitchNeighbors++;
                        }
                        Check(nonSwitchNeighbors > 0,
                            $"Lvl {lvl} seed {seed}: hazard at {hz} encapsulated by walls/switch");
                    }
                }

                Console.WriteLine($"Lvl {lvl} seed {seed}: manualLen={manualLen} turns={level.ManualTurnCount} " +
                                  $"npcRoute={level.NpcRoute.Length} autoSteps={autoSteps} walkable={walkable} " +
                                  $"hazards={level.HazardPositions?.Length ?? 0} OK");
            }
        }
    }
}
