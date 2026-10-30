using System;
using System.Collections.Generic;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Verifies a finished level with the same turn engine used by the game. The optional
    /// uniqueness search is implemented in ValidadorNivel.Uniqueness.cs.
    /// </summary>
    public static partial class ValidadorNivel
    {
        public static ValidationReport Validate(LevelDefinition level, GenerationSettings settings, bool checkUniqueness = true)
        {
            var report = new ValidationReport { Valid = true };
            if (level == null)
            {
                report.AddError("level is null");
                return report;
            }
            if (level.Board == null)
            {
                report.AddError("board is null");
                return report;
            }

            if (level.Width < 6 || level.Width > 16 || level.Height < 6 || level.Height > 16)
                report.AddError("dimensions outside 6..16");
            if (level.Board.Width != level.Width || level.Board.Height != level.Height)
                report.AddError("board dimensions do not match level dimensions");
            if (level.MaxEnergy < 5 || level.MaxEnergy > 10)
                report.AddError("max energy outside 5..10");
            if (level.ZombieConciencia < 2 || level.ZombieConciencia > 4)
                report.AddError("zombie conciencia outside 2..4");
            if (level.PlayerConciencia < 0)
                report.AddError("negative player conciencia");
            if (level.HistoryCapacity < 7)
                report.AddError("history capacity below 7");

            if (!level.Board.IsWalkable(level.PlayerStart)) report.AddError("player start is not walkable");
            if (!level.Board.IsWalkable(level.ZombieStart)) report.AddError("zombie start is not walkable");
            if (!level.Board.HasSwitch) report.AddError("board has no switch");
            if (level.Board.HasSwitch && level.Board.SwitchPosition != level.SwitchPosition)
                report.AddError("switch position mismatch");
            if (!level.Board.IsSwitch(level.SwitchPosition)) report.AddError("switch cell is not a switch tile");

            if (level.NpcRoute == null || level.NpcRoute.Length < 2)
            {
                report.AddError("npc route is too short");
            }
            else
            {
                if (!CyclicRoutePattern.IsValidRoute(level.NpcRoute, out var routeError))
                    report.AddError("npc route invalid: " + routeError);
                if (level.NpcRoute[0] != level.ZombieStart)
                    report.AddError("zombie start is not the first route position");
                for (int i = 0; i < level.NpcRoute.Length; i++)
                {
                    if (!level.Board.IsInside(level.NpcRoute[i])) report.AddError("npc route outside board");
                    else if (!level.Board.IsWalkable(level.NpcRoute[i])) report.AddError("npc route crosses a wall");
                }
            }

            var mandatory = level.GetMandatoryPositions();
            foreach (var p in mandatory)
            {
                if (!level.Board.IsWalkable(p)) report.AddError($"mandatory cell {p} is not walkable");
                if (level.Board.IsDamage(p)) report.AddError($"mandatory cell {p} is a damage tile");
            }

            var routeSet = level.GetRoutePositions();
            if (level.PlayerStart.ManhattanDistance(level.SwitchPosition) <= level.MaxEnergy)
                report.AddError("switch can be reached manually within the energy budget");

            if (level.HazardPositions == null || level.HazardPositions.Length < 1)
            {
                report.AddError("level has no damage tiles");
            }
            else
            {
                var hazardSet = new HashSet<GridPos>();
                for (int i = 0; i < level.HazardPositions.Length; i++)
                {
                    var p = level.HazardPositions[i];
                    if (!level.Board.IsInside(p) || !level.Board.IsDamage(p))
                        report.AddError($"hazard {p} is not a damage tile");
                    if (!hazardSet.Add(p)) report.AddError($"duplicate hazard {p}");
                    if (mandatory.Contains(p)) report.AddError($"hazard {p} is on a mandatory cell");
                    if (routeSet.Contains(p)) report.AddError($"hazard {p} is on the npc route");
                    if (p == level.PlayerStart || p == level.ZombieStart || p == level.SwitchPosition)
                        report.AddError($"hazard {p} overlaps an entity or the switch");
                }
            }

            if (level.PlannedManualDirections == null || level.PlannedManualDirections.Length == 0)
                report.AddError("planned manual trajectory is empty");
            if (level.PlannedManualPositions == null || level.PlannedManualPositions.Length !=
                (level.PlannedManualDirections?.Length ?? 0) + 1)
                report.AddError("planned manual positions do not match directions");
            if (level.PlannedSequence == null || level.PlannedSequence.Length < 3 + level.ZombieConciencia)
                report.AddError("planned inherited sequence is too short");
            if (level.PlannedAutoPositions == null || level.PlannedAutoPositions.Length < 2)
                report.AddError("planned automatic trajectory is empty");
            if (level.PlannedAutoPositions != null && level.PlannedAutoPositions.Length > 0 &&
                level.PlannedAutoPositions[level.PlannedAutoPositions.Length - 1] != level.SwitchPosition)
                report.AddError("switch is not at the end of the planned automatic trajectory");

            if (!report.Valid) return report;

            if (!SimulatePlannedSolution(level, report)) return report;

            if (!checkUniqueness)
            {
                report.Valid = true;
                report.UniqueConfirmed = false;
                report.Message = "structure and planned solution are valid";
                return report;
            }

            int solutions = 0;
            int states = 0;
            bool unknown = false;
            solutions = CountSolutions(level, settings, out states, out unknown);
            report.SolutionCount = solutions;
            report.StatesExplored = states;

            if (unknown)
            {
                report.UniqueConfirmed = false;
                if (settings.requireUniqueSolution)
                    report.AddError("uniqueness could not be confirmed within the validator state limit");
            }
            else if (solutions == 0)
            {
                report.AddError("validator found no winning manual sequence");
            }
            else if (solutions == 1)
            {
                report.UniqueConfirmed = true;
                report.Message = "valid level with one solution";
            }
            else
            {
                report.UniqueConfirmed = false;
                if (settings.requireUniqueSolution)
                    report.AddError($"validator found {solutions}+ winning manual sequences");
                else
                    report.Message = "valid level (multiple solutions allowed)";
            }

            return report;
        }

        private static bool SimulatePlannedSolution(LevelDefinition level, ValidationReport report)
        {
            var state = level.CreateGameState();
            var manual = level.PlannedManualDirections;
            var manualPositions = level.PlannedManualPositions;

            for (int i = 0; i < manual.Length; i++)
            {
                var result = state.TryManualMove(manual[i]);
                if (i < manual.Length - 1)
                {
                    if (result.Outcome != TurnOutcome.Moved)
                    {
                        report.AddError($"planned manual step {i} ended with {result.Outcome}");
                        return false;
                    }
                }
                else
                {
                    if (result.Outcome != TurnOutcome.Infected)
                    {
                        report.AddError($"planned contact did not happen: {result.Outcome}");
                        return false;
                    }
                }

                if (state.Player.Position != manualPositions[i + 1])
                {
                    report.AddError($"planned manual position {i + 1} mismatch");
                    return false;
                }
            }

            if (state.TurnNumber != level.PlannedManualDirections.Length)
            {
                report.AddError("contact turn mismatch");
                return false;
            }
            if (state.Player.Position != manualPositions[manualPositions.Length - 1])
            {
                report.AddError("contact position mismatch");
                return false;
            }
            if (!DirectionsEqual(state.PlaybackSequence, level.PlannedSequence))
            {
                report.AddError("inherited sequence does not match the real turn engine");
                return false;
            }

            var autoPositions = new List<GridPos> { state.Player.Position };
            bool won = false;
            int guard = 0;
            while (state.Phase == GamePhase.InfectedPlayback && guard++ < 256)
            {
                var step = state.AdvancePlaybackStep();
                if (step.PlaybackStepBlocked)
                {
                    report.AddError("planned automatic trajectory contains a blocked move");
                    return false;
                }
                if (step.Moved) autoPositions.Add(state.Player.Position);
                if (step.Outcome == TurnOutcome.SwitchVictory)
                {
                    won = true;
                    break;
                }
                if (step.Outcome == TurnOutcome.DamageDefeat)
                {
                    report.AddError("planned automatic trajectory enters a damage tile");
                    return false;
                }
                if (step.Outcome == TurnOutcome.PlaybackExhausted)
                {
                    break;
                }
            }

            if (!won || state.Phase != GamePhase.Victory)
            {
                report.AddError("planned automatic sequence does not reach the switch");
                return false;
            }
            if (state.Player.Position != level.SwitchPosition)
            {
                report.AddError("player does not finish on the switch");
                return false;
            }
            if (autoPositions.Count != level.PlannedAutoPositions.Length)
            {
                report.AddError("planned automatic trajectory length mismatch");
                return false;
            }
            for (int i = 0; i < autoPositions.Count; i++)
            {
                if (autoPositions[i] != level.PlannedAutoPositions[i])
                {
                    report.AddError($"planned automatic position {i} mismatch");
                    return false;
                }
            }

            return true;
        }

        private static bool DirectionsEqual(IReadOnlyList<Direction> a, IReadOnlyList<Direction> b)
        {
            if (a == null || b == null || a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }
}
