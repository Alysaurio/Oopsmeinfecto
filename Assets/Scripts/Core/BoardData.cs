using System;
using System.Collections.Generic;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Pure data representation of a level grid. Coordinates are integer grid coordinates.
    /// Wall cells are not walkable. Damage and switch cells are walkable.
    /// </summary>
    public sealed class BoardData
    {
        private readonly TileType[] _tiles;

        public int Width { get; }
        public int Height { get; }
        public GridPos SwitchPosition { get; private set; }
        public bool HasSwitch { get; private set; }

        public BoardData(int width, int height, TileType fill = TileType.Floor)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
            _tiles = new TileType[width * height];
            for (int i = 0; i < _tiles.Length; i++) _tiles[i] = fill;
            SwitchPosition = new GridPos(-1, -1);
            HasSwitch = false;
        }

        public int Area => Width * Height;

        public bool IsInside(GridPos position)
        {
            return position.X >= 0 && position.Y >= 0 && position.X < Width && position.Y < Height;
        }

        public bool IsInside(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        public TileType GetTile(GridPos position)
        {
            if (!IsInside(position)) return TileType.Wall;
            return _tiles[position.Y * Width + position.X];
        }

        public TileType GetTile(int x, int y)
        {
            if (!IsInside(x, y)) return TileType.Wall;
            return _tiles[y * Width + x];
        }

        public void SetTile(GridPos position, TileType type)
        {
            if (!IsInside(position)) throw new ArgumentOutOfRangeException(nameof(position));
            if (HasSwitch && SwitchPosition == position && type != TileType.Switch)
            {
                HasSwitch = false;
                SwitchPosition = new GridPos(-1, -1);
            }

            _tiles[position.Y * Width + position.X] = type;

            if (type == TileType.Switch)
            {
                SwitchPosition = position;
                HasSwitch = true;
            }
        }

        public bool IsWalkable(GridPos position)
        {
            return IsInside(position) && TileTypes.IsWalkable(GetTile(position));
        }

        public bool IsDamage(GridPos position)
        {
            return IsInside(position) && GetTile(position) == TileType.Damage;
        }

        public bool IsSwitch(GridPos position)
        {
            return HasSwitch && SwitchPosition == position;
        }

        public BoardData Clone()
        {
            var clone = new BoardData(Width, Height, TileType.Wall);
            Array.Copy(_tiles, clone._tiles, _tiles.Length);
            clone.SwitchPosition = SwitchPosition;
            clone.HasSwitch = HasSwitch;
            return clone;
        }

        public IEnumerable<GridPos> AllPositions()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    yield return new GridPos(x, y);
                }
            }
        }

        public IEnumerable<GridPos> WalkablePositions()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var p = new GridPos(x, y);
                    if (IsWalkable(p)) yield return p;
                }
            }
        }

        public List<GridPos> GetNeighbors(GridPos position)
        {
            var result = new List<GridPos>(4);
            for (int i = 0; i < Directions.All.Length; i++)
            {
                var next = position + Directions.Delta(Directions.All[i]);
                if (IsInside(next)) result.Add(next);
            }
            return result;
        }

        public int CountWalkable()
        {
            int count = 0;
            for (int i = 0; i < _tiles.Length; i++)
            {
                if (TileTypes.IsWalkable(_tiles[i])) count++;
            }
            return count;
        }
    }
}
