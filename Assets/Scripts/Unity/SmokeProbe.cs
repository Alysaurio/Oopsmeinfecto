using System;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Batch-mode runtime smoke test (copy-only helper): enters Play mode on SampleScene,
    /// lets the bootstrap build level 1, replays the planned manual solution, and waits for
    /// the automatic victory transition into level 2.
    /// </summary>
    public sealed class SmokeProbe : MonoBehaviour
    {
        private static readonly FieldInfo FlowLevelField =
            typeof(GameFlowController).GetField("_currentLevel", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo FlowIndexField =
            typeof(GameFlowController).GetField("_levelIndex", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo MoveMethod =
            typeof(TurnManager).GetMethod("PerformManualMove", BindingFlags.NonPublic | BindingFlags.Instance);

#if UNITY_EDITOR
        public static void RunSmoke()
        {
            SmokeLauncher.RunSmoke();
        }
#endif

        private float _elapsed;
        private int _phase;
        private int _moveIndex;
        private float _moveTimer;
        private GameFlowController _flow;
        private TurnManager _turn;
        private HUDController _hud;
        private LevelRenderer _renderer;
        private LevelDefinition _level;
        private int _expectedNextIndex;
        private bool _sawContact;
        private bool _sawAmbientVfx;
        private bool _finished;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            try
            {
                Tick();
            }
            catch (Exception exception)
            {
                if (!_finished) Fail("exception: " + exception);
            }
        }

        private void Tick()
        {
            // Batch mode can stall the main thread; clamp the delta so one long frame does not
            // look like a test timeout.
            float delta = Mathf.Min(Time.unscaledDeltaTime, 0.5f);
            _elapsed += delta;
            if (_elapsed > 60f)
            {
                Fail("timeout phase=" + _phase + " moveIndex=" + _moveIndex);
                return;
            }

            if (_flow == null)
            {
                _flow = FindAnyObjectByType<GameFlowController>();
                if (_flow != null)
                {
                    _turn = _flow.GetComponent<TurnManager>();
                    _hud = _flow.GetComponent<HUDController>();
                    _renderer = _flow.GetComponent<LevelRenderer>();
                }
            }

            if (_flow == null || _turn == null) return;

            var state = _turn.State;
            switch (_phase)
            {
                case 0:
                    if (state == null) return;
                    _level = (LevelDefinition)FlowLevelField.GetValue(_flow);
                    if (_level == null) return;
                    if (_level.PlannedManualDirections == null || _level.PlannedManualDirections.Length == 0)
                    {
                        Fail("level has no planned manual directions");
                        return;
                    }

                    VerifyInitialLevel(state, _level);
                    _expectedNextIndex = _level.LevelIndex + 1;
                    _phase = 1;
                    _moveTimer = 0.1f;
                    _elapsed = 0f;
                    break;

                case 1:
                    if (state == null) return;
                    if (state.Phase == GamePhase.HumanControlled)
                    {
                        _moveTimer -= delta;
                        if (_moveTimer > 0f) return;

                        if (_moveIndex >= _level.PlannedManualDirections.Length)
                        {
                            Fail("planned moves exhausted without contact");
                            return;
                        }

                        var direction = _level.PlannedManualDirections[_moveIndex];
                        MoveMethod.Invoke(_turn, new object[] { direction });
                        Debug.Log("SMOKE_MOVE index=" + _moveIndex + " direction=" + direction +
                                  " phaseAfter=" + state.Phase + " turn=" + state.TurnNumber +
                                  " energy=" + state.CurrentEnergy);
                        _moveIndex++;
                        _moveTimer = 0.12f;

                        if (_moveIndex == 2) VerifyAmbientStarted(state);
                        if (_moveIndex >= _level.PlannedManualDirections.Length)
                        {
                            _phase = 2;
                            _moveTimer = 0f;
                        }
                    }
                    else
                    {
                        _sawContact = true;
                        _phase = 2;
                        _moveTimer = 0f;
                        _elapsed = 0f;
                    }
                    break;

                case 2:
                    if (state == null) return;
                    if (state.Phase == GamePhase.InfectedPlayback) _sawContact = true;

                    int index = (int)FlowIndexField.GetValue(_flow);
                    if (index >= _expectedNextIndex && state.TurnNumber == 0 &&
                        state.Phase == GamePhase.HumanControlled)
                    {
                        VerifyNextLevel(index);
                    }
                    break;
            }
        }

        private void VerifyInitialLevel(GameState state, LevelDefinition level)
        {
            Require(level.Width >= 6 && level.Width <= 16, "width=" + level.Width);
            Require(level.Height >= 6 && level.Height <= 16, "height=" + level.Height);
            Require(level.PlayerConciencia >= 2 && level.PlayerConciencia <= 5,
                "playerConciencia=" + level.PlayerConciencia);
            Require(level.ZombieConciencia >= 2 && level.ZombieConciencia <= 5,
                "zombieConciencia=" + level.ZombieConciencia);
            Require(level.MaxEnergy >= 5 && level.MaxEnergy <= 10, "maxEnergy=" + level.MaxEnergy);
            Require(level.HazardPositions != null && level.HazardPositions.Length >= 1,
                "hazards=" + (level.HazardPositions == null ? 0 : level.HazardPositions.Length));
            Require(level.UniqueConfirmed, "uniqueConfirmed=false");
            Require(level.PlannedSequence != null &&
                    level.PlannedSequence.Length == level.ZombieConciencia + level.PlayerConciencia,
                "sequence length=" + (level.PlannedSequence == null ? 0 : level.PlannedSequence.Length));
            Require(level.PlannedAutoPositions != null &&
                    level.PlannedAutoPositions.Length - 1 == level.PlannedSequence.Length,
                "automatic trajectory does not consume the whole sequence");
            Require(level.PlannedAutoPositions.Length - 1 >= level.ZombieConciencia + 2,
                "automatic trajectory shorter than zombie conciencia + 2");
            Require(level.InitialOptions >= 2, "initialOptions=" + level.InitialOptions);
            Require(level.Bounds.IsValid, "invalid playable bounds");
            Require(level.Seed == SeedUtility.DeriveLevelSeed(_flow.SessionSeed, level.LevelIndex),
                "level seed is not derived from the session seed");
            Require(state.TurnNumber == 0, "turn=" + state.TurnNumber);
            Require(state.Player.Position.Equals(level.PlayerStart), "player start mismatch");
            Require(state.Zombie.Position.Equals(level.ZombieStart), "zombie start mismatch");

            Require(_renderer != null && _renderer.transform.childCount > 0, "renderer has no children");
            Require(_renderer != null && _renderer.ZombieFacing != null, "NPC facing controller missing");
            Require(_renderer != null && _renderer.ZombieFacing != null &&
                    _renderer.GetComponentInChildren<SpriteRenderer>() != null,
                "renderer has no sprites");
            Require(_renderer != null && _renderer.AmbientVfx != null, "ambient infection VFX missing");
            Require(_renderer != null && _renderer.AmbientVfx != null &&
                    _renderer.AmbientVfx.Intensity <= 0.001f,
                "ambient infection VFX should start hidden");
            Require(_hud != null && _hud.PlayerHighlightedArrowCount == 0,
                "player history should have no highlighted arrows at level start");

            var camera = Camera.main;
            Require(camera != null, "main camera missing");
            if (camera != null)
            {
                Require(Mathf.Abs(camera.transform.position.x - level.Bounds.CenterX) < 0.01f,
                    "camera is not centered on the playable bounds");
                Require(Mathf.Abs(camera.transform.position.y - level.Bounds.CenterY) < 0.01f,
                    "camera is not centered on the playable bounds");
            }

            bool levelLabel = false;
            foreach (var text in FindObjectsByType<Text>(FindObjectsInactive.Include))
            {
                if (text.text != null && text.text.StartsWith("NIVEL " + level.LevelIndex))
                {
                    levelLabel = true;
                    break;
                }
            }
            Require(levelLabel, "HUD level label missing");

            Debug.Log("SMOKE_INITIAL ok level=" + level.LevelIndex + " seed=" + level.Seed +
                      " size=" + level.Width + "x" + level.Height +
                      " bounds=" + level.Bounds.Width + "x" + level.Bounds.Height +
                      " energy=" + state.CurrentEnergy + "/" + state.MaxEnergy +
                      " playerConciencia=" + level.PlayerConciencia +
                      " zombieConciencia=" + level.ZombieConciencia +
                      " hazards=" + level.HazardPositions.Length +
                      " manualMoves=" + level.PlannedManualDirections.Length +
                      " renderChildren=" + _renderer.transform.childCount);
        }

        private void VerifyAmbientStarted(GameState state)
        {
            if (_sawAmbientVfx) return;
            _sawAmbientVfx = true;

            Require(_renderer != null && _renderer.AmbientVfx != null,
                "ambient infection VFX missing after two moves");
            if (_renderer == null || _renderer.AmbientVfx == null) return;

            Require(_renderer.AmbientVfx.Intensity > 0.001f,
                "ambient infection VFX did not start after the second valid move");
            int expected = Mathf.Min(state.Player.HistoryCount, _level.PlayerConciencia);
            Require(_hud != null && _hud.PlayerHighlightedArrowCount == expected,
                "player history highlight count=" + (_hud == null ? -1 : _hud.PlayerHighlightedArrowCount) +
                " expected=" + expected);
            Debug.Log("SMOKE_VFX ok intensity=" + _renderer.AmbientVfx.Intensity +
                      " particles=" + _renderer.AmbientVfx.ActiveParticles +
                      " highlights=" + _hud.PlayerHighlightedArrowCount);
        }

        private void VerifyNextLevel(int index)
        {
            var next = (LevelDefinition)FlowLevelField.GetValue(_flow);
            Require(next != null, "next level is null");
            Require(next.LevelIndex == index, "level index=" + next.LevelIndex + " expected=" + index);
            Require(next.Seed != _level.Seed, "next level reused the previous seed");
            Require(next.Seed == SeedUtility.DeriveLevelSeed(_flow.SessionSeed, next.LevelIndex),
                "next level seed is not derived from the session seed");
            Require(next.PlannedManualDirections != null && next.PlannedManualDirections.Length > 0,
                "next level has no planned solution");
            Require(_sawContact, "contact/playback was never observed");
            Require(_sawAmbientVfx, "ambient infection VFX was never observed");

            Debug.Log("SMOKE_VICTORY ok fromLevel=" + _level.LevelIndex + " toLevel=" + next.LevelIndex +
                      " movesApplied=" + _moveIndex + " newSeed=" + next.Seed);
            Debug.Log("SMOKE_RESULT PASS");
            _finished = true;
#if UNITY_EDITOR
            EditorApplication.Exit(0);
#endif
        }

        private void Require(bool condition, string message)
        {
            if (condition) return;
            Fail("assertion failed: " + message);
        }

        private void Fail(string message)
        {
            if (_finished) return;
            _finished = true;
            Debug.LogError("SMOKE_RESULT FAIL " + message);
#if UNITY_EDITOR
            EditorApplication.Exit(1);
#endif
            throw new InvalidOperationException("smoke failed: " + message);
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Survives the startup domain reload by keeping the pending flag in SessionState and
    /// re-registering its update callbacks from the InitializeOnLoad constructor.
    /// </summary>
    [InitializeOnLoad]
    public static class SmokeLauncher
    {
        private const string PendingKey = "Oopsmeinfecto.SmokePending";
        private const string StartKey = "Oopsmeinfecto.SmokeStart";
        private static double _readySince = -1.0;

        static SmokeLauncher()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            Debug.Log("SMOKE_WAIT resumed after domain reload");
            EditorApplication.update += StartWhenReady;
            EditorApplication.update += Watchdog;
        }

        public static void RunSmoke()
        {
            SessionState.SetBool(PendingKey, true);
            SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= StartWhenReady;
            EditorApplication.update -= Watchdog;
            EditorApplication.update += StartWhenReady;
            EditorApplication.update += Watchdog;
            Debug.Log("SMOKE_WAIT launch requested");
        }

        private static void StartWhenReady()
        {
            if (EditorApplication.isCompiling)
            {
                _readySince = -1.0;
                return;
            }

            if (_readySince < 0.0)
            {
                _readySince = EditorApplication.timeSinceStartup;
                Debug.Log("SMOKE_WAIT editor idle");
                return;
            }

            // Let startup search indexing / asset post-processing settle; entering play mode
            // while those are still pending forces a domain reload that wipes the game state.
            if (EditorApplication.timeSinceStartup - _readySince < 45.0) return;

            EditorApplication.update -= StartWhenReady;
            EditorApplication.update -= Watchdog;
            SessionState.SetBool(PendingKey, false);
            Debug.Log("SMOKE_START entering play mode");
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            var probe = new GameObject("Smoke Probe");
            probe.AddComponent<SmokeProbe>();
            EditorApplication.EnterPlaymode();
        }

        private static void Watchdog()
        {
            float start = SessionState.GetFloat(StartKey, 0f);
            if (start <= 0f) return;
            if (EditorApplication.timeSinceStartup - start < 900.0) return;
            SessionState.SetBool(PendingKey, false);
            Debug.LogError("SMOKE_RESULT FAIL watchdog timeout");
            EditorApplication.Exit(2);
        }
    }

    /// <summary>
    /// Play-mode side of the Stop/Play regression check. It waits until the bootstrap has
    /// generated a level and the renderer has real sprites and HUD text, records that fact in
    /// SessionState and then stops Play mode. The launcher runs this three times.
    /// </summary>
    public sealed class CyclePlayProbe : MonoBehaviour
    {
        private float _elapsed;
        private bool _done;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (_done) return;

            _elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.5f);
            if (_elapsed > 120f)
            {
                Fail("cycle timeout waiting for a generated level");
                return;
            }

            var flow = FindAnyObjectByType<GameFlowController>();
            if (flow == null || flow.CurrentLevel == null) return;

            var renderer = flow.GetComponent<LevelRenderer>();
            var turn = flow.GetComponent<TurnManager>();
            if (renderer == null || turn == null || turn.State == null) return;

            var content = renderer.transform.Find("Level Content");
            if (content == null) return;

            var sprites = content.GetComponentsInChildren<SpriteRenderer>(true);
            int withSprite = 0;
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i].sprite != null) withSprite++;
            }
            if (withSprite < 10) return;

            var camera = Camera.main;
            if (camera == null)
            {
                Fail("main camera missing after entering Play mode");
                return;
            }
            if (camera.orthographicSize <= 0.01f)
            {
                Fail("camera has an invalid orthographic size");
                return;
            }

            int texts = 0;
            foreach (var text in FindObjectsByType<Text>(FindObjectsInactive.Include))
            {
                if (text != null && !string.IsNullOrEmpty(text.text)) texts++;
            }
            if (texts < 2)
            {
                Fail("HUD text is missing after entering Play mode");
                return;
            }

            _done = true;
            SessionState.SetBool(CycleLauncher.ReadyKey, true);
            Debug.Log("CYCLE_OK level=" + flow.CurrentLevel.LevelIndex +
                      " seed=" + flow.CurrentLevel.Seed +
                      " sprites=" + withSprite +
                      " texts=" + texts +
                      " ortho=" + camera.orthographicSize);
            EditorApplication.ExitPlaymode();
        }

        private void Fail(string message)
        {
            if (_done) return;
            _done = true;
            SessionState.SetBool(CycleLauncher.PendingKey, false);
            SessionState.SetBool(CycleLauncher.ReadyKey, false);
            Debug.LogError("CYCLE_RESULT FAIL " + message);
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// Runs three consecutive Play -> generate -> Stop -> Play cycles and fails if any cycle
    /// enters Play mode without a camera, sprites or HUD (the previous black-screen symptom).
    /// </summary>
    [InitializeOnLoad]
    public static class CycleLauncher
    {
        internal const string PendingKey = "Oopsmeinfecto.CyclePending";
        internal const string ReadyKey = "Oopsmeinfecto.CycleReady";
        private const string CountKey = "Oopsmeinfecto.CycleCount";
        private const string StateKey = "Oopsmeinfecto.CycleState";
        private const int TotalCycles = 3;

        private static double _idleSince = -1.0;
        private static double _cycleStart;
        private static double _exitAt;

        static CycleLauncher()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            Debug.Log("CYCLE_WAIT resumed after domain reload");
            EditorApplication.update += Tick;
        }

        public static void RunStopPlayCycles()
        {
            SessionState.SetBool(PendingKey, true);
            SessionState.SetBool(ReadyKey, false);
            SessionState.SetInt(CountKey, 0);
            SessionState.SetInt(StateKey, 0);
            _idleSince = -1.0;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log("CYCLE_WAIT launch requested");
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(PendingKey, false))
            {
                EditorApplication.update -= Tick;
                return;
            }

            if (EditorApplication.isCompiling)
            {
                _idleSince = -1.0;
                return;
            }

            int state = SessionState.GetInt(StateKey, 0);
            if (state == 0)
            {
                if (EditorApplication.isPlaying) return;
                if (_idleSince < 0.0)
                {
                    _idleSince = EditorApplication.timeSinceStartup;
                    Debug.Log("CYCLE_WAIT editor idle");
                    return;
                }
                if (EditorApplication.timeSinceStartup - _idleSince < 45.0) return;
                StartCycle();
            }
            else if (state == 1)
            {
                if (EditorApplication.isPlaying) return;

                if (SessionState.GetBool(ReadyKey, false))
                {
                    SessionState.SetBool(ReadyKey, false);
                    int count = SessionState.GetInt(CountKey, 0) + 1;
                    SessionState.SetInt(CountKey, count);
                    Debug.Log("CYCLE_EXIT ok count=" + count);

                    if (count >= TotalCycles)
                    {
                        SessionState.SetBool(PendingKey, false);
                        EditorApplication.update -= Tick;
                        Debug.Log("CYCLE_RESULT PASS cycles=" + count);
                        EditorApplication.Exit(0);
                        return;
                    }

                    SessionState.SetInt(StateKey, 2);
                    _exitAt = EditorApplication.timeSinceStartup;
                    return;
                }

                if (EditorApplication.timeSinceStartup - _cycleStart > 150.0)
                {
                    SessionState.SetBool(PendingKey, false);
                    EditorApplication.update -= Tick;
                    Debug.LogError("CYCLE_RESULT FAIL timeout waiting for Play mode");
                    EditorApplication.Exit(1);
                }
            }
            else
            {
                if (EditorApplication.isPlaying) return;
                if (EditorApplication.timeSinceStartup - _exitAt < 0.75) return;
                StartCycle();
            }
        }

        private static void StartCycle()
        {
            SessionState.SetBool(ReadyKey, false);
            SessionState.SetInt(StateKey, 1);
            _cycleStart = EditorApplication.timeSinceStartup;
            int count = SessionState.GetInt(CountKey, 0);
            Debug.Log("CYCLE_START next=" + (count + 1) + " entering Play mode");
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            var probe = new GameObject("Cycle Probe");
            probe.AddComponent<CyclePlayProbe>();
            EditorApplication.EnterPlaymode();
        }
    }

    /// <summary>
    /// Drives the real energy-exhaustion path: it lowers the current energy to one, performs a
    /// legal move that leaves no energy and no contact, verifies the environmental transformation
    /// presentation, then waits for the defeat overlay and the same-level restart.
    /// </summary>
    public sealed class ExhaustionPlayProbe : MonoBehaviour
    {
        private static readonly FieldInfo EnergyField =
            typeof(GameState).GetField("<CurrentEnergy>k__BackingField",
                BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo DefeatPlayingField =
            typeof(GameFlowController).GetField("_defeatPlaying",
                BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo MoveMethod =
            typeof(TurnManager).GetMethod("PerformManualMove",
                BindingFlags.NonPublic | BindingFlags.Instance);

        private float _elapsed;
        private float _phaseTime;
        private int _stage;
        private bool _sawDefeatOverlay;
        private bool _done;
        private int _originalSeed;
        private int _originalPlayerConciencia;
        private int _originalZombieConciencia;
        private int _originalLevelIndex;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (_done) return;
            try
            {
                Tick();
            }
            catch (Exception exception)
            {
                Fail("exception: " + exception);
            }
        }

        private void Tick()
        {
            _elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.5f);
            if (_elapsed > 120f)
            {
                Fail("timeout stage=" + _stage);
                return;
            }

            var flow = FindAnyObjectByType<GameFlowController>();
            if (flow == null || flow.CurrentLevel == null) return;
            var turn = flow.GetComponent<TurnManager>();
            var renderer = flow.GetComponent<LevelRenderer>();
            if (turn == null || renderer == null || turn.State == null) return;

            if (_stage == 0)
            {
                var state = turn.State;
                if (state.Phase != GamePhase.HumanControlled) return;

                Direction? chosen = FindExhaustingDirection(state);
                if (!chosen.HasValue)
                {
                    Fail("no legal move can exhaust the energy without contact");
                    return;
                }

                _originalSeed = flow.CurrentLevel.Seed;
                _originalPlayerConciencia = flow.CurrentLevel.PlayerConciencia;
                _originalZombieConciencia = flow.CurrentLevel.ZombieConciencia;
                _originalLevelIndex = flow.CurrentLevel.LevelIndex;

                EnergyField.SetValue(state, 1);
                MoveMethod.Invoke(turn, new object[] { chosen.Value });

                Require(state.Phase == GamePhase.EnvironmentalTransformation,
                    "phase after exhaustion=" + state.Phase);
                Require(renderer.IsTransforming,
                    "environmental transformation presentation did not start");

                Debug.Log("EXHAUSTION_TRANSFORM ok level=" + _originalLevelIndex +
                          " direction=" + chosen.Value +
                          " energy=" + state.CurrentEnergy + "/" + state.MaxEnergy +
                          " phase=" + state.Phase);
                _stage = 1;
                _phaseTime = 0f;
                return;
            }

            _phaseTime += Mathf.Min(Time.unscaledDeltaTime, 0.5f);

            if (!_sawDefeatOverlay && _phaseTime > 0.8f)
            {
                bool defeatPlaying = DefeatPlayingField != null &&
                                     (bool)DefeatPlayingField.GetValue(flow);
                Require(defeatPlaying, "defeat overlay did not start after the transformation");
                _sawDefeatOverlay = true;
                Debug.Log("EXHAUSTION_DEFEAT ok overlay started");
            }

            if (!_sawDefeatOverlay) return;

            bool restarted = turn.State != null &&
                             turn.State.TurnNumber == 0 &&
                             turn.State.Phase == GamePhase.HumanControlled &&
                             flow.CurrentLevel.Seed == _originalSeed;
            if (!restarted)
            {
                if (_phaseTime > 8f) Fail("level did not restart after the defeat");
                return;
            }

            Require(flow.CurrentLevel.PlayerConciencia == _originalPlayerConciencia,
                "restart changed player consciousness");
            Require(flow.CurrentLevel.ZombieConciencia == _originalZombieConciencia,
                "restart changed zombie consciousness");
            Require(turn.State.CurrentEnergy == turn.State.MaxEnergy,
                "restart did not refill the energy");
            var content = renderer.transform.Find("Level Content");
            Require(content != null &&
                    content.GetComponentsInChildren<SpriteRenderer>(true).Length > 10,
                "restart rendered no sprites");

            Debug.Log("EXHAUSTION_RESTART ok level=" + flow.CurrentLevel.LevelIndex +
                      " seed=" + flow.CurrentLevel.Seed +
                      " pc=" + flow.CurrentLevel.PlayerConciencia +
                      " zc=" + flow.CurrentLevel.ZombieConciencia +
                      " energy=" + turn.State.CurrentEnergy + "/" + turn.State.MaxEnergy);
            Debug.Log("EXHAUSTION_RESULT PASS");
            _done = true;
            EditorApplication.Exit(0);
        }

        private static Direction? FindExhaustingDirection(GameState state)
        {
            if (EnergyField == null) return null;
            var baseline = state.Clone();
            EnergyField.SetValue(baseline, 1);

            for (int i = 0; i < Directions.All.Length; i++)
            {
                var direction = Directions.All[i];
                var simulation = baseline.Clone();
                var result = simulation.TryManualMove(direction);
                if (result.Outcome == TurnOutcome.EnergyDefeat) return direction;
            }

            return null;
        }

        private void Require(bool condition, string message)
        {
            if (condition) return;
            Fail("assertion failed: " + message);
        }

        private void Fail(string message)
        {
            if (_done) return;
            _done = true;
            Debug.LogError("EXHAUSTION_RESULT FAIL " + message);
            EditorApplication.Exit(1);
        }
    }

    /// <summary>Batch entry point: -executeMethod Oopsmeinfecto.ExhaustionLauncher.RunExhaustionCheck</summary>
    [InitializeOnLoad]
    public static class ExhaustionLauncher
    {
        private const string PendingKey = "Oopsmeinfecto.ExhaustionPending";
        private static double _idleSince = -1.0;

        static ExhaustionLauncher()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            Debug.Log("EXHAUSTION_WAIT resumed after domain reload");
            EditorApplication.update += StartWhenReady;
        }

        public static void RunExhaustionCheck()
        {
            SessionState.SetBool(PendingKey, true);
            _idleSince = -1.0;
            EditorApplication.update -= StartWhenReady;
            EditorApplication.update += StartWhenReady;
            Debug.Log("EXHAUSTION_WAIT launch requested");
        }

        private static void StartWhenReady()
        {
            if (EditorApplication.isCompiling)
            {
                _idleSince = -1.0;
                return;
            }

            if (_idleSince < 0.0)
            {
                _idleSince = EditorApplication.timeSinceStartup;
                Debug.Log("EXHAUSTION_WAIT editor idle");
                return;
            }

            if (EditorApplication.timeSinceStartup - _idleSince < 45.0) return;

            EditorApplication.update -= StartWhenReady;
            SessionState.SetBool(PendingKey, false);
            Debug.Log("EXHAUSTION_START entering Play mode");
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            var probe = new GameObject("Exhaustion Probe");
            probe.AddComponent<ExhaustionPlayProbe>();
            EditorApplication.EnterPlaymode();
        }
    }
#endif
}
