using System;
using System.Collections.Generic;

namespace Oopsmeinfecto
{
    public enum EntityKind
    {
        Player = 0,
        Zombie = 1,
        HumanNpc = 2
    }

    /// <summary>
    /// State shared by the player, the zombie and (in the future) infectable human NPCs.
    /// History is always stored as absolute directions and only records moves that really happened.
    /// </summary>
    public sealed class EntityState
    {
        private readonly List<Direction> _history = new List<Direction>();

        public int Id { get; }
        public EntityKind Kind { get; }
        public GridPos Position { get; set; }
        public bool Infected { get; set; }
        public int Conciencia { get; }
        public int HistoryCapacity { get; }

        public IReadOnlyList<Direction> History => _history;
        public int HistoryCount => _history.Count;

        public EntityState(int id, EntityKind kind, GridPos startPosition, int conciencia, int historyCapacity = 7)
        {
            if (historyCapacity < 1) throw new ArgumentOutOfRangeException(nameof(historyCapacity));
            Id = id;
            Kind = kind;
            Position = startPosition;
            Conciencia = Math.Max(0, conciencia);
            HistoryCapacity = historyCapacity;
        }

        public void Reset(GridPos position)
        {
            Position = position;
            Infected = false;
            _history.Clear();
        }

        public void RecordMove(Direction direction)
        {
            _history.Add(direction);
            while (_history.Count > HistoryCapacity) _history.RemoveAt(0);
        }

        /// <summary>Used only when cloning a simulation state.</summary>
        public void CopyHistoryFrom(EntityState source)
        {
            _history.Clear();
            int start = Math.Max(0, source._history.Count - HistoryCapacity);
            for (int i = start; i < source._history.Count; i++) _history.Add(source._history[i]);
        }

        public void AddHistory(IEnumerable<Direction> directions)
        {
            if (directions == null) return;
            foreach (var direction in directions) RecordMove(direction);
        }

        /// <summary>Returns up to <paramref name="count"/> recent directions, oldest first.</summary>
        public List<Direction> GetLastDirections(int count)
        {
            var result = new List<Direction>();
            if (count <= 0) return result;
            int start = Math.Max(0, _history.Count - count);
            for (int i = start; i < _history.Count; i++) result.Add(_history[i]);
            return result;
        }

        public EntityState Clone()
        {
            var clone = new EntityState(Id, Kind, Position, Conciencia, HistoryCapacity)
            {
                Infected = Infected
            };
            clone.CopyHistoryFrom(this);
            return clone;
        }
    }
}
