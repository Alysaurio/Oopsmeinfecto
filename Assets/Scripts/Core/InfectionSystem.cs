using System;
using System.Collections.Generic;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Builds the frozen inherited sequence at the moment of contact.
    /// The infector always contributes its last three real moves. The infected entity
    /// contributes its own recent moves, in the amount defined by its own Conciencia.
    /// Sequence = last 3 moves of the infector + last (infected.Conciencia) moves of the infected.
    /// </summary>
    public static class InfectionSystem
    {
        public const int InfectorMoveCount = 3;

        public static List<Direction> BuildSequence(EntityState infector, EntityState infected)
        {
            if (infector == null) throw new ArgumentNullException(nameof(infector));
            if (infected == null) throw new ArgumentNullException(nameof(infected));

            var sequence = new List<Direction>(InfectorMoveCount + Math.Max(0, infected.Conciencia));
            sequence.AddRange(infector.GetLastDirections(InfectorMoveCount));
            sequence.AddRange(infected.GetLastDirections(infected.Conciencia));
            return sequence;
        }

        public static bool HasRequiredHistory(EntityState infector, EntityState infected)
        {
            return infector.HistoryCount >= InfectorMoveCount &&
                   infected.HistoryCount >= Math.Max(0, infected.Conciencia);
        }

        /// <summary>Expected length of a complete inherited sequence for the supplied pair.</summary>
        public static int ExpectedLength(EntityState infected)
        {
            if (infected == null) throw new ArgumentNullException(nameof(infected));
            return InfectorMoveCount + Math.Max(0, infected.Conciencia);
        }
    }
}
