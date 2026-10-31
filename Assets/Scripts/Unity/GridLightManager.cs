using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oopsmeinfecto
{
    /// <summary>Lighting category of a level sprite, resolved from its name.</summary>
    public enum RetroLitMode
    {
        Floor = 0,     // baldosa*, DG*, grieta*, moho*
        WallTop = 1,   // paredarriba*
        Unlit = 2,     // paredabajo*, paredizquierda*, paredderecha*, esquinaizquierda*, esquinaderecha*
        Entity = 3,    // Player / Zombie
        None = -1      // anything else: keeps its own material
    }

    /// <summary>
    /// Shared runtime materials for the Custom/RetroLitSprite shader, plus the name rules used to decide
    /// which sprites are tinted and which lighting mode they use. Purely visual.
    /// </summary>
    public static class RetroLitMaterials
    {
        private static Material _floor;
        private static Material _wallTop;
        private static Material _unlit;
        private static Material _entity;

        private static readonly string[] FloorTokens =
        {
            "baldosa", "DG1", "DG2", "DG3", "grieta1", "grieta2", "grieta3",
            "moho1", "moho2", "moho3", "moho4", "moho5"
        };

        private static readonly string[] UnlitWallTokens =
        {
            "paredabajo", "paredizquierda", "paredderecha", "esquinaizquierda", "esquinaderecha"
        };

        /// <summary>True for every sprite that receives the environment tint.</summary>
        public static bool IsTintTarget(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (Contains(name, "pared")) return true;
            if (Contains(name, "esquina")) return true; // corner walls keep the same dark base as the walls
            for (int i = 0; i < FloorTokens.Length; i++)
            {
                if (Contains(name, FloorTokens[i])) return true;
            }
            return false;
        }

        public static RetroLitMode Classify(string name)
        {
            if (string.IsNullOrEmpty(name)) return RetroLitMode.None;
            if (Contains(name, "paredarriba")) return RetroLitMode.WallTop;
            for (int i = 0; i < UnlitWallTokens.Length; i++)
            {
                if (Contains(name, UnlitWallTokens[i])) return RetroLitMode.Unlit;
            }
            for (int i = 0; i < FloorTokens.Length; i++)
            {
                if (Contains(name, FloorTokens[i])) return RetroLitMode.Floor;
            }
            return RetroLitMode.None;
        }

        public static Material Get(RetroLitMode mode)
        {
            switch (mode)
            {
                case RetroLitMode.Floor: return Resolve(ref _floor, 0f, "RetroLit Floor");
                case RetroLitMode.WallTop: return Resolve(ref _wallTop, 1f, "RetroLit WallTop");
                case RetroLitMode.Unlit: return Resolve(ref _unlit, 2f, "RetroLit Unlit");
                case RetroLitMode.Entity: return Resolve(ref _entity, 3f, "RetroLit Entity");
                default: return null;
            }
        }

        private static Material Resolve(ref Material cache, float litMode, string name)
        {
            if (cache != null) return cache;

            // The material asset in Resources guarantees the shader is included in WebGL builds.
            var template = Resources.Load<Material>("RetroLitSprite");
            Shader shader = template != null ? template.shader : Shader.Find("Custom/RetroLitSprite");
            if (shader == null || !shader.isSupported) return null;

            cache = template != null ? new Material(template) : new Material(shader);
            cache.name = name;
            cache.hideFlags = HideFlags.DontSave;
            cache.SetFloat("_LitMode", litMode);
            return cache;
        }

        private static bool Contains(string source, string token)
        {
            return source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    /// <summary>
    /// 2D world-space light manager for the grid. Registers the goal light and one light per lethal
    /// tile, uploads a wall occupancy texture (one AABB per wall cell) and computes, every frame, the
    /// directional lighting and projected floor shadows of the Player and the Zombie.
    /// It only feeds shader globals and MaterialPropertyBlocks: no gameplay state is read or changed
    /// apart from the static level layout.
    /// </summary>
    public sealed class GridLightManager : MonoBehaviour
    {
        public const int MaxLights = 16;
        public const int MaxShadows = 8;
        public const float TileSize = 1f;

        public static readonly Color GoalLightColor = new Color(0.2f, 0.85f, 1.0f);
        public static readonly Color HazardLightColor = new Color(0.2f, 0.9f, 0.35f);
        public const float GoalLightRadius = 4.5f;    // 9 x 9 tiles max area
        public const float HazardLightRadius = 2.25f; // half of the goal radius (~4.5 x 4.5 tiles)
        public const float GoalLightIntensity = 1.0f;
        public const float HazardLightIntensity = 0.9f;

        public const float EntityShadowLength = 1.2f;
        public const float EntityShadowHalfWidth = 0.28f;
        public const float EntityShadowStrength = 0.8f;
        public const float BackLitDarken = 0.9f; // -10%

        private static readonly int EnabledId = Shader.PropertyToID("_RL_Enabled");
        private static readonly int LightCountId = Shader.PropertyToID("_RL_LightCount");
        private static readonly int LightPosId = Shader.PropertyToID("_RL_LightPos");
        private static readonly int LightColorId = Shader.PropertyToID("_RL_LightColor");
        private static readonly int ShadowCountId = Shader.PropertyToID("_RL_ShadowCount");
        private static readonly int ShadowAId = Shader.PropertyToID("_RL_ShadowA");
        private static readonly int ShadowBId = Shader.PropertyToID("_RL_ShadowB");
        private static readonly int OccluderTexId = Shader.PropertyToID("_RL_OccluderTex");
        private static readonly int OccluderSizeId = Shader.PropertyToID("_RL_OccluderSize");
        private static readonly int GridOriginId = Shader.PropertyToID("_RL_GridOrigin");
        private static readonly int EntityLightId = Shader.PropertyToID("_EntityLight");
        private static readonly int EntityDarkenId = Shader.PropertyToID("_EntityDarken");

        private static GridLightManager _active;

        private struct GridLight
        {
            public Vector2 Position;
            public float Radius;
            public float Intensity;
            public Color Color;
        }

        private readonly List<GridLight> _lights = new List<GridLight>();
        private readonly Vector4[] _lightPos = new Vector4[MaxLights];
        private readonly Vector4[] _lightColor = new Vector4[MaxLights];
        private readonly Vector4[] _shadowA = new Vector4[MaxShadows];
        private readonly Vector4[] _shadowB = new Vector4[MaxShadows];
        private int _shadowCount;

        private bool[] _occluders;
        private int _width;
        private int _height;
        private Texture2D _occluderTexture;
        private Transform _gridRoot;

        private SpriteRenderer _player;
        private SpriteRenderer _zombie;
        private MaterialPropertyBlock _block;

        public int LightCount => _lights.Count;

        public void Initialize(LevelDefinition level, Transform gridRoot, SpriteRenderer player, SpriteRenderer zombie)
        {
            _gridRoot = gridRoot;
            _player = player;
            _zombie = zombie;
            _block = new MaterialPropertyBlock();
            _lights.Clear();

            if (level == null || level.Board == null) return;
            var board = level.Board;

            BuildOccluders(board);

            AddLight(new Vector2(level.SwitchPosition.X, level.SwitchPosition.Y),
                GoalLightRadius, GoalLightIntensity, GoalLightColor);

            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    if (board.GetTile(new GridPos(x, y)) == TileType.Damage)
                    {
                        AddLight(new Vector2(x, y), HazardLightRadius, HazardLightIntensity, HazardLightColor);
                    }
                }
            }

            _active = this;
            UpdateEntities();
            PushGlobals();
        }

        private void AddLight(Vector2 gridCell, float radius, float intensity, Color color)
        {
            if (_lights.Count >= MaxLights) return;
            _lights.Add(new GridLight
            {
                Position = gridCell, // stored in grid units; converted to world when pushed
                Radius = radius * TileSize,
                Intensity = intensity,
                Color = color
            });
        }

        private void BuildOccluders(BoardData board)
        {
            _width = board.Width;
            _height = board.Height;
            _occluders = new bool[_width * _height];

            if (_occluderTexture != null)
            {
                if (Application.isPlaying) Destroy(_occluderTexture);
                else DestroyImmediate(_occluderTexture);
            }
            _occluderTexture = new Texture2D(_width, _height, TextureFormat.RGBA32, false, true)
            {
                name = "RetroLit Occluders",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            var pixels = new Color32[_width * _height];
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    bool wall = !board.IsWalkable(new GridPos(x, y));
                    _occluders[y * _width + x] = wall;
                    byte v = wall ? (byte)255 : (byte)0;
                    pixels[y * _width + x] = new Color32(v, v, v, 255);
                }
            }
            _occluderTexture.SetPixels32(pixels);
            _occluderTexture.Apply(false, false);
        }

        private Vector2 GridOrigin => _gridRoot != null ? (Vector2)_gridRoot.position : Vector2.zero;

        private void LateUpdate()
        {
            if (_active != this) return;
            UpdateEntities();
            PushGlobals();
        }

        private void UpdateEntities()
        {
            _shadowCount = 0;
            ApplyEntityLighting(_player);
            ApplyEntityLighting(_zombie);
        }

        private void ApplyEntityLighting(SpriteRenderer entity)
        {
            if (entity == null) return;

            Vector2 origin = GridOrigin;
            Vector2 entityPos = entity.transform.position;
            Color front = Color.black;
            bool backLit = false;

            for (int i = 0; i < _lights.Count; i++)
            {
                var light = _lights[i];
                Vector2 lightPos = origin + light.Position;
                Vector2 toEntity = entityPos - lightPos;
                float dist = toEntity.magnitude;
                if (dist >= light.Radius) continue;
                if (!HasLineOfSight(entityPos - origin, light.Position)) continue;

                float attenuation = 1f - Mathf.SmoothStep(0f, 1f, dist / light.Radius);

                // Directional rule on the Y axis.
                float deltaY = entityPos.y - lightPos.y;
                if (deltaY < -0.2f * TileSize)
                {
                    backLit = true; // light behind the character's back: no frontal light, -10%
                }
                else
                {
                    front += light.Color * (light.Intensity * attenuation);
                }

                // Projected elliptical shadow on the floor, away from the light.
                if (dist > 0.05f && _shadowCount < MaxShadows)
                {
                    Vector2 dir = toEntity / dist;
                    Vector2 feet = entityPos + new Vector2(0f, -0.3f * TileSize);
                    _shadowA[_shadowCount] = new Vector4(feet.x, feet.y, dir.x, dir.y);
                    _shadowB[_shadowCount] = new Vector4(
                        EntityShadowLength * TileSize,
                        EntityShadowHalfWidth * TileSize,
                        EntityShadowStrength * Mathf.Clamp01(attenuation * 1.5f),
                        i);
                    _shadowCount++;
                }
            }

            if (_block == null) _block = new MaterialPropertyBlock();
            entity.GetPropertyBlock(_block);
            _block.SetColor(EntityLightId, new Color(front.r, front.g, front.b, 0f));
            _block.SetFloat(EntityDarkenId, backLit ? BackLitDarken : 1f);
            entity.SetPropertyBlock(_block);
        }

        /// <summary>Same segment vs wall-AABB traversal as the shader (single hard ray).</summary>
        private bool HasLineOfSight(Vector2 fromGrid, Vector2 toGrid)
        {
            // Convert from "cell centre" coordinates to grid space where cell i spans [i, i+1).
            Vector2 p = fromGrid + new Vector2(0.5f, 0.5f);
            Vector2 l = toGrid + new Vector2(0.5f, 0.5f);
            Vector2 d = l - p;

            int cellX = Mathf.FloorToInt(p.x);
            int cellY = Mathf.FloorToInt(p.y);
            int endX = Mathf.FloorToInt(l.x);
            int endY = Mathf.FloorToInt(l.y);
            int stepX = d.x >= 0f ? 1 : -1;
            int stepY = d.y >= 0f ? 1 : -1;
            float tDeltaX = 1f / Mathf.Max(Mathf.Abs(d.x), 1e-5f);
            float tDeltaY = 1f / Mathf.Max(Mathf.Abs(d.y), 1e-5f);
            float fx = p.x - cellX;
            float fy = p.y - cellY;
            float tMaxX = (stepX > 0 ? 1f - fx : fx) * tDeltaX;
            float tMaxY = (stepY > 0 ? 1f - fy : fy) * tDeltaY;

            for (int i = 0; i < 32; i++)
            {
                float t;
                if (tMaxX < tMaxY)
                {
                    t = tMaxX;
                    tMaxX += tDeltaX;
                    cellX += stepX;
                }
                else
                {
                    t = tMaxY;
                    tMaxY += tDeltaY;
                    cellY += stepY;
                }

                if (t >= 1f) return true;
                if (cellX == endX && cellY == endY) return true;
                if (IsOccluder(cellX, cellY)) return false;
            }
            return true;
        }

        private bool IsOccluder(int x, int y)
        {
            if (_occluders == null) return false;
            if (x < 0 || y < 0 || x >= _width || y >= _height) return true;
            return _occluders[y * _width + x];
        }

        private void PushGlobals()
        {
            Vector2 origin = GridOrigin;
            int count = Mathf.Min(_lights.Count, MaxLights);
            for (int i = 0; i < MaxLights; i++)
            {
                if (i < count)
                {
                    var light = _lights[i];
                    Vector2 world = origin + light.Position;
                    _lightPos[i] = new Vector4(world.x, world.y, light.Radius, light.Intensity);
                    _lightColor[i] = new Vector4(light.Color.r, light.Color.g, light.Color.b, 1f);
                }
                else
                {
                    _lightPos[i] = Vector4.zero;
                    _lightColor[i] = Vector4.zero;
                }
            }
            for (int i = _shadowCount; i < MaxShadows; i++)
            {
                _shadowA[i] = Vector4.zero;
                _shadowB[i] = Vector4.zero;
            }

            Shader.SetGlobalFloat(EnabledId, _occluderTexture != null ? 1f : 0f);
            Shader.SetGlobalFloat(LightCountId, count);
            Shader.SetGlobalVectorArray(LightPosId, _lightPos);
            Shader.SetGlobalVectorArray(LightColorId, _lightColor);
            Shader.SetGlobalFloat(ShadowCountId, _shadowCount);
            Shader.SetGlobalVectorArray(ShadowAId, _shadowA);
            Shader.SetGlobalVectorArray(ShadowBId, _shadowB);
            if (_occluderTexture != null)
            {
                Shader.SetGlobalTexture(OccluderTexId, _occluderTexture);
                Shader.SetGlobalVector(OccluderSizeId,
                    new Vector4(_width, _height, 1f / Mathf.Max(1, _width), 1f / Mathf.Max(1, _height)));
            }
            Shader.SetGlobalVector(GridOriginId, new Vector4(origin.x, origin.y, 0f, 0f));
        }

        private void OnDisable()
        {
            if (_active == this)
            {
                Shader.SetGlobalFloat(EnabledId, 0f);
                Shader.SetGlobalFloat(LightCountId, 0f);
                Shader.SetGlobalFloat(ShadowCountId, 0f);
                Shader.SetGlobalVector(OccluderSizeId, Vector4.zero);
            }
        }

        private void OnDestroy()
        {
            if (_active == this)
            {
                _active = null;
                Shader.SetGlobalFloat(EnabledId, 0f);
                Shader.SetGlobalFloat(LightCountId, 0f);
                Shader.SetGlobalFloat(ShadowCountId, 0f);
                Shader.SetGlobalVector(OccluderSizeId, Vector4.zero);
            }

            if (_occluderTexture != null)
            {
                if (Application.isPlaying) Destroy(_occluderTexture);
                else DestroyImmediate(_occluderTexture);
                _occluderTexture = null;
            }
        }
    }
}
