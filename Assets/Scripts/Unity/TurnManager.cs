using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Translates keyboard input into manual turns and drives the automatic infected playback.
    /// All rule decisions stay inside <see cref="GameState"/>.
    /// </summary>
    public sealed class TurnManager : MonoBehaviour
    {
        private GameState _state;
        private LevelRenderer _renderer;
        private HUDController _hud;
        private GameTuning _tuning;
        private Action _onVictory;
        private Action<DefeatReason> _onDefeat;

        [Header("Audio")]
        [SerializeField] private PlayerMoveAudio _moveAudio;

        private bool _active;
        private float _repeatTimer;
        private Direction? _heldDirection;
        private Direction? _blockedDirection;
        private float _autoTimer;

        public GameState State => _state;
        public bool IsActive => _active;

        public void Initialize(GameState state, LevelRenderer renderer, HUDController hud,
            GameTuning tuning, Action onVictory, Action<DefeatReason> onDefeat)
        {
            _state = state;
            _renderer = renderer;
            _hud = hud;
            _tuning = tuning;
            _onVictory = onVictory;
            _onDefeat = onDefeat;

            if (_moveAudio == null) _moveAudio = FindAnyObjectByType<PlayerMoveAudio>();

            _active = state != null && state.Phase == GamePhase.HumanControlled;
            _repeatTimer = 0f;
            _heldDirection = null;
            _blockedDirection = null;
            _autoTimer = 0f;

            if (_state == null) return;

            _renderer.SyncFromState(_state, true);
            _hud.SetInfected(_state.Player.Infected);
            _hud.SetHistories(_state.Player.History, _state.Zombie.History);
            _hud.SetStatus("Turno 0 · Energía " + _state.CurrentEnergy + "/" + _state.MaxEnergy +
                           " · llega al interruptor");
        }

        public void SetActive(bool active)
        {
            _active = active;
            if (!active)
            {
                _heldDirection = null;
                _blockedDirection = null;
                _repeatTimer = 0f;
            }
        }

        private void Update()
        {
            if (!_active || _state == null) return;

            if (_state.Phase == GamePhase.HumanControlled)
            {
                HandleManualInput();
            }
            else if (_state.Phase == GamePhase.InfectedPlayback)
            {
                HandlePlayback();
            }
        }

        private void HandleManualInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            var pressed = ReadPressedDirection(keyboard);
            if (pressed.HasValue)
            {
                if (_blockedDirection.HasValue && _blockedDirection.Value == pressed.Value &&
                    IsHeld(keyboard, pressed.Value))
                {
                    return;
                }

                _blockedDirection = null;
                _heldDirection = pressed.Value;
                _repeatTimer = Mathf.Max(0.02f, _tuning.initialRepeatDelay);
                PerformManualMove(pressed.Value);
                return;
            }

            if (_blockedDirection.HasValue)
            {
                if (IsHeld(keyboard, _blockedDirection.Value)) return;
                _blockedDirection = null;
                _heldDirection = null;
            }

            var held = ReadHeldDirection(keyboard);
            if (!held.HasValue)
            {
                _heldDirection = null;
                _repeatTimer = 0f;
                return;
            }

            if (!_heldDirection.HasValue || !IsHeld(keyboard, _heldDirection.Value))
            {
                _heldDirection = held.Value;
                _repeatTimer = Mathf.Max(0.02f, _tuning.initialRepeatDelay);
                PerformManualMove(held.Value);
                return;
            }

            _repeatTimer -= Time.unscaledDeltaTime;
            if (_repeatTimer <= 0f)
            {
                _repeatTimer += Mathf.Max(0.02f, _tuning.repeatInterval);
                PerformManualMove(_heldDirection.Value);
            }
        }

        private void PerformManualMove(Direction direction)
        {
            var result = _state.TryManualMove(direction);
            if (result.Moved) PlayMoveSound();

            if (result.Outcome == TurnOutcome.Blocked)
            {
                _blockedDirection = direction;
                _heldDirection = direction;
                _repeatTimer = 0.15f;
                _hud.SetStatus("Movimiento bloqueado · prueba otra dirección");
                return;
            }

            _renderer.SyncFromState(_state);
            RefreshHumanHud();
            HandleOutcome(result);
        }

        private void HandlePlayback()
        {
            _autoTimer -= Time.unscaledDeltaTime;
            if (_autoTimer > 0f) return;
            _autoTimer = Mathf.Max(0.03f, _tuning.autoStepInterval);

            var result = _state.AdvancePlaybackStep();
            if (result.Moved) PlayMoveSound();
            _renderer.SyncFromState(_state);
            _hud.SetHistories(_state.Player.History, _state.Zombie.History);

            if (result.Outcome == TurnOutcome.PlaybackStep)
            {
                string blocked = result.PlaybackStepBlocked ? " · paso bloqueado" : "";
                _hud.SetStatus("REPRODUCIENDO " + _state.PlaybackIndex + "/" +
                               _state.PlaybackSequence.Count + blocked);
                return;
            }

            if (result.Outcome == TurnOutcome.NoPlayback)
            {
                _hud.SetStatus("La secuencia no pudo continuar");
                return;
            }

            HandleOutcome(result);
        }

        private void RefreshHumanHud()
        {
            _hud.SetHistories(_state.Player.History, _state.Zombie.History);
            _hud.SetStatus("Turno " + _state.TurnNumber + " · Energía " +
                           _state.CurrentEnergy + "/" + _state.MaxEnergy +
                           " · busca el interruptor");
        }

        private void HandleOutcome(MoveResult result)
        {
            switch (result.Outcome)
            {
                case TurnOutcome.Moved:
                    break;

                case TurnOutcome.Infected:
                    _heldDirection = null;
                    _blockedDirection = null;
                    _hud.SetInfected(true);
                    _hud.SetStatus("¡CONTACTO! Secuencia congelada · " +
                                   _state.PlaybackSequence.Count + " pasos");
                    _autoTimer = Mathf.Max(0.30f, _tuning.autoStepInterval * 2f);
                    break;

                case TurnOutcome.SwitchVictory:
                    _active = false;
                    _hud.SetStatus("¡INTERRUPTOR ALCANZADO!");
                    _onVictory?.Invoke();
                    break;

                case TurnOutcome.DamageDefeat:
                    _active = false;
                    _renderer.PlayDrain(_tuning.barDrainDuration);
                    _hud.SetStatus("Derrota · casilla dañina");
                    _onDefeat?.Invoke(DefeatReason.DamageTile);
                    break;

                case TurnOutcome.EnergyDefeat:
                    _active = false;
                    _hud.SetStatus("Derrota · energía agotada");
                    _onDefeat?.Invoke(DefeatReason.EnergyDepleted);
                    break;

                case TurnOutcome.PlaybackExhausted:
                    _active = false;
                    _hud.SetStatus("Derrota · secuencia agotada");
                    _onDefeat?.Invoke(DefeatReason.PlaybackExhausted);
                    break;
            }
        }

        private void PlayMoveSound()
        {
            if (_moveAudio != null) _moveAudio.PlayRandomMove();
        }

        private static Direction? ReadPressedDirection(Keyboard keyboard)
        {
            if (keyboard[Key.W].wasPressedThisFrame || keyboard[Key.UpArrow].wasPressedThisFrame)
                return Direction.Up;
            if (keyboard[Key.D].wasPressedThisFrame || keyboard[Key.RightArrow].wasPressedThisFrame)
                return Direction.Right;
            if (keyboard[Key.S].wasPressedThisFrame || keyboard[Key.DownArrow].wasPressedThisFrame)
                return Direction.Down;
            if (keyboard[Key.A].wasPressedThisFrame || keyboard[Key.LeftArrow].wasPressedThisFrame)
                return Direction.Left;
            return null;
        }

        private static Direction? ReadHeldDirection(Keyboard keyboard)
        {
            if (IsHeld(keyboard, Direction.Up)) return Direction.Up;
            if (IsHeld(keyboard, Direction.Right)) return Direction.Right;
            if (IsHeld(keyboard, Direction.Down)) return Direction.Down;
            if (IsHeld(keyboard, Direction.Left)) return Direction.Left;
            return null;
        }

        private static bool IsHeld(Keyboard keyboard, Direction direction)
        {
            switch (direction)
            {
                case Direction.Up:
                    return keyboard[Key.W].isPressed || keyboard[Key.UpArrow].isPressed;
                case Direction.Right:
                    return keyboard[Key.D].isPressed || keyboard[Key.RightArrow].isPressed;
                case Direction.Down:
                    return keyboard[Key.S].isPressed || keyboard[Key.DownArrow].isPressed;
                case Direction.Left:
                    return keyboard[Key.A].isPressed || keyboard[Key.LeftArrow].isPressed;
                default:
                    return false;
            }
        }
    }
}
