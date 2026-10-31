using System;

namespace Oopsmeinfecto
{
    /// <summary>
    /// The real rectangle occupied by the generated map. The logical board may be larger because
    /// geometry is carved inside a fixed storage grid; rendering and camera fitting use only this
    /// rectangle (plus one wall border when one exists).
    /// </summary>
    [Serializable]
    public struct PlayableBounds
    {
        public int MinX;
        public int MinY;
        public int MaxX;
        public int MaxY;

        public int Width => MaxX - MinX + 1;
        public int Height => MaxY - MinY + 1;
        public bool IsValid => MaxX >= MinX && MaxY >= MinY;
        public float CenterX => (MinX + MaxX) * 0.5f;
        public float CenterY => (MinY + MaxY) * 0.5f;

        public PlayableBounds(int minX, int minY, int maxX, int maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public bool Contains(GridPos position)
        {
            return position.X >= MinX && position.X <= MaxX &&
                   position.Y >= MinY && position.Y <= MaxY;
        }

        public static PlayableBounds FromBoard(BoardData board, bool includeWallBorder = true)
        {
            if (board == null) return new PlayableBounds(0, 0, -1, -1);

            int minX = board.Width;
            int minY = board.Height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    if (!TileTypes.IsWalkable(board.GetTile(x, y))) continue;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return new PlayableBounds(0, 0, board.Width - 1, board.Height - 1);
            }

            if (includeWallBorder)
            {
                if (minX > 0) minX--;
                if (minY > 0) minY--;
                if (maxX < board.Width - 1) maxX++;
                if (maxY < board.Height - 1) maxY++;
            }

            return new PlayableBounds(minX, minY, maxX, maxY);
        }
    }
}
