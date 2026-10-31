using System;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Deterministic difficulty rules derived from the level number. Difficulty is expressed by
    /// the shape of the manual route (length, turns, direction variety, optional backtracking),
    /// not by making the map progressively larger.
    /// </summary>
    public sealed class DifficultyProfile
    {
        public int Tier { get; private set; }
        public int MinManualMoves { get; private set; }
        public int MaxManualMoves { get; private set; }
        public int MinTurns { get; private set; }
        public int MinDistinctDirections { get; private set; }
        public int MaxStraightRun { get; private set; }
        public int MaxImmediateReversals { get; private set; }
        public int MaxRevisitedCells { get; private set; }
        public int MinInitialOptions { get; private set; }
        public int PreferredDistanceMin { get; private set; }
        public int PreferredDistanceMax { get; private set; }
        public bool AllowsBacktracking { get; private set; }
        public double BacktrackChance { get; private set; }

        private DifficultyProfile()
        {
        }

        public static int TierForLevel(int levelIndex)
        {
            if (levelIndex <= 1) return 1;
            if (levelIndex == 2) return 2;
            if (levelIndex <= 4) return 3;
            if (levelIndex <= 8) return 4;
            return 5;
        }

        public static DifficultyProfile ForLevel(int levelIndex)
        {
            int level = Math.Max(1, levelIndex);
            int tier = TierForLevel(level);

            switch (tier)
            {
                case 1:
                    return new DifficultyProfile
                    {
                        Tier = 1,
                        MinManualMoves = 3,
                        MaxManualMoves = 4,
                        MinTurns = 1,
                        MinDistinctDirections = 2,
                        MaxStraightRun = 3,
                        MaxImmediateReversals = 0,
                        MaxRevisitedCells = 0,
                        MinInitialOptions = 2,
                        PreferredDistanceMin = 2,
                        PreferredDistanceMax = 4,
                        AllowsBacktracking = false,
                        BacktrackChance = 0.0
                    };

                case 2:
                    return new DifficultyProfile
                    {
                        Tier = 2,
                        MinManualMoves = 3,
                        MaxManualMoves = 5,
                        MinTurns = 1,
                        MinDistinctDirections = 2,
                        MaxStraightRun = 4,
                        MaxImmediateReversals = 0,
                        MaxRevisitedCells = 0,
                        MinInitialOptions = 2,
                        PreferredDistanceMin = 2,
                        PreferredDistanceMax = 5,
                        AllowsBacktracking = false,
                        BacktrackChance = 0.0
                    };

                case 3:
                    return new DifficultyProfile
                    {
                        Tier = 3,
                        MinManualMoves = 4,
                        MaxManualMoves = 6,
                        MinTurns = 2,
                        MinDistinctDirections = 2,
                        MaxStraightRun = 4,
                        MaxImmediateReversals = 1,
                        MaxRevisitedCells = 1,
                        MinInitialOptions = 2,
                        PreferredDistanceMin = 2,
                        PreferredDistanceMax = 5,
                        AllowsBacktracking = true,
                        BacktrackChance = 0.15
                    };

                case 4:
                    return new DifficultyProfile
                    {
                        Tier = 4,
                        MinManualMoves = 4,
                        MaxManualMoves = 7,
                        MinTurns = 2,
                        MinDistinctDirections = 3,
                        MaxStraightRun = 4,
                        MaxImmediateReversals = 2,
                        MaxRevisitedCells = 2,
                        MinInitialOptions = 2,
                        PreferredDistanceMin = 2,
                        PreferredDistanceMax = 6,
                        AllowsBacktracking = true,
                        BacktrackChance = 0.30
                    };

                default:
                    return new DifficultyProfile
                    {
                        Tier = 5,
                        MinManualMoves = 5,
                        MaxManualMoves = 8,
                        MinTurns = 3,
                        MinDistinctDirections = 3,
                        MaxStraightRun = 5,
                        MaxImmediateReversals = 3,
                        MaxRevisitedCells = 3,
                        MinInitialOptions = 2,
                        PreferredDistanceMin = 3,
                        PreferredDistanceMax = 7,
                        AllowsBacktracking = true,
                        BacktrackChance = 0.50
                    };
            }
        }

        public int ClampMaxMoves(int energy)
        {
            return Math.Max(MinManualMoves, Math.Min(MaxManualMoves, energy));
        }

        public bool Accepts(SolutionPlan plan, int maxEnergy, int zombieConciencia, int playerConciencia)
        {
            if (plan == null) return false;
            int length = plan.ManualDirections.Count;
            if (length < MinManualMoves || length > MaxManualMoves) return false;
            if (length > maxEnergy) return false;
            if (plan.ManualTurnCount < MinTurns) return false;
            if (plan.DistinctDirections < MinDistinctDirections) return false;
            if (plan.MaxStraightRun > MaxStraightRun) return false;
            if (plan.ImmediateReversals > MaxImmediateReversals) return false;
            if (plan.RevisitedCells > MaxRevisitedCells) return false;
            if (plan.InitialOptions < MinInitialOptions) return false;
            if (plan.Sequence.Count != zombieConciencia + playerConciencia) return false;
            if (plan.AutomaticSteps < zombieConciencia + 2) return false;
            if (plan.SwitchPosition == plan.ContactPosition) return false;
            return true;
        }

        public int Score(SolutionPlan plan, int maxEnergy)
        {
            if (plan == null) return int.MinValue;

            int score = 100;
            score += plan.ManualDirections.Count * 7;
            score += plan.ManualTurnCount * 13;
            score += plan.DistinctDirections * 5;
            score += plan.AutomaticSteps * 2;

            if (plan.ImmediateReversals > 0 && AllowsBacktracking) score += 18;
            if (plan.RevisitedCells > 0 && AllowsBacktracking) score += 10;

            int distance = plan.InitialDistance;
            if (distance < PreferredDistanceMin) score -= (PreferredDistanceMin - distance) * 8;
            else if (distance > PreferredDistanceMax) score -= (distance - PreferredDistanceMax) * 4;
            else score += 12;

            if (plan.ManualDirections.Count == maxEnergy) score += 4;
            score += Math.Min(12, plan.SwitchDistance) * 2;
            return score;
        }

        public override string ToString()
        {
            return "tier " + Tier + " moves " + MinManualMoves + ".." + MaxManualMoves +
                   " turns>=" + MinTurns + " distinct>=" + MinDistinctDirections;
        }
    }
}
