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
            Node node,
            GameState contactState,
            MoveResult result,
            Direction finalDirection,
            GridPos target,
            GridPos nextZombiePosition,
            int zombieConciencia,
            int playerConciencia,
            int maxEnergy,
            bool strict)
        {
            if (!contactState.ContactHasRequiredHistory())
            {
                Log?.Invoke($"      contact {contactState.Player.Position} turn {contactState.TurnNumber} rejected: history player={contactState.Player.HistoryCount} zombie={contactState.Zombie.HistoryCount}");
                return null;
            }

            var contactPosition = contactState.Player.Position;
            if (contactPosition != target || contactPosition != nextZombiePosition)
            {
                Log?.Invoke($"      contact rejected: position mismatch {contactPosition}/{target}/{nextZombiePosition}");
                return null;
            }

            var manualPositions = new List<GridPos>(node.Positions) { target };
            var manualDirections = new List<Direction>(node.Directions) { finalDirection };

            if (strict)
            {
                var seenManual = new HashSet<GridPos>();
                for (int i = 0; i < manualPositions.Count; i++)
                {
                    var p = manualPositions[i];
                    if (!seenManual.Add(p)) return null;
                    if (routeSet.Contains(p) && p != contactPosition) return null;
                }
            }

            var simulationState = contactState.Clone();
            var simulation = simulationState.SimulatePlaybackToEnd();
            if (simulation.AnyBlockedStep)
            {
                Log?.Invoke($"      contact {contactPosition} turn {contactState.TurnNumber} rejected: blocked playback");
                return null;
            }
            if (simulation.Positions.Count < 2)
            {
                Log?.Invoke($"      contact {contactPosition} turn {contactState.TurnNumber} rejected: empty playback");
                return null;
            }

            var autoPositions = new List<GridPos>(simulation.Positions);
            var manualSet = new HashSet<GridPos>(manualPositions);
            for (int i = 0; i < autoPositions.Count; i++)
            {
                var p = autoPositions[i];
                if (!board.IsWalkable(p)) return null;
            }

            var switchPosition = simulation.FinalPosition;
            if (!board.IsWalkable(switchPosition))
            {
                Log?.Invoke($"      contact {contactPosition} rejected: switch not walkable {switchPosition}");
                return null;
            }
            if (switchPosition == playerStart || manualSet.Contains(switchPosition))
            {
                Log?.Invoke($"      contact {contactPosition} rejected: switch overlaps start/manual {switchPosition}");
                return null;
            }

            // If the frozen sequence revisits the final cell, victory happens on the first
            // visit. Keep only the trajectory up to that first visit.
            int switchIndex = autoPositions.FindIndex(p => p == switchPosition);
            if (switchIndex < 0) return null;
            if (switchIndex + 1 < autoPositions.Count)
            {
                autoPositions.RemoveRange(switchIndex + 1, autoPositions.Count - switchIndex - 1);
            }

            // The switch must be impossible to reach with the manual energy budget.
            // Manhattan distance is a lower bound for any grid path.
            if (playerStart.ManhattanDistance(switchPosition) <= maxEnergy)
            {
                Log?.Invoke($"      contact {contactPosition} rejected: switch too close to start ({playerStart.ManhattanDistance(switchPosition)} <= {maxEnergy})");
                return null;
            }

            var plan = new SolutionPlan
            {
                ContactPosition = contactPosition,
                SwitchPosition = switchPosition,
                ContactTurn = contactState.TurnNumber,
                ZombiePhaseAtContact = contactState.ZombiePhase
            };
            plan.ManualDirections.AddRange(manualDirections);
            plan.ManualPositions.AddRange(manualPositions);
            plan.Sequence.AddRange(contactState.PlaybackSequence);
            plan.AutoPositions.AddRange(autoPositions);

            int score = 0;
            if (plan.ContactTurn == maxEnergy) score += 30;
            score += playerStart.ManhattanDistance(switchPosition) * 2;
            score += autoPositions.Count * 3;
            if (strict) score += 10;
            if (contactState.PlaybackSequence.Count >= 3 + zombieConciencia) score += 5;
            plan.Score = score;
            return plan;
        }

        private static bool IsAdjacentToOtherRoute(GridPos cell, HashSet<GridPos> routeSet, GridPos? allowedRouteCell)
        {
            for (int i = 0; i < Directions.All.Length; i++)
            {
                var neighbor = cell + Directions.Delta(Directions.All[i]);
                if (!routeSet.Contains(neighbor)) continue;
                if (allowedRouteCell.HasValue && neighbor == allowedRouteCell.Value) continue;
                return true;
            }
            return false;
        }
    }
}
