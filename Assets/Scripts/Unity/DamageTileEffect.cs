using UnityEngine;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Visual presentation for a DamageTile (lethal hazard).
    /// Displays a pulsing toxic glow and hosts the toxic gas particle system.
    /// Does not alter gameplay logic or collision rules.
    /// </summary>
    public sealed class DamageTileEffect : MonoBehaviour
    {
        [Header("Glow Configuration")]
        [SerializeField] private SpriteRenderer _glowRenderer;
        [SerializeField] private Color _glowColor = new Color(0.20f, 0.86f, 0.32f, 0.38f);
        [SerializeField] private float _pulseSpeed = 2.8f;
        [SerializeField] private float _minAlpha = 0.22f;
        [SerializeField] private float _maxAlpha = 0.48f;
        [SerializeField] private float _minScale = 0.88f;
        [SerializeField] private float _maxScale = 1.08f;

        [Header("Particles")]
        [SerializeField] private ParticleSystem _gasParticles;

        public SpriteRenderer GlowRenderer => _glowRenderer;
        public ParticleSystem GasParticles => _gasParticles;

        private void Awake()
        {
            if (_gasParticles == null)
            {
                _gasParticles = GetComponentInChildren<ParticleSystem>(true);
            }
            if (_gasParticles != null)
            {
                var main = _gasParticles.main;
                main.playOnAwake = false;
                _gasParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public void Initialize(Color color, Sprite glowSprite, GameObject gasParticlePrefab)
        {
            _glowColor = color;

            if (_glowRenderer == null)
            {
                var glowObj = new GameObject("Glow");
                glowObj.transform.SetParent(transform, false);
                glowObj.transform.localPosition = Vector3.zero;
                _glowRenderer = glowObj.AddComponent<SpriteRenderer>();
            }

            if (glowSprite != null)
            {
                _glowRenderer.sprite = glowSprite;
            }

            _glowRenderer.sortingOrder = 5;
            _glowRenderer.color = _glowColor;

            if (_gasParticles == null && gasParticlePrefab != null)
            {
                var particles = Instantiate(gasParticlePrefab, transform, false);
                particles.name = "ToxicGasParticles";
                particles.transform.localPosition = Vector3.zero;
                _gasParticles = particles.GetComponent<ParticleSystem>();
            }

            if (_gasParticles != null)
            {
                var main = _gasParticles.main;
                main.playOnAwake = false;
                _gasParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public void PlayParticles()
        {
            if (_gasParticles == null)
            {
                _gasParticles = GetComponentInChildren<ParticleSystem>(true);
            }
            if (_gasParticles != null)
            {
                var main = _gasParticles.main;
                main.playOnAwake = false;
                _gasParticles.Play(true);
            }
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
            _glowRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
