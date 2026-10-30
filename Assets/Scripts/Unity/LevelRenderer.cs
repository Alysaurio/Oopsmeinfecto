using System.Collections.Generic;
using UnityEngine;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Builds and animates the provisional pixel-art view of a level. The renderer only reads
    /// <see cref="GameState"/>; it never changes gameplay state.
    /// </summary>
    public sealed class LevelRenderer : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private Transform _sceneContentRoot;

        private GameTuning _tuning;
        private LevelDefinition _level;
        private Camera _camera;
        private Transform _contentRoot;
        private SpriteRenderer _playerRenderer;
        private SpriteRenderer _zombieRenderer;
        private SpriteRenderer _switchRenderer;
        private readonly List<SpriteRenderer> _damageRenderers = new List<SpriteRenderer>();
        private SegmentedEnergyBar _energyBar;

        private Vector3 _playerTarget;
        private Vector3 _zombieTarget;
        private bool _hasState;
        private float _pulseTime;
        private int _lastScreenWidth;
        private int _lastScreenHeight;

        public SegmentedEnergyBar EnergyBar => _energyBar;

        public void Clear()
        {
            if (_contentRoot != null && _contentRoot != _sceneContentRoot)
            {
                _contentRoot.gameObject.SetActive(false);
                Destroy(_contentRoot.gameObject);
            }

            if (_sceneContentRoot != null)
            {
                for (int i = _sceneContentRoot.childCount - 1; i >= 0; i--)
                {
                    var child = _sceneContentRoot.GetChild(i).gameObject;
                    child.SetActive(false);
                    Destroy(child);
                }
            }

            _contentRoot = null;
            _playerRenderer = null;
            _zombieRenderer = null;
            _switchRenderer = null;
            _damageRenderers.Clear();
            _energyBar = null;
            _hasState = false;
        }

        public void Build(LevelDefinition level, GameTuning tuning, Camera camera)
        {
            Clear();
            _level = level;
            _tuning = tuning;
            _camera = camera;

            _contentRoot = _sceneContentRoot != null
                ? _sceneContentRoot
                : new GameObject("Level Content").transform;
            _contentRoot.SetParent(transform, false);
            _contentRoot.localPosition = Vector3.zero;
            _contentRoot.gameObject.SetActive(true);

            BuildBoard();
            BuildEntities();
            FitCamera(true);

            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
        }

        private void BuildBoard()
        {
            var boardRoot = new GameObject("Board").transform;
            boardRoot.SetParent(_contentRoot, false);

            for (int y = 0; y < _level.Height; y++)
            {
                for (int x = 0; x < _level.Width; x++)
                {
                    var position = new GridPos(x, y);
                    var tile = _level.Board.GetTile(position);
                    var tileObject = new GameObject("Tile " + x + "," + y);
                    tileObject.transform.SetParent(boardRoot, false);
                    tileObject.transform.localPosition = new Vector3(x, y, 0f);

                    var renderer = tileObject.AddComponent<SpriteRenderer>();
                    renderer.sortingOrder = 0;
                    if (PixelArtFactory.SpriteMaterial != null)
                    {
                        renderer.sharedMaterial = PixelArtFactory.SpriteMaterial;
                    }

                    if (tile == TileType.Wall)
                    {
                        renderer.sprite = PixelArtFactory.WallSprite;
                        renderer.color = _tuning.wallColor;
                    }
                    else
                    {
                        renderer.sprite = ((x + y) & 1) == 0
                            ? PixelArtFactory.FloorA
                            : PixelArtFactory.FloorB;
                        renderer.color = ((x + y) & 1) == 0
                            ? _tuning.floorColorA
                            : _tuning.floorColorB;
                    }

                    if (tile == TileType.Damage)
                    {
                        var damage = CreateOverlay(tileObject.transform, "Damage",
                            PixelArtFactory.DamageSprite, _tuning.damageColor, 2);
                        _damageRenderers.Add(damage);
                    }
                    else if (tile == TileType.Switch)
                    {
                        _switchRenderer = CreateOverlay(tileObject.transform, "Switch",
                            PixelArtFactory.SwitchSprite, _tuning.switchColor, 3);
                    }
                }
            }
        }

        private static SpriteRenderer CreateOverlay(Transform parent, string objectName, Sprite sprite,
            Color color, int sortingOrder)
        {
            var overlayObject = new GameObject(objectName);
            overlayObject.transform.SetParent(parent, false);
            overlayObject.transform.localPosition = Vector3.zero;

            var renderer = overlayObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            if (PixelArtFactory.SpriteMaterial != null)
            {
                renderer.sharedMaterial = PixelArtFactory.SpriteMaterial;
            }

            return renderer;
        }

        private void BuildEntities()
        {
            _zombieRenderer = CreateEntity("Zombie", PixelArtFactory.ZombieSprite, 11,
                new Vector3(_level.ZombieStart.X, _level.ZombieStart.Y, 0f));
            _playerRenderer = CreateEntity("Player", PixelArtFactory.PlayerSprite, 12,
                new Vector3(_level.PlayerStart.X, _level.PlayerStart.Y, 0f));

            var energyObject = new GameObject("Energy Bar");
            energyObject.transform.SetParent(_contentRoot, false);
            _energyBar = energyObject.AddComponent<SegmentedEnergyBar>();
            _energyBar.Initialize(_tuning, _level.MaxEnergy, _playerRenderer.transform);

            _playerTarget = _playerRenderer.transform.position;
            _zombieTarget = _zombieRenderer.transform.position;
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
            if (PixelArtFactory.SpriteMaterial != null) renderer.sharedMaterial = PixelArtFactory.SpriteMaterial;
            return renderer;
        }

        public void SyncFromState(GameState state, bool instant = false)
        {
            if (state == null || _playerRenderer == null || _zombieRenderer == null) return;

            _playerTarget = new Vector3(state.Player.Position.X, state.Player.Position.Y, 0f);
            _zombieTarget = new Vector3(state.Zombie.Position.X, state.Zombie.Position.Y, 0f);
            _hasState = true;

            if (instant)
            {
                _playerRenderer.transform.position = _playerTarget;
                _zombieRenderer.transform.position = _zombieTarget;
            }

            bool infected = state.Player.Infected || state.Phase == GamePhase.InfectedPlayback;
            _playerRenderer.color = infected
                ? Color.Lerp(Color.white, _tuning.infectedDesaturation, 0.65f)
                : Color.white;

            if (_energyBar != null)
            {
                _energyBar.SetEnergy(state.CurrentEnergy, state.MaxEnergy);
                _energyBar.SetInfected(infected);
            }
        }

        public void PlayDrain(float duration)
        {
            if (_energyBar != null) _energyBar.PlayDrain(duration);
        }

        public void FitCamera(bool force)
        {
            if (_camera == null || _level == null || _tuning == null) return;
            if (!force && Screen.width == _lastScreenWidth && Screen.height == _lastScreenHeight) return;

            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;

            float hudFraction = Mathf.Clamp(_tuning.hudViewportFraction, 0.10f, 0.30f);
            _camera.rect = new Rect(0f, hudFraction, 1f, 1f - hudFraction);
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = _tuning.cameraBackground;

            float margin = Mathf.Max(0.5f, _tuning.cameraMargin);
            float boardWidth = Mathf.Max(1f, _level.Width - 1f + margin * 2f);
            float boardHeight = Mathf.Max(1f, _level.Height - 1f + margin * 2f);

            float viewportWidth = Mathf.Max(1f, Screen.width);
            float viewportHeight = Mathf.Max(1f, Screen.height * (1f - hudFraction));
            float viewAspect = viewportWidth / viewportHeight;

            float verticalSize = boardHeight * 0.5f;
            float horizontalSize = boardWidth / (2f * Mathf.Max(0.01f, viewAspect));
            _camera.orthographicSize = Mathf.Max(verticalSize, horizontalSize);

            _camera.transform.position = new Vector3(
                (_level.Width - 1f) * 0.5f,
                (_level.Height - 1f) * 0.5f,
                -10f);
        }

        private void Update()
        {
            _pulseTime += Time.unscaledDeltaTime;

            float damagePulse = 1f + Mathf.Sin(_pulseTime * 3.2f) * 0.045f;
            for (int i = 0; i < _damageRenderers.Count; i++)
            {
                var renderer = _damageRenderers[i];
                if (renderer == null) continue;
                renderer.transform.localScale = new Vector3(damagePulse, damagePulse, 1f);
                renderer.color = Color.Lerp(_tuning.damageColor, Color.white,
                    0.15f + Mathf.Sin(_pulseTime * 3.2f) * 0.10f);
            }

            if (_switchRenderer != null)
            {
                float switchPulse = 1f + Mathf.Sin(_pulseTime * 2.4f) * 0.055f;
                _switchRenderer.transform.localScale = new Vector3(switchPulse, switchPulse, 1f);
                _switchRenderer.color = Color.Lerp(_tuning.switchColor, Color.white,
                    0.20f + Mathf.Sin(_pulseTime * 2.4f) * 0.12f);
            }

            if (_hasState && _playerRenderer != null && _zombieRenderer != null)
            {
                float blend = 1f - Mathf.Exp(-26f * Time.unscaledDeltaTime);
                _playerRenderer.transform.position =
                    Vector3.Lerp(_playerRenderer.transform.position, _playerTarget, blend);
                _zombieRenderer.transform.position =
                    Vector3.Lerp(_zombieRenderer.transform.position, _zombieTarget, blend);
            }

            FitCamera(false);
        }

        private void OnDestroy()
        {
            Clear();
        }
    }
}
