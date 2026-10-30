using System;
using System.Collections.Generic;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Small deterministic RNG. It does not depend on System.Random, so the same seed produces
    /// the same level in the Unity player, the editor and the external test harness.
    /// </summary>
    public sealed class SeededRandom
    {
        private ulong _state;

        public SeededRandom(int seed)
        {
            // SplitMix64 initialisation.
            ulong z = unchecked((ulong)(uint)seed + 0x9E3779B97F4A7C15UL);
            z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
            z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
            z ^= z >> 31;
            _state = z == 0 ? 0x9E3779B97F4A7C15UL : z;
        }

        public uint NextUInt()
        {
            // xorshift64*
            ulong x = _state;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            _state = x;
            return unchecked((uint)((x * 0x2545F4914F6CDD1DUL) >> 32));
        }

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)(NextUInt() % (uint)maxExclusive);
        }

        public int Range(int minInclusive, int maxInclusive)
        {
            if (maxInclusive < minInclusive)
                throw new ArgumentOutOfRangeException(nameof(maxInclusive), "max must be >= min");
            return minInclusive + Next(maxInclusive - minInclusive + 1);
        }

        public double NextDouble()
        {
            return NextUInt() / 4294967296.0;
        }

        public bool Chance(double probability)
        {
            return NextDouble() < probability;
        }

        public T Pick<T>(IReadOnlyList<T> items)
        {
            if (items == null || items.Count == 0) throw new ArgumentException("Cannot pick from an empty list.", nameof(items));
            return items[Next(items.Count)];
        }

        public void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
