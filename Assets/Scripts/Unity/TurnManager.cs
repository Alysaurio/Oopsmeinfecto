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
        private bool hasShownInfectionPause = false;
        private bool _infectionPaused = false;

        [Header("CRT Retro Effect")]
        [SerializeField] private Material _crtMaterial;

        private static readonly int InfectionProgressId = Shader.PropertyToID("_InfectionProgress");
        private static readonly int StepFlickerId = Shader.PropertyToID("_StepFlicker");
        private Coroutine _stepFlickerCoroutine;
        private Coroutine _switchVictoryCoroutine;
        private Coroutine _damageDefeatCoroutine;
        private Coroutine _infectionSequenceCoroutine;

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
            hasShownInfectionPause = false;
            _infectionPaused = false;
            Time.timeScale = 1f;

            if (_state == null) return;

            ResolveCrtMaterial();
            UpdateCrtInfectionProgress();
            if (_crtMaterial != null)
            {
                _crtMaterial.SetFloat(StepFlickerId, 0f);
            }

            _renderer.SyncFromState(_state, true);
            _hud.SetConciencia(_state.PlayerConciencia, _state.ZombieConciencia);
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
            if (_infectionPaused)
            {
                Time.timeScale = 1f;
                _infectionPaused = false;
            }

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

        public void ClearInfectionVisuals()
        {
            if (_hud != null)
            {
                _hud.ResetAllArrowPulses();
            }

            if (_renderer != null && _renderer.PlayerConcienciaVfx != null)
            {
                _renderer.PlayerConcienciaVfx.StopGlow();
            }

            if (_crtMaterial != null)
            {
                _crtMaterial.SetFloat(StepFlickerId, 0f);
                _crtMaterial.SetFloat(InfectionProgressId, 0f);
            }
        }

        private void OnDestroy()
        {
            if (_switchVictoryCoroutine != null)
            {
                StopCoroutine(_switchVictoryCoroutine);
                _switchVictoryCoroutine = null;
            }

            if (_damageDefeatCoroutine != null)
            {
                StopCoroutine(_damageDefeatCoroutine);
                _damageDefeatCoroutine = null;
            }

            if (_infectionSequenceCoroutine != null)
            {
                StopCoroutine(_infectionSequenceCoroutine);
                _infectionSequenceCoroutine = null;
            }

            if (_infectionPaused)
            {
                Time.timeScale = 1f;
                _infectionPaused = false;
            }

            if (_crtMaterial != null)
            {
                _crtMaterial.SetFloat(StepFlickerId, 0f);
                _crtMaterial.SetFloat(InfectionProgressId, 0f);
            }
        }

        private void Update()
        {
            if (HUDController.IsPauseMenuOpen || (_hud != null && _hud.IsPaused))
            {
                _heldDirection = null;
                _blockedDirection = null;
                _repeatTimer = 0f;
                return;
            }

            if (_infectionPaused) return;

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

            if (result.Outcome == TurnOutcome.DamageDefeat)
            {
                AudioManager.Instance?.PlayMovePlayer();
                AudioManager.Instance?.PlayMoveEnemy();
            }
            else if (result.Outcome == TurnOutcome.EnergyDefeat)
            {
                AudioManager.Instance?.PlayDefeat(DefeatReason.EnergyDepleted);
            }
            else if (result.Outcome == TurnOutcome.SwitchVictory)
            {
                AudioManager.Instance?.PlayVictory();
            }
            else
            {
                AudioManager.Instance?.PlayMovePlayer();
                AudioManager.Instance?.PlayMoveEnemy();
            }

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

            if (_renderer != null && _renderer.PlayerConcienciaVfx != null && _renderer.PlayerConcienciaVfx.IsActive)
            {
                int zombieSteps = Mathf.Max(1, _state.ZombieConciencia);
                float timeUntilZombieStepsEnd = (zombieSteps - _state.PlaybackIndex) * stepInterval + Mathf.Max(0f, _autoTimer);
                if (_state.PlaybackIndex >= zombieSteps || timeUntilZombieStepsEnd <= 0.45f)
                {
                    _renderer.PlayerConcienciaVfx.BeginTransitionToBlue(0.40f);
                }

                if (_state.PlaybackIndex >= _state.PlaybackSequence.Count && !_renderer.IsPlayerMovingToTarget)
                {
                    _renderer.PlayerConcienciaVfx.StopGlow();
                }
            }

            var keyboard = Keyboard.current;
            bool manualStep = false;
            if (keyboard != null)
            {
                var pressed = ReadPressedDirection(keyboard);
                if (pressed.HasValue)
                {
                    manualStep = true;
                }
            }

            if (_autoTimer > 0f && !manualStep) return;
            _autoTimer = stepInterval;

            int executedStepIndex = _state.PlaybackIndex;
            var result = _state.AdvancePlaybackStep();

            if (_renderer != null && _renderer.PlayerConcienciaVfx != null && _renderer.PlayerConcienciaVfx.IsActive)
            {
                int zombieSteps = Mathf.Max(1, _state.ZombieConciencia);
                float timeUntilZombieStepsEnd = (zombieSteps - _state.PlaybackIndex) * stepInterval + Mathf.Max(0f, _autoTimer);
                if (_state.PlaybackIndex >= zombieSteps || timeUntilZombieStepsEnd <= 0.45f)
                {
                    _renderer.PlayerConcienciaVfx.BeginTransitionToBlue(0.40f);
                }
            }

            if (result.Outcome == TurnOutcome.DamageDefeat)
            {
                AudioManager.Instance?.PlayMovePlayer();
            }
            else if (result.Outcome == TurnOutcome.PlaybackExhausted)
            {
                AudioManager.Instance?.PlayDefeat(DefeatReason.PlaybackExhausted);
            }
            else if (result.Outcome == TurnOutcome.SwitchVictory)
            {
                AudioManager.Instance?.PlayVictory();
            }
            else if (result.Moved)
            {
                AudioManager.Instance?.PlayMovePlayer();
            }

            _renderer.SyncFromState(_state);
            _hud.SetHistories(_state.Player.History, _state.Zombie.History);

            if (_hud != null)
            {
                if (result.Outcome != TurnOutcome.PlaybackExhausted && result.Outcome != TurnOutcome.NoPlayback)
                {
                    float pulseDuration = Mathf.Clamp(stepInterval * 0.65f, 0.22f, 0.30f);
                    _hud.PulsePlaybackArrow(executedStepIndex, pulseDuration);
                }
                else
                {
                    _hud.ResetAllArrowPulses();
                }
            }

            if (result.Outcome == TurnOutcome.PlaybackStep)
            {
                AudioManager.Instance?.PlayMoveEnemy();
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
                    _active = false;
                    _hud.SetInfected(true);
                    _hud.SetStatus("¡CONTACTO! Secuencia congelada · " +
                                   _state.PlaybackSequence.Count + " pasos");

                    if (!hasShownInfectionPause)
                    {
                        hasShownInfectionPause = true;
                        if (_infectionSequenceCoroutine != null)
                        {
                            StopCoroutine(_infectionSequenceCoroutine);
                        }
                        _infectionSequenceCoroutine = StartCoroutine(InfectionSequenceRoutine());
                    }
                    else
                    {
                        _autoTimer = Mathf.Max(0.03f,
                            _tuning.autoStepInterval + _tuning.playbackExtraDelay);
                    }
                    break;

                case TurnOutcome.SwitchVictory:
                    AudioManager.Instance?.PlayVictory();
                    _active = false;
                    _hud.SetStatus("");
                    if (_switchVictoryCoroutine != null)
                    {
                        StopCoroutine(_switchVictoryCoroutine);
                    }
                    _switchVictoryCoroutine = StartCoroutine(SwitchVictoryRoutine());
                    break;

                case TurnOutcome.DamageDefeat:
                    _active = false;
                    _hud.SetStatus("");
                    if (_damageDefeatCoroutine != null)
                    {
                        StopCoroutine(_damageDefeatCoroutine);
                    }
                    _damageDefeatCoroutine = StartCoroutine(DamageDefeatRoutine());
                    break;

                case TurnOutcome.EnergyDefeat:
                    AudioManager.Instance?.PlayDefeat(DefeatReason.EnergyDepleted);
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
                    if (_renderer != null)
                    {
                        StartCoroutine(StopGlowWhenPlayerArrivesRoutine());
                    }
                    AudioManager.Instance?.PlayDefeat(DefeatReason.PlaybackExhausted);
                    _active = false;
                    _renderer.PlayerVisual?.PlayDefeat(DefeatReason.PlaybackExhausted, _tuning.defeatDuration);
                    _hud.SetStatus("Derrota · secuencia agotada");
                    _onDefeat?.Invoke(DefeatReason.PlaybackExhausted);
                    break;
            }
        }

        private System.Collections.IEnumerator StopGlowWhenPlayerArrivesRoutine()
        {
            if (_renderer != null)
            {
                yield return _renderer.WaitForPlayerArrival();
                _renderer.PlayerConcienciaVfx?.StopGlow();
            }
        }

        private System.Collections.IEnumerator InfectionSequenceRoutine()
        {
            _active = false;

            // Esperar a que el jugador concluya el desplazamiento al punto de colisión
            if (_renderer != null)
            {
                yield return _renderer.WaitForPlayerArrival();
                _renderer.FreezeEntities();
            }

            Transform playerVisual = _renderer != null ? _renderer.PlayerVisualTransform : null;

            // Fase 1 (Impacto Humano): Al chocar, bloquea el movimiento y haz que el sprite original del Player
            // oscile suavemente (0.92 a 1.08) durante 0.5 segundos.
            if (_renderer != null && _renderer.PlayerVisual != null)
            {
                _renderer.PlayerVisual.KeepHumanSprite();
            }

            float elapsed1 = 0f;
            while (elapsed1 < 0.5f)
            {
                elapsed1 += Time.deltaTime;
                float t = (Mathf.Sin(elapsed1 * Mathf.PI * 4f) + 1f) * 0.5f;
                float scale = Mathf.Lerp(0.92f, 1.08f, t);
                if (playerVisual != null)
                {
                    playerVisual.localScale = new Vector3(scale, scale, 1f);
                }
                yield return null;
            }

            // Fase 2 (Metamorfosis): Tras ese medio segundo, cambio directo al sprite 'playerinfected' (NUNCA pescadito)
            if (_renderer != null && _renderer.PlayerVisual != null)
            {
                _renderer.PlayerVisual.SwitchToInfectedSprite();
            }
            if (_renderer != null && _renderer.PlayerConcienciaVfx != null)
            {
                _renderer.PlayerConcienciaVfx.StartInfectionGlow();
            }

            // Fase 3 (Asimilacion): Continua oscilando el sprite infectado durante 0.5 segundos adicionales.
            float elapsed2 = 0f;
            while (elapsed2 < 0.5f)
            {
                elapsed2 += Time.deltaTime;
                float t = (Mathf.Sin(elapsed2 * Mathf.PI * 4f) + 1f) * 0.5f;
                float scale = Mathf.Lerp(0.92f, 1.08f, t);
                if (playerVisual != null)
                {
                    playerVisual.localScale = new Vector3(scale, scale, 1f);
                }
                yield return null;
            }

            // Fase 4 (Mensaje): Restaura la escala a 1.0, y en este instante exacto, dispara la logica existente
            // que muestra el mensaje "Oops me infecto" y continua con el flujo que ya tenias programado para este estado.
            if (playerVisual != null)
            {
                playerVisual.localScale = Vector3.one;
            }

            _infectionPaused = true;
            Time.timeScale = 0f;
            if (_hud != null) _hud.ShowInfectionBanner(true, "Oops, me infectó");

            float bannerWait = 0f;
            while (bannerWait < 1.0f)
            {
                if (!HUDController.IsPauseMenuOpen && (_hud == null || !_hud.IsPaused))
                {
                    bannerWait += Time.unscaledDeltaTime;
                }
                Time.timeScale = 0f;
                yield return null;
            }

            if (_hud != null) _hud.ShowInfectionBanner(false);
            if (!HUDController.IsPauseMenuOpen && (_hud == null || !_hud.IsPaused))
            {
                Time.timeScale = 1f;
            }
            _infectionPaused = false;

            if (_renderer != null && _renderer.PlayerVisual != null)
            {
                _renderer.PlayerVisual.EndAgonyAndSetInfected(true);
            }

            // REACTIVACIÓN OBLIGATORIA DEL CONTROL Y DEL FLUJO DEL JUEGO
            _active = true;
            _heldDirection = null;
            _blockedDirection = null;
            _repeatTimer = 0f;
            _infectionSequenceCoroutine = null;
            _autoTimer = 0.05f;
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

        private System.Collections.IEnumerator SwitchVictoryRoutine()
        {
            _active = false;

            // Esperar estrictamente a que concluya la interpolación espacial hacia la meta
            if (_renderer != null)
            {
                yield return _renderer.WaitForPlayerArrival();
                _renderer.PlayerConcienciaVfx?.StopGlow();
            }

            // Pausa breve para ver al jugador infectado detenerse por completo sobre el brillo cian de la meta
            yield return new WaitForSeconds(0.12f);

            _switchVictoryCoroutine = null;
            _onVictory?.Invoke();
        }

        private System.Collections.IEnumerator DamageDefeatRoutine()
        {
            _active = false;

            // Esperar estrictamente a que concluya la interpolación espacial hacia la casilla letal
            if (_renderer != null)
            {
                yield return _renderer.WaitForPlayerArrival();
                _renderer.SnapPlayerToTarget();
                _renderer.PlayerConcienciaVfx?.StopGlow();
            }

            _damageDefeatCoroutine = null;
            AudioManager.Instance?.PlayDefeat(DefeatReason.DamageTile);
            _onDefeat?.Invoke(DefeatReason.DamageTile);
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
                _crtMaterial.SetFloat(InfectionProgressId, curve * 0.252f);
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
