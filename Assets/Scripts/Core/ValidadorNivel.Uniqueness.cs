using System;
using System.Collections.Generic;

namespace Oopsmeinfecto
{
    public static partial class ValidadorNivel
    {
        private struct StateKey : IEquatable<StateKey>
        {
            public int PlayerX;
            public int PlayerY;
            public int ZombiePhase;
            public int Energy;
            public ulong PlayerHistory;
            public ulong ZombieHistory;

            public bool Equals(StateKey other)
            {
                return PlayerX == other.PlayerX &&
                       PlayerY == other.PlayerY &&
                       ZombiePhase == other.ZombiePhase &&
                       Energy == other.Energy &&
                       PlayerHistory == other.PlayerHistory &&
                       ZombieHistory == other.ZombieHistory;
            }

            public override bool Equals(object obj) => obj is StateKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = PlayerX;
                    hash = (hash * 397) ^ PlayerY;
                    hash = (hash * 397) ^ ZombiePhase;
                    hash = (hash * 397) ^ Energy;
                    hash = (hash * 397) ^ PlayerHistory.GetHashCode();
                    hash = (hash * 397) ^ ZombieHistory.GetHashCode();
                    return hash;
                }
            }
        }

        private struct QueueEntry
        {
            public GameState State;
            public int Delta;
        }

        private static int CountSolutions(LevelDefinition level, GenerationSettings settings, out int statesExplored, out bool unknown)
        {
            statesExplored = 0;
            unknown = false;
            int solutions = 0;

            var start = level.CreateGameState();
            var waysSeen = new Dictionary<StateKey, int>();
            var queue = new Queue<QueueEntry>();

            var startKey = MakeKey(start);
            waysSeen[startKey] = 1;
            queue.Enqueue(new QueueEntry { State = start, Delta = 1 });

            while (queue.Count > 0)
            {
                if (statesExplored >= settings.maxValidatorStates)
                {
                    unknown = true;
                    break;
                }

                var entry = queue.Dequeue();
                statesExplored++;
                var state = entry.State;

                for (int i = 0; i < Directions.All.Length; i++)
                {
                    var direction = Directions.All[i];
                    var child = state.Clone();
                    var result = child.TryManualMove(direction);

                    if (result.Outcome == TurnOutcome.Blocked) continue;
                    if (result.Outcome == TurnOutcome.DamageDefeat) continue;
                    if (result.Outcome == TurnOutcome.EnergyDefeat) continue;

                    if (result.Outcome == TurnOutcome.SwitchVictory)
                    {
                        solutions += entry.Delta;
                        if (solutions >= 2) return 2;
                        continue;
                    }

                    if (result.Outcome == TurnOutcome.Infected)
                    {
                        var playback = child.Clone();
                        var simulation = playback.SimulatePlaybackToEnd();
                        if (simulation.ReachedSwitch)
                        {
                            solutions += entry.Delta;
                            if (solutions >= 2) return 2;
                        }
                        continue;
                    }

                    if (result.Outcome != TurnOutcome.Moved) continue;

                    var key = MakeKey(child);
                    int previous;
                    if (waysSeen.TryGetValue(key, out previous))
                    {
                        if (previous >= 2) continue;
                        int next = Math.Min(2, previous + entry.Delta);
                        if (next <= previous) continue;
                        waysSeen[key] = next;
                        queue.Enqueue(new QueueEntry { State = child, Delta = next - previous });
                    }
                    else
                    {
                        int ways = Math.Min(2, entry.Delta);
                        waysSeen[key] = ways;
                        queue.Enqueue(new QueueEntry { State = child, Delta = ways });
                    }
                }
            }

            return Math.Min(2, solutions);
        }

        private static StateKey MakeKey(GameState state)
        {
            return new StateKey
            {
                PlayerX = state.Player.Position.X,
                PlayerY = state.Player.Position.Y,
                ZombiePhase = state.ZombiePhase,
                Energy = state.CurrentEnergy,
                PlayerHistory = EncodeHistory(state.Player.History),
                ZombieHistory = EncodeHistory(state.Zombie.History)
            };
        }

        private static ulong EncodeHistory(IReadOnlyList<Direction> history)
        {
            ulong code = (ulong)history.Count;
            for (int i = 0; i < history.Count; i++)
            {
                code = (code << 2) | (ulong)(uint)history[i];
            }
            return code;
        }
    }
}
