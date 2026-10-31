using System;
using System.Collections.Generic;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Constructive solution planner. It searches the real turn engine for manual paths that end
    /// in contact, then freezes and simulates the inherited sequence to find the switch cell.
    ///
    /// The search is deliberately not a shortest-path BFS: for every allowed route length it
    /// explores several different paths and scores them by length, turns, direction variety,
    /// immediate reversals, revisits and initial distance. This produces deterministic but
    /// non-trivial pre-contact trajectories.
    /// </summary>
    public static partial class PlanificadorSolucion
    {
        /// <summary>Optional diagnostic hook used by the external harness; null in the game.</summary>
        public static Action<string> Log;

        public static List<SolutionPlan> FindCandidates(
            BoardData openBoard,
            GridPos playerStart,
            PatternPlacement placement,
            int zombieConciencia,
            int playerConciencia,
            int maxEnergy,
            GenerationSettings settings,
            int levelIndex,
            SeededRandom rng)
        {
            var results = new List<SolutionPlan>();
            if (openBoard == null || placement == null || placement.Route == null) return results;
            if (!openBoard.IsWalkable(playerStart)) return results;

            var profile = DifficultyProfile.ForLevel(levelIndex);
            var routeSet = new HashSet<GridPos>(placement.Route);
            var pattern = new CyclicRoutePattern(placement.Route);
            int initialOptions = CountInitialOptions(openBoard, playerStart, routeSet);
            if (initialOptions < profile.MinInitialOptions) return results;

            int minDepth = Math.Max(Math.Max(zombieConciencia, playerConciencia), profile.MinManualMoves);
            int maxDepth = Math.Min(maxEnergy, profile.MaxManualMoves);
            if (minDepth > maxDepth) return results;

            int budget = Math.Max(2000, settings.maxPlannerStates / 8);
            int explored = 0;
            int contacts = 0;
            int perDepthLimit = Math.Max(4, Math.Min(10, settings.maxPlanCandidates));
            var seenPaths = new HashSet<string>();

            for (int depth = minDepth; depth <= maxDepth && explored < budget; depth++)
            {
                var target = pattern.PositionAt(depth);
                if (target == playerStart) continue;

                var paths = new List<List<Direction>>();
                var path = new List<GridPos> { playerStart };
                var directions = new List<Direction>();
                var visited = new HashSet<GridPos> { playerStart };
                ExploreToTarget(
                    openBoard, playerStart, target, depth, routeSet, profile,
                    path, directions, visited, 0, 0, 1, null,
                    paths, perDepthLimit, ref explored, budget, rng);
                Log?.Invoke($"      depth {depth} target {target} paths {paths.Count} explored {explored}");

                for (int p = 0; p < paths.Count; p++)
                {
                    string key = Directions.SequenceToString(paths[p]);
                    if (!seenPaths.Add(key)) continue;

                    var plan = TryBuildPlan(
                        openBoard, playerStart, placement, pattern, routeSet,
                        paths[p], zombieConciencia, playerConciencia, maxEnergy,
                        profile, initialOptions, rng);
                    if (plan == null) continue;
                    contacts++;
                    results.Add(plan);
                }
            }

            Log?.Invoke($"    planner start={playerStart} profile={profile} explored={explored} contacts={contacts} plans={results.Count}");
            results.Sort(ComparePlans);
            if (results.Count > settings.maxPlanCandidates)
                results.RemoveRange(settings.maxPlanCandidates, results.Count - settings.maxPlanCandidates);
            return results;
        }

        private static int ComparePlans(SolutionPlan a, SolutionPlan b)
        {
            if (a.MeetsDifficulty != b.MeetsDifficulty) return a.MeetsDifficulty ? -1 : 1;
            return b.Score.CompareTo(a.Score);
        }

        private static void ExploreToTarget(
            BoardData board,
            GridPos start,
            GridPos target,
            int depth,
            HashSet<GridPos> routeSet,
            DifficultyProfile profile,
            List<GridPos> path,
            List<Direction> directions,
            HashSet<GridPos> visited,
            int revisitedCells,
            int immediateReversals,
            int straightRun,
            Direction? lastDirection,
            List<List<Direction>> output,
            int maxOutput,
            ref int explored,
            int budget,
            SeededRandom rng)
        {
            if (output.Count >= maxOutput || explored >= budget) return;

            int remaining = depth - directions.Count;
            if (remaining == 0)
            {
                if (path[path.Count - 1] == target)
                {
                    output.Add(new List<Direction>(directions));
                    explored++;
                }
                return;
            }

            var current = path[path.Count - 1];
            if (current.ManhattanDistance(target) > remaining) return;
            explored++;

            var order = new List<Direction>(Directions.All);
            rng.Shuffle(order);

            for (int i = 0; i < order.Count; i++)
            {
                var direction = order[i];
                var next = current + Directions.Delta(direction);

                if (!board.IsWalkable(next)) continue;
                if (remaining > 1)
                {
                    if (routeSet.Contains(next)) continue;
                }
                else if (next != target)
                {
                    continue;
                }

                bool revisits = visited.Contains(next);
                int nextRevisited = revisitedCells + (revisits ? 1 : 0);
                if (nextRevisited > profile.MaxRevisitedCells) continue;

                int nextReversals = immediateReversals;
                if (lastDirection.HasValue && direction == Directions.Opposite(lastDirection.Value))
                    nextReversals++;
                if (nextReversals > profile.MaxImmediateReversals) continue;

                int nextRun = lastDirection.HasValue && lastDirection.Value == direction
                    ? straightRun + 1
                    : 1;
                if (nextRun > profile.MaxStraightRun) continue;

                path.Add(next);
                directions.Add(direction);
                bool added = visited.Add(next);
                ExploreToTarget(
                    board, start, target, depth, routeSet, profile,
                    path, directions, visited, nextRevisited, nextReversals, nextRun,
                    direction, output, maxOutput, ref explored, budget, rng);
                if (added) visited.Remove(next);
                directions.RemoveAt(directions.Count - 1);
                path.RemoveAt(path.Count - 1);

                if (output.Count >= maxOutput || explored >= budget) return;
            }
        }

        private static int CountInitialOptions(BoardData board, GridPos start, HashSet<GridPos> routeSet)
        {
            int count = 0;
            for (int i = 0; i < Directions.All.Length; i++)
            {
                var next = start + Directions.Delta(Directions.All[i]);
                if (!board.IsWalkable(next)) continue;
                if (routeSet.Contains(next)) continue;
                count++;
            }
            return count;
        }
    }
}
