using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Holds references to the 33 imported tiles and procedural VFX prefabs/sprites.
    /// ScriptableObject asset saved in the project for runtime and editor use.
    /// </summary>
    [CreateAssetMenu(menuName = "Oopsmeinfecto/Level Tileset Config", fileName = "LevelTilesetConfig")]
    public sealed class LevelTilesetConfig : ScriptableObject
    {
        [Header("Ground Base - Group P (~70%)")]
        public TileBase[] groundP = new TileBase[3];

        [Header("Ground Base - Group U (~30%)")]
        public TileBase[] groundU = new TileBase[3];

        [Header("Moss (~30% of non-route walkable cells)")]
        public TileBase[] moss = new TileBase[5];

        [Header("Zombie Trail DG (100% of cyclic route cells)")]
        public TileBase[] zombieTrailDG = new TileBase[3];

        [Header("Cracks (~20% of walkable cells)")]
        public TileBase[] cracks = new TileBase[3];

        [Header("Walls - Left Boundary")]
        public TileBase[] wallsLeft = new TileBase[2];

        [Header("Walls - Right Boundary")]
        public TileBase[] wallsRight = new TileBase[2];

        [Header("Walls - Top Boundary")]
        public TileBase[] wallsTop = new TileBase[7];

        [Header("Walls - Bottom Boundary")]
        public TileBase[] wallsBottom = new TileBase[3];

        [Header("Walls - Corners")]
        public TileBase cornerTopLeft;
        public TileBase cornerTopRight;

        [Header("VFX References")]
        public GameObject toxicGasParticlesPrefab;
        public Sprite toxicGlowSprite;
        public Sprite goalGlowSprite;
        public GameObject goalGlowPrefab;
        public GameObject damageTilePrefab;
    }
}
