using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Animated GIF background. It decodes the GIF once, plays it on a single texture and keeps
    /// itself alive across scene loads (singleton + DontDestroyOnLoad). The sprite always renders
    /// on the "Background" sorting layer, behind the level and the HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PersistentBackground : MonoBehaviour
    {
        private const string DefaultGifResourcePath = "Backgrounds/Background Esoacui.gif";
        private const float PixelsPerUnit = 100f;
        private const float CoverageMargin = 1.04f;
        private const float MinimumFrameDelay = 0.02f;
        private const float BackgroundSortingOrder = 0f;

        public static PersistentBackground Instance { get; private set; }

        [Header("Scene references")]
        [SerializeField] private SpriteRenderer _renderer;

        [Header("GIF")]
        [SerializeField] private string _gifResourcePath = DefaultGifResourcePath;
        [SerializeField] private bool _playOnStart = true;
        [Range(0.1f, 3f)]
        [SerializeField] private float _playbackSpeed = 1f;

        private GifAnimation _animation;
        private Task<GifAnimation> _decodeTask;
        private Exception _decodeException;
        private Texture2D _texture;
        private Sprite _sprite;
        private Color32[] _canvas;
        private Color32[] _savedCanvas;
        private Color32[] _uploadBuffer;
        private Camera _camera;
        private int _frameIndex = -1;
        private float _timer;
        private bool _isPrimary;
        private bool _applied;
        private bool _failed;

        public bool IsReady => _applied && _animation != null;
        public bool HasFailed => _failed;
        public int FrameCount => _animation != null ? _animation.Frames.Length : 0;
        public int CurrentFrame => _frameIndex;
        public string GifResourcePath => _gifResourcePath;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _isPrimary = true;
            DontDestroyOnLoad(gameObject);

            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null) _renderer = gameObject.AddComponent<SpriteRenderer>();

            if (string.IsNullOrEmpty(_gifResourcePath)) _gifResourcePath = DefaultGifResourcePath;

            ApplySorting();
            BeginDecode();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (_sprite != null) Destroy(_sprite);
            if (_texture != null) Destroy(_texture);
            _sprite = null;
            _texture = null;
        }

        private void ApplySorting()
        {
            bool hasBackgroundLayer = false;
            var layers = SortingLayer.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i].name == "Background")
                {
                    hasBackgroundLayer = true;
                    break;
                }
            }

            _renderer.sortingLayerName = hasBackgroundLayer ? "Background" : "Default";
            _renderer.sortingOrder = hasBackgroundLayer ? (int)BackgroundSortingOrder : -1000;
            _renderer.color = Color.white;
        }

        private void BeginDecode()
        {
            byte[] bytes = LoadGifBytes(_gifResourcePath);
            if (bytes == null || bytes.Length == 0)
            {
                Fail("No GIF bytes found at Resources/" + _gifResourcePath +
                     ". The static sprite assigned in the scene will be kept.");
                return;
            }

            _decodeTask = Task.Run(() =>
            {
                try
                {
                    return GifDecoder.Decode(bytes);
                }
                catch (Exception exception)
                {
                    _decodeException = exception;
                    return null;
                }
            });
        }

        private void Update()
        {
            if (_decodeTask != null && _decodeTask.IsCompleted) CompleteDecode();
            if (!_applied || !_playOnStart || _animation == null) return;
            if (_animation.Frames.Length <= 1) return;

            _timer += Time.unscaledDeltaTime * Mathf.Max(0.01f, _playbackSpeed);
            float delay = Mathf.Max(MinimumFrameDelay, _animation.Frames[_frameIndex].Delay);

            int guard = 0;
            while (_timer >= delay && guard++ < 64)
            {
                _timer -= delay;
                Advance();
                delay = Mathf.Max(MinimumFrameDelay, _animation.Frames[_frameIndex].Delay);
            }
        }

        private void LateUpdate()
        {
            FitToCamera();
        }

        private void CompleteDecode()
        {
            var task = _decodeTask;
            _decodeTask = null;

            GifAnimation animation = null;
            try
            {
                animation = task.Result;
            }
            catch (Exception exception)
            {
                _decodeException = exception;
            }

            if (animation == null || animation.Frames == null || animation.Frames.Length == 0)
            {
                Fail("The GIF could not be decoded.");
                if (_decodeException != null) Debug.LogException(_decodeException);
                return;
            }

            ApplyAnimation(animation);
        }

        private void ApplyAnimation(GifAnimation animation)
        {
            _animation = animation;
            _canvas = new Color32[animation.Width * animation.Height];
            _uploadBuffer = new Color32[_canvas.Length];

            _texture = new Texture2D(animation.Width, animation.Height, TextureFormat.RGBA32, false)
            {
                name = "GlobalBackground (GIF)",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            _sprite = Sprite.Create(_texture, new Rect(0f, 0f, animation.Width, animation.Height),
                new Vector2(0.5f, 0.5f), PixelsPerUnit);
            _sprite.name = "GlobalBackground (GIF)";
            _sprite.hideFlags = HideFlags.DontSave;

            _renderer.sprite = _sprite;

            ClearCanvas();
            DrawFrame(0);
            Upload();
            _frameIndex = 0;
            _timer = 0f;
            _applied = true;
            _failed = false;
        }

        private void Advance()
        {
            var frames = _animation.Frames;
            ApplyDisposal(frames[_frameIndex]);

            int next = _frameIndex + 1;
            if (next >= frames.Length)
            {
                next = 0;
                ClearCanvas();
            }

            DrawFrame(next);
            Upload();
            _frameIndex = next;
        }

        private void DrawFrame(int index)
        {
            var frame = _animation.Frames[index];
            if (frame.DisposalMethod == 3) SaveCanvas();

            int canvasWidth = _animation.Width;
            int canvasHeight = _animation.Height;

            for (int y = 0; y < frame.Height; y++)
            {
                int canvasY = frame.Top + y;
                if (canvasY < 0 || canvasY >= canvasHeight) continue;

                int sourceRow = y * frame.Width;
                int targetRow = canvasY * canvasWidth;

                for (int x = 0; x < frame.Width; x++)
                {
                    int canvasX = frame.Left + x;
                    if (canvasX < 0 || canvasX >= canvasWidth) continue;

                    byte paletteIndex = frame.Indices[sourceRow + x];
                    if (frame.TransparentIndex >= 0 && paletteIndex == frame.TransparentIndex) continue;
                    if (paletteIndex >= frame.Palette.Length) continue;

                    _canvas[targetRow + canvasX] = frame.Palette[paletteIndex];
                }
            }
        }

        private void ApplyDisposal(GifFrame frame)
        {
            switch (frame.DisposalMethod)
            {
                case 2:
                    ClearRect(frame);
                    break;
                case 3:
                    RestoreCanvas();
                    break;
            }
        }

        private void ClearRect(GifFrame frame)
        {
            Color32 color = _animation.BackgroundColor;
            int canvasWidth = _animation.Width;
            int canvasHeight = _animation.Height;

            for (int y = 0; y < frame.Height; y++)
            {
                int canvasY = frame.Top + y;
                if (canvasY < 0 || canvasY >= canvasHeight) continue;

                for (int x = 0; x < frame.Width; x++)
                {
                    int canvasX = frame.Left + x;
                    if (canvasX < 0 || canvasX >= canvasWidth) continue;
                    _canvas[canvasY * canvasWidth + canvasX] = color;
                }
            }
        }

        private void SaveCanvas()
        {
            if (_savedCanvas == null || _savedCanvas.Length != _canvas.Length)
                _savedCanvas = new Color32[_canvas.Length];
            Array.Copy(_canvas, _savedCanvas, _canvas.Length);
        }

        private void RestoreCanvas()
        {
            if (_savedCanvas == null || _savedCanvas.Length != _canvas.Length) return;
            Array.Copy(_savedCanvas, _canvas, _canvas.Length);
        }

        private void ClearCanvas()
        {
            Color32 color = _animation.BackgroundColor;
            for (int i = 0; i < _canvas.Length; i++) _canvas[i] = color;
        }

        private void Upload()
        {
            int width = _animation.Width;
            int height = _animation.Height;

            // GIF rows start at the top; Unity textures start at the bottom.
            for (int y = 0; y < height; y++)
                Array.Copy(_canvas, y * width, _uploadBuffer, (height - 1 - y) * width, width);

            _texture.SetPixels32(_uploadBuffer);
            _texture.Apply(false, false);
        }

        private void FitToCamera()
        {
            if (!_isPrimary) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            float visibleHeight = _camera.orthographicSize * 2f;
            float visibleWidth = visibleHeight * _camera.aspect;

            // Use the animated texture size when ready, otherwise the placeholder sprite so the
            // background covers the camera from the first frame of Play mode.
            float spriteWidth = 12.8f;
            float spriteHeight = 6.4f;
            if (_animation != null)
            {
                spriteWidth = _animation.Width / PixelsPerUnit;
                spriteHeight = _animation.Height / PixelsPerUnit;
            }
            else if (_renderer != null && _renderer.sprite != null)
            {
                Vector3 spriteSize = _renderer.sprite.bounds.size;
                if (spriteSize.x > 0.01f) spriteWidth = spriteSize.x;
                if (spriteSize.y > 0.01f) spriteHeight = spriteSize.y;
            }

            float scale = Mathf.Max(visibleWidth / spriteWidth, visibleHeight / spriteHeight) * CoverageMargin;

            Vector3 cameraPosition = _camera.transform.position;
            transform.position = new Vector3(cameraPosition.x, cameraPosition.y, transform.position.z);
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void Fail(string message)
        {
            _failed = true;
            Debug.LogWarning("[PersistentBackground] " + message, this);
        }

        private static byte[] LoadGifBytes(string resourcePath)
        {
            var textAsset = Resources.Load<TextAsset>(resourcePath);
            if (textAsset != null && textAsset.bytes != null && textAsset.bytes.Length > 0)
                return textAsset.bytes;

#if UNITY_EDITOR
            try
            {
                string editorPath = Path.Combine(Application.dataPath, "Resources", resourcePath);
                if (File.Exists(editorPath)) return File.ReadAllBytes(editorPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[PersistentBackground] Editor GIF fallback failed: " + exception.Message);
            }
#endif

            return null;
        }
    }
}
