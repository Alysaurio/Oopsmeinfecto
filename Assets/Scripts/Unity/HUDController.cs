using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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

        public static readonly Color ZombieHighlightColor = new Color(0.70f, 0.25f, 0.25f, 1f);
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
        private Text _escHintLabel;
        private Text _infectionBanner;

        private GameObject _menuWindowRoot;
        private GameObject _confirmWindowRoot;
        private Slider _volumeSlider;
        private bool _isPaused;

        public bool IsPaused => _isPaused;
        public static bool IsPauseMenuOpen { get; private set; }

        private GameObject _tooltipRoot;
        private Text _tooltipSeedLabel;
        private Text _tooltipValidationLabel;

        private readonly Image[] _playerSlotBorders = new Image[SlotCount];
        private readonly Image[] _playerSlotBackgrounds = new Image[SlotCount];
        private readonly Image[] _playerBoxGlows = new Image[SlotCount];
        private readonly Outline[] _playerBoxOutlines = new Outline[SlotCount];
        private readonly Image[] _playerArrowGlows = new Image[SlotCount];
        private readonly Image[] _playerArrows = new Image[SlotCount];
        private readonly Outline[] _playerArrowShadows = new Outline[SlotCount];

        private readonly Image[] _zombieSlotBorders = new Image[SlotCount];
        private readonly Image[] _zombieSlotBackgrounds = new Image[SlotCount];
        private readonly Image[] _zombieBoxGlows = new Image[SlotCount];
        private readonly Outline[] _zombieBoxOutlines = new Outline[SlotCount];
        private readonly Image[] _zombieArrowGlows = new Image[SlotCount];
        private readonly Image[] _zombieArrows = new Image[SlotCount];
        private readonly Outline[] _zombieArrowShadows = new Outline[SlotCount];

        private static Sprite _cachedArrowGlowSprite;
        private static Sprite _cachedBoxGlowSprite;
        private bool _pulseActive;
        private bool _pulseIsPlayer;
        private int _pulseSlotIndex = -1;
        private float _pulseElapsed;
        private float _pulseDuration = 0.26f;
        private const float PulseBoxMaxScale = 1.25f;
        private const float PulseArrowLocalMaxScale = 1.16f; // 1.25f * 1.16f = 1.45f escala visual combinada
        private const float PulseMaxGlowAlpha = 0.40f;

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
            BuildPauseMenu(root);
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
                new Vector2(-28f, -114f), new Vector2(460f, 124f));
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
            Color hintColor = Color.Lerp(_tuning.hudTextColor, _tuning.hudBorderColor, 0.35f);

            _hintLabel = CreateText(root, "Controls Hint",
                "WASD / ← ↑ ↓ →\nInfo (i)", 21,
                hintColor, TextAnchor.MiddleRight);
            _hintLabel.raycastTarget = true;
            SetRect(_hintLabel.rectTransform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-28f, -22f), new Vector2(400f, 56f));

            // Justo debajo de "info (i)", añade un texto con exactamente la misma fuente, estilo y color: "Esc - menú"
            _escHintLabel = CreateText(root, "Esc Menu Hint",
                "Esc - menú", _hintLabel.fontSize,
                _hintLabel.color, TextAnchor.MiddleRight);
            _escHintLabel.font = _hintLabel.font;
            _escHintLabel.fontStyle = _hintLabel.fontStyle;
            SetRect(_escHintLabel.rectTransform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-28f, -78f), new Vector2(400f, 28f));

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
                    new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f),
                    new Vector2(i * (slotSize + gap) + slotSize * 0.5f, slotSize * 0.5f), new Vector2(slotSize, slotSize));

                var boxOutline = slotBorder.gameObject.AddComponent<Outline>();
                boxOutline.effectDistance = new Vector2(2.5f, 2.5f);
                boxOutline.useGraphicAlpha = true;
                boxOutline.enabled = false;

                var boxGlow = CreateImage(slotBorder.transform, "Box Glow " + i,
                    player ? _tuning.playerHistoryHighlightColor : ZombieHighlightColor);
                boxGlow.sprite = GetOrCreateBoxGlowSprite();
                boxGlow.preserveAspect = true;
                boxGlow.raycastTarget = false;
                boxGlow.enabled = false;
                SetRect(boxGlow.rectTransform,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(84f, 84f));

                var background = CreateImage(slotBorder.transform, "Slot " + i,
                    player ? _tuning.historySlotColor : ZombieBoxBgColor);
                Stretch(background.rectTransform, 2f);

                var arrowGlow = CreateImage(background.transform, "Arrow Glow " + i,
                    player ? _tuning.playerHistoryHighlightColor : ZombieHighlightColor);
                arrowGlow.sprite = GetOrCreateArrowGlowSprite();
                arrowGlow.preserveAspect = true;
                arrowGlow.raycastTarget = false;
                arrowGlow.enabled = false;
                SetRect(arrowGlow.rectTransform,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(58f, 58f));

                var arrow = CreateImage(background.transform, "Arrow " + i,
                    player ? _tuning.playerHistoryColor : ZombieHighlightColor);
                arrow.sprite = PixelArtFactory.ArrowSprite;
                arrow.preserveAspect = true;
                arrow.enabled = false;
                SetRect(arrow.rectTransform,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(48f, 48f));

                var shadow = arrow.gameObject.AddComponent<Outline>();
                shadow.effectDistance = new Vector2(2f, 2f);
                shadow.useGraphicAlpha = true;
                shadow.enabled = false;

                if (player)
                {
                    _playerSlotBorders[i] = slotBorder;
                    _playerSlotBackgrounds[i] = background;
                    _playerBoxGlows[i] = boxGlow;
                    _playerBoxOutlines[i] = boxOutline;
                    _playerArrowGlows[i] = arrowGlow;
                    _playerArrows[i] = arrow;
                    _playerArrowShadows[i] = shadow;
                }
                else
                {
                    _zombieSlotBorders[i] = slotBorder;
                    _zombieSlotBackgrounds[i] = background;
                    _zombieBoxGlows[i] = boxGlow;
                    _zombieBoxOutlines[i] = boxOutline;
                    _zombieArrowGlows[i] = arrowGlow;
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
                if (show)
                {
                    if (!string.IsNullOrEmpty(message))
                    {
                        _infectionBanner.text = message;
                    }
                    // Asegura que "Generando nivel..." jamás coincida en pantalla mientras el banner sea visible
                    if (_loadingRoot != null)
                    {
                        _loadingRoot.SetActive(false);
                    }
                    _loading = false;

                    _infectionBanner.gameObject.SetActive(true);
                    _infectionBanner.transform.SetAsLastSibling();
                    KeepPauseMenuOnTop();
                }
                else
                {
                    _infectionBanner.text = "";
                    _infectionBanner.gameObject.SetActive(false);
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

        private void BuildPauseMenu(RectTransform root)
        {
            _isPaused = false;
            IsPauseMenuOpen = false;

            Font oopsFont = _infectionBanner != null && _infectionBanner.font != null ? _infectionBanner.font : _font;
            FontStyle oopsStyle = _infectionBanner != null ? _infectionBanner.fontStyle : FontStyle.Bold;

            Font nivelFont = _levelLabel != null && _levelLabel.font != null ? _levelLabel.font : _font;
            FontStyle nivelStyle = _levelLabel != null ? _levelLabel.fontStyle : FontStyle.Bold;

            Color windowBgColor = new Color(0f, 0f, 0f, 0.75f);

            // =========================================================
            // 1. VENTANA MENÚ
            // =========================================================
            var menuBg = CreateImage(root, "Ventana Menu", windowBgColor);
            menuBg.raycastTarget = true;
            SetRect(menuBg.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(560f, 430f));
            _menuWindowRoot = menuBg.gameObject;

            // Esquina superior derecha: Botón "X" en color rojo exacto de las flechas inferiores derechas del HUD (ZombieHighlightColor)
            var closeBtnImg = CreateImage(menuBg.transform, "Close Button X", new Color(0f, 0f, 0f, 0f));
            closeBtnImg.raycastTarget = true;
            SetRect(closeBtnImg.rectTransform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-14f, -14f), new Vector2(48f, 48f));
            var closeBtn = closeBtnImg.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeBtnImg;
            closeBtn.onClick.AddListener(ClosePauseMenuAndResume);

            var closeBtnText = CreateText(closeBtnImg.transform, "X Label", "X", 34,
                ZombieHighlightColor, TextAnchor.MiddleCenter);
            closeBtnText.font = nivelFont;
            closeBtnText.fontStyle = FontStyle.Bold;
            Stretch(closeBtnText.rectTransform, 0f);

            // 1) Título superior centrado horizontalmente: "Menú"
            var titleText = CreateText(menuBg.transform, "Menu Title", "Menú", 38,
                _tuning.hudTextColor, TextAnchor.MiddleCenter);
            titleText.font = nivelFont;
            titleText.fontStyle = FontStyle.Bold;
            SetRect(titleText.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f), new Vector2(360f, 52f));

            // 2) Control de Audio: "Volumen" a la izquierda + UI Slider horizontal (0 a 1) a su derecha
            var volRow = CreateRect(menuBg.transform, "Volume Row",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -120f), new Vector2(460f, 48f));

            var volLabel = CreateText(volRow, "Volume Label", "Volumen", 26,
                _tuning.hudTextColor, TextAnchor.MiddleLeft);
            volLabel.font = nivelFont;
            volLabel.fontStyle = FontStyle.Bold;
            SetRect(volLabel.rectTransform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(0f, 0f), new Vector2(140f, 40f));

            _volumeSlider = BuildVolumeSlider(volRow);

            // 3) Sección de Créditos con salto de línea exacto
            string creditsContent = "Créditos:\n- Aly Quispe Borda.\n- Anderson Estrada Barzola.\n\"Los muchanchos\"";
            var creditsText = CreateText(menuBg.transform, "Credits Label", creditsContent, 24,
                _tuning.hudTextColor, TextAnchor.MiddleCenter);
            creditsText.font = _font;
            creditsText.lineSpacing = 1.18f;
            SetRect(creditsText.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -22f), new Vector2(460f, 130f));

            // 4) Botón "Salir": Fondo verde muy desaturado (#4A6B53) con texto centrado "Salir"
            Color desaturatedGreen = new Color(0x4A / 255f, 0x6B / 255f, 0x53 / 255f, 1f);
            var exitBtnImg = CreateImage(menuBg.transform, "Salir Button", desaturatedGreen);
            exitBtnImg.raycastTarget = true;
            SetRect(exitBtnImg.rectTransform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 28f), new Vector2(200f, 54f));
            var exitBtn = exitBtnImg.gameObject.AddComponent<Button>();
            exitBtn.targetGraphic = exitBtnImg;
            exitBtn.onClick.AddListener(OpenConfirmationWindow);

            var exitBtnText = CreateText(exitBtnImg.transform, "Salir Label", "Salir", 28,
                _tuning.hudTextColor, TextAnchor.MiddleCenter);
            exitBtnText.font = nivelFont;
            exitBtnText.fontStyle = nivelStyle;
            Stretch(exitBtnText.rectTransform, 0f);

            // =========================================================
            // 2. VENTANA CONFIRMACIÓN (¿Salir?)
            // =========================================================
            var confirmBg = CreateImage(root, "Ventana Confirmacion", windowBgColor);
            confirmBg.raycastTarget = true;
            SetRect(confirmBg.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(640f, 300f));
            _confirmWindowRoot = confirmBg.gameObject;

            // Mensaje superior centrado con EXACTAMENTE la misma fuente que "Oops, me infectó"
            var confirmMsg = CreateText(confirmBg.transform, "Confirm Question",
                "¿Estás seguro que quieres salir?", 34,
                _tuning.hudTextColor, TextAnchor.MiddleCenter);
            confirmMsg.font = oopsFont;
            confirmMsg.fontStyle = oopsStyle;
            confirmMsg.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetRect(confirmMsg.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -44f), new Vector2(580f, 96f));

            // Color azul exacto de las flechas del HUD abajo izquierda con 75% de transparencia
            Color playerArrowBlue = _tuning != null ? _tuning.playerHistoryHighlightColor : new Color(0.30f, 0.65f, 1.00f, 1f);
            Color yesBgColor = new Color(playerArrowBlue.r, playerArrowBlue.g, playerArrowBlue.b, 0.75f);

            // Color rojo exacto de las flechas del HUD abajo derecha con 75% de transparencia
            Color noBgColor = new Color(ZombieHighlightColor.r, ZombieHighlightColor.g, ZombieHighlightColor.b, 0.75f);

            // Botón izquierdo: "Sí" (misma fuente que "NIVEL")
            var yesBtnImg = CreateImage(confirmBg.transform, "Yes Button", yesBgColor);
            yesBtnImg.raycastTarget = true;
            SetRect(yesBtnImg.rectTransform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-140f, 42f), new Vector2(220f, 58f));
            var yesBtn = yesBtnImg.gameObject.AddComponent<Button>();
            yesBtn.targetGraphic = yesBtnImg;
            yesBtn.onClick.AddListener(ConfirmExitGame);

            var yesBtnText = CreateText(yesBtnImg.transform, "Yes Label", "Sí", 28,
                _tuning.hudTextColor, TextAnchor.MiddleCenter);
            yesBtnText.font = nivelFont;
            yesBtnText.fontStyle = nivelStyle;
            Stretch(yesBtnText.rectTransform, 0f);

            // Botón derecho: "No, perdón" (misma fuente que "NIVEL")
            var noBtnImg = CreateImage(confirmBg.transform, "No Button", noBgColor);
            noBtnImg.raycastTarget = true;
            SetRect(noBtnImg.rectTransform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(140f, 42f), new Vector2(220f, 58f));
            var noBtn = noBtnImg.gameObject.AddComponent<Button>();
            noBtn.targetGraphic = noBtnImg;
            noBtn.onClick.AddListener(CancelExitAndReturnToMenu);

            var noBtnText = CreateText(noBtnImg.transform, "No Label", "No, perdón", 28,
                _tuning.hudTextColor, TextAnchor.MiddleCenter);
            noBtnText.font = nivelFont;
            noBtnText.fontStyle = nivelStyle;
            Stretch(noBtnText.rectTransform, 0f);

            _menuWindowRoot.SetActive(false);
            _confirmWindowRoot.SetActive(false);
        }

        private Slider BuildVolumeSlider(RectTransform parent)
        {
            var sliderRect = CreateRect(parent, "Volume Slider",
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(72f, 0f), new Vector2(-144f, 32f));

            var bgImg = CreateImage(sliderRect, "Background", new Color(0.18f, 0.20f, 0.24f, 0.95f));
            bgImg.raycastTarget = true;
            SetRect(bgImg.rectTransform,
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(0f, 12f));

            var fillArea = CreateRect(sliderRect, "Fill Area",
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(-12f, 12f));

            Color fillColor = _tuning != null ? _tuning.playerHistoryHighlightColor : new Color(0.30f, 0.65f, 1.00f, 1f);
            var fillImg = CreateImage(fillArea, "Fill", fillColor);
            Stretch(fillImg.rectTransform, 0f);

            var handleArea = CreateRect(sliderRect, "Handle Slide Area",
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(-12f, 0f));

            var handleImg = CreateImage(handleArea, "Handle", _tuning != null ? _tuning.hudTextColor : Color.white);
            handleImg.raycastTarget = true;
            SetRect(handleImg.rectTransform,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(18f, 28f));

            var slider = sliderRect.gameObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.fillRect = fillImg.rectTransform;
            slider.handleRect = handleImg.rectTransform;
            slider.targetGraphic = handleImg;

            float initialVol = AudioManager.Instance != null ? AudioManager.Instance.SliderVolume : 1f;
            slider.SetValueWithoutNotify(initialVol);
            slider.onValueChanged.AddListener(OnVolumeSliderChanged);

            return slider;
        }

        private void OnVolumeSliderChanged(float value)
        {
            AudioManager.Instance?.SetUserVolume(value);
        }

        private void HandleEscapeKey()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[Key.Escape].wasPressedThisFrame) return;

            bool confirmOpen = _confirmWindowRoot != null && _confirmWindowRoot.activeSelf;
            bool menuOpen = _menuWindowRoot != null && _menuWindowRoot.activeSelf;

            if (confirmOpen)
            {
                // 2. Si la 'Ventana Confirmación' está abierta al presionar Esc: ciérrala y regresa a la 'Ventana Menú' (manteniendo la pausa)
                CancelExitAndReturnToMenu();
            }
            else if (menuOpen)
            {
                // 3. Si la 'Ventana Menú' está abierta al presionar Esc: cierra la 'Ventana Menú', reanuda el juego y restaura el volumen
                ClosePauseMenuAndResume();
            }
            else
            {
                // 1. Si el menú está cerrado: Pausa el juego, abre la 'Ventana Menú' y atenúa la música al 20%
                OpenPauseMenu();
            }
        }

        public void OpenPauseMenu()
        {
            _isPaused = true;
            IsPauseMenuOpen = true;
            Time.timeScale = 0f;

            if (_volumeSlider != null && AudioManager.Instance != null)
            {
                _volumeSlider.SetValueWithoutNotify(AudioManager.Instance.SliderVolume);
            }

            if (_confirmWindowRoot != null) _confirmWindowRoot.SetActive(false);
            if (_menuWindowRoot != null)
            {
                _menuWindowRoot.SetActive(true);
                _menuWindowRoot.transform.SetAsLastSibling();
            }

            AudioManager.Instance?.SetPauseDucking(true, 0.25f);
        }

        public void ClosePauseMenuAndResume()
        {
            if (_confirmWindowRoot != null) _confirmWindowRoot.SetActive(false);
            if (_menuWindowRoot != null) _menuWindowRoot.SetActive(false);

            _isPaused = false;
            IsPauseMenuOpen = false;
            Time.timeScale = 1f;

            AudioManager.Instance?.SetPauseDucking(false, 0.25f);
        }

        private void OpenConfirmationWindow()
        {
            if (_menuWindowRoot != null) _menuWindowRoot.SetActive(false);
            if (_confirmWindowRoot != null)
            {
                _confirmWindowRoot.SetActive(true);
                _confirmWindowRoot.transform.SetAsLastSibling();
            }
        }

        private void CancelExitAndReturnToMenu()
        {
            if (_confirmWindowRoot != null) _confirmWindowRoot.SetActive(false);
            if (_menuWindowRoot != null)
            {
                _menuWindowRoot.SetActive(true);
                _menuWindowRoot.transform.SetAsLastSibling();
            }
        }

        private void ConfirmExitGame()
        {
            _isPaused = false;
            IsPauseMenuOpen = false;
            Time.timeScale = 1f;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
#else
            Application.Quit();
#endif
        }

        private void KeepPauseMenuOnTop()
        {
            if (_menuWindowRoot != null) _menuWindowRoot.transform.SetAsLastSibling();
            if (_confirmWindowRoot != null) _confirmWindowRoot.transform.SetAsLastSibling();
        }

        public void SetLevelInfo(LevelDefinition level)
        {
            if (level == null) return;
            _playerConciencia = level.PlayerConciencia;
            _zombieConciencia = level.ZombieConciencia;
            _infected = false;
            ResetAllArrowPulses();

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
            if (!infected)
            {
                ResetAllArrowPulses();
            }
            UpdateEntityTitles();
        }

        private void UpdateEntityTitles()
        {
        }

        /// <summary>
        /// Dispara el pulso diferencial de escala y brillo ligero sincronizado (alpha 0 -> 0.40 -> 0)
        /// tanto en el cuadro de fondo (1.0 -> 1.25 -> 1.0) como en la flecha interior (escala local 1.0 -> 1.16 -> 1.0,
        /// escala visual combinada ~1.45) correspondiente al paso actual de la secuencia de infección.
        /// </summary>
        public void PulsePlaybackArrow(int stepIndex, float duration = 0.26f)
        {
            ResetAllArrowPulses();

            if (stepIndex < 0) return;

            int zCount = Mathf.Clamp(_zombieHighlightedCount, 0, SlotCount);
            int pCount = Mathf.Clamp(_playerHighlightedCount, 0, SlotCount);

            if (stepIndex < zCount)
            {
                int slotIndex = (SlotCount - zCount) + stepIndex;
                if (slotIndex >= 0 && slotIndex < SlotCount)
                {
                    _pulseActive = true;
                    _pulseIsPlayer = false;
                    _pulseSlotIndex = slotIndex;
                    _pulseElapsed = 0f;
                    _pulseDuration = Mathf.Max(0.10f, duration);
                    if (_zombieSlotBorders[slotIndex] != null)
                    {
                        _zombieSlotBorders[slotIndex].transform.SetAsLastSibling();
                    }
                }
            }
            else
            {
                int playerOffset = stepIndex - zCount;
                if (playerOffset >= 0 && playerOffset < pCount)
                {
                    int slotIndex = (SlotCount - pCount) + playerOffset;
                    if (slotIndex >= 0 && slotIndex < SlotCount)
                    {
                        _pulseActive = true;
                        _pulseIsPlayer = true;
                        _pulseSlotIndex = slotIndex;
                        _pulseElapsed = 0f;
                        _pulseDuration = Mathf.Max(0.10f, duration);
                        if (_playerSlotBorders[slotIndex] != null)
                        {
                            _playerSlotBorders[slotIndex].transform.SetAsLastSibling();
                        }
                    }
                }
            }
        }

        public void ResetAllArrowPulses()
        {
            _pulseActive = false;
            _pulseSlotIndex = -1;
            _pulseElapsed = 0f;

            Color pColor = _tuning != null ? _tuning.playerHistoryHighlightColor : new Color(0.30f, 0.65f, 1.00f, 1f);
            Color zColor = ZombieHighlightColor;

            for (int i = 0; i < SlotCount; i++)
            {
                if (_playerSlotBorders[i] != null)
                {
                    _playerSlotBorders[i].rectTransform.localScale = Vector3.one;
                }
                if (_playerSlotBackgrounds[i] != null)
                {
                    _playerSlotBackgrounds[i].rectTransform.localScale = Vector3.one;
                }
                if (_playerBoxGlows[i] != null)
                {
                    _playerBoxGlows[i].rectTransform.localScale = Vector3.one;
                    _playerBoxGlows[i].color = new Color(pColor.r, pColor.g, pColor.b, 0f);
                    _playerBoxGlows[i].enabled = false;
                }
                if (_playerBoxOutlines[i] != null)
                {
                    _playerBoxOutlines[i].effectColor = new Color(pColor.r, pColor.g, pColor.b, 0f);
                    _playerBoxOutlines[i].enabled = false;
                }
                if (_playerArrows[i] != null)
                {
                    _playerArrows[i].rectTransform.localScale = Vector3.one;
                }
                if (_playerArrowGlows[i] != null)
                {
                    _playerArrowGlows[i].rectTransform.localScale = Vector3.one;
                    _playerArrowGlows[i].color = new Color(pColor.r, pColor.g, pColor.b, 0f);
                    _playerArrowGlows[i].enabled = false;
                }
                if (_playerArrowShadows[i] != null)
                {
                    _playerArrowShadows[i].effectColor = new Color(pColor.r, pColor.g, pColor.b, 0f);
                    _playerArrowShadows[i].enabled = false;
                }

                if (_zombieSlotBorders[i] != null)
                {
                    _zombieSlotBorders[i].rectTransform.localScale = Vector3.one;
                }
                if (_zombieSlotBackgrounds[i] != null)
                {
                    _zombieSlotBackgrounds[i].rectTransform.localScale = Vector3.one;
                }
                if (_zombieBoxGlows[i] != null)
                {
                    _zombieBoxGlows[i].rectTransform.localScale = Vector3.one;
                    _zombieBoxGlows[i].color = new Color(zColor.r, zColor.g, zColor.b, 0f);
                    _zombieBoxGlows[i].enabled = false;
                }
                if (_zombieBoxOutlines[i] != null)
                {
                    _zombieBoxOutlines[i].effectColor = new Color(zColor.r, zColor.g, zColor.b, 0f);
                    _zombieBoxOutlines[i].enabled = false;
                }
                if (_zombieArrows[i] != null)
                {
                    _zombieArrows[i].rectTransform.localScale = Vector3.one;
                }
                if (_zombieArrowGlows[i] != null)
                {
                    _zombieArrowGlows[i].rectTransform.localScale = Vector3.one;
                    _zombieArrowGlows[i].color = new Color(zColor.r, zColor.g, zColor.b, 0f);
                    _zombieArrowGlows[i].enabled = false;
                }
                if (_zombieArrowShadows[i] != null)
                {
                    _zombieArrowShadows[i].effectColor = new Color(zColor.r, zColor.g, zColor.b, 0f);
                    _zombieArrowShadows[i].enabled = false;
                }
            }
        }

        private void UpdateArrowPulse()
        {
            if (!_pulseActive || _pulseSlotIndex < 0 || _pulseSlotIndex >= SlotCount) return;

            if (!_isPaused && !IsPauseMenuOpen)
            {
                _pulseElapsed += Time.unscaledDeltaTime;
            }

            float t = Mathf.Clamp01(_pulseElapsed / Mathf.Max(0.01f, _pulseDuration));
            float curve = Mathf.Sin(t * Mathf.PI);

            float boxScale = Mathf.Lerp(1.0f, PulseBoxMaxScale, curve);
            float arrowLocalScale = Mathf.Lerp(1.0f, PulseArrowLocalMaxScale, curve);
            float glowAlpha = Mathf.Lerp(0.0f, PulseMaxGlowAlpha, curve);

            Image slotBorder = _pulseIsPlayer ? _playerSlotBorders[_pulseSlotIndex] : _zombieSlotBorders[_pulseSlotIndex];
            Image boxGlow = _pulseIsPlayer ? _playerBoxGlows[_pulseSlotIndex] : _zombieBoxGlows[_pulseSlotIndex];
            Outline boxOutline = _pulseIsPlayer ? _playerBoxOutlines[_pulseSlotIndex] : _zombieBoxOutlines[_pulseSlotIndex];
            Image arrow = _pulseIsPlayer ? _playerArrows[_pulseSlotIndex] : _zombieArrows[_pulseSlotIndex];
            Image arrowGlow = _pulseIsPlayer ? _playerArrowGlows[_pulseSlotIndex] : _zombieArrowGlows[_pulseSlotIndex];
            Outline arrowShadow = _pulseIsPlayer ? _playerArrowShadows[_pulseSlotIndex] : _zombieArrowShadows[_pulseSlotIndex];

            Color baseColor = _pulseIsPlayer
                ? (_tuning != null ? _tuning.playerHistoryHighlightColor : new Color(0.30f, 0.65f, 1.00f, 1f))
                : ZombieHighlightColor;

            if (t >= 1.0f)
            {
                if (slotBorder != null) slotBorder.rectTransform.localScale = Vector3.one;
                if (boxGlow != null)
                {
                    boxGlow.rectTransform.localScale = Vector3.one;
                    boxGlow.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
                    boxGlow.enabled = false;
                }
                if (boxOutline != null)
                {
                    boxOutline.effectColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
                    boxOutline.enabled = false;
                }
                if (arrow != null) arrow.rectTransform.localScale = Vector3.one;
                if (arrowGlow != null)
                {
                    arrowGlow.rectTransform.localScale = Vector3.one;
                    arrowGlow.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
                    arrowGlow.enabled = false;
                }
                if (arrowShadow != null)
                {
                    arrowShadow.effectColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
                    arrowShadow.enabled = false;
                }

                _pulseActive = false;
                _pulseSlotIndex = -1;
                _pulseElapsed = 0f;
                return;
            }

            if (slotBorder != null)
            {
                slotBorder.rectTransform.localScale = new Vector3(boxScale, boxScale, 1f);
            }
            if (boxGlow != null)
            {
                boxGlow.rectTransform.localScale = Vector3.one;
                boxGlow.color = new Color(baseColor.r, baseColor.g, baseColor.b, glowAlpha);
                boxGlow.enabled = glowAlpha > 0.001f;
            }
            if (boxOutline != null)
            {
                boxOutline.effectColor = new Color(baseColor.r, baseColor.g, baseColor.b, glowAlpha);
                boxOutline.enabled = glowAlpha > 0.001f;
            }

            if (arrow != null)
            {
                arrow.rectTransform.localScale = new Vector3(arrowLocalScale, arrowLocalScale, 1f);
            }
            if (arrowGlow != null)
            {
                arrowGlow.rectTransform.localScale = new Vector3(arrowLocalScale, arrowLocalScale, 1f);
                arrowGlow.color = new Color(baseColor.r, baseColor.g, baseColor.b, glowAlpha);
                arrowGlow.enabled = glowAlpha > 0.001f;
            }
            if (arrowShadow != null)
            {
                arrowShadow.effectColor = new Color(baseColor.r, baseColor.g, baseColor.b, glowAlpha);
                arrowShadow.enabled = glowAlpha > 0.001f;
            }
        }

        private static Sprite GetOrCreateBoxGlowSprite()
        {
            if (_cachedBoxGlowSprite != null) return _cachedBoxGlowSprite;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float center = (size - 1) * 0.5f;
            float maxRadius = center;
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - center) / maxRadius;
                    float dy = Mathf.Abs(y - center) / maxRadius;
                    // Distancia suave a un cuadrado con esquinas redondeadas
                    float qx = Mathf.Max(0f, dx - 0.55f) / 0.45f;
                    float qy = Mathf.Max(0f, dy - 0.55f) / 0.45f;
                    float dist = Mathf.Sqrt(qx * qx + qy * qy);
                    float falloff = Mathf.Clamp01(1f - dist);
                    float alpha = falloff * falloff;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);

            _cachedBoxGlowSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                64f);
            _cachedBoxGlowSprite.name = "HUDBoxPulseGlowSprite";
            return _cachedBoxGlowSprite;
        }

        private static Sprite GetOrCreateArrowGlowSprite()
        {
            if (_cachedArrowGlowSprite != null) return _cachedArrowGlowSprite;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float center = (size - 1) * 0.5f;
            float maxRadius = center;
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / maxRadius;
                    float dy = (y - center) / maxRadius;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float falloff = Mathf.Clamp01(1f - dist);
                    float alpha = falloff * falloff;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);

            _cachedArrowGlowSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                64f);
            _cachedArrowGlowSprite.name = "HUDArrowPulseGlowSprite";
            return _cachedArrowGlowSprite;
        }

        public void SetHistories(IReadOnlyList<Direction> playerHistory,
            IReadOnlyList<Direction> zombieHistory)
        {
            _playerHighlightedCount = UpdateSlots(
                _playerArrows, _playerArrowGlows, _playerBoxGlows, _playerBoxOutlines,
                _playerSlotBackgrounds, _playerSlotBorders, _playerArrowShadows, playerHistory,
                _tuning.playerHistoryColor, _tuning.playerHistoryHighlightColor,
                _playerConciencia, true);
            _zombieHighlightedCount = UpdateSlots(
                _zombieArrows, _zombieArrowGlows, _zombieBoxGlows, _zombieBoxOutlines,
                _zombieSlotBackgrounds, _zombieSlotBorders, _zombieArrowShadows, zombieHistory,
                _tuning.zombieHistoryColor, ZombieHighlightColor,
                _zombieConciencia, false);
        }

        private int UpdateSlots(Image[] arrows, Image[] arrowGlows, Image[] boxGlows, Outline[] boxOutlines,
            Image[] backgrounds, Image[] borders, Outline[] shadows,
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
                bool isCurrentlyPulsing = _pulseActive && (_pulseIsPlayer == isPlayer) && (_pulseSlotIndex == i);

                bool filled = i >= firstSlot && i - firstSlot < visible;
                if (filled)
                {
                    int indexInHistory = start + (i - firstSlot);
                    bool isHighlighted = indexInHistory >= count - highlighted;
                    var direction = history[indexInHistory];

                    arrows[i].enabled = true;
                    arrows[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, AngleFor(direction));

                    if (!isCurrentlyPulsing)
                    {
                        borders[i].rectTransform.localScale = Vector3.one;
                        backgrounds[i].rectTransform.localScale = Vector3.one;
                        arrows[i].rectTransform.localScale = Vector3.one;
                        if (boxGlows[i] != null)
                        {
                            boxGlows[i].rectTransform.localScale = Vector3.one;
                            boxGlows[i].color = new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0f);
                            boxGlows[i].enabled = false;
                        }
                        if (boxOutlines[i] != null)
                        {
                            boxOutlines[i].effectColor = new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0f);
                            boxOutlines[i].enabled = false;
                        }
                        if (arrowGlows[i] != null)
                        {
                            arrowGlows[i].rectTransform.localScale = Vector3.one;
                            arrowGlows[i].color = new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0f);
                            arrowGlows[i].enabled = false;
                        }
                        if (shadows[i] != null)
                        {
                            shadows[i].effectColor = new Color(highlightColor.r, highlightColor.g, highlightColor.b, 0f);
                            shadows[i].enabled = false;
                        }
                    }

                    if (isHighlighted)
                    {
                        // Flecha activa (con cuadro): conserva cuadro y relleno sólido
                        arrows[i].sprite = PixelArtFactory.ArrowSprite;
                        arrows[i].color = isPlayer ? highlightColor : ZombieHighlightColor;

                        borders[i].enabled = true;
                        borders[i].color = isPlayer ? _tuning.hudBorderColor : ZombieBoxBorderColor;

                        backgrounds[i].enabled = true;
                        backgrounds[i].color = isPlayer
                            ? Color.Lerp(_tuning.historySlotColor, highlightColor, 0.24f)
                            : ZombieBoxBgColor;
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
                    }
                }
                else
                {
                    // Casilla vacía (sin turno en el historial)
                    arrows[i].enabled = false;
                    arrows[i].rectTransform.localScale = Vector3.one;
                    borders[i].enabled = false;
                    borders[i].rectTransform.localScale = Vector3.one;
                    backgrounds[i].enabled = false;
                    backgrounds[i].rectTransform.localScale = Vector3.one;
                    if (boxGlows[i] != null)
                    {
                        boxGlows[i].rectTransform.localScale = Vector3.one;
                        boxGlows[i].enabled = false;
                    }
                    if (boxOutlines[i] != null)
                    {
                        boxOutlines[i].enabled = false;
                    }
                    if (arrowGlows[i] != null)
                    {
                        arrowGlows[i].rectTransform.localScale = Vector3.one;
                        arrowGlows[i].enabled = false;
                    }
                    if (shadows[i] != null)
                    {
                        shadows[i].enabled = false;
                    }
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
            if (loading)
            {
                // El texto "Generando nivel..." NO debe mostrarse jamás mientras "bien" siga visible:
                // si el banner sigue activo, oculta el banner por completo antes de activar el overlay.
                if (_infectionBanner != null && _infectionBanner.gameObject.activeSelf)
                {
                    _infectionBanner.text = "";
                    _infectionBanner.gameObject.SetActive(false);
                }
            }

            _loading = loading;
            if (_loadingRoot != null)
            {
                _loadingRoot.SetActive(loading);
                if (loading)
                {
                    _loadingRoot.transform.SetAsLastSibling();
                    KeepPauseMenuOnTop();
                }
            }
            if (loading)
            {
                _loadingBase = string.IsNullOrEmpty(text) ? "Generando nivel" : text;
                if (_loadingLabel != null) _loadingLabel.text = _loadingBase;
            }
            else if (_loadingLabel != null)
            {
                _loadingLabel.text = "";
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

            HandleEscapeKey();
            UpdateArrowPulse();

            // Garantía estricta de exclusión mutua: nunca mostrar "Generando nivel..." si el banner ("bien") está visible
            if (_infectionBanner != null && _infectionBanner.gameObject.activeSelf)
            {
                if (_loadingRoot != null && _loadingRoot.activeSelf)
                {
                    _loadingRoot.SetActive(false);
                }
                return;
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
            if (_isPaused)
            {
                _isPaused = false;
                IsPauseMenuOpen = false;
                Time.timeScale = 1f;
            }
            if (_canvasObject != null) Destroy(_canvasObject);
        }
    }
}
