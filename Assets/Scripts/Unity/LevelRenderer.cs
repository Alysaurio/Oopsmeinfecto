using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Builds and animates the Unity Tilemap visual presentation of a level.
    /// The renderer only reads <see cref="GameState"/> and <see cref="LevelDefinition"/>;
    /// it never changes gameplay state or logic.
    /// </summary>
    public sealed class LevelRenderer : MonoBehaviour
    {
        private GameTuning _tuning;
        private LevelDefinition _level;
        private Camera _camera;
        private Transform _contentRoot;
        private LevelTilesetConfig _tileset;
        private CharacterSpritesConfig _characterSprites;

        private Tilemap _groundTilemap;
        private Tilemap _cracksTilemap;
        private Tilemap _mossTilemap;
        private Tilemap _zombieTrailTilemap;

        // Dedicated multi-tilemap layers for walls to prevent overwrites in shared/narrow coordinates
        private Tilemap _wallsTilemap; // Primary reference / fallback
        private Tilemap _wallsBottomTilemap;
        private Tilemap _wallsTopTilemap;
        private Tilemap _wallsLeftTilemap;
        private Tilemap _wallsRightTilemap;
        private Tilemap _wallsCornersTilemap;

        private Transform _specialEffectsRoot;

        private SpriteRenderer _playerRenderer;
        private SpriteRenderer _zombieRenderer;
        private PlayerVisualController _playerVisual;
        private readonly List<DamageTileEffect> _damageEffects = new List<DamageTileEffect>();
        private GoalGlowEffect _goalEffect;

        private SegmentedEnergyBar _energyBar;
        private NPCFacingController _facing;
        private AmbientInfectionVFX _vfx;

        private PlayableBounds _visualBounds;
        private Vector3 _playerTarget;
        private Vector3 _zombieTarget;
        private bool _hasState;
        private int _lastScreenWidth;
        private int _lastScreenHeight;

        private bool _transforming;
        private float _transformTimer;
        private float _transformDuration;
        private Vector3 _transformBase;
        private Action _transformComplete;
        private bool _transformCallbackInvoked;

        public SegmentedEnergyBar EnergyBar => _energyBar;
        public AmbientInfectionVFX AmbientVfx => _vfx;
        public NPCFacingController ZombieFacing => _facing;
        public PlayerVisualController PlayerVisual => _playerVisual;
        public bool IsTransforming => _transforming;

        [Header("Camera Framing")]
        [SerializeField] private float cameraYOffset = 0.35f;

        public float CameraYOffset
        {
            get => cameraYOffset;
            set => cameraYOffset = value;
        }

        public Tilemap GroundBaseTilemap => _groundTilemap;
        public Tilemap CracksTilemap => _cracksTilemap;
        public Tilemap MossTilemap => _mossTilemap;
        public Tilemap ZombieTrailTilemap => _zombieTrailTilemap;
        public Tilemap WallsTilemap => _wallsTilemap;
        public Tilemap WallsBottomTilemap => _wallsBottomTilemap;
        public Tilemap WallsTopTilemap => _wallsTopTilemap;
        public Tilemap WallsLeftTilemap => _wallsLeftTilemap;
        public Tilemap WallsRightTilemap => _wallsRightTilemap;
        public Tilemap WallsCornersTilemap => _wallsCornersTilemap;
        public Transform SpecialEffectsRoot => _specialEffectsRoot;

        public void Clear()
        {
            if (_contentRoot != null)
            {
                _contentRoot.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(_contentRoot.gameObject);
                else DestroyImmediate(_contentRoot.gameObject);
            }

            _contentRoot = null;
            _groundTilemap = null;
            _cracksTilemap = null;
            _mossTilemap = null;
            _zombieTrailTilemap = null;
            _wallsTilemap = null;
            _wallsBottomTilemap = null;
            _wallsTopTilemap = null;
            _wallsLeftTilemap = null;
            _wallsRightTilemap = null;
            _wallsCornersTilemap = null;
            _specialEffectsRoot = null;

            _playerRenderer = null;
            _zombieRenderer = null;
            _playerVisual = null;
            _damageEffects.Clear();
            _goalEffect = null;

            _energyBar = null;
            _facing = null;
            _vfx = null;
            _level = null;
            _camera = null;
            _tileset = null;
            _characterSprites = null;
            _hasState = false;
            _transforming = false;
            _transformComplete = null;
            _transformCallbackInvoked = false;
        }

        public void Build(LevelDefinition level, GameTuning tuning, Camera camera)
        {
            Clear();
            _level = level;
            _tuning = tuning;
            _camera = camera;

            _tileset = tuning != null && tuning.tilesetConfig != null
                ? tuning.tilesetConfig
                : Resources.Load<LevelTilesetConfig>("LevelTilesetConfig");

            _characterSprites = tuning != null && tuning.characterSpritesConfig != null
                ? tuning.characterSpritesConfig
                : Resources.Load<CharacterSpritesConfig>("CharacterSpritesConfig");

            _contentRoot = new GameObject("Level Content").transform;
            _contentRoot.SetParent(transform, false);
            _contentRoot.localPosition = Vector3.zero;

            BuildBoard();
            BuildEntities();
            BuildLighting();
            BuildAmbientVfx();
            FitCamera(true);

            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
        }

        private void BuildLighting()
        {
            if (_contentRoot == null || _level == null) return;
            var lightManager = _contentRoot.gameObject.AddComponent<GridLightManager>();
            lightManager.Initialize(_level, _contentRoot, _playerRenderer, _zombieRenderer);
        }

        private void BuildBoard()
        {
            // Grid container with (-0.5, -0.5) offset so cell center is exactly at (x, y, 0)
            var gridObject = new GameObject("Grid");
            gridObject.transform.SetParent(_contentRoot, false);
            gridObject.transform.localPosition = new Vector3(-0.5f, -0.5f, 0f);

            var grid = gridObject.AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Rectangle;
            grid.cellSize = new Vector3(1f, 1f, 0f);
            grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;

            Material fallbackMaterial = PixelArtFactory.SpriteMaterial;
            Material floorMaterial = RetroLitMaterials.Get(RetroLitMode.Floor) ?? fallbackMaterial;
            Material wallTopMaterial = RetroLitMaterials.Get(RetroLitMode.WallTop) ?? fallbackMaterial;
            Material unlitWallMaterial = RetroLitMaterials.Get(RetroLitMode.Unlit) ?? fallbackMaterial;

            // 4 floor/decoration layers
            _groundTilemap = CreateTilemapLayer(gridObject.transform, "GroundBaseTilemap", 0, floorMaterial);
            _cracksTilemap = CreateTilemapLayer(gridObject.transform, "CracksTilemap", 1, floorMaterial);
            _mossTilemap = CreateTilemapLayer(gridObject.transform, "MossTilemap", 2, floorMaterial);
            _zombieTrailTilemap = CreateTilemapLayer(gridObject.transform, "ZombieTrailTilemap", 3, floorMaterial);

            // 5 independent Tilemap layers for walls (Individual mode + TopLeft sorting for Y-descending order)
            // Priority: paredes arriba (order 6) se dibujan por delante de paredes laterales (order 5), esquinas (order 7)
            _wallsBottomTilemap = CreateTilemapLayer(gridObject.transform, "WallsBottomTilemap", 4, unlitWallMaterial,
                TilemapRenderer.Mode.Individual, TilemapRenderer.SortOrder.TopLeft);
            _wallsLeftTilemap = CreateTilemapLayer(gridObject.transform, "WallsLeftTilemap", 5, unlitWallMaterial,
                TilemapRenderer.Mode.Individual, TilemapRenderer.SortOrder.TopLeft);
            _wallsRightTilemap = CreateTilemapLayer(gridObject.transform, "WallsRightTilemap", 5, unlitWallMaterial,
                TilemapRenderer.Mode.Individual, TilemapRenderer.SortOrder.TopLeft);
            _wallsTopTilemap = CreateTilemapLayer(gridObject.transform, "WallsTopTilemap", 6, wallTopMaterial,
                TilemapRenderer.Mode.Individual, TilemapRenderer.SortOrder.TopLeft);
            _wallsCornersTilemap = CreateTilemapLayer(gridObject.transform, "WallsCornersTilemap", 7, unlitWallMaterial,
                TilemapRenderer.Mode.Individual, TilemapRenderer.SortOrder.TopLeft);

            _wallsTilemap = _wallsTopTilemap;

            // Container for special hazard / victory VFX
            var fxObj = new GameObject("SpecialEffects");
            fxObj.transform.SetParent(_contentRoot, false);
            fxObj.transform.localPosition = Vector3.zero;
            _specialEffectsRoot = fxObj.transform;

            PopulateTilesAndEffects();
            ApplyEnvironmentColorUnification();
        }

        private void ApplyEnvironmentColorUnification()
        {
            var envColor = new Color(0.45f, 0.50f, 0.60f, 1f);

            if (_contentRoot != null)
            {
                var spriteRenderers = _contentRoot.GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < spriteRenderers.Length; i++)
                {
                    var sr = spriteRenderers[i];
                    if (sr == null || sr.gameObject == null) continue;

                    if (sr == _playerRenderer || sr == _zombieRenderer) continue;
                    if (sr.GetComponentInParent<ParticleSystem>() != null) continue;
                    if (sr.GetComponentInParent<DamageTileEffect>() != null || sr.GetComponentInParent<GoalGlowEffect>() != null) continue;

                    string goName = sr.gameObject.name;
                    string spriteName = sr.sprite != null ? sr.sprite.name : null;

                    bool isTile = RetroLitMaterials.IsTintTarget(goName) || RetroLitMaterials.IsTintTarget(spriteName);
                    if (isTile)
                    {
                        sr.color = envColor;

                        RetroLitMode mode = RetroLitMaterials.Classify(goName);
                        if (mode == RetroLitMode.None) mode = RetroLitMaterials.Classify(spriteName);
                        if (mode != RetroLitMode.None)
                        {
                            Material litMat = RetroLitMaterials.Get(mode);
                            if (litMat != null) sr.sharedMaterial = litMat;
                        }
                    }
                }
            }

            if (_groundTilemap != null) _groundTilemap.color = envColor;
            if (_wallsBottomTilemap != null) _wallsBottomTilemap.color = envColor;
            if (_wallsLeftTilemap != null) _wallsLeftTilemap.color = envColor;
            if (_wallsRightTilemap != null) _wallsRightTilemap.color = envColor;
            if (_wallsTopTilemap != null) _wallsTopTilemap.color = envColor;
            if (_wallsCornersTilemap != null) _wallsCornersTilemap.color = envColor;
            if (_cracksTilemap != null) _cracksTilemap.color = envColor;
            if (_mossTilemap != null) _mossTilemap.color = envColor;
            if (_zombieTrailTilemap != null) _zombieTrailTilemap.color = envColor;
        }

        private static Tilemap CreateTilemapLayer(Transform parent, string name, int sortingOrder, Material material,
            TilemapRenderer.Mode mode = TilemapRenderer.Mode.Chunk,
            TilemapRenderer.SortOrder sortOrder = TilemapRenderer.SortOrder.BottomLeft)
        {
            var layerObj = new GameObject(name);
            layerObj.transform.SetParent(parent, false);
            layerObj.transform.localPosition = Vector3.zero;

            var tilemap = layerObj.AddComponent<Tilemap>();
            var renderer = layerObj.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            renderer.mode = mode;
            renderer.sortOrder = sortOrder;
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            return tilemap;
        }

        private void PopulateTilesAndEffects()
        {
            if (_level == null || _level.Board == null) return;

            var board = _level.Board;

            // Deterministic independent visual RNG: Hash(levelSeed, "VisualDecoration")
            uint hash = 2166136261u;
            hash = (hash ^ (uint)_level.Seed) * 16777619u;
            foreach (char c in "VisualDecoration") hash = (hash ^ (byte)c) * 16777619u;
            var rng = new SeededRandom((int)hash);

            // Determine walkable cells
            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            var walkablePositions = new List<GridPos>();

            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var pos = new GridPos(x, y);
                    if (board.IsWalkable(pos))
                    {
                        walkablePositions.Add(pos);
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (walkablePositions.Count == 0)
            {
                minX = 0; maxX = board.Width - 1;
                minY = 0; maxY = board.Height - 1;
            }

            var routePositions = _level.GetRoutePositions();

            // 1. Walkable tiles: Ground Base, Cracks, Moho / DG, and Special Effects
            for (int i = 0; i < walkablePositions.Count; i++)
            {
                var pos = walkablePositions[i];
                var cell = new Vector3Int(pos.X, pos.Y, 0);

                // --- Ground Base: ~70% Group P, ~30% Group U ---
                if (_tileset != null)
                {
                    bool isGroupP = rng.NextDouble() < 0.70;
                    TileBase floorTile;
                    if (isGroupP && _tileset.groundP != null && _tileset.groundP.Length > 0)
                    {
                        floorTile = _tileset.groundP[rng.Next(_tileset.groundP.Length)];
                    }
                    else if (!isGroupP && _tileset.groundU != null && _tileset.groundU.Length > 0)
                    {
                        floorTile = _tileset.groundU[rng.Next(_tileset.groundU.Length)];
                    }
                    else
                    {
                        floorTile = _tileset.groundP != null && _tileset.groundP.Length > 0
                            ? _tileset.groundP[0]
                            : null;
                    }

                    if (floorTile != null)
                    {
                        _groundTilemap.SetTile(cell, floorTile);
                    }
                }

                // --- Cracks: ~20% of all walkable cells ---
                if (_tileset != null && _tileset.cracks != null && _tileset.cracks.Length > 0)
                {
                    if (rng.NextDouble() < 0.20)
                    {
                        var crackTile = _tileset.cracks[rng.Next(_tileset.cracks.Length)];
                        if (crackTile != null)
                        {
                            _cracksTilemap.SetTile(cell, crackTile);
                        }
                    }
                }

                // --- Zombie route (DG) vs Moho ---
                if (routePositions.Contains(pos))
                {
                    // 100% of cyclic route cells receive DG (and NO normal moss)
                    if (_tileset != null && _tileset.zombieTrailDG != null && _tileset.zombieTrailDG.Length > 0)
                    {
                        var dgTile = _tileset.zombieTrailDG[rng.Next(_tileset.zombieTrailDG.Length)];
                        if (dgTile != null)
                        {
                            _zombieTrailTilemap.SetTile(cell, dgTile);
                        }
                    }
                }
                else
                {
                    // ~30% of non-route walkable cells receive Moho
                    if (_tileset != null && _tileset.moss != null && _tileset.moss.Length > 0)
                    {
                        if (rng.NextDouble() < 0.30)
                        {
                            var mossTile = _tileset.moss[rng.Next(_tileset.moss.Length)];
                            if (mossTile != null)
                            {
                                _mossTilemap.SetTile(cell, mossTile);
                            }
                        }
                    }
                }

                // --- Special Effects: DamageTile ---
                if (board.GetTile(pos) == TileType.Damage)
                {
                    CreateDamageEffect(pos);
                }
            }

            // --- Victory Switch Effect ---
            CreateSwitchEffect(_level.SwitchPosition);

            // --- 2. Walls Placement: Contour-based on walkable adjacency ---
            PlaceWallsAdjacency(board, walkablePositions, rng);

            // Set visual bounds for camera framing
            _visualBounds = new PlayableBounds(minX - 1, minY - 1, maxX + 1, maxY + 1);
        }

        /// <summary>
        /// Places walls strictly adjacent to actual walkable tiles. No empty space or bounding box walls.
        /// Uses separate tilemap layers for each direction so multiple walls can share coordinates (e.g. 1-tile corridors).
        /// </summary>
        private void PlaceWallsAdjacency(BoardData board, List<GridPos> walkablePositions, SeededRandom rng)
        {
            if (_tileset == null || walkablePositions == null || walkablePositions.Count == 0) return;

            var walkableSet = new HashSet<GridPos>(walkablePositions);
            var topWallCells = new HashSet<Vector3Int>();
            var bottomWallCells = new HashSet<Vector3Int>();
            var leftWallCells = new HashSet<Vector3Int>();
            var rightWallCells = new HashSet<Vector3Int>();

            // Inspect the 4 orthogonal neighbors of each walkable position
            for (int i = 0; i < walkablePositions.Count; i++)
            {
                var pos = walkablePositions[i];

                // 1. North neighbor (pos.X, pos.Y + 1) -> Top wall
                var north = new GridPos(pos.X, pos.Y + 1);
                if (!walkableSet.Contains(north))
                {
                    var cell = new Vector3Int(north.X, north.Y, 0);
                    if (topWallCells.Add(cell))
                    {
                        var tile = PickTile(_tileset.wallsTop, rng);
                        if (tile != null) _wallsTopTilemap.SetTile(cell, tile);
                    }
                }

                // 2. South neighbor (pos.X, pos.Y - 1) -> Bottom wall
                var south = new GridPos(pos.X, pos.Y - 1);
                if (!walkableSet.Contains(south))
                {
                    var cell = new Vector3Int(south.X, south.Y, 0);
                    if (bottomWallCells.Add(cell))
                    {
                        var tile = PickTile(_tileset.wallsBottom, rng);
                        if (tile != null) _wallsBottomTilemap.SetTile(cell, tile);
                    }
                }

                // 3. West neighbor (pos.X - 1, pos.Y) -> Left wall
                var west = new GridPos(pos.X - 1, pos.Y);
                if (!walkableSet.Contains(west))
                {
                    var cell = new Vector3Int(west.X, west.Y, 0);
                    if (leftWallCells.Add(cell))
                    {
                        var tile = PickTile(_tileset.wallsLeft, rng);
                        if (tile != null) _wallsLeftTilemap.SetTile(cell, tile);
                    }
                }

                // 4. East neighbor (pos.X + 1, pos.Y) -> Right wall
                var east = new GridPos(pos.X + 1, pos.Y);
                if (!walkableSet.Contains(east))
                {
                    var cell = new Vector3Int(east.X, east.Y, 0);
                    if (rightWallCells.Add(cell))
                    {
                        var tile = PickTile(_tileset.wallsRight, rng);
                        if (tile != null) _wallsRightTilemap.SetTile(cell, tile);
                    }
                }
            }

            // Corners: strictly where a top wall meets a side wall
            var cornerLeftCells = new HashSet<Vector3Int>();
            var cornerRightCells = new HashSet<Vector3Int>();

            for (int i = 0; i < walkablePositions.Count; i++)
            {
                var pos = walkablePositions[i];

                // North-West diagonal: pos is (x+1, y-1), non-walkable corner is (pos.X - 1, pos.Y + 1)
                var nw = new GridPos(pos.X - 1, pos.Y + 1);
                if (!walkableSet.Contains(nw))
                {
                    var cell = new Vector3Int(nw.X, nw.Y, 0);
                    // Check if top wall is to its East (pos.X, pos.Y + 1) AND left wall is to its South (pos.X - 1, pos.Y)
                    if (topWallCells.Contains(new Vector3Int(pos.X, pos.Y + 1, 0)) &&
                        leftWallCells.Contains(new Vector3Int(pos.X - 1, pos.Y, 0)))
                    {
                        if (_tileset.cornerTopLeft != null && cornerLeftCells.Add(cell))
                        {
                            _wallsCornersTilemap.SetTile(cell, _tileset.cornerTopLeft);
                        }
                    }
                }

                // North-East diagonal: pos is (x-1, y-1), non-walkable corner is (pos.X + 1, pos.Y + 1)
                var ne = new GridPos(pos.X + 1, pos.Y + 1);
                if (!walkableSet.Contains(ne))
                {
                    var cell = new Vector3Int(ne.X, ne.Y, 0);
                    // Check if top wall is to its West (pos.X, pos.Y + 1) AND right wall is to its South (pos.X + 1, pos.Y)
                    if (topWallCells.Contains(new Vector3Int(pos.X, pos.Y + 1, 0)) &&
                        rightWallCells.Contains(new Vector3Int(pos.X + 1, pos.Y, 0)))
                    {
                        if (_tileset.cornerTopRight != null && cornerRightCells.Add(cell))
                        {
                            _wallsCornersTilemap.SetTile(cell, _tileset.cornerTopRight);
                        }
                    }
                }
            }
        }

        private static TileBase PickTile(TileBase[] array, SeededRandom rng)
        {
            if (array == null || array.Length == 0) return null;
            return array[rng.Next(array.Length)];
        }

        private void CreateDamageEffect(GridPos pos)
        {
            var effectPos = new Vector3(pos.X, pos.Y, 0f);
            DamageTileEffect effectInstance = null;

            if (_tileset != null && _tileset.damageTilePrefab != null)
            {
                var instance = Instantiate(_tileset.damageTilePrefab, effectPos, Quaternion.identity, _specialEffectsRoot);
                instance.transform.localPosition = effectPos;
                effectInstance = instance.GetComponent<DamageTileEffect>();
            }
            else
            {
                var go = new GameObject("DamageTileEffect " + pos.X + "," + pos.Y);
                go.transform.SetParent(_specialEffectsRoot, false);
                go.transform.localPosition = effectPos;
                effectInstance = go.AddComponent<DamageTileEffect>();
                var glowSprite = _tileset != null ? _tileset.toxicGlowSprite : null;
                var gasPrefab = _tileset != null ? _tileset.toxicGasParticlesPrefab : null;
                var color = _tuning != null ? _tuning.damageColor : new Color(0.20f, 0.78f, 0.28f, 1f);
                effectInstance.Initialize(color, glowSprite, gasPrefab);
            }

            if (effectInstance != null)
            {
                effectInstance.transform.localPosition = effectPos;
                // Activate particle systems (.Play()) strictly after being positioned at the correct grid coordinates
                effectInstance.PlayParticles();
                _damageEffects.Add(effectInstance);
            }
        }

        private void CreateSwitchEffect(GridPos pos)
        {
            var effectPos = new Vector3(pos.X, pos.Y, 0f);

            if (_tileset != null && _tileset.goalGlowPrefab != null)
            {
                var instance = Instantiate(_tileset.goalGlowPrefab, _specialEffectsRoot);
                instance.transform.localPosition = effectPos;
                _goalEffect = instance.GetComponent<GoalGlowEffect>();
            }
            else
            {
                var go = new GameObject("GoalGlowEffect");
                go.transform.SetParent(_specialEffectsRoot, false);
                go.transform.localPosition = effectPos;
                _goalEffect = go.AddComponent<GoalGlowEffect>();
                var glowSprite = _tileset != null ? _tileset.goalGlowSprite : null;
                var color = _tuning != null ? _tuning.switchColor : new Color(0.25f, 0.95f, 0.85f, 1f);
                _goalEffect.Initialize(color, glowSprite);
            }

            if (_goalEffect != null)
            {
                _goalEffect.transform.localPosition = effectPos;
                _goalEffect.PlayParticles();
            }
        }

        private void BuildEntities()
        {
            if (_characterSprites != null)
            {
                _characterSprites.EnsureLoaded();
            }

            Sprite initialPlayerSprite = (_characterSprites != null && _characterSprites.playerIdle != null &&
                                          _characterSprites.playerIdle.Length > 0 && _characterSprites.playerIdle[0] != null)
                ? _characterSprites.playerIdle[0]
                : PixelArtFactory.PlayerSprite;

            Sprite initialZombieSprite = (_characterSprites != null && _characterSprites.zombieDown != null &&
                                          _characterSprites.zombieDown.Length > 0 && _characterSprites.zombieDown[0] != null)
                ? _characterSprites.zombieDown[0]
                : PixelArtFactory.ZombieSprite;

            _zombieRenderer = CreateEntity("Zombie", initialZombieSprite, 11,
                new Vector3(_level.ZombieStart.X, _level.ZombieStart.Y, 0f));
            _playerRenderer = CreateEntity("Player", initialPlayerSprite, 12,
                new Vector3(_level.PlayerStart.X, _level.PlayerStart.Y, 0f));

            // Attach Animator components with their respective runtime controllers
            var playerAnimator = _playerRenderer.gameObject.AddComponent<Animator>();
            var playerCtrl = _characterSprites != null ? _characterSprites.playerAnimatorController : null;
            if (playerCtrl == null) playerCtrl = Resources.Load<RuntimeAnimatorController>("PlayerAnimatorController");
            playerAnimator.runtimeAnimatorController = playerCtrl;

            var zombieAnimator = _zombieRenderer.gameObject.AddComponent<Animator>();
            var zombieCtrl = _characterSprites != null ? _characterSprites.zombieAnimatorController : null;
            if (zombieCtrl == null) zombieCtrl = Resources.Load<RuntimeAnimatorController>("ZombieAnimatorController");
            zombieAnimator.runtimeAnimatorController = zombieCtrl;

            _playerVisual = _playerRenderer.gameObject.AddComponent<PlayerVisualController>();
            _playerVisual.Initialize(_playerRenderer, _characterSprites);

            _facing = _zombieRenderer.gameObject.AddComponent<NPCFacingController>();
            _facing.Initialize(_zombieRenderer, _tuning, _characterSprites, InitialZombieFacing());

            var energyObject = new GameObject("Energy Bar");
            energyObject.transform.SetParent(_contentRoot, false);
            _energyBar = energyObject.AddComponent<SegmentedEnergyBar>();
            _energyBar.Initialize(_tuning, _level.MaxEnergy, _playerRenderer.transform);

            _playerTarget = _playerRenderer.transform.position;
            _zombieTarget = _zombieRenderer.transform.position;
        }

        private NpcFacing InitialZombieFacing()
        {
            if (_level.NpcRoute == null || _level.NpcRoute.Length < 2) return NpcFacing.Down;
            var pattern = new CyclicRoutePattern(_level.NpcRoute);
            var current = pattern.PositionAt(_level.ZombiePhaseStart);
            var twoAhead = pattern.PositionAt(_level.ZombiePhaseStart + 2);
            var delta = twoAhead - current;
            if (delta.X != 0 || delta.Y != 0) return NPCFacingController.FromDelta(delta);

            var next = pattern.PositionAt(_level.ZombiePhaseStart + 1);
            var nextDelta = next - current;
            if (nextDelta.X == 0 && nextDelta.Y == 0) return NpcFacing.Down;
            return NPCFacingController.FromDirection(Directions.FromDelta(nextDelta));
        }

        private void BuildAmbientVfx()
        {
            var vfxObject = new GameObject("Ambient Infection VFX");
            vfxObject.transform.SetParent(_contentRoot, false);
            _vfx = vfxObject.AddComponent<AmbientInfectionVFX>();
            _vfx.Initialize(_tuning, _playerRenderer.transform);
        }

        private SpriteRenderer CreateEntity(string objectName, Sprite sprite, int sortingOrder,
            Vector3 worldPosition)
        {
            var entityObject = new GameObject(objectName);
            entityObject.transform.SetParent(_contentRoot, false);
            entityObject.transform.localPosition = worldPosition;

            var shadowObject = new GameObject("Shadow");
            shadowObject.transform.SetParent(entityObject.transform, false);
            shadowObject.transform.localPosition = new Vector3(0f, -0.36f, 0f);
            shadowObject.transform.localScale = new Vector3(0.58f, 0.20f, 1f);
            var shadow = shadowObject.AddComponent<SpriteRenderer>();
            shadow.sprite = PixelArtFactory.WhiteSprite;
            shadow.color = new Color(0f, 0f, 0f, 0.38f);
            shadow.sortingOrder = sortingOrder - 2;
            if (PixelArtFactory.SpriteMaterial != null) shadow.sharedMaterial = PixelArtFactory.SpriteMaterial;

            var renderer = entityObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingOrder = sortingOrder;
            Material entityMaterial = RetroLitMaterials.Get(RetroLitMode.Entity) ?? PixelArtFactory.SpriteMaterial;
            if (entityMaterial != null)
            {
                renderer.sharedMaterial = entityMaterial;
            }
            return renderer;
        }

        public void SyncFromState(GameState state, bool instant = false)
        {
            if (state == null || _playerRenderer == null || _zombieRenderer == null) return;

            var prevPlayerTarget = _playerTarget;
            var prevZombieTarget = _zombieTarget;

            _playerTarget = new Vector3(state.Player.Position.X, state.Player.Position.Y, 0f);
            _zombieTarget = new Vector3(state.Zombie.Position.X, state.Zombie.Position.Y, 0f);
            _hasState = true;

            bool infected = state.Player.Infected || state.Phase == GamePhase.InfectedPlayback;
            if (_playerVisual != null)
            {
                _playerVisual.SetInfected(infected);
            }

            if (instant)
            {
                _playerRenderer.transform.position = _playerTarget;
                _zombieRenderer.transform.position = _zombieTarget;
                if (_playerVisual != null) _playerVisual.ResetState();
                if (_facing != null)
                {
                    _facing.SetMoving(false);
                    _facing.SetBasePosition(_zombieTarget);
                }
            }
            else
            {
                if (Vector3.Distance(prevPlayerTarget, _playerTarget) > 0.01f && _playerVisual != null)
                {
                    if (infected && _playerRenderer != null)
                    {
                        RetroGhostTrail.Spawn(prevPlayerTarget, _playerRenderer.sprite, _playerRenderer.flipX, _playerRenderer.sortingOrder - 1, PixelArtFactory.SpriteMaterial);
                    }
                    _playerVisual.OnMoveStarted(_playerRenderer.transform.position, _playerTarget);
                }
                if (Vector3.Distance(prevZombieTarget, _zombieTarget) > 0.01f && _facing != null)
                {
                    var displacement = _zombieTarget - prevZombieTarget;
                    var moveFacing = NPCFacingController.FromDelta(new GridPos(
                        Mathf.RoundToInt(displacement.x),
                        Mathf.RoundToInt(displacement.y)));
                    _facing.StartMovement(moveFacing);
                }
            }

            if (!_transforming)
            {
                _playerRenderer.color = infected
                    ? Color.Lerp(Color.white, _tuning.infectedDesaturation, 0.65f)
                    : Color.white;
            }

            if (_energyBar != null)
            {
                _energyBar.SetEnergy(state.CurrentEnergy, state.MaxEnergy);
                _energyBar.SetInfected(infected);
            }

            UpdateZombieFacing(state);
            UpdateAmbientVfx(state);
        }

        private void UpdateZombieFacing(GameState state)
        {
            if (_facing == null) return;

            var twoAhead = state.Pattern.PositionAt(state.ZombiePhase + 2);
            var next = state.Pattern.PositionAt(state.ZombiePhase + 1);
            var nextDelta = next - state.Zombie.Position;
            Direction? nextMove = (nextDelta.X == 0 && nextDelta.Y == 0)
                ? (Direction?)null
                : Directions.FromDelta(nextDelta);
            _facing.UpdateFacing(state.Zombie.Position, twoAhead, nextMove);
        }

        private void UpdateAmbientVfx(GameState state)
        {
            if (_vfx == null) return;

            if (state.Phase == GamePhase.HumanControlled)
            {
                int consumed = Mathf.Max(0, state.MaxEnergy - state.CurrentEnergy);
                float intensity = state.TurnNumber >= 2
                    ? consumed / (float)Mathf.Max(1, state.MaxEnergy)
                    : 0f;
                _vfx.SetIntensity(intensity);
            }
            else if (state.Phase == GamePhase.InfectedPlayback)
            {
                _vfx.FadeOutAndClear(0.12f);
            }
            else if (state.Phase == GamePhase.EnvironmentalTransformation)
            {
                if (!_transforming) _vfx.FadeOutAndClear(Mathf.Max(0.15f, _tuning.transformationDuration * 0.75f));
            }
            else
            {
                _vfx.FadeOutAndClear(0.20f);
            }
        }

        public void PlayDrain(float duration)
        {
            if (_energyBar != null) _energyBar.PlayDrain(duration);
        }

        /// <summary>
        /// Starts the exhaustion presentation. The callback is invoked exactly once after the
        /// configured pause. This only changes visuals and never touches <see cref="GameState"/>.
        /// </summary>
        public void BeginEnvironmentalTransformation(float duration, Action onComplete)
        {
            if (_transforming) return;
            _transforming = true;
            _transformTimer = 0f;
            _transformDuration = Mathf.Max(0.1f, duration);
            _transformBase = _playerRenderer != null ? _playerRenderer.transform.position : _playerTarget;
            _transformComplete = onComplete;
            _transformCallbackInvoked = false;

            if (_playerVisual != null && !_playerVisual.IsDefeated)
            {
                _playerVisual.PlayDefeat(DefeatReason.EnergyDepleted, _transformDuration);
            }

            if (_vfx != null) _vfx.FadeOutAndClear(Mathf.Max(0.15f, _transformDuration * 0.75f));
        }

        public void FitCamera(bool force)
        {
            if (_camera == null || _level == null || _tuning == null) return;
            if (!force && Screen.width == _lastScreenWidth && Screen.height == _lastScreenHeight) return;

            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;

            float hudFraction = Mathf.Clamp(_tuning.hudViewportFraction, 0.10f, 0.30f);
            _camera.rect = new Rect(0f, 0f, 1f, 1f);
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = _tuning.cameraBackground;

            var bounds = _level.GetPlayableBounds();
            float boundsWidth = bounds.IsValid ? bounds.Width : _level.Width;
            float boundsHeight = bounds.IsValid ? bounds.Height : _level.Height;
            float centerX = bounds.IsValid ? bounds.CenterX : (_level.Width - 1f) * 0.5f;
            float centerY = bounds.IsValid ? bounds.CenterY : (_level.Height - 1f) * 0.5f;

            float margin = Mathf.Max(0.5f, _tuning.cameraMargin);
            float boardWidth = Mathf.Max(1f, boundsWidth - 1f + margin * 2f);
            float boardHeight = Mathf.Max(1f, boundsHeight - 1f + margin * 2f);

            float viewportWidth = Mathf.Max(1f, Screen.width);
            float viewportHeight = Mathf.Max(1f, Screen.height * (1f - hudFraction));
            float viewAspect = viewportWidth / viewportHeight;

            float verticalSize = boardHeight * 0.5f;
            float horizontalSize = boardWidth / (2f * Mathf.Max(0.01f, viewAspect));
            float targetHalfHeight = Mathf.Max(verticalSize, horizontalSize);

            float totalOrthoSize = targetHalfHeight / (1f - hudFraction);
            _camera.orthographicSize = totalOrthoSize;

            float yOffset = _tuning != null ? _tuning.cameraYOffset : cameraYOffset;
            float cameraCenterY = centerY - totalOrthoSize * hudFraction + yOffset;
            _camera.transform.position = new Vector3(centerX, cameraCenterY, -10f);
        }

        private void Update()
        {
            if (_hasState && _playerRenderer != null && _zombieRenderer != null)
            {
                float blend = 1f - Mathf.Exp(-26f * Time.unscaledDeltaTime);
                if (!_transforming && !_playerVisual.IsDefeated)
                {
                    _playerRenderer.transform.position =
                        Vector3.Lerp(_playerRenderer.transform.position, _playerTarget, blend);
                }
                _zombieRenderer.transform.position =
                    Vector3.Lerp(_zombieRenderer.transform.position, _zombieTarget, blend);

                if (Vector3.Distance(_playerRenderer.transform.position, _playerTarget) < 0.02f)
                {
                    if (_playerVisual != null && _playerVisual.IsMoving)
                    {
                        _playerVisual.OnMoveFinished();
                    }
                }

                if (Vector3.Distance(_zombieRenderer.transform.position, _zombieTarget) < 0.02f)
                {
                    if (_facing != null && _facing.IsMoving)
                    {
                        _facing.SetMoving(false);
                    }
                }
            }

            if (_transforming && _playerRenderer != null)
            {
                TickTransformation();
            }

            FitCamera(false);
        }

        private void TickTransformation()
        {
            _transformTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_transformTimer / Mathf.Max(0.01f, _transformDuration));

            _playerRenderer.color = Color.Lerp(Color.white, _tuning.transformationTint, 0.35f + t * 0.60f);

            if (t >= 1f && !_transformCallbackInvoked)
            {
                _transformCallbackInvoked = true;
                var callback = _transformComplete;
                _transformComplete = null;
                callback?.Invoke();
            }
        }

        private void OnDestroy()
        {
            Clear();
        }
    }
}
