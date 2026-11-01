using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Runtime uGUI overlay: level label, status line, controls hint and the bottom history
    /// panel with seven slots for the player and seven for the zombie.
    /// </summary>
    public sealed class HUDController : MonoBehaviour
    {
        private const int SlotCount = 6;

        private static readonly Color ZombieHighlightColor = new Color(0.70f, 0.25f, 0.25f, 1f);
        private static readonly Color ZombieBoxBgColor = new Color(0.290f, 0.125f, 0.125f, 0.90f);
        private static readonly Color ZombieBoxBorderColor = new Color(0.45f, 0.20f, 0.20f, 0.95f);

        private GameTuning _tuning;
        private Font _font;
        private GameObject _canvasObject;

        private Text _levelLabel;
        private TextMeshProUGUI _concienciaLabel;
        private Image _playerIconImage;
        private Image _zombieIconImage;
        private Text _statusLabel;
        private Text _hintLabel;
        private Text _infectionBanner;

        private GameObject _tooltipRoot;
        private Text _tooltipSeedLabel;
        private Text _tooltipValidationLabel;

        private readonly Image[] _playerSlotBorders = new Image[SlotCount];
        private readonly Image[] _playerSlotBackgrounds = new Image[SlotCount];
        private readonly Image[] _playerArrows = new Image[SlotCount];
        private readonly Shadow[] _playerArrowShadows = new Shadow[SlotCount];

        private readonly Image[] _zombieSlotBorders = new Image[SlotCount];
        private readonly Image[] _zombieSlotBackgrounds = new Image[SlotCount];
        private readonly Image[] _zombieArrows = new Image[SlotCount];
        private readonly Shadow[] _zombieArrowShadows = new Shadow[SlotCount];

        private GameObject _loadingRoot;
        private Text _loadingLabel;
        private string _loadingBase = "Generando nivel";
        private bool _loading;

        private int _playerConciencia;
        private int _zombieConciencia;
        private bool _infected;
        private int _playerHighlightedCount;
        private int _zombieHighlightedCount;

        public int PlayerHighlightedArrowCount => _playerHighlightedCount;
        public int ZombieHighlightedArrowCount => _zombieHighlightedCount;

        public void Build(GameTuning tuning, Camera uiCamera = null)
        {
            _tuning = tuning;
            _font = LoadFont();

            if (_canvasObject != null) Destroy(_canvasObject);

            EnsureEventSystem();

            _canvasObject = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = _canvasObject.GetComponent<Canvas>();

            var cam = ResolveCamera(uiCamera);
            if (cam != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 5f;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            canvas.sortingOrder = 100;

            var scaler = _canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var root = _canvasObject.GetComponent<RectTransform>();
            BuildTopLeft(root);
            BuildTooltip(root);
            BuildHint(root);
            BuildHistoryPanel(root);
            BuildInfectionBanner(root);
            BuildLoadingOverlay(root);
        }

        private static Camera ResolveCamera(Camera candidate)
        {
            if (candidate != null) return candidate;
            var main = Camera.main;
            if (main != null) return main;
            return Object.FindAnyObjectByType<Camera>();
        }

        private void BuildTopLeft(RectTransform root)
        {
            _levelLabel = CreateText(root, "Level Label", "", 30,
                _tuning.hudTextColor, TextAnchor.UpperLeft);
            _levelLabel.fontStyle = FontStyle.Bold;
            SetRect(_levelLabel.rectTransform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, -28f), new Vector2(300f, 40f));
        }

        private void BuildTooltip(RectTransform root)
        {
            var border = CreateImage(root, "Info Tooltip Border", new Color(0.42f, 0.18f, 0.18f, 0.90f));
            SetRect(border.rectTransform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-28f, -86f), new Vector2(460f, 124f));
            _tooltipRoot = border.gameObject;

            var panel = CreateImage(border.transform, "Info Tooltip Panel", new Color(0.290f, 0.125f, 0.125f, 0.88f));
            Stretch(panel.rectTransform, 2f);

            _tooltipSeedLabel = CreateText(panel.transform, "Tooltip Seed Label", "", 18,
                new Color(0.95f, 0.85f, 0.85f), TextAnchor.MiddleLeft);
            _tooltipSeedLabel.fontStyle = FontStyle.Bold;
            SetRect(_tooltipSeedLabel.rectTransform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -12f), new Vector2(-36f, 28f));

            _tooltipValidationLabel = CreateText(panel.transform, "Tooltip Validation Label", "", 16,
                new Color(0.88f, 0.78f, 0.78f), TextAnchor.MiddleLeft);
            SetRect(_tooltipValidationLabel.rectTransform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -42f), new Vector2(-36f, 26f));

            _statusLabel = CreateText(panel.transform, "Status Label", "", 15,
                new Color(0.80f, 0.70f, 0.70f), TextAnchor.MiddleLeft);
            SetRect(_statusLabel.rectTransform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -70f), new Vector2(-36f, 38f));

            _tooltipRoot.SetActive(false);
        }

        private void BuildHint(RectTransform root)
        {
            _hintLabel = CreateText(root, "Controls Hint",
                "WASD / ← ↑ ↓ →\nInfo (i)", 21,
                Color.Lerp(_tuning.hudTextColor, _tuning.hudBorderColor, 0.35f), TextAnchor.MiddleRight);
            _hintLabel.raycastTarget = true;
            SetRect(_hintLabel.rectTransform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-28f, -22f), new Vector2(400f, 56f));

            var infoHitArea = CreateRect(_hintLabel.transform, "Info (i) Area",
                new Vector2(1f, 0f), new Vector2(1f, 0.5f), new Vector2(1f, 0f),
                Vector2.zero, new Vector2(110f, 28f));
            var hitImg = infoHitArea.gameObject.AddComponent<Image>();
            hitImg.color = Color.clear;
            hitImg.raycastTarget = true;
            var trigger = infoHitArea.gameObject.AddComponent<InfoTooltipTrigger>();
            trigger.Initialize(_tooltipRoot);

            var hintTrigger = _hintLabel.gameObject.AddComponent<InfoTooltipTrigger>();
            hintTrigger.Initialize(_tooltipRoot);
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null) return;
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;

            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        private void BuildHistoryPanel(RectTransform root)
        {
            float hudHeight = Mathf.Clamp(_tuning.hudViewportFraction * 1080f, 156f, 280f);

            // Panel de fondo oscuro en la parte inferior con transparencia (~68% / alfa 175/255)
            var panel = CreateImage(root, "History Panel", new Color(0f, 0f, 0f, 175f / 255f));
            SetRect(panel.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(0f, hudHeight));

            BuildHalf(panel.transform, true);
            BuildHalf(panel.transform, false);

            BuildConcienciaLabel(root, hudHeight);
            BuildIcons(root);
        }

        private void BuildConcienciaLabel(RectTransform root, float hudHeight)
        {
            var go = new GameObject("Conciencia Label", typeof(RectTransform));
            go.transform.SetParent(root, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 121f);
            rt.sizeDelta = new Vector2(600f, 50f);

            _concienciaLabel = go.AddComponent<TextMeshProUGUI>();

            var fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF") ?? TMP_Settings.defaultFontAsset;
            if (fontAsset != null)
            {
                _concienciaLabel.font = fontAsset;
            }

            _concienciaLabel.fontSize = 36;
            _concienciaLabel.fontStyle = FontStyles.Bold;
            _concienciaLabel.alignment = TextAlignmentOptions.Center;
            _concienciaLabel.color = Color.white;
            _concienciaLabel.richText = true;
            _concienciaLabel.raycastTarget = false;

            go.transform.SetAsLastSibling();
        }

        private void BuildIcons(RectTransform root)
        {
            var pSprite = LoadIconSprite("iconoplayer");
            var zSprite = LoadIconSprite("iconozombi");

            var playerIconGo = new GameObject("Icono Player", typeof(RectTransform), typeof(Image));
            playerIconGo.transform.SetParent(root, false);
            var pRt = playerIconGo.GetComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0.5f, 0f);
            pRt.anchorMax = new Vector2(0.5f, 0f);
            pRt.pivot = new Vector2(0.5f, 0.5f);
            pRt.sizeDelta = new Vector2(36f, 36f);
            pRt.anchoredPosition = new Vector2(-88f, 78f);

            _playerIconImage = playerIconGo.GetComponent<Image>();
            _playerIconImage.sprite = pSprite;
            _playerIconImage.preserveAspect = true;
            _playerIconImage.raycastTarget = false;

            var zombieIconGo = new GameObject("Icono Zombi", typeof(RectTransform), typeof(Image));
            zombieIconGo.transform.SetParent(root, false);
            var zRt = zombieIconGo.GetComponent<RectTransform>();
            zRt.anchorMin = new Vector2(0.5f, 0f);
            zRt.anchorMax = new Vector2(0.5f, 0f);
            zRt.pivot = new Vector2(0.5f, 0.5f);
            zRt.sizeDelta = new Vector2(36f, 36f);
            zRt.anchoredPosition = new Vector2(88f, 78f);

            _zombieIconImage = zombieIconGo.GetComponent<Image>();
            _zombieIconImage.sprite = zSprite;
            _zombieIconImage.preserveAspect = true;
            _zombieIconImage.raycastTarget = false;

            playerIconGo.transform.SetAsLastSibling();
            zombieIconGo.transform.SetAsLastSibling();
        }

        private static Sprite LoadIconSprite(string name)
        {
            var resSprite = Resources.Load<Sprite>("Iconos/" + name) ?? Resources.Load<Sprite>(name);
            if (resSprite != null) return resSprite;

#if UNITY_EDITOR
            string editorPath = "Assets/Sprites/Iconos/" + name + ".png";
            var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(editorPath);
            if (assets != null)
            {
                foreach (var a in assets)
                {
                    if (a is Sprite s) return s;
                }
            }
#endif

            string[] candidatePaths = {
                Path.Combine(Application.dataPath, "Sprites/Iconos", name + ".png"),
                Path.Combine(Directory.GetCurrentDirectory(), "Assets/Sprites/Iconos", name + ".png"),
                Path.Combine(Application.dataPath, "Resources/Iconos", name + ".png")
            };
            foreach (var p in candidatePaths)
            {
                if (File.Exists(p))
                {
                    byte[] bytes = File.ReadAllBytes(p);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    tex.filterMode = FilterMode.Point;
                    if (tex.LoadImage(bytes))
                    {
                        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 16f);
                        sprite.name = name;
                        return sprite;
                    }
                }
            }
            return null;
        }

        private void UpdateIconPositions()
        {
            if (_concienciaLabel == null) return;

            float playerX = -88f;
            float zombieX = 88f;

            try
            {
                _concienciaLabel.ForceMeshUpdate();
                var textInfo = _concienciaLabel.textInfo;
                if (textInfo != null && textInfo.characterCount > 0)
                {
                    int firstVisible = -1;
                    int lastVisible = -1;
                    for (int i = 0; i < textInfo.characterCount; i++)
                    {
                        if (textInfo.characterInfo[i].isVisible)
                        {
                            if (firstVisible == -1) firstVisible = i;
                            lastVisible = i;
                        }
                    }

                    if (firstVisible >= 0 && lastVisible >= 0 && firstVisible != lastVisible)
                    {
                        var charP = textInfo.characterInfo[firstVisible];
                        var charZ = textInfo.characterInfo[lastVisible];
                        playerX = (charP.bottomLeft.x + charP.topRight.x) * 0.5f;
                        zombieX = (charZ.bottomLeft.x + charZ.topRight.x) * 0.5f;
                    }
                }
            }
            catch
            {
                // Fallback to precomputed default offsets
            }

            const float exactIconY = 78f;

            if (_playerIconImage != null)
            {
                _playerIconImage.rectTransform.anchoredPosition = new Vector2(playerX, exactIconY);
                _playerIconImage.transform.SetAsLastSibling();
            }

            if (_zombieIconImage != null)
            {
                _zombieIconImage.rectTransform.anchoredPosition = new Vector2(zombieX, exactIconY);
                _zombieIconImage.transform.SetAsLastSibling();
            }
        }

        private void BuildHalf(Transform parent, bool player)
        {
            var half = CreateRect(parent, player ? "Player Half" : "Zombie Half",
                player ? new Vector2(0f, 0f) : new Vector2(0.5f, 0f),
                player ? new Vector2(0.5f, 1f) : new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            const float slotSize = 64f;
            const float gap = 10f;
            float totalWidth = SlotCount * slotSize + (SlotCount - 1) * gap;

            var slots = CreateRect(half, player ? "Player Slots" : "Zombie Slots",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(totalWidth, slotSize));

            for (int i = 0; i < SlotCount; i++)
            {
                var slotBorder = CreateImage(slots, "Slot Border " + i,
                    player ? _tuning.hudBorderColor : ZombieBoxBorderColor);
                SetRect(slotBorder.rectTransform,
                    new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(i * (slotSize + gap), 0f), new Vector2(slotSize, slotSize));

                var background = CreateImage(slotBorder.transform, "Slot " + i,
                    player ? _tuning.historySlotColor : ZombieBoxBgColor);
                Stretch(background.rectTransform, 2f);

                var arrow = CreateImage(background.transform, "Arrow " + i,
                    player ? _tuning.playerHistoryColor : ZombieHighlightColor);
                arrow.sprite = PixelArtFactory.ArrowSprite;
                arrow.preserveAspect = true;
                arrow.enabled = false;
                SetRect(arrow.rectTransform,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(48f, 48f));

                var shadow = arrow.gameObject.AddComponent<Shadow>();
                shadow.effectDistance = Vector2.zero;
                shadow.useGraphicAlpha = true;
                shadow.enabled = false;

                if (player)
                {
                    _playerSlotBorders[i] = slotBorder;
                    _playerSlotBackgrounds[i] = background;
                    _playerArrows[i] = arrow;
                    _playerArrowShadows[i] = shadow;
                }
                else
                {
                    _zombieSlotBorders[i] = slotBorder;
                    _zombieSlotBackgrounds[i] = background;
                    _zombieArrows[i] = arrow;
                    _zombieArrowShadows[i] = shadow;
                }
            }
        }

        private void BuildLoadingOverlay(RectTransform root)
        {
            var overlay = CreateImage(root, "Loading Overlay", Color.black);
            Stretch(overlay.rectTransform, 0f);
            _loadingRoot = overlay.gameObject;

            _loadingLabel = CreateText(overlay.transform, "Loading Label", _loadingBase, 34,
                _tuning.hudTextColor, TextAnchor.MiddleCenter);
            _loadingLabel.fontStyle = FontStyle.Bold;
            SetRect(_loadingLabel.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(800f, 60f));

            _loadingRoot.SetActive(false);
        }

        private void BuildInfectionBanner(RectTransform root)
        {
            _infectionBanner = CreateText(root, "Infection Banner", "Oops, me infectó", 64,
                _tuning.hudTextColor, TextAnchor.MiddleCenter);
            _infectionBanner.fontStyle = FontStyle.Bold;
            SetRect(_infectionBanner.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1200f, 100f));
            _infectionBanner.transform.SetAsLastSibling();
            _infectionBanner.gameObject.SetActive(false);
        }

        public void ShowInfectionBanner(bool show, string message = "Oops, me infectó")
        {
            if (_infectionBanner != null)
            {
                if (show && !string.IsNullOrEmpty(message))
                {
                    _infectionBanner.text = message;
                }
                _infectionBanner.gameObject.SetActive(show);
                if (show)
                {
                    _infectionBanner.transform.SetAsLastSibling();
                }
            }
        }

        public void ShowVictoryBanner(string message = "bien")
        {
            ShowInfectionBanner(true, message);
        }

        public void HideVictoryBanner()
        {
            ShowInfectionBanner(false);
        }

        public void ShowDefeatBanner(string message = "mal")
        {
            ShowInfectionBanner(true, message);
        }

        public void HideDefeatBanner()
        {
            ShowInfectionBanner(false);
        }

        public void SetLevelInfo(LevelDefinition level)
        {
            if (level == null) return;
            _playerConciencia = level.PlayerConciencia;
            _zombieConciencia = level.ZombieConciencia;
            _infected = false;

            if (_levelLabel != null)
            {
                _levelLabel.text = "NIVEL " + level.LevelIndex;
            }

            if (_tooltipSeedLabel != null)
            {
                _tooltipSeedLabel.text = "Semilla: " + level.Seed;
            }

            if (_tooltipValidationLabel != null)
            {
                if (level.UsedFallback)
                {
                    _tooltipValidationLabel.text = "Nivel de emergencia";
                }
                else if (level.UniqueConfirmed)
                {
                    _tooltipValidationLabel.text = "Solución única validada";
                }
                else if (level.SolutionCount > 1)
                {
                    _tooltipValidationLabel.text = "Solución no única (" + level.SolutionCount + ")";
                }
                else
                {
                    _tooltipValidationLabel.text = "Solución validada";
                }
            }

            SetConciencia(level.PlayerConciencia, level.ZombieConciencia);
            UpdateEntityTitles();
        }

        public void SetConciencia(int playerConciencia, int zombiConciencia)
        {
            _playerConciencia = playerConciencia;
            _zombieConciencia = zombiConciencia;
            if (_concienciaLabel != null)
            {
                var rt = _concienciaLabel.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, 121f);

                _concienciaLabel.text = "<color=#4A90E2>" + playerConciencia + "</color>          |          <color=#E24A4A>" + zombiConciencia + "</color>";
                _concienciaLabel.gameObject.SetActive(true);
                _concienciaLabel.transform.SetAsLastSibling();
                UpdateIconPositions();
            }
        }

        public void SetStatus(string text)
        {
            if (_statusLabel != null && text != null)
            {
                string display = text;
                if (display.StartsWith("Solución única validada · "))
                {
                    display = display.Substring("Solución única validada · ".Length);
                }
                else if (display.StartsWith("Nivel de emergencia · "))
                {
                    display = display.Substring("Nivel de emergencia · ".Length);
                }
                else if (display.StartsWith("Solución no única"))
                {
                    int dot = display.IndexOf(" · ");
                    if (dot >= 0) display = display.Substring(dot + 3);
                }
                _statusLabel.text = display;
            }
        }

        public void SetInfected(bool infected)
        {
            _infected = infected;
            UpdateEntityTitles();
        }

        private void UpdateEntityTitles()
        {
        }

        public void SetHistories(IReadOnlyList<Direction> playerHistory,
            IReadOnlyList<Direction> zombieHistory)
        {
            _playerHighlightedCount = UpdateSlots(
                _playerArrows, _playerSlotBackgrounds, _playerSlotBorders, _playerArrowShadows, playerHistory,
                _tuning.playerHistoryColor, _tuning.playerHistoryHighlightColor,
                _playerConciencia, true);
            _zombieHighlightedCount = UpdateSlots(
                _zombieArrows, _zombieSlotBackgrounds, _zombieSlotBorders, _zombieArrowShadows, zombieHistory,
                _tuning.zombieHistoryColor, ZombieHighlightColor,
                _zombieConciencia, false);
        }

        private int UpdateSlots(Image[] arrows, Image[] backgrounds, Image[] borders, Shadow[] shadows,
            IReadOnlyList<Direction> history, Color arrowColor, Color highlightColor,
            int highlightCount, bool isPlayer)
        {
            int count = history != null ? history.Count : 0;
            int start = Mathf.Max(0, count - SlotCount);
            int visible = count - start;
            int firstSlot = SlotCount - visible;
            int highlighted = Mathf.Clamp(highlightCount, 0, SlotCount);

            for (int i = 0; i < SlotCount; i++)
            {
                bool filled = i >= firstSlot && i - firstSlot < visible;
                if (filled)
                {
                    int indexInHistory = start + (i - firstSlot);
                    bool isHighlighted = indexInHistory >= count - highlighted;
                    var direction = history[indexInHistory];

                    arrows[i].enabled = true;
                    arrows[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, AngleFor(direction));

                    if (isHighlighted)
                    {
                        // Flecha activa (con cuadro): conserva cuadro, relleno sólido y brillo sutil
                        arrows[i].sprite = PixelArtFactory.ArrowSprite;
                        arrows[i].color = isPlayer ? highlightColor : ZombieHighlightColor;

                        borders[i].enabled = true;
                        borders[i].color = isPlayer ? _tuning.hudBorderColor : ZombieBoxBorderColor;

                        backgrounds[i].enabled = true;
                        backgrounds[i].color = isPlayer
                            ? Color.Lerp(_tuning.historySlotColor, highlightColor, 0.24f)
                            : ZombieBoxBgColor;

                        shadows[i].enabled = true;
                        shadows[i].effectDistance = Vector2.zero;
                        shadows[i].effectColor = new Color(arrows[i].color.r, arrows[i].color.g, arrows[i].color.b, 0.40f);
                    }
                    else
                    {
                        // Flecha inactiva (gastada): solo contorno hueco, sin cuadro contenedor, sin relleno, sin brillo
                        arrows[i].sprite = PixelArtFactory.ArrowHollowSprite;
                        arrows[i].color = isPlayer
                            ? new Color(0.35f, 0.65f, 0.85f, 0.55f)
                            : new Color(0.65f, 0.28f, 0.28f, 0.55f);

                        borders[i].enabled = false;
                        backgrounds[i].enabled = false;
                        shadows[i].enabled = false;
                    }
                }
                else
                {
                    // Casilla vacía (sin turno en el historial)
                    arrows[i].enabled = false;
                    borders[i].enabled = false;
                    backgrounds[i].enabled = false;
                    shadows[i].enabled = false;
                }
            }

            return Mathf.Min(visible, highlighted);
        }

        private static float AngleFor(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return 0f;
                case Direction.Right: return -90f;
                case Direction.Down: return 180f;
                case Direction.Left: return 90f;
                default: return 0f;
            }
        }

        public void SetLoading(bool loading, string text = null)
        {
            _loading = loading;
            if (_loadingRoot != null) _loadingRoot.SetActive(loading);
            if (loading)
            {
                _loadingBase = string.IsNullOrEmpty(text) ? "Generando nivel" : text;
                if (_loadingLabel != null) _loadingLabel.text = _loadingBase;
            }
        }

        private void Update()
        {
            if (_canvasObject != null)
            {
                var canvas = _canvasObject.GetComponent<Canvas>();
                if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
                {
                    canvas.worldCamera = ResolveCamera(null);
                }
            }

            if (!_loading || _loadingLabel == null) return;
            int dots = 1 + (int)(Time.unscaledTime * 2.5f) % 3;
            _loadingLabel.text = _loadingBase + new string('.', dots);
        }

        private Text CreateText(Transform parent, string objectName, string content, int fontSize,
            Color color, TextAnchor alignment)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);

            var text = textObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.text = content;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = new Vector2(1f, -1f);

            return text;
        }

        private static Image CreateImage(Transform parent, string objectName, Color color)
        {
            var imageObject = new GameObject(objectName, typeof(RectTransform));
            imageObject.transform.SetParent(parent, false);

            var image = imageObject.AddComponent<Image>();
            image.sprite = PixelArtFactory.WhiteSprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform CreateRect(Transform parent, string objectName,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var rectObject = new GameObject(objectName, typeof(RectTransform));
            rectObject.transform.SetParent(parent, false);
            var rect = rectObject.GetComponent<RectTransform>();
            SetRect(rect, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
            return rect;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static Font LoadFont()
        {
            Font font = null;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch { /* Fall through to the next option. */ }

            if (font == null)
            {
                try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                catch { /* Fall through to an OS font. */ }
            }

            if (font == null)
            {
                try { font = Font.CreateDynamicFontFromOSFont("Arial", 16); }
                catch { /* Text will simply not render without a font. */ }
            }

            return font;
        }

        private void OnDestroy()
        {
            if (_canvasObject != null) Destroy(_canvasObject);
        }
    }
}
