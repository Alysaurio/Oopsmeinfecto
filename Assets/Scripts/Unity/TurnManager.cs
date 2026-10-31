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

        private bool _active;
        private float _repeatTimer;
        private Direction? _heldDirection;
        private Direction? _blockedDirection;
        private float _autoTimer;
        private bool _transformationStarted;

        [Header("CRT Retro Effect")]
        [SerializeField] private Material _crtMaterial;

        private static readonly int InfectionProgressId = Shader.PropertyToID("_InfectionProgress");
        private static readonly int StepFlickerId = Shader.PropertyToID("_StepFlicker");
        private Coroutine _stepFlickerCoroutine;

        public GameState State => _state;
        public bool IsActive => _active;
        public bool IsTransforming => _transformationStarted;
        public Material CrtMaterial
        {
            get => _crtMaterial;
            set => _crtMaterial = value;
        }

        public void Initialize(GameState state, LevelRenderer renderer, HUDController hud,
            GameTuning tuning, Action onVictory, Action<DefeatReason> onDefeat)
        {
            _state = state;
            _renderer = renderer;
            _hud = hud;
            _tuning = tuning;
            _onVictory = onVictory;
            _onDefeat = onDefeat;

            _active = state != null && state.Phase == GamePhase.HumanControlled;
            _repeatTimer = 0f;
            _heldDirection = null;
            _blockedDirection = null;
            _autoTimer = 0f;
            _transformationStarted = false;

            if (_state == null) return;

            ResolveCrtMaterial();
            UpdateCrtInfectionProgress();
            if (_crtMaterial != null)
            {
                _crtMaterial.SetFloat(StepFlickerId, 0f);
            }

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

        private void OnDisable()
        {
            _heldDirection = null;
            _blockedDirection = null;
            _repeatTimer = 0f;
            _autoTimer = 0f;

            if (_stepFlickerCoroutine != null)
            {
                StopCoroutine(_stepFlickerCoroutine);
                _stepFlickerCoroutine = null;
            }

            if (_crtMaterial != null)
            {
                _crtMaterial.SetFloat(StepFlickerId, 0f);
            }
        }

        private void OnDestroy()
        {
            if (_crtMaterial != null)
            {
                _crtMaterial.SetFloat(StepFlickerId, 0f);
                _crtMaterial.SetFloat(InfectionProgressId, 0f);
            }
        }

        private void Update()
        {
            UpdateCrtInfectionProgress();

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

            // A physical press always produces exactly one move immediately, even if the key is
            // released in the same frame. Repeat is only considered for a key that stays held.
            var pressed = ReadPressedDirection(keyboard);
            if (pressed.HasValue)
            {
                _blockedDirection = null;
                _heldDirection = pressed.Value;
                _repeatTimer = Mathf.Max(0.01f, _tuning.initialRepeatDelay);
                PerformManualMove(pressed.Value);
                return;
            }

            if (_blockedDirection.HasValue)
            {
                if (IsHeld(keyboard, _blockedDirection.Value)) return;
                _blockedDirection = null;
                _heldDirection = null;
                _repeatTimer = 0f;
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
                // The key was already down when the turn became available. Do not generate an
                // involuntary move: wait for a new physical press or the normal repeat delay.
                _heldDirection = held.Value;
                _repeatTimer = Mathf.Max(0.01f, _tuning.initialRepeatDelay);
                return;
            }

            _repeatTimer -= Time.unscaledDeltaTime;
            if (_repeatTimer <= 0f)
            {
                _repeatTimer += Mathf.Max(0.01f, _tuning.repeatInterval);
                PerformManualMove(_heldDirection.Value);
            }
        }

        private void PerformManualMove(Direction direction)
        {
            if (_state == null || _state.Phase != GamePhase.HumanControlled) return;

            var result = _state.TryManualMove(direction);

            if (result.Outcome == TurnOutcome.Blocked)
            {
                _blockedDirection = direction;
                _heldDirection = direction;
                _repeatTimer = Mathf.Max(0.01f, _tuning.initialRepeatDelay);
                _hud.SetStatus("Movimiento bloqueado · prueba otra dirección");
                return;
            }

            _blockedDirection = null;
            TriggerStepFlicker();
            _renderer.SyncFromState(_state);
            RefreshHumanHud();
            HandleOutcome(result);
        }

        private void HandlePlayback()
        {
            float stepInterval = Mathf.Max(0.03f,
                _tuning.autoStepInterval + _tuning.playbackExtraDelay);

            _autoTimer -= Time.unscaledDeltaTime;
            if (_autoTimer > 0f) return;
            _autoTimer = stepInterval;

            var result = _state.AdvancePlaybackStep();
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
                    _repeatTimer = 0f;
                    _hud.SetInfected(true);
                    _hud.SetStatus("¡CONTACTO! Secuencia congelada · " +
                                   _state.PlaybackSequence.Count + " pasos");
                    _autoTimer = Mathf.Max(0.03f,
                        _tuning.autoStepInterval + _tuning.playbackExtraDelay);
                    break;

                case TurnOutcome.SwitchVictory:
                    _active = false;
                    _hud.SetStatus("¡INTERRUPTOR ALCANZADO!");
                    _onVictory?.Invoke();
                    break;

                case TurnOutcome.DamageDefeat:
                    _active = false;
                    _renderer.PlayDrain(_tuning.barDrainDuration);
                    _renderer.PlayerVisual?.PlayDefeat(DefeatReason.DamageTile, _tuning.defeatDuration);
                    _hud.SetStatus("Derrota · casilla dañina");
                    _onDefeat?.Invoke(DefeatReason.DamageTile);
                    break;

                case TurnOutcome.EnergyDefeat:
                    _active = false;
                    _heldDirection = null;
                    _blockedDirection = null;
                    _repeatTimer = 0f;
                    _renderer.PlayerVisual?.PlayDefeat(DefeatReason.EnergyDepleted, _tuning.defeatDuration);
                    _hud.SetStatus("Transformación ambiental · la infección te consume");
                    if (!_transformationStarted)
                    {
                        _transformationStarted = true;
                        _renderer.BeginEnvironmentalTransformation(
                            _tuning.transformationDuration,
                            () =>
                            {
                                _hud.SetStatus("Derrota · energía agotada");
                                _onDefeat?.Invoke(DefeatReason.EnergyDepleted);
                            });
                    }
                    break;

                case TurnOutcome.PlaybackExhausted:
                    _active = false;
                    _renderer.PlayerVisual?.PlayDefeat(DefeatReason.PlaybackExhausted, _tuning.defeatDuration);
                    _hud.SetStatus("Derrota · secuencia agotada");
                    _onDefeat?.Invoke(DefeatReason.PlaybackExhausted);
                    break;
            }
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

        private void ResolveCrtMaterial()
        {
            if (_crtMaterial != null) return;

            if (_tuning != null && _tuning.crtMaterial != null)
            {
                _crtMaterial = _tuning.crtMaterial;
                return;
            }

            _crtMaterial = Resources.Load<Material>("Mat_CRT_Retro");
        }

        private void UpdateCrtInfectionProgress()
        {
            if (_crtMaterial == null)
            {
                ResolveCrtMaterial();
                if (_crtMaterial == null) return;
            }

            if (_state != null)
            {
                int energiaActual = _state.CurrentEnergy;
                int energiaMaxima = Mathf.Max(1, _state.MaxEnergy);
                float ratio = 1.0f - ((float)energiaActual / (float)energiaMaxima);
                float curve = Mathf.Pow(Mathf.Clamp01(ratio), 3.0f);
                _crtMaterial.SetFloat(InfectionProgressId, curve * 0.315f);
            }
        }

        private void TriggerStepFlicker()
        {
            if (_crtMaterial == null)
            {
                ResolveCrtMaterial();
                if (_crtMaterial == null) return;
            }

            if (_stepFlickerCoroutine != null)
            {
                StopCoroutine(_stepFlickerCoroutine);
            }

            float curve = 0f;
            if (_state != null)
            {
                int energiaActual = _state.CurrentEnergy;
                int energiaMaxima = Mathf.Max(1, _state.MaxEnergy);
                float ratio = 1.0f - ((float)energiaActual / (float)energiaMaxima);
                curve = Mathf.Pow(Mathf.Clamp01(ratio), 3.0f);
            }

            _stepFlickerCoroutine = StartCoroutine(StepFlickerRoutine(curve * 0.20f));
        }

        private System.Collections.IEnumerator StepFlickerRoutine(float initialFlicker)
        {
            float duration = 0.15f;
            float elapsed = 0f;
            _crtMaterial.SetFloat(StepFlickerId, initialFlicker);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float flicker = Mathf.Lerp(initialFlicker, 0.0f, t);
                _crtMaterial.SetFloat(StepFlickerId, flicker);
                yield return null;
            }

            _crtMaterial.SetFloat(StepFlickerId, 0.0f);
            _stepFlickerCoroutine = null;
        }
    }
}
