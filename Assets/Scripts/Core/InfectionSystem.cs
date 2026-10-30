using System;
using System.Collections.Generic;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Builds the frozen inherited sequence at the moment of contact.
    /// Sequence = last 3 moves of the infector + last (infector.Conciencia) moves of the infected.
    /// </summary>
    public static class InfectionSystem
    {
        public static List<Direction> BuildSequence(EntityState infector, EntityState infected)
        {
            if (infector == null) throw new ArgumentNullException(nameof(infector));
            if (infected == null) throw new ArgumentNullException(nameof(infected));

            var sequence = new List<Direction>(3 + Math.Max(0, infector.Conciencia));
            sequence.AddRange(infector.GetLastDirections(3));
            sequence.AddRange(infected.GetLastDirections(infector.Conciencia));
            return sequence;
        }

        public static bool HasRequiredHistory(EntityState infector, EntityState infected)
        {
            return infector.HistoryCount >= 3 && infected.HistoryCount >= infector.Conciencia;
        }
    }
}
