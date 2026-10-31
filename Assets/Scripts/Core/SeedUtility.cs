using System;
using System.Security.Cryptography;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Session and level seed helpers. A session seed is fresh for every Play run; level seeds
    /// are derived deterministically from it, so a defeat can rebuild exactly the same level.
    /// </summary>
    public static class SeedUtility
    {
        public static int GenerateSessionSeed()
        {
            try
            {
                var bytes = new byte[4];
                using (var generator = RandomNumberGenerator.Create())
                {
                    generator.GetBytes(bytes);
                }
                int value = BitConverter.ToInt32(bytes, 0);
                return value == 0 ? 1 : value;
            }
            catch
            {
                int value = Guid.NewGuid().GetHashCode();
                return value == 0 ? 1 : value;
            }
        }

        public static int DeriveLevelSeed(int sessionSeed, int levelIndex)
        {
            unchecked
            {
                uint value = (uint)sessionSeed + (uint)levelIndex * 0x9E3779B9u;
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return (int)value;
            }
        }

        public static int DeriveAttemptSeed(int levelSeed, int attempt, int levelIndex)
        {
            unchecked
            {
                uint x = (uint)levelSeed;
                x ^= (uint)attempt * 0x9E3779B9u;
                x ^= (uint)levelIndex * 0x85EBCA6Bu;
                x ^= x >> 16;
                x *= 0x7FEB352Du;
                x ^= x >> 15;
                x *= 0x846CA68Bu;
                x ^= x >> 16;
                return (int)x;
            }
        }
    }
}
