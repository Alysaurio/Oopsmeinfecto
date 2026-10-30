#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Oopsmeinfecto.EditorTools
{
    /// <summary>
    /// Editor-only smoke test. It loads the real tuning asset, generates levels with the same
    /// settings used by the game and re-validates them, including uniqueness.
    /// </summary>
    public static class ProjectValidation
    {
        private const string TuningPath = "Assets/Resources/GameTuning.asset";
        private const int SeedCount = 10;

        [MenuItem("Oopsmeinfecto/Validate generation (10 seeds)")]
        public static void ValidateFromMenu()
        {
            Run(false);
        }

        /// <summary>Entry point for Unity batchmode: -executeMethod Oopsmeinfecto.EditorTools.ProjectValidation.RunBatchValidation</summary>
        public static void RunBatchValidation()
        {
            Run(true);
        }

        private static void Run(bool batch)
        {
            int valid = 0;
            int unique = 0;
            int fallback = 0;

            try
            {
                var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(TuningPath);
                if (tuning == null)
                {
                    throw new InvalidOperationException("GameTuning.asset was not found at " + TuningPath);
                }

                var settings = tuning.ToGenerationSettings();
                for (int i = 0; i < SeedCount; i++)
                {
                    int seed = 7000 + i;
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var result = GeneradorNivel.Generate(1, seed, settings);
                    stopwatch.Stop();

                    if (result == null || result.Level == null)
                    {
                        Debug.LogError("UNITY_VALIDATION seed " + seed + ": no level returned");
                        continue;
                    }

                    var report = ValidadorNivel.Validate(result.Level, settings, true);
                    if (!report.Valid)
                    {
                        Debug.LogError("UNITY_VALIDATION seed " + seed + ": invalid level: " + report);
                        continue;
                    }

                    valid++;
                    if (report.SolutionCount == 1) unique++;
                    if (result.Level.UsedFallback) fallback++;

                    Debug.Log("UNITY_VALIDATION seed " + seed +
                              " size=" + result.Level.Width + "x" + result.Level.Height +
                              " unique=" + report.SolutionCount +
                              " fallback=" + result.Level.UsedFallback +
                              " ms=" + stopwatch.ElapsedMilliseconds);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            Debug.Log("UNITY_VALIDATION_RESULT valid=" + valid + "/" + SeedCount +
                      " unique=" + unique + "/" + SeedCount +
                      " fallback=" + fallback);

            if (batch)
            {
                EditorApplication.Exit(valid == SeedCount && unique == SeedCount && fallback == 0 ? 0 : 1);
            }
        }
    }
}
#endif
