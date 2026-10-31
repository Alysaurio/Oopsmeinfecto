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
            int tuningErrors = 0;

            try
            {
                var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(TuningPath);
                if (tuning == null)
                {
                    throw new InvalidOperationException("GameTuning.asset was not found at " + TuningPath);
                }

                tuningErrors = ValidateTuning(tuning);
                var settings = tuning.ToGenerationSettings();
                for (int i = 0; i < SeedCount; i++)
                {
                    int levelIndex = i + 1;
                    int seed = SeedUtility.DeriveLevelSeed(4242 + i * 97, levelIndex);
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    var result = GeneradorNivel.Generate(levelIndex, seed, settings);
                    stopwatch.Stop();

                    if (result == null || result.Level == null)
                    {
                        Debug.LogError("UNITY_VALIDATION seed " + seed + ": no level returned");
                        continue;
                    }

                    var level = result.Level;
                    var report = ValidadorNivel.Validate(level, settings, true);
                    if (!report.Valid)
                    {
                        Debug.LogError("UNITY_VALIDATION seed " + seed + ": invalid level: " + report);
                        continue;
                    }

                    if (level.Seed != seed)
                    {
                        Debug.LogError("UNITY_VALIDATION seed " + seed + ": level seed changed to " + level.Seed);
                        continue;
                    }

                    if (level.PlayerConciencia < 2 || level.PlayerConciencia > 5 ||
                        level.ZombieConciencia < 2 || level.ZombieConciencia > 5)
                    {
                        Debug.LogError("UNITY_VALIDATION seed " + seed + ": invalid consciousness " +
                                       level.PlayerConciencia + "/" + level.ZombieConciencia);
                        continue;
                    }

                    if (level.PlannedSequence == null ||
                        level.PlannedSequence.Length != 3 + level.PlayerConciencia)
                    {
                        Debug.LogError("UNITY_VALIDATION seed " + seed + ": wrong sequence length");
                        continue;
                    }

                    if (level.PlannedAutoPositions == null ||
                        level.PlannedAutoPositions.Length - 1 != level.PlannedSequence.Length ||
                        level.PlannedAutoPositions.Length - 1 < level.ZombieConciencia + 2)
                    {
                        Debug.LogError("UNITY_VALIDATION seed " + seed + ": wrong automatic trajectory");
                        continue;
                    }

                    if (level.MaxEnergy < 5 || level.MaxEnergy > 10)
                    {
                        Debug.LogError("UNITY_VALIDATION seed " + seed + ": energy " + level.MaxEnergy);
                        continue;
                    }

                    if (!level.Bounds.IsValid || level.Bounds.MinX < 0 || level.Bounds.MinY < 0 ||
                        level.Bounds.MaxX >= level.Width || level.Bounds.MaxY >= level.Height)
                    {
                        Debug.LogError("UNITY_VALIDATION seed " + seed + ": invalid playable bounds");
                        continue;
                    }

                    valid++;
                    if (report.SolutionCount == 1) unique++;
                    if (level.UsedFallback) fallback++;

                    Debug.Log("UNITY_VALIDATION level " + levelIndex + " seed " + seed +
                              " size=" + level.Width + "x" + level.Height +
                              " bounds=" + level.Bounds.Width + "x" + level.Bounds.Height +
                              " energy=" + level.MaxEnergy +
                              " pc=" + level.PlayerConciencia +
                              " zc=" + level.ZombieConciencia +
                              " tier=" + level.DifficultyTier +
                              " hazards=" + (level.HazardPositions == null ? 0 : level.HazardPositions.Length) +
                              " unique=" + report.SolutionCount +
                              " fallback=" + level.UsedFallback +
                              " ms=" + stopwatch.ElapsedMilliseconds);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            Debug.Log("UNITY_VALIDATION_RESULT valid=" + valid + "/" + SeedCount +
                      " unique=" + unique + "/" + SeedCount +
                      " fallback=" + fallback +
                      " tuningErrors=" + tuningErrors);

            if (batch)
            {
                EditorApplication.Exit(
                    tuningErrors == 0 && valid == SeedCount && unique == SeedCount && fallback == 0 ? 0 : 1);
            }
        }

        private static int ValidateTuning(GameTuning tuning)
        {
            int errors = 0;

            void Fail(string message)
            {
                errors++;
                Debug.LogError("UNITY_TUNING " + message);
            }

            if (tuning.minPlayerConciencia < 2 || tuning.maxPlayerConciencia > 5 ||
                tuning.minPlayerConciencia > tuning.maxPlayerConciencia)
                Fail("player consciousness range must be inside 2..5");

            if (tuning.minZombieConciencia < 2 || tuning.maxZombieConciencia > 5 ||
                tuning.minZombieConciencia > tuning.maxZombieConciencia)
                Fail("zombie consciousness range must be inside 2..5");

            if (tuning.initialRepeatDelay < 0.29f || tuning.initialRepeatDelay > 0.31f)
                Fail("initialRepeatDelay should be 0.30 s");

            if (tuning.repeatInterval < 0.19f || tuning.repeatInterval > 0.21f)
                Fail("repeatInterval should be 0.20 s");

            if (tuning.playbackExtraDelay < 0.20f || tuning.playbackExtraDelay > 0.30f)
                Fail("playbackExtraDelay should be inside 0.20..0.30 s");

            if (tuning.transformationDuration <= 0f)
                Fail("transformationDuration must be positive");

            if (tuning.ambientParticleCount < 1)
                Fail("ambientParticleCount must be positive");

            if (tuning.historyCapacity < 7)
                Fail("historyCapacity must be at least 7");

            return errors;
        }
    }
}
#endif
