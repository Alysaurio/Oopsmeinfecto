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
                maxGenerationAttempts = 2,
                maxPlannerStates = 30000,
                maxValidatorStates = 30000,
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
        bool quick = args.Length > 0 && args[0] == "quick";
        TestGeneration(seedCount: quick ? 2 : 20, requireUnique: true, attempts: quick ? 8 : 60);
        TestGeneration(seedCount: quick ? 1 : 5, requireUnique: false, attempts: quick ? 4 : 20);
        TestDeterminism();
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
        var zombie = new EntityState(1, EntityKind.Zombie, new GridPos(5, 5), 3, 7);
        var player = new EntityState(0, EntityKind.Player, new GridPos(0, 0), 3, 7);
        for (int i = 0; i < 9; i++)
        {
            zombie.RecordMove(Directions.All[i % 4]);
            player.RecordMove(Directions.All[(i + 1) % 4]);
        }
        Check(zombie.HistoryCount == 7, "zombie history cap is not 7");
        Check(player.HistoryCount == 7, "player history cap is not 7");
        var sequence = InfectionSystem.BuildSequence(zombie, player);
        Check(sequence.Count == 6, "sequence should be 3 + conciencia(3) = 6");
        var expectedZombie = zombie.GetLastDirections(3);
        var expectedPlayer = player.GetLastDirections(3);
        for (int i = 0; i < 3; i++)
        {
            Check(sequence[i] == expectedZombie[i], "zombie part of sequence mismatch");
            Check(sequence[i + 3] == expectedPlayer[i], "player part of sequence mismatch");
        }
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

        // Switch: immediate victory, NPC does not move.
        var switchBoard = new BoardData(5, 3, TileType.Floor);
        switchBoard.SetTile(new GridPos(1, 0), TileType.Switch);
        var switchState = new GameState(switchBoard, new GridPos(0, 0), route, 2, 3, 5, 7);
        var switchResult = switchState.TryManualMove(Direction.Right);
        Check(switchResult.Outcome == TurnOutcome.SwitchVictory, "switch did not win");
        Check(switchState.Zombie.HistoryCount == 0, "NPC moved after switch victory");

        // Energy exhaustion without contact.
        var exhaustBoard = new BoardData(8, 3, TileType.Floor);
        var exhaustRoute = new[] { new GridPos(7, 2), new GridPos(7, 1) };
        var exhaust = new GameState(exhaustBoard, new GridPos(0, 0), exhaustRoute, 2, 3, 2, 7);
        var first = exhaust.TryManualMove(Direction.Right);
        Check(first.Outcome == TurnOutcome.Moved, "exhaust first move failed");
        var second = exhaust.TryManualMove(Direction.Right);
        Check(second.Outcome == TurnOutcome.EnergyDefeat, "energy exhaustion did not defeat");
        Check(exhaust.Phase == GamePhase.Defeat, "energy exhaustion did not set Defeat phase");

        // Contact on the last energy unit has priority over exhaustion.
        var contactBoard = new BoardData(5, 3, TileType.Floor);
        var contactRoute = new[] { new GridPos(2, 0), new GridPos(1, 0) };
        var contact = new GameState(contactBoard, new GridPos(0, 0), contactRoute, 2, 3, 1, 7);
        var contactResult = contact.TryManualMove(Direction.Right);
        Check(contactResult.Outcome == TurnOutcome.Infected, "contact at zero energy did not infect");
        Check(contact.Phase == GamePhase.InfectedPlayback, "contact did not enter playback");

        Console.WriteLine("Blocked movement, energy, damage, switch and contact priority OK");
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
                Check(level.ZombieConciencia >= 2 && level.ZombieConciencia <= 4, $"seed {i}: bad conciencia");
                Check(PlannedSolutionReachesSwitch(level), $"seed {i}: independent planned solution simulation failed");
            }

            Console.WriteLine(
                $"seed {i,3}: {level.Width}x{level.Height} energy={level.MaxEnergy} conciencia={level.ZombieConciencia} " +
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
        var settings = new GenerationSettings { maxGenerationAttempts = 30, requireUniqueSolution = true };
        settings.Sanitize();
        var a = GeneradorNivel.Generate(7, 424242, settings);
        var b = GeneradorNivel.Generate(7, 424242, settings);
        Check(a.Level.Width == b.Level.Width && a.Level.Height == b.Level.Height, "dimensions not deterministic");
        Check(a.Level.PlayerStart == b.Level.PlayerStart, "start not deterministic");
        Check(a.Level.SwitchPosition == b.Level.SwitchPosition, "switch not deterministic");
        Check(a.Level.MaxEnergy == b.Level.MaxEnergy, "energy not deterministic");
        Check(a.Level.ZombieConciencia == b.Level.ZombieConciencia, "conciencia not deterministic");
        Check(a.Level.NpcRoute.Length == b.Level.NpcRoute.Length, "route length not deterministic");
        for (int i = 0; i < a.Level.NpcRoute.Length; i++)
            Check(a.Level.NpcRoute[i] == b.Level.NpcRoute[i], "route not deterministic");
        for (int y = 0; y < a.Level.Height; y++)
            for (int x = 0; x < a.Level.Width; x++)
                Check(a.Level.Board.GetTile(x, y) == b.Level.Board.GetTile(x, y), "board not deterministic");
        Console.WriteLine("Same seed reproduces the same level");
    }
}
