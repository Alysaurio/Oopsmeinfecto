using System;
using UnityEngine;
using UnityEngine.UI;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Full-screen defeat fade. The fade duration is configurable (1-2 s). The restart callback
    /// is invoked exactly once when the overlay reaches full opacity.
    /// </summary>
    public sealed class DefeatPresentationController : MonoBehaviour
    {
        private GameTuning _tuning;
        private GameObject _canvasObject;
        private CanvasGroup _group;
        private Image _overlay;
        private Text _title;

        private bool _playing;
        private bool _callbackInvoked;
        private float _timer;
        private float _duration = 1.5f;
        private Action _onComplete;

        public void Build(GameTuning tuning, Camera uiCamera = null)
        {
            _tuning = tuning;

            if (_canvasObject != null) Destroy(_canvasObject);

            _canvasObject = new GameObject("Defeat Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var canvas = _canvasObject.GetComponent<Canvas>();

            var cam = uiCamera != null ? uiCamera : Camera.main;
            if (cam != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 2f;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            canvas.sortingOrder = 300;

            var scaler = _canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            _group = _canvasObject.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = true;
            _group.interactable = true;

            var root = _canvasObject.GetComponent<RectTransform>();

            _overlay = CreateImage(root, "Defeat Overlay", tuning.defeatOverlayColor);
            Stretch(_overlay.rectTransform);

            _title = CreateText(root, "End Title", "", 76, tuning.hudTextColor,
                TextAnchor.MiddleCenter);
            _title.fontStyle = FontStyle.Bold;
            SetRect(_title.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1200f, 140f));

            _canvasObject.SetActive(false);
        }

        public void Play(string title, string subtitle, float duration, Action onComplete)
        {
            if (_canvasObject == null) Build(_tuning);
            if (_playing) return;

            _playing = true;
            _callbackInvoked = false;
            _timer = 0f;
            _duration = Mathf.Clamp(duration, 0.1f, 5f);
            _onComplete = onComplete;

            if (_title != null) _title.text = title ?? "";
            if (_overlay != null) _overlay.color = _tuning.defeatOverlayColor;
            if (_group != null) _group.alpha = 0f;

            _canvasObject.SetActive(true);
        }

        public void Hide()
        {
            _playing = false;
            _onComplete = null;
            if (_group != null) _group.alpha = 0f;
            if (_canvasObject != null) _canvasObject.SetActive(false);
        }

        private void Update()
        {
            if (!_playing) return;

            _timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_timer / Mathf.Max(0.01f, _duration));
            if (_group != null) _group.alpha = t * t;

            if (t >= 1f && !_callbackInvoked)
            {
                _callbackInvoked = true;
                var callback = _onComplete;
                _onComplete = null;
                callback?.Invoke();
            }
        }

        private static Image CreateImage(Transform parent, string objectName, Color color)
        {
            var imageObject = new GameObject(objectName, typeof(RectTransform));
            imageObject.transform.SetParent(parent, false);

            var image = imageObject.AddComponent<Image>();
            image.sprite = PixelArtFactory.WhiteSprite;
            image.color = color;
            image.raycastTarget = true;
            return image;
        }

        private Text CreateText(Transform parent, string objectName, string content, int fontSize,
            Color color, TextAnchor alignment)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);

            var text = textObject.AddComponent<Text>();
            text.font = LoadFont();
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.text = content;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        private static Font LoadFont()
        {
            Font font = null;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch { /* Fall through. */ }

            if (font == null)
            {
                try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                catch { /* Fall through. */ }
            }

            if (font == null)
            {
                try { font = Font.CreateDynamicFontFromOSFont("Arial", 16); }
                catch { /* No font available. */ }
            }

            return font;
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

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void OnDestroy()
        {
            if (_canvasObject != null) Destroy(_canvasObject);
        }
    }
}
