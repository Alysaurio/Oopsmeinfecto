#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Oopsmeinfecto.EditorTools
{
    public static class SpriteDiagnostic
    {
        [InitializeOnLoadMethod]
        public static void RunDiagnostic()
        {
            var logPath = "Temp/sprite_diagnostic.txt";
            using (var writer = new StreamWriter(logPath, false))
            {
                writer.WriteLine("=== SPRITE DIAGNOSTIC START ===");
                
                string[] paths = new string[]
                {
                    "Assets/Sprites/Player/PlayerIdle.png",
                    "Assets/Sprites/Player/PlayerJump.png",
                    "Assets/Sprites/Player/PlayerInfected.png",
                    "Assets/Sprites/Player/PlayerLimitTime.png",
                    "Assets/Sprites/Zombi/ZDown.png",
                    "Assets/Sprites/Zombi/ZUp.png",
                    "Assets/Sprites/Zombi/ZLeft.png",
                    "Assets/Sprites/Zombi/ZRight.png",
                    "Assets/Sprites/Zombi/ZRightUp.png",
                    "Assets/Sprites/Zombi/ZRightDown.png",
                    "Assets/Sprites/Zombi/ZDownLeft.png",
                    "Assets/Sprites/Zombi/ZLeftUp.png"
                };

                foreach (var p in paths)
                {
                    var importer = AssetImporter.GetAtPath(p) as TextureImporter;
                    writer.WriteLine($"PATH: {p}");
                    if (importer == null)
                    {
                        writer.WriteLine("  TextureImporter is NULL!");
                        continue;
                    }

                    writer.WriteLine($"  spriteImportMode: {importer.spriteImportMode}");
                    writer.WriteLine($"  spritePixelsPerUnit: {importer.spritePixelsPerUnit}");
                    writer.WriteLine($"  filterMode: {importer.filterMode}");
                    writer.WriteLine($"  textureCompression: {importer.textureCompression}");
                    
                    var subAssets = AssetDatabase.LoadAllAssetsAtPath(p);
                    writer.WriteLine($"  SubAssets count: {subAssets.Length}");
                    int spriteCount = 0;
                    foreach (var sub in subAssets)
                    {
                        if (sub is Sprite s)
                        {
                            spriteCount++;
                            writer.WriteLine($"    Sprite: {s.name}, rect: {s.rect}, bounds: {s.bounds.size}, textureRect: {s.textureRect}");
                        }
                    }
                    writer.WriteLine($"  Total Sprites found: {spriteCount}");
                }

                // Also check CharacterSpritesConfig
                var config = Resources.Load<CharacterSpritesConfig>("CharacterSpritesConfig");
                writer.WriteLine($"CharacterSpritesConfig loaded via Resources: {(config != null ? "YES" : "NO")}");
                if (config != null)
                {
                    writer.WriteLine($"  playerIdle: {(config.playerIdle != null ? config.playerIdle.Length.ToString() : "NULL")}");
                    if (config.playerIdle != null)
                    {
                        for (int i = 0; i < config.playerIdle.Length; i++)
                        {
                            writer.WriteLine($"    [{i}] = {(config.playerIdle[i] != null ? config.playerIdle[i].name : "NULL")}");
                        }
                    }
                    writer.WriteLine($"  playerLimitTime: {(config.playerLimitTime != null ? config.playerLimitTime.name : "NULL")}");
                    writer.WriteLine($"  zombieDown: {(config.zombieDown != null ? config.zombieDown.Length.ToString() : "NULL")}");
                    if (config.zombieDown != null)
                    {
                        for (int i = 0; i < config.zombieDown.Length; i++)
                        {
                            writer.WriteLine($"    [{i}] = {(config.zombieDown[i] != null ? config.zombieDown[i].name : "NULL")}");
                        }
                    }
                }

                // Check GameTuning
                var tuning = Resources.Load<GameTuning>("GameTuning");
                writer.WriteLine($"GameTuning loaded via Resources: {(tuning != null ? "YES" : "NO")}");
                if (tuning != null)
                {
                    writer.WriteLine($"  characterSpritesConfig reference: {(tuning.characterSpritesConfig != null ? "ASSIGNED" : "NULL")}");
                }

                writer.WriteLine("=== SPRITE DIAGNOSTIC END ===");
            }
        }
    }
}
#endif
