using System.Collections.Generic;
using UnityEngine;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Purely visual ambient infection cloud. It is hidden at the start of a level, starts after
    /// the second valid manual move and grows with the energy already consumed. It never reads or
    /// writes gameplay state.
    /// </summary>
    public sealed class AmbientInfectionVFX : MonoBehaviour
    {
        private sealed class Particle
        {
            public SpriteRenderer Renderer;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Age;
            public float Lifetime;
            public bool Active;
        }

        private GameTuning _tuning;
        private Transform _follow;
        private readonly List<Particle> _particles = new List<Particle>();
        private readonly List<Particle> _active = new List<Particle>();

        private float _intensity;
        private float _emissionAccumulator;
        private float _fadeTimer = -1f;
        private float _fadeDuration = 0.2f;
        private uint _rng = 0x9E3779B9u;
        private bool _initialized;

        public float Intensity => _intensity;
        public int ActiveParticles => _active.Count;

        public void Initialize(GameTuning tuning, Transform follow)
        {
            _tuning = tuning;
            _follow = follow;
            _initialized = true;
            _intensity = 0f;
            _emissionAccumulator = 0f;
            _fadeTimer = -1f;
            ClearImmediate();
        }

        public void SetIntensity(float intensity)
        {
            if (!_initialized) return;
            _intensity = Mathf.Clamp01(intensity);
            _fadeTimer = -1f;
            if (_intensity <= 0.001f) ClearImmediate();
        }

        public void FadeOutAndClear(float duration)
        {
            if (!_initialized) return;
            _fadeDuration = Mathf.Max(0.01f, duration);
            _fadeTimer = 0f;
            _emissionAccumulator = 0f;
        }

        public void ClearImmediate()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                var particle = _active[i];
                particle.Active = false;
                if (particle.Renderer != null) particle.Renderer.enabled = false;
            }
            _active.Clear();
            _emissionAccumulator = 0f;
        }

        private void Update()
        {
            if (!_initialized || _tuning == null) return;

            float delta = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (delta <= 0f) return;

            float fade = 1f;
            if (_fadeTimer >= 0f)
            {
                _fadeTimer += delta;
                fade = 1f - Mathf.Clamp01(_fadeTimer / _fadeDuration);
                _intensity = Mathf.Min(_intensity, fade);
                if (fade <= 0.001f)
                {
                    ClearImmediate();
                    return;
                }
            }

            EnsurePool();
            Emit(delta, fade);
            Simulate(delta, fade);
        }

        private void EnsurePool()
        {
            int maximum = Mathf.Max(1, _tuning.ambientParticleCount);
            while (_particles.Count < maximum)
            {
                var particleObject = new GameObject("Infection Particle " + _particles.Count);
                particleObject.transform.SetParent(transform, false);
                var renderer = particleObject.AddComponent<SpriteRenderer>();
                renderer.sprite = PixelArtFactory.InfectionParticleSprite;
                renderer.sortingOrder = 9;
                renderer.enabled = false;
                if (PixelArtFactory.SpriteMaterial != null)
                    renderer.sharedMaterial = PixelArtFactory.SpriteMaterial;

                _particles.Add(new Particle { Renderer = renderer });
            }
        }

        private void Emit(float delta, float fade)
        {
            if (_intensity <= 0.001f || fade <= 0.001f) return;

            int maximum = Mathf.Max(1, _tuning.ambientParticleCount);
            int desired = Mathf.Clamp(Mathf.RoundToInt(maximum * _intensity), 0, maximum);
            _emissionAccumulator += _tuning.ambientEmissionRate * _intensity * fade * delta;

            while (_emissionAccumulator >= 1f && _active.Count < desired)
            {
                _emissionAccumulator -= 1f;
                SpawnParticle();
            }

            // If the intensity fell sharply, retire the oldest particles so the visible count
            // follows the consumed energy instead of waiting for every lifetime to expire.
            while (_active.Count > desired)
            {
                Despawn(_active[0]);
            }
        }

        private void SpawnParticle()
        {
            Particle particle = null;
            for (int i = 0; i < _particles.Count; i++)
            {
                if (_particles[i].Active) continue;
                particle = _particles[i];
                break;
            }
            if (particle == null) return;

            var emitter = EmitterPosition();
            float angle = NextFloat() * Mathf.PI * 2f;
            float radius = Mathf.Max(0.05f, _tuning.ambientRadius) * Mathf.Sqrt(NextFloat());
            particle.Position = emitter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            particle.Velocity = new Vector2(
                (NextFloat() - 0.5f) * 0.30f,
                _tuning.ambientParticleRise * (0.55f + NextFloat() * 0.75f));
            particle.Age = 0f;
            particle.Lifetime = Mathf.Max(0.15f, _tuning.ambientParticleLifetime) *
                                (0.60f + NextFloat() * 0.80f);
            particle.Active = true;
            if (particle.Renderer != null) particle.Renderer.enabled = true;
            _active.Add(particle);
        }

        private void Simulate(float delta, float fade)
        {
            var emitter = EmitterPosition();
            Color cold = _tuning.ambientParticleColor;
            Color hot = _tuning.ambientParticleHotColor;
            Color color = Color.Lerp(cold, hot, _intensity);

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var particle = _active[i];
                particle.Age += delta;
                if (particle.Age >= particle.Lifetime)
                {
                    Despawn(particle);
                    continue;
                }

                // Drift upward while gently staying around the moving player.
                particle.Position += particle.Velocity * delta;
                particle.Position += (emitter - particle.Position) * (0.9f * delta);

                float life = Mathf.Clamp01(particle.Age / particle.Lifetime);
                float alpha = (1f - life) * (0.25f + 0.75f * _intensity) * fade;
                color.a = alpha;

                var renderer = particle.Renderer;
                if (renderer == null) continue;
                renderer.color = color;
                renderer.transform.position = new Vector3(particle.Position.x, particle.Position.y, 0f);
                float size = Mathf.Max(0.01f, _tuning.ambientParticleSize) *
                             (0.65f + 0.60f * _intensity) *
                             (1f - 0.35f * life);
                renderer.transform.localScale = new Vector3(size, size, 1f);
                renderer.enabled = alpha > 0.01f;
            }
        }

        private Vector2 EmitterPosition()
        {
            if (_follow == null) return transform.position;
            return new Vector2(_follow.position.x, _follow.position.y + 0.05f);
        }

        private void Despawn(Particle particle)
        {
            particle.Active = false;
            if (particle.Renderer != null) particle.Renderer.enabled = false;
            _active.Remove(particle);
        }

        private float NextFloat()
        {
            _rng ^= _rng << 13;
            _rng ^= _rng >> 17;
            _rng ^= _rng << 5;
            return (_rng & 0x00FFFFFFu) / 16777216f;
        }
    }
}
