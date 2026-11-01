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
        private AudioManager _audio;

        private GameState _state;
        private LevelDefinition _currentLevel;
        private int _levelIndex = 1;
        private int _sessionSeed;
        private int _levelSeed;

        private bool _generating;
        private Coroutine _generationCoroutine;
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

            AmbientDustController.AttachToCamera(_camera);

            _renderer = GetComponent<LevelRenderer>();
            if (_renderer == null) _renderer = gameObject.AddComponent<LevelRenderer>();

            _hud = GetComponent<HUDController>();
            if (_hud == null) _hud = gameObject.AddComponent<HUDController>();
            _hud.Build(_tuning, _camera);

            _defeat = GetComponent<DefeatPresentationController>();
            if (_defeat == null) _defeat = gameObject.AddComponent<DefeatPresentationController>();
            _defeat.Build(_tuning, _camera);

            _turn = GetComponent<TurnManager>();
            if (_turn == null) _turn = gameObject.AddComponent<TurnManager>();

            _audio = GetComponent<AudioManager>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioManager>();
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

        private void BeginLevel(int levelIndex, int seed, bool showLoadingOverlay = true)
        {
            if (_generating) return;

            _generating = true;
            _pendingIndex = levelIndex;
            _pendingSeed = seed;
            _levelSeed = seed;
            _turn.SetActive(false);

            if (_hud != null)
            {
                _hud.HideVictoryBanner();
                if (showLoadingOverlay)
                {
                    _hud.SetLoading(true, "Generando nivel " + levelIndex);
                }
                else
                {
                    _hud.SetLoading(false);
                }
            }

            if (_generationCoroutine != null)
            {
                StopCoroutine(_generationCoroutine);
            }
            _generationCoroutine = StartCoroutine(GenerateLevelRoutine(levelIndex, seed));
        }

        private System.Collections.IEnumerator GenerateLevelRoutine(int levelIndex, int seed)
        {
            var settings = _tuning.ToGenerationSettings();
            GenerationResult result = null;

            yield return GeneradorNivel.GenerateCoroutine(levelIndex, seed, settings, r => result = r);

            _generationCoroutine = null;

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

        private void Update()
        {
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
            _curaCoroutine = null;
            _muerteCoroutine = null;

            _state = level.CreateGameState();

            _renderer.Build(level, _tuning, _camera);
            _renderer.SyncFromState(_state, true);

            _hud.SetLevelInfo(level);
            _hud.SetConciencia(level.PlayerConciencia, level.ZombieConciencia);
            _hud.SetInfected(false);
            _hud.SetHistories(_state.Player.History, _state.Zombie.History);
            _hud.SetLoading(false);
            _hud.HideVictoryBanner();
            _defeat.Hide();
            AudioManager.Instance?.RestoreMusicVolume();

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

        private Coroutine _curaCoroutine;
        private Coroutine _muerteCoroutine;

        private void HandleVictory()
        {
            if (_victoryDelay >= 0f || _defeatPlaying || _generating || _curaCoroutine != null) return;

            _curaCoroutine = StartCoroutine(SecuenciaCura());
        }

        private System.Collections.IEnumerator SecuenciaCura()
        {
            // Fase 1 (Bloqueo): Deshabilita el control del jugador y detiene el movimiento del zombi.
            _turn.SetActive(false);
            if (_renderer != null)
            {
                yield return _renderer.WaitForPlayerArrival();
                _renderer.FreezeEntities();
            }

            // Fase 2 (Oscilacion suave entre 0.92 y 1.08):
            Transform playerVisual = _renderer != null ? _renderer.PlayerVisualTransform : null;
            float duration = 1.0f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = (Mathf.Sin(elapsed * Mathf.PI * 4f) + 1f) * 0.5f;
                float scale = Mathf.Lerp(0.92f, 1.08f, t);

                if (playerVisual != null)
                {
                    playerVisual.localScale = new Vector3(scale, scale, 1f);
                }
                yield return null;
            }

            // Fase 3 (Purificacion): Restaura la escala a 1.0. Cambia el sprite del Player por su sprite original (sano) y restaura su color a Color.white.
            if (playerVisual != null)
            {
                playerVisual.localScale = Vector3.one;
            }
            if (_renderer != null)
            {
                _renderer.PurifyPlayer();
            }
            if (_turn != null)
            {
                _turn.ClearInfectionVisuals();
            }

            // Asignación y activación explícita del mensaje de victoria "bien" (visible en pantalla)
            if (_hud != null)
            {
                _hud.SetStatus("bien");
                _hud.ShowVictoryBanner("bien");
            }

            // Fase 4 (Pausa de visualizacion): Pausa de 1.5s estrictamente posterior a la asignacion y activacion
            yield return new WaitForSeconds(1.5f);

            if (_hud != null)
            {
                _hud.HideVictoryBanner();
                _hud.SetStatus("");
            }

            // Esperar un frame tras ocultar "bien" antes de iniciar la generacion del siguiente nivel
            // para garantizar que nunca se crucen ni solapen en pantalla "bien" y "Generando nivel..."
            yield return null;

            // Fase 5 (Transicion): Llama a la funcion que genera y carga el siguiente nivel.
            _curaCoroutine = null;
            _levelIndex++;
            _levelSeed = SeedUtility.DeriveLevelSeed(_sessionSeed, _levelIndex);
            BeginLevel(_levelIndex, _levelSeed, false);
        }

        private void HandleDefeat(DefeatReason reason)
        {
            if (_defeatPlaying) return;
            _defeatPlaying = true;
            _turn.SetActive(false);

            if (reason == DefeatReason.DamageTile)
            {
                if (_muerteCoroutine != null)
                {
                    StopCoroutine(_muerteCoroutine);
                }
                _muerteCoroutine = StartCoroutine(SecuenciaMuerte());
                return;
            }

            if (_hud != null)
            {
                _hud.SetStatus("");
                _hud.HideDefeatBanner();
            }
            AudioManager.Instance?.PlayDefeat(reason);
            _defeat.Play("mal", "", _tuning.defeatDuration, RestartSameLevel);
        }

        private System.Collections.IEnumerator SecuenciaMuerte()
        {
            // Bloqueo de controles y congelación de entidades
            _turn.SetActive(false);
            if (_renderer != null)
            {
                yield return _renderer.WaitForPlayerArrival();
                _renderer.FreezeEntities();
            }

            // Desactiva cualquier texto debug como "Mal"
            _hud.SetStatus("");

            Transform playerVisual = _renderer != null ? _renderer.PlayerVisualTransform : null;

            // Fase 1 (Agonia Humana): Manten el sprite original del Player y ejecuta la oscilacion suave (0.92 a 1.08)
            // durante exactamente 0.5 segundos.
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

            // Fase 2 (Metamorfosis): Justo al terminar ese medio segundo, cambia el sprite a 'PlayerLimitTime' (pescadito).
            if (_renderer != null && _renderer.PlayerVisual != null)
            {
                _renderer.PlayerVisual.PlayAgonyLimitTime();
            }

            // Fase 3 (Agonia Infecciosa del Pescadito): Frecuencia acelerada x3.0 para sincronizar con el ritmo rápido del audio
            float elapsed2 = 0f;
            const float fastFrequency = Mathf.PI * 4f * 3.0f; // Multiplicador x3.0
            while (elapsed2 < 0.5f)
            {
                elapsed2 += Time.deltaTime;
                float t = (Mathf.Sin(elapsed2 * fastFrequency) + 1f) * 0.5f;
                float scale = Mathf.Lerp(0.92f, 1.08f, t);
                if (playerVisual != null)
                {
                    playerVisual.localScale = new Vector3(scale, scale, 1f);
                }
                yield return null;
            }

            // Fase 4 (Resolucion):
            // 1. Activar la animación de derrota idéntica a la derrota por zombi (achatamiento vertical suave del pescadito hasta 0.78)
            if (_renderer != null && _renderer.PlayerVisual != null)
            {
                _renderer.PlayerVisual.PlayDefeat(DefeatReason.DamageTile, 1.4f);
            }
            else if (playerVisual != null)
            {
                playerVisual.localScale = Vector3.one;
            }

            // 2. Activar y mostrar ÚNICAMENTE el texto progresivo "mal" junto con el oscurecimiento (fade a negro),
            // eliminando el primer "mal" súbito de fuente pequeña (_hud.ShowDefeatBanner) para evitar duplicación.
            if (_hud != null)
            {
                _hud.SetStatus("");
                _hud.HideDefeatBanner();
            }
            if (_defeat != null)
            {
                _defeat.Play("mal", "", 1.4f, null);
            }

            // 3. Ejecutar la pausa dramática extendida de 1.4s (igualando la duración de la pantalla de derrota del Zombi)
            yield return new WaitForSeconds(1.4f);

            if (_hud != null)
            {
                _hud.HideDefeatBanner();
            }

            // 4. Restaurar limpiamente la escala a Vector3.one y llamar a la función de reinicio de nivel
            if (playerVisual != null)
            {
                playerVisual.localScale = Vector3.one;
                playerVisual.localPosition = new Vector3(0f, 0.25f, 0f);
            }
            if (_renderer != null && _renderer.PlayerVisual != null)
            {
                _renderer.PlayerVisual.ResetState();
            }

            _muerteCoroutine = null;
            _defeatPlaying = false;
            RestartSameLevel();
        }

        private void RestartSameLevel()
        {
            _defeatPlaying = false;
            if (_hud != null)
            {
                _hud.HideDefeatBanner();
                _hud.HideVictoryBanner();
            }
            _defeat.Hide();
            AudioManager.Instance?.RestoreMusicVolume();

            if (_renderer != null)
            {
                if (_renderer.PlayerVisual != null)
                {
                    _renderer.PlayerVisual.ResetState();
                }
                if (_renderer.PlayerVisualTransform != null)
                {
                    _renderer.PlayerVisualTransform.localScale = Vector3.one;
                    _renderer.PlayerVisualTransform.localPosition = new Vector3(0f, 0.25f, 0f);
                }
            }
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
            if (_curaCoroutine != null)
            {
                StopCoroutine(_curaCoroutine);
                _curaCoroutine = null;
            }

            if (_muerteCoroutine != null)
            {
                StopCoroutine(_muerteCoroutine);
                _muerteCoroutine = null;
            }

            if (_generationCoroutine != null)
            {
                StopCoroutine(_generationCoroutine);
                _generationCoroutine = null;
            }
        }
    }
}
