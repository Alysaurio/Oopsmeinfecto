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
        private LevelDefinition _level;
        private int _expectedNextIndex;
        private bool _sawContact;
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
                if (_flow != null) _turn = _flow.GetComponent<TurnManager>();
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
            Require(level.PlayerConciencia == 3, "playerConciencia=" + level.PlayerConciencia);
            Require(level.ZombieConciencia >= 2 && level.ZombieConciencia <= 4,
                "zombieConciencia=" + level.ZombieConciencia);
            Require(level.MaxEnergy >= 5 && level.MaxEnergy <= 10, "maxEnergy=" + level.MaxEnergy);
            Require(level.HazardPositions != null && level.HazardPositions.Length >= 1,
                "hazards=" + (level.HazardPositions == null ? 0 : level.HazardPositions.Length));
            Require(level.UniqueConfirmed, "uniqueConfirmed=false");
            Require(state.TurnNumber == 0, "turn=" + state.TurnNumber);
            Require(state.Player.Position.Equals(level.PlayerStart), "player start mismatch");
            Require(state.Zombie.Position.Equals(level.ZombieStart), "zombie start mismatch");

            var renderer = _flow.GetComponent<LevelRenderer>();
            Require(renderer != null && renderer.transform.childCount > 0, "renderer has no children");

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
                      " energy=" + state.CurrentEnergy + "/" + state.MaxEnergy +
                      " zombieConciencia=" + level.ZombieConciencia +
                      " hazards=" + level.HazardPositions.Length +
                      " manualMoves=" + level.PlannedManualDirections.Length +
                      " renderChildren=" + renderer.transform.childCount);
        }

        private void VerifyNextLevel(int index)
        {
            var next = (LevelDefinition)FlowLevelField.GetValue(_flow);
            Require(next != null, "next level is null");
            Require(next.LevelIndex == index, "level index=" + next.LevelIndex + " expected=" + index);
            Require(next.Seed != _level.Seed, "next level reused the previous seed");
            Require(next.PlannedManualDirections != null && next.PlannedManualDirections.Length > 0,
                "next level has no planned solution");
            Require(_sawContact, "contact/playback was never observed");

            Debug.Log("SMOKE_VICTORY ok fromLevel=" + _level.LevelIndex + " toLevel=" + next.LevelIndex +
                      " movesApplied=" + _moveIndex + " newSeed=" + next.Seed);
            Debug.Log("SMOKE_RESULT PASS");
            _finished = true;
#if UNITY_EDITOR
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
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
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
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
            if (!Application.isBatchMode)
            {
                SessionState.SetBool(PendingKey, false);
                return;
            }
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
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(2);
            }
        }
    }
#endif
}
