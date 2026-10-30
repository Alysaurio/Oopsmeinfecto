using System;
using System.Collections.Generic;

namespace Oopsmeinfecto
{
    public enum GamePhase
    {
        HumanControlled = 0,
        InfectedPlayback = 1,
        Victory = 2,
        Defeat = 3
    }

    public enum DefeatReason
    {
        None = 0,
        DamageTile = 1,
        EnergyDepleted = 2,
        PlaybackExhausted = 3,
        GenerationFailure = 4
    }

    public enum TurnOutcome
    {
        Blocked = 0,
        Moved = 1,
        Infected = 2,
        SwitchVictory = 3,
        DamageDefeat = 4,
        EnergyDefeat = 5,
        PlaybackStep = 6,
        PlaybackExhausted = 7,
        NoPlayback = 8
    }

    public struct MoveResult
    {
        public TurnOutcome Outcome;
        public GridPos From;
        public GridPos To;
        public bool Moved;
        public bool Contacted;
        public bool PlaybackStepBlocked;

        public bool IsGameOver => Outcome == TurnOutcome.SwitchVictory ||
                                  Outcome == TurnOutcome.DamageDefeat ||
                                  Outcome == TurnOutcome.EnergyDefeat ||
                                  Outcome == TurnOutcome.PlaybackExhausted;
    }

    /// <summary>
    /// The single authoritative turn engine. The real game, the solution planner and the
    /// validator all use this class, so the turn order cannot drift between them.
    ///
    /// Manual turn:
    ///   move player -> record -> spend energy -> damage -> switch -> move NPC -> record NPC
    ///   -> contact/infection -> energy exhaustion.
    ///
    /// Playback turn:
    ///   consume one instruction -> move if possible -> damage -> switch -> move NPC.
    ///   No energy is spent and contact does not restart infection.
    /// </summary>
    public sealed class GameState
    {
        private List<Direction> _playbackSequence = new List<Direction>();

        public BoardData Board { get; }
        public EntityState Player { get; }
        public EntityState Zombie { get; }
        public IMovementPattern Pattern { get; }
        public int ZombiePhase { get; private set; }
        public int MaxEnergy { get; }
        public int CurrentEnergy { get; private set; }
        public GamePhase Phase { get; private set; }
        public DefeatReason DefeatReason { get; private set; }
        public int TurnNumber { get; private set; }
        public int PlaybackIndex { get; private set; }
        public bool LastPlaybackStepBlocked { get; private set; }

        public IReadOnlyList<Direction> PlaybackSequence => _playbackSequence;
        public int PlaybackRemaining => Math.Max(0, _playbackSequence.Count - PlaybackIndex);

        public GameState(
            BoardData board,
            GridPos playerStart,
            IEnumerable<GridPos> zombieRoute,
            int zombieConciencia,
            int playerConciencia,
            int maxEnergy,
            int historyCapacity = 7,
            int initialZombiePhase = 0)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            Pattern = new CyclicRoutePattern(zombieRoute);
            MaxEnergy = Math.Max(1, maxEnergy);
            CurrentEnergy = MaxEnergy;
            Phase = GamePhase.HumanControlled;
            DefeatReason = DefeatReason.None;

            int capacity = Math.Max(3, historyCapacity);
            Player = new EntityState(0, EntityKind.Player, playerStart, playerConciencia, capacity);
            Zombie = new EntityState(1, EntityKind.Zombie, Pattern.PositionAt(initialZombiePhase), zombieConciencia, capacity);
            ZombiePhase = NormalizePhase(initialZombiePhase);
            TurnNumber = 0;
        }

        private GameState(GameState source)
        {
            Board = source.Board;
            Pattern = source.Pattern;
            ZombiePhase = source.ZombiePhase;
            MaxEnergy = source.MaxEnergy;
            CurrentEnergy = source.CurrentEnergy;
            Phase = source.Phase;
            DefeatReason = source.DefeatReason;
            TurnNumber = source.TurnNumber;
            PlaybackIndex = source.PlaybackIndex;
            LastPlaybackStepBlocked = source.LastPlaybackStepBlocked;
            Player = source.Player.Clone();
            Zombie = source.Zombie.Clone();
            _playbackSequence = new List<Direction>(source._playbackSequence);
        }

        public GameState Clone() => new GameState(this);

        private int NormalizePhase(int phase)
        {
            int length = Pattern.Length;
            return ((phase % length) + length) % length;
        }

