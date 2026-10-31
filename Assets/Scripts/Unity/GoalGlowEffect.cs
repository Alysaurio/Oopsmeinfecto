using UnityEngine;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Visual presentation for the victory switch tile.
    /// Emits a soft, pulsing halo to clearly distinguish the goal position without altering gameplay logic.
    /// Placed on top of the base tile and below entities.
    /// </summary>
    public sealed class GoalGlowEffect : MonoBehaviour
    {
        [Header("Glow Configuration")]
        [SerializeField] private SpriteRenderer _glowRenderer;
        [SerializeField] private Color _glowColor = new Color(0.25f, 0.95f, 0.85f, 0.45f);
        [SerializeField] private float _pulseSpeed = 2.2f;
        [SerializeField] private float _minAlpha = 0.28f;
        [SerializeField] private float _maxAlpha = 0.65f;
        [SerializeField] private float _minScale = 0.90f;
        [SerializeField] private float _maxScale = 1.18f;

        public SpriteRenderer GlowRenderer => _glowRenderer;

        public void Initialize(Color color, Sprite glowSprite)
        {
            _glowColor = color;

            if (_glowRenderer == null)
            {
                _glowRenderer = GetComponent<SpriteRenderer>();
                if (_glowRenderer == null)
                {
                    _glowRenderer = gameObject.AddComponent<SpriteRenderer>();
                }
            }

            if (glowSprite != null)
            {
                _glowRenderer.sprite = glowSprite;
            }

            _glowRenderer.sortingOrder = 5;
            _glowRenderer.color = _glowColor;
        }

        private void Update()
        {
            if (_glowRenderer == null) return;

            float wave = (Mathf.Sin(Time.unscaledTime * _pulseSpeed) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(_minAlpha, _maxAlpha, wave);
            float scale = Mathf.Lerp(_minScale, _maxScale, wave);

            var color = _glowColor;
            color.a = alpha;
            _glowRenderer.color = color;
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
