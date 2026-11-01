using System;
using System.Collections.Generic;

namespace Oopsmeinfecto
{
    public static class HazardPlacer
    {
        public static int GetMaxHazards(int width, int height, int cap)
        {
            int byArea = (int)Math.Ceiling((width * height) / 50.0);
            return Math.Max(1, Math.Min(Math.Max(1, cap), byArea));
        }

        public static List<GridPos> Place(
            BoardData board,
            IReadOnlyList<GridPos> optionalCells,
            HashSet<GridPos> protectedCells,
            IReadOnlyList<GridPos> previousTrajectory,
            IReadOnlyList<GridPos> finalTrajectory,
            IReadOnlyList<GridPos> npcRoute,
            int maxHazardsCap,
            SeededRandom rng)
        {
            if (board == null || optionalCells == null || protectedCells == null) return null;

            int maxHazards = GetMaxHazards(board.Width, board.Height, maxHazardsCap);
            int count = rng.Range(1, maxHazards);

            var trajectory = new HashSet<GridPos>();
            AddTrajectory(trajectory, previousTrajectory);
            AddTrajectory(trajectory, finalTrajectory);
            AddTrajectory(trajectory, npcRoute);

            // Compute the set of cells reachable by the player without crossing the Goal (Meta)
            var reachable = ComputeReachableFloor(board, previousTrajectory);

            var preferred = new List<GridPos>();
            var relaxed = new List<GridPos>();
            for (int i = 0; i < optionalCells.Count; i++)
            {
                var p = optionalCells[i];
                if (!board.IsWalkable(p)) continue;
                if (board.IsSwitch(p) || board.IsDamage(p)) continue;
                if (protectedCells.Contains(p)) continue;
                if (!IsInterior(board, p)) continue;
                if (CountWalkableNeighbors(board, p) == 0) continue;

                // Candidate must be reachable by the player from start without crossing through the Meta
                if (!reachable.Contains(p)) continue;

                // Discard if encapsulated by walls and/or the Meta (must have at least one non-switch walkable neighbor)
                if (CountWalkableNonSwitchNeighbors(board, p) == 0) continue;

                int distance = DistanceToTrajectory(p, trajectory);
                if (distance >= 1 && distance <= 2) preferred.Add(p);
                else if (distance <= 3) relaxed.Add(p);
            }

            rng.Shuffle(preferred);
            rng.Shuffle(relaxed);

            var candidatePool = new List<GridPos>(preferred);
            if (candidatePool.Count < count)
            {
                for (int i = 0; i < relaxed.Count; i++)
                {
                    if (!candidatePool.Contains(relaxed[i]))
                        candidatePool.Add(relaxed[i]);
                }
            }

            if (candidatePool.Count < count) return null;

            var hazards = new List<GridPos>(count);
            for (int i = 0; i < candidatePool.Count && hazards.Count < count; i++)
            {
                var candidate = candidatePool[i];
                if (TooCloseToHazards(candidate, hazards)) continue;
                hazards.Add(candidate);
            }

            // If the spread rule was too strict, fill from the same useful candidates.
            for (int i = 0; i < candidatePool.Count && hazards.Count < count; i++)
            {
                var candidate = candidatePool[i];
                if (hazards.Contains(candidate)) continue;
                hazards.Add(candidate);
            }

            if (hazards.Count < count) return null;

            for (int i = 0; i < hazards.Count; i++)
                board.SetTile(hazards[i], TileType.Damage);

            return hazards;
        }

        private static void AddTrajectory(HashSet<GridPos> set, IReadOnlyList<GridPos> cells)
        {
            if (cells == null) return;
            for (int i = 0; i < cells.Count; i++) set.Add(cells[i]);
        }

        private static bool IsInterior(BoardData board, GridPos p)
        {
            return p.X >= 1 && p.Y >= 1 && p.X <= board.Width - 2 && p.Y <= board.Height - 2;
        }

        private static int CountWalkableNeighbors(BoardData board, GridPos p)
        {
            int count = 0;
            for (int i = 0; i < Directions.All.Length; i++)
            {
                if (board.IsWalkable(p + Directions.Delta(Directions.All[i]))) count++;
            }
            return count;
        }

        private static int CountWalkableNonSwitchNeighbors(BoardData board, GridPos p)
        {
            int count = 0;
            for (int i = 0; i < Directions.All.Length; i++)
            {
                var neighbor = p + Directions.Delta(Directions.All[i]);
                if (board.IsWalkable(neighbor) && !board.IsSwitch(neighbor)) count++;
            }
            return count;
        }

        private static HashSet<GridPos> ComputeReachableFloor(
            BoardData board, IReadOnlyList<GridPos> startPoints)
        {
            var reachable = new HashSet<GridPos>();
            var queue = new Queue<GridPos>();

            if (startPoints != null)
            {
                for (int i = 0; i < startPoints.Count; i++)
                {
                    var p = startPoints[i];
                    if (board.IsWalkable(p) && !board.IsSwitch(p) && reachable.Add(p))
                    {
                        queue.Enqueue(p);
                    }
                }
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                for (int i = 0; i < Directions.All.Length; i++)
                {
                    var next = current + Directions.Delta(Directions.All[i]);
                    if (!board.IsWalkable(next)) continue;
                    if (board.IsSwitch(next)) continue; // The goal cannot be passed through to dead ends behind it
                    if (reachable.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            return reachable;
        }

        private static int DistanceToTrajectory(GridPos p, HashSet<GridPos> trajectory)
        {
            if (trajectory.Count == 0) return int.MaxValue;
            int best = int.MaxValue;
            foreach (var cell in trajectory)
            {
                int distance = p.ManhattanDistance(cell);
                if (distance < best) best = distance;
            }
            return best;
        }

        private static bool TooCloseToHazards(GridPos candidate, List<GridPos> hazards)
        {
            for (int i = 0; i < hazards.Count; i++)
            {
                if (candidate.ManhattanDistance(hazards[i]) < 2) return true;
            }
            return false;
        }
    }
}
