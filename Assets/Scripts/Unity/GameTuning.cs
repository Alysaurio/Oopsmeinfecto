using UnityEngine;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Inspector-editable tuning for generation, timing and provisional pixel-art colors.
    /// The runtime loads Assets/Resources/GameTuning.asset when no explicit reference is set.
    /// </summary>
    [CreateAssetMenu(menuName = "Oopsmeinfecto/Game Tuning", fileName = "GameTuning")]
    public sealed class GameTuning : ScriptableObject
    {
        [Header("Generation")]
        public bool useFixedSeed = false;
        public int fixedSeed = 12345;
        public int minWidth = 6;
        public int maxWidth = 16;
        public int minHeight = 6;
        public int maxHeight = 16;
        public int minZombieConciencia = 2;
        public int maxZombieConciencia = 5;
        public int minPlayerConciencia = 2;
        public int maxPlayerConciencia = 5;
        public int historyCapacity = 7;
        public int minEnergy = 5;
        public int maxEnergy = 10;
        public int maxLateralExpansion = 4;
        [Range(0.05f, 1f)] public float expansionDensity = 0.5f;
        public int maxHazardsCap = 5;
        public int maxGenerationAttempts = 100;
        public int maxPlannerStates = 120000;
        public int maxValidatorStates = 120000;
        public bool requireUniqueSolution = true;
        public int maxPlanCandidates = 12;

        [Header("Timing")]
        [Tooltip("Delay before a held direction starts repeating. A short tap always performs one move.")]
        public float initialRepeatDelay = 0.30f;
        public float repeatInterval = 0.20f;
        public float autoStepInterval = 0.18f;
        [Tooltip("Extra pause added to every inherited-playback step so it reads as a different rhythm than manual input.")]
        [Range(0.20f, 0.30f)] public float playbackExtraDelay = 0.25f;
        [Tooltip("Environmental transformation pause before the defeat transition/restart.")]
        [Range(0.2f, 2f)] public float transformationDuration = 0.5f;
        [Range(0.1f, 1f)] public float barDrainDuration = 0.5f;
        [Range(1f, 2f)] public float defeatDuration = 1.5f;

        [Header("Camera / Layout")]
        public float cameraMargin = 1.0f;
        [Range(0.10f, 0.30f)] public float hudViewportFraction = 0.18f;

        [Header("Ambient infection VFX")]
        public int ambientParticleCount = 28;
        public float ambientParticleSize = 0.11f;
        public float ambientRadius = 0.85f;
        public float ambientEmissionRate = 26f;
        public float ambientParticleLifetime = 0.9f;
        public float ambientParticleRise = 0.45f;
        public Color ambientParticleColor = new Color(0.45f, 1.00f, 0.55f, 1f);
        public Color ambientParticleHotColor = new Color(0.85f, 1.00f, 0.25f, 1f);
        public Color transformationTint = new Color(0.55f, 0.95f, 0.45f, 1f);

        [Header("Zombie facing (optional overrides)")]
        public Sprite zombieFacingUp;
        public Sprite zombieFacingUpRight;
        public Sprite zombieFacingRight;
        public Sprite zombieFacingDownRight;
        public Sprite zombieFacingDown;
        public Sprite zombieFacingDownLeft;
        public Sprite zombieFacingLeft;
        public Sprite zombieFacingUpLeft;

        [Header("Patrol ground (optional overrides)")]
        public Sprite patrolGroundSpriteA;
        public Sprite patrolGroundSpriteB;
        public Color patrolGroundColorA = new Color(0.28f, 0.45f, 0.58f, 1f);
        public Color patrolGroundColorB = new Color(0.22f, 0.38f, 0.50f, 1f);
        [Range(0f, 1f)] public float patrolGroundAlpha = 0.45f;

        [Header("Tileset Config (optional override)")]
        public LevelTilesetConfig tilesetConfig;

        [Header("Character Sprites Config (optional override)")]
        public CharacterSpritesConfig characterSpritesConfig;

        [Header("Colors")]
        public Color floorColorA = new Color(0.20f, 0.22f, 0.25f, 1f);
        public Color floorColorB = new Color(0.17f, 0.19f, 0.22f, 1f);
        public Color wallColor = new Color(0.07f, 0.08f, 0.10f, 1f);
        public Color damageColor = new Color(0.20f, 0.78f, 0.28f, 1f);
        public Color switchColor = new Color(0.25f, 0.95f, 0.85f, 1f);
        public Color cameraBackground = new Color(0.02f, 0.025f, 0.03f, 1f);

        public Color energyFilledColor = new Color(0.35f, 0.95f, 0.55f, 1f);
        public Color energyEmptyColor = new Color(0.12f, 0.14f, 0.16f, 1f);
        public Color energyBackgroundColor = new Color(0.03f, 0.04f, 0.05f, 1f);
        public Color energyBorderColor = new Color(0.75f, 0.85f, 0.90f, 1f);
        public Color infectedDesaturation = new Color(0.55f, 0.55f, 0.55f, 1f);

        public Color hudPanelColor = new Color(0.05f, 0.06f, 0.08f, 0.94f);
        public Color hudBorderColor = new Color(0.55f, 0.65f, 0.70f, 1f);
        public Color hudTextColor = new Color(0.92f, 0.95f, 0.98f, 1f);
        public Color historySlotColor = new Color(0.08f, 0.09f, 0.11f, 1f);
        public Color playerHistoryColor = new Color(0.35f, 0.80f, 1.00f, 1f);
        public Color zombieHistoryColor = new Color(0.55f, 1.00f, 0.35f, 1f);
        public Color playerHistoryHighlightColor = new Color(0.30f, 0.65f, 1.00f, 1f);
        public Color zombieHistoryHighlightColor = new Color(0.35f, 1.00f, 0.20f, 1f);
        [Range(0f, 1f)] public float historyDimmedAlpha = 0.42f;
        public Color defeatOverlayColor = new Color(0f, 0f, 0f, 1f);

        public GenerationSettings ToGenerationSettings()
        {
            return new GenerationSettings
            {
                baseSeed = fixedSeed,
                minWidth = minWidth,
                maxWidth = maxWidth,
                minHeight = minHeight,
                maxHeight = maxHeight,
                minZombieConciencia = minZombieConciencia,
                maxZombieConciencia = maxZombieConciencia,
                minPlayerConciencia = minPlayerConciencia,
                maxPlayerConciencia = maxPlayerConciencia,
                historyCapacity = historyCapacity,
                minEnergy = minEnergy,
                maxEnergy = maxEnergy,
                maxLateralExpansion = maxLateralExpansion,
                expansionDensity = expansionDensity,
                maxHazardsCap = maxHazardsCap,
                maxGenerationAttempts = maxGenerationAttempts,
                maxPlannerStates = maxPlannerStates,
                maxValidatorStates = maxValidatorStates,
                requireUniqueSolution = requireUniqueSolution,
                maxPlanCandidates = maxPlanCandidates
            };
        }
    }
}
