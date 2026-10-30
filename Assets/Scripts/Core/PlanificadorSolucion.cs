using System;
using System.Collections.Generic;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Constructive solution planner. It searches the real turn engine for a manual path that
    /// ends in contact, then freezes and simulates the inherited sequence to find the switch cell.
    /// </summary>
    public static partial class PlanificadorSolucion
    {
        /// <summary>Optional diagnostic hook used by the external harness; null in the game.</summary>
        public static Action<string> Log;

        private sealed class Node
        {
            public GameState State;
            public List<Direction> Directions = new List<Direction>();
            public List<GridPos> Positions = new List<GridPos>();
            public HashSet<GridPos> Visited = new HashSet<GridPos>();
            public int Depth;
        }

        public static List<SolutionPlan> FindCandidates(
            BoardData openBoard,
            GridPos playerStart,
            PatternPlacement placement,
            int zombieConciencia,
            int playerConciencia,
            int maxEnergy,
            GenerationSettings settings)
        {
            if (openBoard == null || placement == null) return new List<SolutionPlan>();

            var strict = Search(openBoard, playerStart, placement, zombieConciencia, playerConciencia, maxEnergy, settings, true);
            if (strict.Count > 0) return strict;
            return Search(openBoard, playerStart, placement, zombieConciencia, playerConciencia, maxEnergy, settings, false);
        }

        private static List<SolutionPlan> Search(
            BoardData board,
            GridPos playerStart,
            PatternPlacement placement,
            int zombieConciencia,
            int playerConciencia,
            int maxEnergy,
            GenerationSettings settings,
            bool strict)
        {
            var results = new List<SolutionPlan>();
            if (!board.IsWalkable(playerStart)) return results;

            var route = placement.Route;
            var routeSet = new HashSet<GridPos>(route);
            var pattern = new CyclicRoutePattern(route);

            var startState = new GameState(board, playerStart, route, zombieConciencia, playerConciencia, maxEnergy, settings.historyCapacity);
            var startNode = new Node { State = startState, Depth = 0 };
            startNode.Positions.Add(playerStart);
            startNode.Visited.Add(playerStart);

            var queue = new Queue<Node>();
            queue.Enqueue(startNode);
            int explored = 0;
            int contacts = 0;

            while (queue.Count > 0 && explored < settings.maxPlannerStates)
            {
                var node = queue.Dequeue();
                explored++;
                if (node.Depth >= maxEnergy) continue;

                for (int d = 0; d < Directions.All.Length; d++)
                {
                    var direction = Directions.All[d];
                    var target = node.State.Player.Position + Directions.Delta(direction);
                    if (!board.IsWalkable(target)) continue;
                    if (strict && node.Visited.Contains(target)) continue;

                    int nextPhase = (node.State.ZombiePhase + 1) % pattern.Length;
                    var nextZombiePosition = pattern.PositionAt(nextPhase);

                    if (strict)
                    {
                        // A route cell can only be entered when it is the contact cell.
                        if (routeSet.Contains(target) && target != nextZombiePosition) continue;
                    }

                    var childState = node.State.Clone();
                    var result = childState.TryManualMove(direction);
                    if (result.Outcome == TurnOutcome.Blocked) continue;

                    if (result.Outcome == TurnOutcome.Infected)
                    {
                        contacts++;
                        var plan = TryBuildPlan(
                            board, playerStart, placement, pattern, routeSet,
                            node, childState, result, direction, target, nextZombiePosition,
                            zombieConciencia, playerConciencia, maxEnergy, strict);

                        if (plan != null) results.Add(plan);
                        continue;
                    }

                    if (result.Outcome != TurnOutcome.Moved) continue;

                    var childNode = new Node
                    {
                        State = childState,
                        Depth = node.Depth + 1,
                        Directions = new List<Direction>(node.Directions) { direction },
                        Positions = new List<GridPos>(node.Positions) { target },
                        Visited = new HashSet<GridPos>(node.Visited) { target }
                    };
                    queue.Enqueue(childNode);
                }
            }

            Log?.Invoke($"    planner strict={strict} start={playerStart} explored={explored} contacts={contacts} plans={results.Count}");
            results.Sort((a, b) => b.Score.CompareTo(a.Score));
            if (results.Count > settings.maxPlanCandidates)
                results.RemoveRange(settings.maxPlanCandidates, results.Count - settings.maxPlanCandidates);
            return results;
        }
    }
}
