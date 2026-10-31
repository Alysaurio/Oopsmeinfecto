using System;
using System.Collections.Generic;

namespace Oopsmeinfecto
{
    public static partial class PlanificadorSolucion
    {
        private static SolutionPlan TryBuildPlan(
            BoardData board,
            GridPos playerStart,
            PatternPlacement placement,
            CyclicRoutePattern pattern,
            HashSet<GridPos> routeSet,
            IReadOnlyList<Direction> manualDirections,
            int zombieConciencia,
            int playerConciencia,
            int maxEnergy,
            DifficultyProfile profile,
            int initialOptions,
            SeededRandom rng)
        {
            if (manualDirections == null || manualDirections.Count == 0) return null;

            SolutionPlan Reject(string reason)
            {
                Log?.Invoke("      plan reject: " + reason);
                return null;
            }

            var state = new GameState(
                board, playerStart, placement.Route, zombieConciencia,
                playerConciencia, maxEnergy, 7);

            var manualPositions = new List<GridPos> { playerStart };
            for (int i = 0; i < manualDirections.Count; i++)
            {
                var result = state.TryManualMove(manualDirections[i]);
                bool last = i == manualDirections.Count - 1;
                if (last)
                {
                    if (result.Outcome != TurnOutcome.Infected) return Reject("last manual move was " + result.Outcome);
                }
                else if (result.Outcome != TurnOutcome.Moved)
                {
                    return Reject("manual move " + i + " was " + result.Outcome);
                }
                manualPositions.Add(state.Player.Position);
            }

            if (state.Phase != GamePhase.InfectedPlayback) return Reject("not in playback");
            if (!state.ContactHasRequiredHistory()) return Reject("missing history");
            if (state.PlaybackSequence.Count != zombieConciencia + playerConciencia)
                return Reject("sequence length " + state.PlaybackSequence.Count);

            var simulationState = state.Clone();
            var simulation = simulationState.SimulatePlaybackToEnd();
            if (simulation.AnyBlockedStep) return Reject("blocked playback step");
            if (simulation.Outcome != GamePhase.Defeat ||
                simulation.DefeatReason != DefeatReason.PlaybackExhausted)
                return Reject("playback outcome " + simulation.Outcome + "/" + simulation.DefeatReason);

            var autoPositions = new List<GridPos>(simulation.Positions);
            if (autoPositions.Count != state.PlaybackSequence.Count + 1) return Reject("auto positions " + autoPositions.Count);

            // The switch does not exist yet: the planner chooses the end of the inherited
            // sequence as the switch cell. It must not be visited earlier, or the real game
            // would win before the sequence is consumed.
            var switchPosition = simulation.FinalPosition;
            if (autoPositions[autoPositions.Count - 1] != switchPosition) return Reject("switch not final");
            if (autoPositions.Count - 1 < zombieConciencia + 2) return Reject("auto steps too short");

            var manualSet = new HashSet<GridPos>(manualPositions);
            if (switchPosition == playerStart || manualSet.Contains(switchPosition)) return Reject("switch on start/manual path");

            int switchVisits = 0;
            for (int i = 0; i < autoPositions.Count; i++)
            {
                if (autoPositions[i] == switchPosition) switchVisits++;
            }
            if (switchVisits != 1) return Reject("switch visits " + switchVisits);

            for (int i = 0; i < autoPositions.Count; i++)
            {
                if (!board.IsWalkable(autoPositions[i])) return Reject("auto path blocked");
            }

            int turns = 0;
            int reversals = 0;
            int maxStraight = 1;
            int run = 1;
            var distinct = new HashSet<Direction>();
            for (int i = 0; i < manualDirections.Count; i++)
            {
                var direction = manualDirections[i];
                distinct.Add(direction);
                if (i > 0)
                {
                    var previous = manualDirections[i - 1];
                    if (direction != previous) turns++;
                    if (direction == Directions.Opposite(previous)) reversals++;
                    run = direction == previous ? run + 1 : 1;
                    if (run > maxStraight) maxStraight = run;
                }
            }

            var seenPositions = new HashSet<GridPos>();
            int revisits = 0;
            for (int i = 0; i < manualPositions.Count; i++)
            {
                if (!seenPositions.Add(manualPositions[i])) revisits++;
            }

            var plan = new SolutionPlan
            {
                ContactPosition = state.Player.Position,
                SwitchPosition = switchPosition,
                ContactTurn = state.TurnNumber,
                ZombiePhaseAtContact = state.ZombiePhase,
                ManualTurnCount = turns,
                DistinctDirections = distinct.Count,
                MaxStraightRun = maxStraight,
                ImmediateReversals = reversals,
                RevisitedCells = revisits,
                InitialDistance = playerStart.ManhattanDistance(placement.Route[0]),
                InitialOptions = initialOptions,
                AutomaticSteps = autoPositions.Count - 1,
                SwitchDistance = playerStart.ManhattanDistance(switchPosition),
                HasBacktrack = reversals > 0 || revisits > 0
            };
            plan.ManualDirections.AddRange(manualDirections);
            plan.ManualPositions.AddRange(manualPositions);
            plan.Sequence.AddRange(state.PlaybackSequence);
            plan.AutoPositions.AddRange(autoPositions);

            plan.MeetsDifficulty = profile.Accepts(plan, maxEnergy, zombieConciencia, playerConciencia);
            plan.Score = profile.Score(plan, maxEnergy) + rng.Range(0, 11);
            return plan;
        }
    }
}

