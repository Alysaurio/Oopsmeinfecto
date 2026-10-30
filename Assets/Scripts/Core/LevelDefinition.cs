using System;
using System.Collections.Generic;

namespace Oopsmeinfecto
{
    [Serializable]
    public sealed class GenerationSettings
    {
        public int baseSeed = 12345;

        public int minWidth = 6;
        public int maxWidth = 16;
        public int minHeight = 6;
        public int maxHeight = 16;

        public int minZombieConciencia = 2;
        public int maxZombieConciencia = 4;
        public int playerConciencia = 3;
        public int historyCapacity = 7;

        public int minEnergy = 5;
        public int maxEnergy = 10;

        public int maxLateralExpansion = 4;
        public double expansionDensity = 0.5;
        public int maxHazardsCap = 5;

        public int maxGenerationAttempts = 100;
        public int maxPlannerStates = 200000;
        public int maxValidatorStates = 180000;
        public bool requireUniqueSolution = true;

        public int maxPlanCandidates = 12;

        public GenerationSettings Clone()
        {
            return (GenerationSettings)MemberwiseClone();
        }

        public void Sanitize()
        {
            minWidth = Math.Max(6, minWidth);
            maxWidth = Math.Max(minWidth, Math.Min(16, maxWidth));
            minHeight = Math.Max(6, minHeight);
            maxHeight = Math.Max(minHeight, Math.Min(16, maxHeight));
            minZombieConciencia = Math.Max(1, minZombieConciencia);
            maxZombieConciencia = Math.Max(minZombieConciencia, maxZombieConciencia);
            playerConciencia = Math.Max(0, playerConciencia);
            historyCapacity = Math.Max(7, historyCapacity);
            minEnergy = Math.Max(1, minEnergy);
            maxEnergy = Math.Max(minEnergy, maxEnergy);
            maxLateralExpansion = Math.Max(0, Math.Min(8, maxLateralExpansion));
            expansionDensity = Math.Max(0.05, Math.Min(1.0, expansionDensity));
            maxHazardsCap = Math.Max(1, Math.Min(5, maxHazardsCap));
            maxGenerationAttempts = Math.Max(1, maxGenerationAttempts);
            maxPlannerStates = Math.Max(1000, maxPlannerStates);
            maxValidatorStates = Math.Max(1000, maxValidatorStates);
            maxPlanCandidates = Math.Max(1, maxPlanCandidates);
        }
    }

    public sealed class SolutionPlan
    {
        public List<Direction> ManualDirections { get; } = new List<Direction>();
        public List<GridPos> ManualPositions { get; } = new List<GridPos>();
        public List<Direction> Sequence { get; } = new List<Direction>();
        public List<GridPos> AutoPositions { get; } = new List<GridPos>();
        public GridPos ContactPosition { get; set; }
        public GridPos SwitchPosition { get; set; }
        public int ContactTurn { get; set; }
        public int Score { get; set; }
        public int ZombiePhaseAtContact { get; set; }

        public SolutionPlan Clone()
        {
            var clone = new SolutionPlan
            {
                ContactPosition = ContactPosition,
                SwitchPosition = SwitchPosition,
                ContactTurn = ContactTurn,
                Score = Score,
                ZombiePhaseAtContact = ZombiePhaseAtContact
            };
            clone.ManualDirections.AddRange(ManualDirections);
            clone.ManualPositions.AddRange(ManualPositions);
            clone.Sequence.AddRange(Sequence);
            clone.AutoPositions.AddRange(AutoPositions);
            return clone;
        }
    }

    public sealed class LevelDefinition
    {
        public int Seed;
        public int LevelIndex;
        public int Width;
        public int Height;
        public BoardData Board;

        public GridPos PlayerStart;
        public GridPos ZombieStart;
        public GridPos SwitchPosition;

        public GridPos[] NpcRoute;
        public int ZombiePhaseStart;
        public int ZombieConciencia;
        public int PlayerConciencia;
        public int MaxEnergy;
        public int HistoryCapacity;

        public Direction[] PlannedManualDirections;
        public GridPos[] PlannedManualPositions;
        public Direction[] PlannedSequence;
        public GridPos[] PlannedAutoPositions;

        public GridPos[] HazardPositions;

        public string PatternName;
        public int PatternRotation;
        public bool PatternMirrorH;
        public bool PatternMirrorV;
        public int PatternPhase;
        public GridPos PatternOrigin;

        public bool UniqueConfirmed;
        public int SolutionCount;
        public int GenerationAttemptsUsed;
        public bool UsedFallback;
        public string GenerationMessage;

        public GameState CreateGameState()
        {
            return new GameState(
                Board,
                PlayerStart,
                NpcRoute,
                ZombieConciencia,
                PlayerConciencia,
                MaxEnergy,
                HistoryCapacity,
                ZombiePhaseStart);
        }

        public HashSet<GridPos> GetMandatoryPositions()
        {
            var mandatory = new HashSet<GridPos>();
            if (PlannedManualPositions != null)
            {
                for (int i = 0; i < PlannedManualPositions.Length; i++) mandatory.Add(PlannedManualPositions[i]);
            }
            if (NpcRoute != null)
            {
                for (int i = 0; i < NpcRoute.Length; i++) mandatory.Add(NpcRoute[i]);
            }
            if (PlannedAutoPositions != null)
            {
                for (int i = 0; i < PlannedAutoPositions.Length; i++) mandatory.Add(PlannedAutoPositions[i]);
            }
            mandatory.Add(PlayerStart);
            mandatory.Add(ZombieStart);
            mandatory.Add(SwitchPosition);
            return mandatory;
        }

        public HashSet<GridPos> GetRoutePositions()
        {
            var result = new HashSet<GridPos>();
            if (NpcRoute != null)
            {
                for (int i = 0; i < NpcRoute.Length; i++) result.Add(NpcRoute[i]);
            }
            return result;
        }
    }

    public sealed class ValidationReport
    {
        public bool Valid;
        public bool UniqueConfirmed;
        public int SolutionCount;
        public int StatesExplored;
        public string Message = "";
        public readonly List<string> Errors = new List<string>();

        public void AddError(string error)
        {
            Errors.Add(error);
            Valid = false;
        }

        public override string ToString()
        {
            if (Errors.Count == 0) return Message;
            return Message + " | " + string.Join("; ", Errors);
        }
    }

    public sealed class GenerationResult
    {
        public LevelDefinition Level;
        public bool Success;
        public bool UniqueConfirmed;
        public int Attempts;
        public string Message = "";
    }
}
