using System;
using System.Collections.Generic;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Interface kept intentionally small so other deterministic NPC patterns can be added later.
    /// </summary>
    public interface IMovementPattern
    {
        IReadOnlyList<GridPos> Route { get; }
        int Length { get; }
        GridPos PositionAt(int phase);
        Direction DirectionFrom(int phase);
    }

    /// <summary>
    /// Cyclic list of absolute grid positions. The transition from the last position back to the
    /// first one is part of the cycle and is validated as orthogonal.
    /// </summary>
    public sealed class CyclicRoutePattern : IMovementPattern
    {
        private readonly GridPos[] _route;

        public IReadOnlyList<GridPos> Route => _route;
        public int Length => _route.Length;

        public CyclicRoutePattern(IEnumerable<GridPos> route)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            var list = new List<GridPos>(route);
            if (list.Count < 2) throw new ArgumentException("A cyclic route needs at least two positions.", nameof(route));

            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                var b = list[(i + 1) % list.Count];
                if (!a.IsOrthogonallyAdjacent(b))
                {
                    throw new ArgumentException($"Route transition {i} ({a} -> {b}) is not orthogonally adjacent.", nameof(route));
                }
            }

            _route = list.ToArray();
        }

        public GridPos PositionAt(int phase)
        {
            int index = ((phase % _route.Length) + _route.Length) % _route.Length;
            return _route[index];
        }

        public Direction DirectionFrom(int phase)
        {
            int index = ((phase % _route.Length) + _route.Length) % _route.Length;
            int next = (index + 1) % _route.Length;
            return Directions.FromDelta(_route[next] - _route[index]);
        }

        public static bool IsValidRoute(IReadOnlyList<GridPos> route, out string error)
        {
            error = null;
            if (route == null || route.Count < 2)
            {
                error = "route has fewer than two positions";
                return false;
            }

            for (int i = 0; i < route.Count; i++)
            {
                if (!route[i].IsOrthogonallyAdjacent(route[(i + 1) % route.Count]))
                {
                    error = $"transition {i} is not orthogonal";
                    return false;
                }
            }

            return true;
        }
    }
}
