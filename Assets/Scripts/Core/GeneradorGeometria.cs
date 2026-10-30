using System;
using System.Collections.Generic;

namespace Oopsmeinfecto
{
    public sealed class GeometryResult
    {
        public BoardData Board;
        public List<GridPos> OptionalCells = new List<GridPos>();
        public int ExpansionRadius;
    }

    /// <summary>
    /// Builds the final geometry around the mandatory solution core. Mandatory cells are never
    /// walls or hazards. Optional cells grow probabilistically, with probability falling off with
    /// Manhattan distance from the core.
    /// </summary>
    public static class GeneradorGeometria
    {
        public static GeometryResult Build(
            int width,
            int height,
            IEnumerable<GridPos> mandatoryCore,
            int expansionRadius,
            double density,
            SeededRandom rng,
            int minOptionalCells)
        {
            if (width < 6 || height < 6) return null;
            if (mandatoryCore == null) return null;

            var core = new HashSet<GridPos>();
            foreach (var p in mandatoryCore)
            {
                if (p.X < 0 || p.Y < 0 || p.X >= width || p.Y >= height) return null;
                core.Add(p);
            }
            if (core.Count == 0) return null;

            var board = new BoardData(width, height, TileType.Wall);
            foreach (var p in core) board.SetTile(p, TileType.Floor);

            var optional = new List<GridPos>();
            var optionalSet = new HashSet<GridPos>();
            var distance = ComputeDistanceToCore(width, height, core);

            if (expansionRadius > 0)
            {
                var candidates = new List<GridPos>();
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var p = new GridPos(x, y);
                        if (core.Contains(p)) continue;
                        if (distance[y * width + x] <= expansionRadius) candidates.Add(p);
                    }
                }

                // Several randomized passes let regions grow organically from the core.
                for (int pass = 0; pass < 3; pass++)
                {
                    rng.Shuffle(candidates);
                    for (int i = 0; i < candidates.Count; i++)
                    {
                        var p = candidates[i];
                        if (optionalSet.Contains(p)) continue;
                        int dist = distance[p.Y * width + p.X];
                        if (dist < 1 || dist > expansionRadius) continue;
                        if (!HasWalkableNeighbor(board, p)) continue;

                        double falloff = 1.0 - 0.60 * ((dist - 1.0) / Math.Max(1.0, expansionRadius));
                        double probability = Math.Max(0.05, density * falloff);
                        if (!rng.Chance(probability)) continue;

                        AddOptional(board, optional, optionalSet, p);

                        // Occasionally widen a corridor into a small room.
                        if (rng.Chance(0.16))
                        {
                            for (int oy = 0; oy < 2; oy++)
                            {
                                for (int ox = 0; ox < 2; ox++)
                                {
                                    var q = new GridPos(p.X + ox, p.Y + oy);
                                    if (q.X >= width || q.Y >= height) continue;
                                    if (core.Contains(q) || optionalSet.Contains(q)) continue;
                                    if (!HasWalkableNeighbor(board, q)) continue;
                                    AddOptional(board, optional, optionalSet, q);
                                }
                            }
                        }
                    }
                }
            }

            // Guarantee enough optional cells for the required hazards. Prefer dead ends.
            if (optional.Count < minOptionalCells)
            {
                var fallbackCandidates = new List<GridPos>();
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var p = new GridPos(x, y);
                        if (core.Contains(p) || optionalSet.Contains(p)) continue;
                        if (distance[y * width + x] > Math.Max(1, expansionRadius)) continue;
                        if (HasWalkableNeighbor(board, p)) fallbackCandidates.Add(p);
                    }
                }
                rng.Shuffle(fallbackCandidates);

                // First pass: only cells with one walkable neighbour (true dead ends).
                for (int i = 0; i < fallbackCandidates.Count && optional.Count < minOptionalCells; i++)
                {
                    var p = fallbackCandidates[i];
                    if (CountWalkableNeighbors(board, p) != 1) continue;
                    AddOptional(board, optional, optionalSet, p);
                }

                // Second pass: any connected candidate.
                for (int i = 0; i < fallbackCandidates.Count && optional.Count < minOptionalCells; i++)
                {
                    var p = fallbackCandidates[i];
                    if (optionalSet.Contains(p)) continue;
                    if (!HasWalkableNeighbor(board, p)) continue;
                    AddOptional(board, optional, optionalSet, p);
                }
            }

            if (optional.Count < minOptionalCells) return null;

            return new GeometryResult
            {
                Board = board,
                OptionalCells = optional,
                ExpansionRadius = expansionRadius
            };
        }

        private static void AddOptional(BoardData board, List<GridPos> optional, HashSet<GridPos> optionalSet, GridPos p)
        {
            board.SetTile(p, TileType.Floor);
            optional.Add(p);
            optionalSet.Add(p);
        }

        private static bool HasWalkableNeighbor(BoardData board, GridPos p)
        {
            return CountWalkableNeighbors(board, p) > 0;
        }

        private static int CountWalkableNeighbors(BoardData board, GridPos p)
        {
            int count = 0;
            for (int i = 0; i < Directions.All.Length; i++)
            {
                var n = p + Directions.Delta(Directions.All[i]);
                if (board.IsWalkable(n)) count++;
            }
            return count;
        }

        private static int[] ComputeDistanceToCore(int width, int height, HashSet<GridPos> core)
        {
            var distance = new int[width * height];
            for (int i = 0; i < distance.Length; i++) distance[i] = int.MaxValue;

            var queue = new Queue<GridPos>();
            foreach (var p in core)
            {
                distance[p.Y * width + p.X] = 0;
                queue.Enqueue(p);
            }

            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                int d = distance[p.Y * width + p.X];
                for (int i = 0; i < Directions.All.Length; i++)
                {
                    var n = p + Directions.Delta(Directions.All[i]);
                    if (n.X < 0 || n.Y < 0 || n.X >= width || n.Y >= height) continue;
                    int index = n.Y * width + n.X;
                    if (distance[index] <= d + 1) continue;
                    distance[index] = d + 1;
                    queue.Enqueue(n);
                }
            }

            return distance;
        }
    }
}
