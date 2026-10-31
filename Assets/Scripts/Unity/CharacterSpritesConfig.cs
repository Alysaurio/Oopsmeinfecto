using System;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Oopsmeinfecto
{
    /// <summary>
    /// Holds references to the sliced pixel art frames for player and zombie.
    /// Loaded at runtime from Resources/CharacterSpritesConfig or configured via GameTuning.
    /// Includes runtime auto-recovery to load original PNG slices from Assets/Sprites
    /// if serialized references are unassigned or failed to deserialize.
    /// </summary>
    [CreateAssetMenu(menuName = "Oopsmeinfecto/Character Sprites Config", fileName = "CharacterSpritesConfig")]
    public sealed class CharacterSpritesConfig : ScriptableObject
    {
        [Header("Animator Controllers")]
        public RuntimeAnimatorController playerAnimatorController;
        public RuntimeAnimatorController zombieAnimatorController;

        [Header("Player - Idle (4 frames)")]
        public Sprite[] playerIdle = new Sprite[4];

        [Header("Player - Jump (6 frames)")]
        public Sprite[] playerJump = new Sprite[6];

        [Header("Player - Infected (4 frames)")]
        public Sprite[] playerInfected = new Sprite[4];

        [Header("Player - Limit Time (1 frame)")]
        public Sprite playerLimitTime;

        [Header("Zombie - 8 Directions (4 frames each)")]
        public Sprite[] zombieUp = new Sprite[4];
        public Sprite[] zombieUpRight = new Sprite[4];
        public Sprite[] zombieRight = new Sprite[4];
        public Sprite[] zombieDownRight = new Sprite[4];
        public Sprite[] zombieDown = new Sprite[4];
        public Sprite[] zombieDownLeft = new Sprite[4];
        public Sprite[] zombieLeft = new Sprite[4];
        public Sprite[] zombieUpLeft = new Sprite[4];

        public Sprite[] GetZombieDirectionSprites(NpcFacing facing)
        {
            switch (facing)
            {
                case NpcFacing.Up: return zombieUp;
                case NpcFacing.UpRight: return zombieUpRight;
                case NpcFacing.Right: return zombieRight;
                case NpcFacing.DownRight: return zombieDownRight;
                case NpcFacing.Down: return zombieDown;
                case NpcFacing.DownLeft: return zombieDownLeft;
                case NpcFacing.Left: return zombieLeft;
                default: return zombieUpLeft;
            }
        }

        /// <summary>
        /// Guarantees all character sprites and controllers are populated with the original
        /// 16x16 pixel art slices, recovering from asset database or PNG files on disk.
        /// </summary>
        public void EnsureLoaded()
        {
            EnsureControllers();

            bool needsPlayer = IsArrayIncomplete(playerIdle, 4) ||
                               IsArrayIncomplete(playerJump, 6) ||
                               IsArrayIncomplete(playerInfected, 4) ||
                               playerLimitTime == null;

            bool needsZombie = IsArrayIncomplete(zombieUp, 4) ||
                               IsArrayIncomplete(zombieUpRight, 4) ||
                               IsArrayIncomplete(zombieRight, 4) ||
                               IsArrayIncomplete(zombieDownRight, 4) ||
                               IsArrayIncomplete(zombieDown, 4) ||
                               IsArrayIncomplete(zombieDownLeft, 4) ||
                               IsArrayIncomplete(zombieLeft, 4) ||
                               IsArrayIncomplete(zombieUpLeft, 4);

            if (needsPlayer)
            {
                LoadPlayerSprites();
            }

            if (needsZombie)
            {
                LoadZombieSprites();
            }
        }

        private void EnsureControllers()
        {
            if (playerAnimatorController == null)
            {
                playerAnimatorController = Resources.Load<RuntimeAnimatorController>("PlayerAnimatorController");
#if UNITY_EDITOR
                if (playerAnimatorController == null)
                {
                    playerAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                        "Assets/Oopsmeinfecto/Animations/Player/PlayerAnimatorController.controller");
                }
#endif
            }

            if (zombieAnimatorController == null)
            {
                zombieAnimatorController = Resources.Load<RuntimeAnimatorController>("ZombieAnimatorController");
#if UNITY_EDITOR
                if (zombieAnimatorController == null)
                {
                    zombieAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                        "Assets/Oopsmeinfecto/Animations/Zombi/ZombieAnimatorController.controller");
                }
#endif
            }
        }

        private static bool IsArrayIncomplete(Sprite[] array, int expectedLength)
        {
            if (array == null || array.Length < expectedLength) return true;
            for (int i = 0; i < expectedLength; i++)
            {
                if (array[i] == null) return true;
            }
            return false;
        }

        private void LoadPlayerSprites()
        {
            if (IsArrayIncomplete(playerIdle, 4))
                playerIdle = LoadOrSliceSpriteSheet("Assets/Sprites/Player/PlayerIdle.png", 4);

            if (IsArrayIncomplete(playerJump, 6))
                playerJump = LoadOrSliceSpriteSheet("Assets/Sprites/Player/PlayerJump.png", 6);

            if (IsArrayIncomplete(playerInfected, 4))
                playerInfected = LoadOrSliceSpriteSheet("Assets/Sprites/Player/PlayerInfected.png", 4);

            if (playerLimitTime == null)
            {
                var single = LoadOrSliceSpriteSheet("Assets/Sprites/Player/PlayerLimitTime.png", 1);
                if (single != null && single.Length > 0) playerLimitTime = single[0];
            }
        }

        private void LoadZombieSprites()
        {
            if (IsArrayIncomplete(zombieUp, 4))
                zombieUp = LoadOrSliceSpriteSheet("Assets/Sprites/Zombi/ZUp.png", 4);
            if (IsArrayIncomplete(zombieUpRight, 4))
                zombieUpRight = LoadOrSliceSpriteSheet("Assets/Sprites/Zombi/ZRightUp.png", 4);
            if (IsArrayIncomplete(zombieRight, 4))
                zombieRight = LoadOrSliceSpriteSheet("Assets/Sprites/Zombi/ZRight.png", 4);
            if (IsArrayIncomplete(zombieDownRight, 4))
                zombieDownRight = LoadOrSliceSpriteSheet("Assets/Sprites/Zombi/ZRightDown.png", 4);
            if (IsArrayIncomplete(zombieDown, 4))
                zombieDown = LoadOrSliceSpriteSheet("Assets/Sprites/Zombi/ZDown.png", 4);
            if (IsArrayIncomplete(zombieDownLeft, 4))
                zombieDownLeft = LoadOrSliceSpriteSheet("Assets/Sprites/Zombi/ZLeftDown.png", 4);
            if (IsArrayIncomplete(zombieLeft, 4))
                zombieLeft = LoadOrSliceSpriteSheet("Assets/Sprites/Zombi/ZLeft.png", 4);
            if (IsArrayIncomplete(zombieUpLeft, 4))
                zombieUpLeft = LoadOrSliceSpriteSheet("Assets/Sprites/Zombi/ZLeftUp.png", 4);
        }

        private static Sprite[] LoadOrSliceSpriteSheet(string relativePath, int frameCount)
        {
#if UNITY_EDITOR
            var editorAssets = AssetDatabase.LoadAllAssetsAtPath(relativePath);
            if (editorAssets != null && editorAssets.Length > 1)
            {
                var list = new System.Collections.Generic.List<Sprite>();
                foreach (var obj in editorAssets)
                {
                    if (obj is Sprite s) list.Add(s);
                }
                if (list.Count >= frameCount)
                {
                    list.Sort((a, b) => a.rect.x.CompareTo(b.rect.x));
                    return list.GetRange(0, frameCount).ToArray();
                }
            }
#endif
            // Standalone runtime or fallback: load PNG bytes from disk and slice 16x16
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), relativePath);
            if (!File.Exists(fullPath))
            {
                fullPath = Path.Combine(Application.dataPath, relativePath.Replace("Assets/", ""));
            }

            if (File.Exists(fullPath))
            {
                byte[] pngBytes = File.ReadAllBytes(fullPath);
                var tex = new Texture2D(frameCount * 16, 16, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    anisoLevel = 0,
                    hideFlags = HideFlags.DontSave
                };
                if (tex.LoadImage(pngBytes))
                {
                    tex.filterMode = FilterMode.Point;
                    var result = new Sprite[frameCount];
                    for (int i = 0; i < frameCount; i++)
                    {
                        var rect = new Rect(i * 16f, 0f, 16f, 16f);
                        var sprite = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 16f);
                        sprite.name = Path.GetFileNameWithoutExtension(relativePath) + "_" + i;
                        sprite.hideFlags = HideFlags.DontSave;
                        result[i] = sprite;
                    }
                    return result;
                }
            }

            return new Sprite[frameCount];
        }
    }
}
