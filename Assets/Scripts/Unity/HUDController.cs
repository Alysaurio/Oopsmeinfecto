using System.Collections.Generic;
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
        private const int SlotCount = 7;

        private GameTuning _tuning;
        private Font _font;
        private GameObject _canvasObject;

        private Text _levelLabel;
        private Text _statusLabel;
        private Text _playerTitle;
        private Text _zombieTitle;
        private Text _hintLabel;

        private readonly Image[] _playerSlotBackgrounds = new Image[SlotCount];
        private readonly Image[] _playerArrows = new Image[SlotCount];
        private readonly Image[] _zombieSlotBackgrounds = new Image[SlotCount];
        private readonly Image[] _zombieArrows = new Image[SlotCount];

        private GameObject _loadingRoot;
        private Text _loadingLabel;
        private string _loadingBase = "Generando nivel";
        private bool _loading;

        private int _playerConciencia;
        private int _zombieConciencia;
        private bool _infected;

        public void Build(GameTuning tuning)
        {
            _tuning = tuning;
            _font = LoadFont();

            if (_canvasObject != null) Destroy(_canvasObject);

            _canvasObject = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = _canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var root = _canvasObject.GetComponent<RectTransform>();
            BuildTopLeft(root);
            BuildHint(root);
            BuildHistoryPanel(root);
            BuildLoadingOverlay(root);
        }

        private void BuildTopLeft(RectTransform root)
        {
            var border = CreateImage(root, "Level Panel Border", _tuning.hudBorderColor);
            SetRect(border.rectTransform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -24f), new Vector2(610f, 88f));

            var inner = CreateImage(border.transform, "Level Panel", _tuning.hudPanelColor);
            Stretch(inner.rectTransform, 3f);

            _levelLabel = CreateText(inner.transform, "Level Label", "", 27,
                _tuning.hudTextColor, TextAnchor.MiddleLeft);
            _levelLabel.fontStyle = FontStyle.Bold;
            SetRect(_levelLabel.rectTransform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -8f), new Vector2(-36f, 36f));

            _statusLabel = CreateText(inner.transform, "Status Label", "", 18,
                Color.Lerp(_tuning.hudTextColor, _tuning.hudBorderColor, 0.25f), TextAnchor.MiddleLeft);
            SetRect(_statusLabel.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
                new Vector2(18f, 8f), new Vector2(-36f, 26f));
        }

        private void BuildHint(RectTransform root)
        {
            _hintLabel = CreateText(root, "Controls Hint",
                "WASD / Flechas  ·  mantén para repetir", 17,
                Color.Lerp(_tuning.hudTextColor, _tuning.hudBorderColor, 0.35f), TextAnchor.MiddleRight);
            SetRect(_hintLabel.rectTransform,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-28f, -30f), new Vector2(650f, 30f));
        }

        private void BuildHistoryPanel(RectTransform root)
        {
            float hudHeight = Mathf.Clamp(_tuning.hudViewportFraction * 1080f, 156f, 280f);

            var border = CreateImage(root, "History Panel Border", _tuning.hudBorderColor);
            SetRect(border.rectTransform,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(0f, hudHeight));

            var inner = CreateImage(border.transform, "History Panel", _tuning.hudPanelColor);
            Stretch(inner.rectTransform, 4f);

            var divider = CreateImage(inner.transform, "Center Divider", _tuning.hudBorderColor);
            SetRect(divider.rectTransform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(2f, -28f));

            BuildHalf(inner.transform, true);
            BuildHalf(inner.transform, false);
        }

        private void BuildHalf(Transform parent, bool player)
        {
            var half = CreateRect(parent, player ? "Player Half" : "Zombie Half",
                player ? new Vector2(0f, 0f) : new Vector2(0.5f, 0f),
                player ? new Vector2(0.5f, 1f) : new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            var title = CreateText(half, player ? "Player Title" : "Zombie Title", "", 21,
                player ? _tuning.playerHistoryColor : _tuning.zombieHistoryColor,
                TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            SetRect(title.rectTransform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(30f, -12f), new Vector2(-60f, 30f));

            if (player) _playerTitle = title;
            else _zombieTitle = title;

            const float slotSize = 58f;
            const float gap = 8f;
            float totalWidth = SlotCount * slotSize + (SlotCount - 1) * gap;

            var slots = CreateRect(half, player ? "Player Slots" : "Zombie Slots",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 16f), new Vector2(totalWidth, slotSize));

            for (int i = 0; i < SlotCount; i++)
            {
                var slotBorder = CreateImage(slots, "Slot Border " + i,
                    Color.Lerp(_tuning.hudBorderColor, _tuning.historySlotColor, 0.55f));
                SetRect(slotBorder.rectTransform,
                    new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(i * (slotSize + gap), 0f), new Vector2(slotSize, slotSize));

                var background = CreateImage(slotBorder.transform, "Slot " + i, _tuning.historySlotColor);
                Stretch(background.rectTransform, 2f);

                var arrow = CreateImage(background.transform, "Arrow " + i,
                    player ? _tuning.playerHistoryColor : _tuning.zombieHistoryColor);
                arrow.sprite = PixelArtFactory.ArrowSprite;
                arrow.preserveAspect = true;
                arrow.enabled = false;
                SetRect(arrow.rectTransform,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(34f, 34f));

                if (player)
                {
                    _playerSlotBackgrounds[i] = background;
                    _playerArrows[i] = arrow;
                }
                else
                {
                    _zombieSlotBackgrounds[i] = background;
                    _zombieArrows[i] = arrow;
                }
            }
        }

        private void BuildLoadingOverlay(RectTransform root)
        {
            var overlay = CreateImage(root, "Loading Overlay", new Color(0.01f, 0.015f, 0.02f, 0.93f));
            Stretch(overlay.rectTransform, 0f);
            _loadingRoot = overlay.gameObject;

            var border = CreateImage(overlay.transform, "Loading Panel Border", _tuning.hudBorderColor);
            SetRect(border.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(660f, 196f));

            var panel = CreateImage(border.transform, "Loading Panel", _tuning.hudPanelColor);
            Stretch(panel.rectTransform, 4f);

            _loadingLabel = CreateText(panel.transform, "Loading Label", _loadingBase, 34,
                _tuning.hudTextColor, TextAnchor.MiddleCenter);
            _loadingLabel.fontStyle = FontStyle.Bold;
            SetRect(_loadingLabel.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 26f), new Vector2(600f, 54f));

            var subtitle = CreateText(panel.transform, "Loading Subtitle",
                "Construyendo el camino obligatorio y validando la solución", 17,
                Color.Lerp(_tuning.hudTextColor, _tuning.hudBorderColor, 0.30f), TextAnchor.MiddleCenter);
            SetRect(subtitle.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -34f), new Vector2(600f, 32f));

            _loadingRoot.SetActive(false);
        }

        public void SetLevelInfo(LevelDefinition level)
        {
            if (level == null) return;
            _playerConciencia = level.PlayerConciencia;
            _zombieConciencia = level.ZombieConciencia;
            _infected = false;

            if (_levelLabel != null)
            {
                _levelLabel.text = "NIVEL " + level.LevelIndex + "   ·   SEMILLA " + level.Seed;
            }

            UpdateEntityTitles();
        }

        public void SetStatus(string text)
        {
            if (_statusLabel != null && text != null) _statusLabel.text = text;
        }

        public void SetInfected(bool infected)
        {
            _infected = infected;
            UpdateEntityTitles();
        }

        private void UpdateEntityTitles()
        {
            if (_playerTitle != null)
            {
                _playerTitle.text = _infected
                    ? "TÚ  ·  INFECTADO"
                    : "TÚ  ·  conciencia " + _playerConciencia;
            }

            if (_zombieTitle != null)
            {
                _zombieTitle.text = "ZOMBI  ·  conciencia " + _zombieConciencia;
            }
        }

        public void SetHistories(IReadOnlyList<Direction> playerHistory,
            IReadOnlyList<Direction> zombieHistory)
        {
            UpdateSlots(_playerArrows, _playerSlotBackgrounds, playerHistory, _tuning.playerHistoryColor);
            UpdateSlots(_zombieArrows, _zombieSlotBackgrounds, zombieHistory, _tuning.zombieHistoryColor);
        }

        private void UpdateSlots(Image[] arrows, Image[] backgrounds,
            IReadOnlyList<Direction> history, Color arrowColor)
        {
            int count = history != null ? history.Count : 0;
            int start = Mathf.Max(0, count - SlotCount);
            int visible = count - start;
            int firstSlot = SlotCount - visible;
            Color emptyColor = Color.Lerp(_tuning.historySlotColor, Color.black, 0.28f);

            for (int i = 0; i < SlotCount; i++)
            {
                bool filled = i >= firstSlot && i - firstSlot < visible;
                if (filled)
                {
                    var direction = history[start + (i - firstSlot)];
                    arrows[i].enabled = true;
                    arrows[i].color = arrowColor;
                    arrows[i].rectTransform.localEulerAngles =
                        new Vector3(0f, 0f, AngleFor(direction));
                    backgrounds[i].color = Color.Lerp(_tuning.historySlotColor, arrowColor, 0.10f);
                }
                else
                {
                    arrows[i].enabled = false;
                    backgrounds[i].color = emptyColor;
                }
            }
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