        public MoveResult TryManualMove(Direction direction)
        {
            var result = new MoveResult
            {
                Outcome = TurnOutcome.Blocked,
                From = Player.Position,
                To = Player.Position,
                Moved = false,
                Contacted = false
            };

            if (Phase != GamePhase.HumanControlled) return result;

            var target = Player.Position + Directions.Delta(direction);
            if (!Board.IsWalkable(target))
            {
                return result;
            }

            result.Moved = true;
            result.To = target;
            Player.Position = target;
            Player.RecordMove(direction);
            CurrentEnergy = Math.Max(0, CurrentEnergy - 1);
            TurnNumber++;

            // Damage stops the turn immediately: the NPC does not move.
            if (Board.IsDamage(target))
            {
                Phase = GamePhase.Defeat;
                DefeatReason = DefeatReason.DamageTile;
                result.Outcome = TurnOutcome.DamageDefeat;
                return result;
            }

            // Reaching the switch wins immediately: the NPC does not move.
            if (Board.IsSwitch(target))
            {
                Phase = GamePhase.Victory;
                result.Outcome = TurnOutcome.SwitchVictory;
                return result;
            }

            AdvanceZombie();

            // Only ending the turn on the same cell counts as contact. A position swap does not.
            if (Player.Position == Zombie.Position)
            {
                Player.Infected = true;
                _playbackSequence = InfectionSystem.BuildSequence(Zombie, Player);
                PlaybackIndex = 0;
                Phase = GamePhase.InfectedPlayback;
                result.Contacted = true;
                result.Outcome = TurnOutcome.Infected;
                return result;
            }

            // Contact has priority over energy exhaustion. If there was no contact and the
            // budget is gone, environmental infection defeats the player.
            if (CurrentEnergy <= 0)
            {
                Phase = GamePhase.Defeat;
                DefeatReason = DefeatReason.EnergyDepleted;
                result.Outcome = TurnOutcome.EnergyDefeat;
                return result;
            }

            result.Outcome = TurnOutcome.Moved;
            return result;
        }

        public MoveResult AdvancePlaybackStep()
        {
            var result = new MoveResult
            {
                Outcome = TurnOutcome.NoPlayback,
                From = Player.Position,
                To = Player.Position,
                Moved = false,
                Contacted = false,
                PlaybackStepBlocked = false
            };

            if (Phase != GamePhase.InfectedPlayback) return result;

            if (PlaybackIndex >= _playbackSequence.Count)
            {
                Phase = GamePhase.Defeat;
                DefeatReason = DefeatReason.PlaybackExhausted;
                result.Outcome = TurnOutcome.PlaybackExhausted;
                return result;
            }

            var direction = _playbackSequence[PlaybackIndex++];
            var target = Player.Position + Directions.Delta(direction);
            LastPlaybackStepBlocked = false;

            if (Board.IsWalkable(target))
            {
                Player.Position = target;
                Player.RecordMove(direction);
                result.Moved = true;
                result.To = target;
            }
            else
            {
                // The instruction is consumed even if it cannot be executed. This prevents
                // infinite loops waiting for the same direction.
                LastPlaybackStepBlocked = true;
                result.PlaybackStepBlocked = true;
            }

            if (Board.IsDamage(Player.Position))
            {
                Phase = GamePhase.Defeat;
                DefeatReason = DefeatReason.DamageTile;
                result.Outcome = TurnOutcome.DamageDefeat;
                return result;
            }

            if (Board.IsSwitch(Player.Position))
            {
                Phase = GamePhase.Victory;
                result.Outcome = TurnOutcome.SwitchVictory;
                return result;
            }

            AdvanceZombie();

            if (PlaybackIndex >= _playbackSequence.Count)
            {
                Phase = GamePhase.Defeat;
                DefeatReason = DefeatReason.PlaybackExhausted;
                result.Outcome = TurnOutcome.PlaybackExhausted;
                return result;
            }

            result.Outcome = TurnOutcome.PlaybackStep;
            return result;
        }

        private void AdvanceZombie()
        {
            var oldPosition = Zombie.Position;
            ZombiePhase = (ZombiePhase + 1) % Pattern.Length;
            var newPosition = Pattern.PositionAt(ZombiePhase);
            var direction = Directions.FromDelta(newPosition - oldPosition);
            Zombie.Position = newPosition;
            Zombie.RecordMove(direction);
        }

        /// <summary>Used by the planner to inspect a contact that has just happened.</summary>
        public bool ContactHasRequiredHistory()
        {
            return InfectionSystem.HasRequiredHistory(Zombie, Player);
        }

        /// <summary>Simulates the frozen playback to its end. Used by the planner and validator.</summary>
        public PlaybackSimulation SimulatePlaybackToEnd()
        {
            var simulation = new PlaybackSimulation();
            if (Phase != GamePhase.InfectedPlayback)
            {
                simulation.FinalPosition = Player.Position;
                simulation.Outcome = Phase;
                return simulation;
            }

            simulation.Positions.Add(Player.Position);
            int guard = 0;
            while (Phase == GamePhase.InfectedPlayback && guard++ < 256)
            {
                var step = AdvancePlaybackStep();
                if (step.PlaybackStepBlocked) simulation.AnyBlockedStep = true;
                if (step.Moved) simulation.Positions.Add(Player.Position);
                if (step.Outcome == TurnOutcome.SwitchVictory)
                {
                    simulation.ReachedSwitch = true;
                    break;
                }
                if (step.Outcome == TurnOutcome.DamageDefeat || step.Outcome == TurnOutcome.PlaybackExhausted)
                {
                    break;
                }
            }

            simulation.FinalPosition = Player.Position;
            simulation.Outcome = Phase;
            simulation.DefeatReason = DefeatReason;
            return simulation;
        }

        public void ForcePhase(GamePhase phase, DefeatReason reason = DefeatReason.None)
        {
            Phase = phase;
            DefeatReason = reason;
        }
    }

    public sealed class PlaybackSimulation
    {
        public List<GridPos> Positions { get; } = new List<GridPos>();
        public GridPos FinalPosition { get; set; }
        public bool ReachedSwitch { get; set; }
        public bool AnyBlockedStep { get; set; }
        public GamePhase Outcome { get; set; }
        public DefeatReason DefeatReason { get; set; }
    }
}
