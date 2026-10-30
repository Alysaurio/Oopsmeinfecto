using System;
using System.Collections.Generic;

namespace Oopsmeinfecto
{
    /// <summary>Absolute movement directions. Order is stable because it is used by the RNG and history encoding.</summary>
    public enum Direction
    {
        Up = 0,
        Right = 1,
        Down = 2,
        Left = 3
    }

    public static class Directions
    {
        public static readonly Direction[] All =
        {
            Direction.Up,
            Direction.Right,
            Direction.Down,
            Direction.Left
        };

        public static GridPos Delta(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return new GridPos(0, 1);
                case Direction.Right: return new GridPos(1, 0);
                case Direction.Down: return new GridPos(0, -1);
                case Direction.Left: return new GridPos(-1, 0);
                default: throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        public static Direction Opposite(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return Direction.Down;
                case Direction.Right: return Direction.Left;
                case Direction.Down: return Direction.Up;
                case Direction.Left: return Direction.Right;
                default: throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        /// <summary>Returns the absolute direction between two orthogonally adjacent cells.</summary>
        public static Direction FromDelta(GridPos delta)
        {
            if (delta == new GridPos(0, 1)) return Direction.Up;
            if (delta == new GridPos(1, 0)) return Direction.Right;
            if (delta == new GridPos(0, -1)) return Direction.Down;
            if (delta == new GridPos(-1, 0)) return Direction.Left;
            throw new ArgumentException($"Delta {delta} is not orthogonally adjacent.", nameof(delta));
        }

        public static string Symbol(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return "U";
                case Direction.Right: return "R";
                case Direction.Down: return "D";
                case Direction.Left: return "L";
                default: return "?";
            }
        }

        public static string Arrow(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return "\u2191";
                case Direction.Right: return "\u2192";
                case Direction.Down: return "\u2193";
                case Direction.Left: return "\u2190";
                default: return "?";
            }
        }

        public static string SequenceToString(IReadOnlyList<Direction> sequence)
        {
            if (sequence == null || sequence.Count == 0) return "-";
            var parts = new string[sequence.Count];
            for (int i = 0; i < sequence.Count; i++) parts[i] = Symbol(sequence[i]);
            return string.Join("", parts);
        }
    }
}
