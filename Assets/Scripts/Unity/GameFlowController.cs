using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Owns the level lifecycle: asynchronous constructive generation, loading, victory,
    /// defeat presentation and the same-seed restart / new-seed next-level transitions.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameFlowController : MonoBehaviour
    {
        private GameTuning _tuning;
        private Camera _camera;
        private LevelRenderer _renderer;
        private HUDController _hud;
        private DefeatPresentationController _defeat;
        private TurnManager _turn;

        private GameState _state;
        private LevelDefinition _currentLevel;
        private int _levelIndex = 1;
        private int _sessionSeed;
        private int _levelSeed;

        private bool _generating;
        private Task<GenerationResult> _generationTask;
        private int _pendingIndex;
        private int _pendingSeed;
        private int _generationRetries;

        private bool _defeatPlaying;
        private float _victoryDelay = -1f;

        public int SessionSeed => _sessionSeed;
        public int CurrentLevelSeed => _levelSeed;
        public int CurrentLevelIndex => _levelIndex;
        public LevelDefinition CurrentLevel => _currentLevel;

        private void Awake()
        {
            var controllers = FindObjectsByType<GameFlowController>(FindObjectsInactive.Exclude);
            if (controllers.Length > 1)
            {
                Destroy(gameObject);
                return;
            }

            _tuning = Resources.Load<GameTuning>("GameTuning");
            if (_tuning == null)
            {
                _tuning = ScriptableObject.CreateInstance<GameTuning>();
                _tuning.name = "GameTuning (runtime defaults)";
            }

            _camera = Camera.main;
            if (_camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                _camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = _tuning.cameraBackground;

            _renderer = GetComponent<LevelRenderer>();
            if (_renderer == null) _renderer = gameObject.AddComponent<LevelRenderer>();

            _hud = GetComponent<HUDController>();
            if (_hud == null) _hud = gameObject.AddComponent<HUDController>();
            _hud.Build(_tuning);

            _defeat = GetComponent<DefeatPresentationController>();
            if (_defeat == null) _defeat = gameObject.AddComponent<DefeatPresentationController>();
            _defeat.Build(_tuning);

            _turn = GetComponent<TurnManager>();
            if (_turn == null) _turn = gameObject.AddComponent<TurnManager>();
        }

        private void Start()
        {
            // A fresh session seed on every Play run. With the debug option enabled the same
            // session is reused, which is useful while inspecting one specific level.
            _sessionSeed = _tuning.useFixedSeed
                ? _tuning.fixedSeed
                : SeedUtility.GenerateSessionSeed();
            _levelIndex = 1;
            _levelSeed = SeedUtility.DeriveLevelSeed(_sessionSeed, _levelIndex);
            BeginLevel(_levelIndex, _levelSeed);
        }

        private void BeginLevel(int levelIndex, int seed)
        {
            if (_generating) return;

            _generating = true;
            _pendingIndex = levelIndex;
            _pendingSeed = seed;
            _levelSeed = seed;
            _turn.SetActive(false);
            _hud.SetLoading(true, "Generando nivel " + levelIndex);

            var settings = _tuning.ToGenerationSettings();
            _generationTask = Task.Run(() =>
            {
                try
                {
                    return GeneradorNivel.Generate(levelIndex, seed, settings);
                }
                catch
                {
                    return null;
                }
            });
        }

        private void Update()
        {
            if (_generating && _generationTask != null && _generationTask.IsCompleted)
            {
                var task = _generationTask;
                _generationTask = null;

                GenerationResult result = null;
                try { result = task.Result; }
                catch (Exception exception) { Debug.LogException(exception); }

                if (result != null && result.Level != null)
                {
                    _generating = false;
                    _generationRetries = 0;
                    _hud.SetLoading(false);
                    LoadLevel(result.Level);
                }
                else
                {
                    _generationRetries++;
                    _generating = false;
                    if (_generationRetries <= 3)
                    {
                        BeginLevel(_pendingIndex, _pendingSeed);
                    }
                    else
                    {
                        _hud.SetLoading(true, "No se pudo generar el nivel");
                        _hud.SetStatus("Error de generación · reinicia Play para volver a intentarlo");
                    }
                }
            }

            if (_victoryDelay >= 0f)
            {
                _victoryDelay -= Time.unscaledDeltaTime;
                if (_victoryDelay <= 0f)
                {
                    _victoryDelay = -1f;
                    _levelIndex++;
                    _levelSeed = SeedUtility.DeriveLevelSeed(_sessionSeed, _levelIndex);
                    BeginLevel(_levelIndex, _levelSeed);
                }
            }
        }

        private void LoadLevel(LevelDefinition level)
        {
            if (level == null) return;

            _currentLevel = level;
            _levelIndex = level.LevelIndex;
            _levelSeed = level.Seed;
            _victoryDelay = -1f;
            _defeatPlaying = false;

            _state = level.CreateGameState();

            _renderer.Build(level, _tuning, _camera);
            _renderer.SyncFromState(_state, true);

            _hud.SetLevelInfo(level);
            _hud.SetInfected(false);
            _hud.SetHistories(_state.Player.History, _state.Zombie.History);
            _hud.SetLoading(false);
            _defeat.Hide();

            _turn.Initialize(_state, _renderer, _hud, _tuning, HandleVictory, HandleDefeat);

            string status = "Turno 0 · Energía " + _state.CurrentEnergy + "/" + _state.MaxEnergy +
                            " · llega al interruptor";
            if (level.UsedFallback)
            {
                status = "Nivel de emergencia · " + status;
            }
            else if (level.UniqueConfirmed)
            {
                status = "Solución única validada · " + status;
            }
            else if (level.SolutionCount > 1)
            {
                status = "Solución no única (" + level.SolutionCount + ") · " + status;
            }

            _hud.SetStatus(status);
        }

        private void HandleVictory()
        {
            if (_victoryDelay >= 0f || _defeatPlaying || _generating) return;

            _turn.SetActive(false);
            _hud.SetStatus("¡NIVEL SUPERADO! Preparando el siguiente nivel…");
            _victoryDelay = 0.35f;
        }

        private void HandleDefeat(DefeatReason reason)
        {
            if (_defeatPlaying) return;
            _defeatPlaying = true;
            _turn.SetActive(false);

            string title;
            string subtitle;
            switch (reason)
            {
                case DefeatReason.DamageTile:
                    title = "CASILLA DAÑINA";
                    subtitle = "La energía se drena hasta cero. El nivel se reinicia.";
                    break;
                case DefeatReason.EnergyDepleted:
                    title = "TRANSFORMACIÓN AMBIENTAL";
                    subtitle = "Sin energía, la infección te transforma. El nivel se reinicia.";
                    break;
                case DefeatReason.PlaybackExhausted:
                    title = "SECUENCIA AGOTADA";
                    subtitle = "El patrón heredado no llegó al interruptor. El nivel se reinicia.";
                    break;
                default:
                    title = "DERROTA";
                    subtitle = "El nivel se reinicia.";
                    break;
            }

            _hud.SetStatus("Derrota · " + title.ToLowerInvariant());
            _defeat.Play(title, subtitle, _tuning.defeatDuration, RestartSameLevel);
        }

        private void RestartSameLevel()
        {
            _defeatPlaying = false;
            if (_currentLevel != null)
            {
                // Reuse the exact level definition, including its seed and consciousness values.
                LoadLevel(_currentLevel);
            }
            else
            {
                BeginLevel(_pendingIndex, _pendingSeed);
            }
        }

        private void OnDestroy()
        {
            _generationTask = null;
        }
    }
}
