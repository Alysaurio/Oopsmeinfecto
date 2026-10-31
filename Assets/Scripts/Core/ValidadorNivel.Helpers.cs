using System;
using System.Collections.Generic;

namespace Oopsmeinfecto
{
    public static partial class ValidadorNivel
    {
        /// <summary>
        /// Returns true when the player can step on the switch while still human-controlled.
        /// Hazards are lethal, so they are treated as impassable for this reachability check.
        /// The switch is only a valid goal during the inherited playback, so any manual route
        /// that reaches it would invalidate the level.
        /// </summary>
        private static bool CanReachSwitchManually(LevelDefinition level, int maxStates)
        {
            if (level == null || level.Board == null) return false;
            if (level.PlayerStart == level.SwitchPosition) return true;

            var start = level.PlayerStart;
            var target = level.SwitchPosition;
            var visited = new HashSet<GridPos> { start };
            var queue = new Queue<GridPos>();
            var distance = new Dictionary<GridPos, int> { [start] = 0 };
            queue.Enqueue(start);
            int explored = 0;

            while (queue.Count > 0)
            {
                if (explored++ >= maxStates) break;
                var current = queue.Dequeue();
                int currentDistance = distance[current];
                if (currentDistance >= level.MaxEnergy) continue;

                for (int i = 0; i < Directions.All.Length; i++)
                {
                    var next = current + Directions.Delta(Directions.All[i]);
                    if (!level.Board.IsWalkable(next)) continue;
                    if (level.Board.IsDamage(next)) continue;
                    if (!visited.Add(next)) continue;

                    int nextDistance = currentDistance + 1;
                    if (next == target) return true;
                    distance[next] = nextDistance;
                    queue.Enqueue(next);
                }
            }

            return false;
        }

        private static int CountInitialOptions(LevelDefinition level, HashSet<GridPos> routeSet)
        {
            if (level == null || level.Board == null) return 0;
            int count = 0;
            for (int i = 0; i < Directions.All.Length; i++)
            {
                var next = level.PlayerStart + Directions.Delta(Directions.All[i]);
                if (!level.Board.IsWalkable(next)) continue;
                if (level.Board.IsDamage(next)) continue;
                if (routeSet != null && routeSet.Contains(next)) continue;
                count++;
            }
            return count;
        }

        private static int MinDistanceToRoute(GridPos position, GridPos[] route)
        {
            if (route == null || route.Length == 0) return int.MaxValue;
            int best = int.MaxValue;
            for (int i = 0; i < route.Length; i++)
            {
                int distance = position.ManhattanDistance(route[i]);
                if (distance < best) best = distance;
            }
            return best;
        }

        private static void AddTrajectory(HashSet<GridPos> set, IReadOnlyList<GridPos> cells)
        {
            if (set == null || cells == null) return;
            for (int i = 0; i < cells.Count; i++) set.Add(cells[i]);
        }

        private static int DistanceToTrajectory(GridPos position, HashSet<GridPos> trajectory)
        {
            if (trajectory == null || trajectory.Count == 0) return int.MaxValue;
            int best = int.MaxValue;
            foreach (var cell in trajectory)
            {
                int distance = position.ManhattanDistance(cell);
                if (distance < best) best = distance;
            }
            return best;
        }
    }
}
