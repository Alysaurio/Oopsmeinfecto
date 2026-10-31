using System;
using System.Collections.Generic;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Builds the frozen inherited sequence at the moment of contact.
    /// The infector contributes its recent moves, in the amount defined by its own Conciencia.
    /// The infected entity contributes its own recent moves, in the amount defined by its own Conciencia.
    /// Sequence = last (infector.Conciencia) moves of the infector + last (infected.Conciencia) moves of the infected.
    /// </summary>
    public static class InfectionSystem
    {
        public const int InfectorMoveCount = 3;

        /// <summary>
        /// Single source of truth: extracts the exact moves an entity contributes based on its Conciencia.
        /// </summary>
        public static List<Direction> GetSelectedMoves(EntityState entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            return entity.GetLastDirections(entity.Conciencia);
        }

        public static List<Direction> BuildSequence(EntityState infector, EntityState infected)
        {
            if (infector == null) throw new ArgumentNullException(nameof(infector));
            if (infected == null) throw new ArgumentNullException(nameof(infected));

            var infectorMoves = GetSelectedMoves(infector);
            var infectedMoves = GetSelectedMoves(infected);

            var sequence = new List<Direction>(infectorMoves.Count + infectedMoves.Count);
            sequence.AddRange(infectorMoves);
            sequence.AddRange(infectedMoves);
            return sequence;
        }

        public static bool HasRequiredHistory(EntityState infector, EntityState infected)
        {
            if (infector == null || infected == null) return false;
            return infector.HistoryCount >= Math.Max(0, infector.Conciencia) &&
                   infected.HistoryCount >= Math.Max(0, infected.Conciencia);
        }

        /// <summary>Expected length of a complete inherited sequence for the supplied pair.</summary>
        public static int ExpectedLength(EntityState infector, EntityState infected)
        {
            if (infector == null) throw new ArgumentNullException(nameof(infector));
            if (infected == null) throw new ArgumentNullException(nameof(infected));
            return Math.Max(0, infector.Conciencia) + Math.Max(0, infected.Conciencia);
        }

        /// <summary>Overload for backwards compatibility.</summary>
        public static int ExpectedLength(EntityState infected)
        {
            if (infected == null) throw new ArgumentNullException(nameof(infected));
            return InfectorMoveCount + Math.Max(0, infected.Conciencia);
        }
    }
}
