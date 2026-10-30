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
            int maxHazardsCap,
            SeededRandom rng)
        {
            if (board == null || optionalCells == null || protectedCells == null) return null;

            int maxHazards = GetMaxHazards(board.Width, board.Height, maxHazardsCap);
            int count = rng.Range(1, maxHazards);

            var eligible = new List<GridPos>();
            for (int i = 0; i < optionalCells.Count; i++)
            {
                var p = optionalCells[i];
                if (!board.IsWalkable(p)) continue;
                if (board.IsSwitch(p)) continue;
                if (protectedCells.Contains(p)) continue;
                eligible.Add(p);
            }

            if (eligible.Count < count) return null;
            rng.Shuffle(eligible);

            var hazards = new List<GridPos>(count);
            for (int i = 0; i < count; i++)
            {
                var p = eligible[i];
                board.SetTile(p, TileType.Damage);
                hazards.Add(p);
            }

            return hazards;
        }
    }
}
