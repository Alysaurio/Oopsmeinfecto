using UnityEngine;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Visual feedback system on the Player reflecting Conciencia influence after infection:
    /// 1. Fade-in (~0.25s) in Zombie Conciencia Red when switching to 'playerinfected'.
    /// 2. Progressive color transition from Red to Player Conciencia Blue between 0.3s and 0.5s
    ///    before completing the Zombie Conciencia steps.
    /// 3. Maintains Blue until all steps finish (whether reaching the goal or not), then
    ///    fades out to 0 at the exact same speed (~0.25s).
    /// 4. Synchronized particle system whose particles always match the exact glow color and alpha.
    /// </summary>
    public sealed class PlayerConcienciaVFX : MonoBehaviour
    {
        public const float FadeDuration = 0.25f;
        public const float DefaultColorTransitionDuration = 0.40f;

        private SpriteRenderer _glowRenderer;
        private ParticleSystem _particleSystem;
        private ParticleSystem.Particle[] _particleBuffer = new ParticleSystem.Particle[64];

        private Color _zombieRedColor = new Color(0.70f, 0.25f, 0.25f, 1f);
        private Color _playerBlueColor = new Color(0.30f, 0.65f, 1.00f, 1f);

        private bool _active;
        private bool _fadingOut;
        private bool _transitioningToBlue;

        private float _fadeAlpha;
        private float _colorBlend;
        private float _colorTransitionDuration = DefaultColorTransitionDuration;

        private const float MaxGlowAlpha = 0.72f;
        private const float MinGlowScale = 1.15f;
        private const float MaxGlowScale = 1.32f;
        private const float PulseSpeed = 3.2f;

        private static Sprite _radialGlowSprite;
        private static Material _particleMaterial;

        public Color CurrentColor => Color.Lerp(_zombieRedColor, _playerBlueColor, _colorBlend);
        public float CurrentAlpha => _fadeAlpha;
        public bool IsActive => _active;

        public void Initialize(Color zombieRedColor, Color playerBlueColor)
        {
            _zombieRedColor = new Color(zombieRedColor.r, zombieRedColor.g, zombieRedColor.b, 1f);
            _playerBlueColor = new Color(playerBlueColor.r, playerBlueColor.g, playerBlueColor.b, 1f);

            EnsureGlowRenderer();
            EnsureParticleSystem();
            ResetImmediate();
        }

        public void StartInfectionGlow()
        {
            EnsureGlowRenderer();
            EnsureParticleSystem();

            _active = true;
            _fadingOut = false;
            _transitioningToBlue = false;
            _fadeAlpha = 0f;
            _colorBlend = 0f;

            if (_glowRenderer != null)
            {
                _glowRenderer.enabled = true;
            }

            if (_particleSystem != null)
            {
                _particleSystem.Clear(true);
                var emission = _particleSystem.emission;
                emission.enabled = true;
                _particleSystem.Play(true);
            }

            ApplyVisualsImmediate();
        }

        public void BeginTransitionToBlue(float duration = DefaultColorTransitionDuration)
        {
            if (!_active) return;
            _colorTransitionDuration = Mathf.Max(0.05f, duration);
            _transitioningToBlue = true;
        }

        public void StopGlow()
        {
            if (!_active || _fadingOut) return;
            _fadingOut = true;

            if (_particleSystem != null)
            {
                var emission = _particleSystem.emission;
                emission.enabled = false;
            }
        }

        public void ResetImmediate()
        {
            _active = false;
            _fadingOut = false;
            _transitioningToBlue = false;
            _fadeAlpha = 0f;
            _colorBlend = 0f;

            if (_glowRenderer != null)
            {
                _glowRenderer.color = new Color(_zombieRedColor.r, _zombieRedColor.g, _zombieRedColor.b, 0f);
                _glowRenderer.enabled = false;
            }

            if (_particleSystem != null)
            {
                var emission = _particleSystem.emission;
                emission.enabled = false;
                _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void Update()
        {
            if (!_active) return;

            // Respect game pause (Time.timeScale == 0 during Pause Menu), while still animating
            // during the brief infection banner pause if needed, or using unscaledDeltaTime when not in pause menu.
            float dt = HUDController.IsPauseMenuOpen ? 0f : Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            if (dt <= 0f) return;

            if (!_fadingOut)
            {
                _fadeAlpha = Mathf.MoveTowards(_fadeAlpha, 1f, dt / FadeDuration);
            }
            else
            {
                _fadeAlpha = Mathf.MoveTowards(_fadeAlpha, 0f, dt / FadeDuration);
                if (_fadeAlpha <= 0.001f)
                {
                    ResetImmediate();
                    return;
                }
            }

            if (_transitioningToBlue)
            {
                _colorBlend = Mathf.MoveTowards(_colorBlend, 1f, dt / _colorTransitionDuration);
            }

            ApplyVisualsImmediate();
        }

        private void ApplyVisualsImmediate()
        {
            Color currentBaseColor = Color.Lerp(_zombieRedColor, _playerBlueColor, _colorBlend);

            if (_glowRenderer != null)
            {
                float wave = (Mathf.Sin(Time.unscaledTime * PulseSpeed) + 1f) * 0.5f;
                float pulseScale = Mathf.Lerp(MinGlowScale, MaxGlowScale, wave);
                float pulseAlpha = Mathf.Lerp(MaxGlowAlpha * 0.80f, MaxGlowAlpha, wave) * _fadeAlpha;

                Color glowColor = currentBaseColor;
                glowColor.a = pulseAlpha;

                _glowRenderer.color = glowColor;
                _glowRenderer.transform.localScale = new Vector3(pulseScale, pulseScale, 1f);
                _glowRenderer.enabled = pulseAlpha > 0.005f;
            }

            if (_particleSystem != null)
            {
                Color particleColor = currentBaseColor;
                particleColor.a = _fadeAlpha * 0.92f;

                var main = _particleSystem.main;
                main.startColor = particleColor;

                int count = _particleSystem.GetParticles(_particleBuffer);
                for (int i = 0; i < count; i++)
                {
                    _particleBuffer[i].startColor = particleColor;
                }
                if (count > 0)
                {
                    _particleSystem.SetParticles(_particleBuffer, count);
                }
            }
        }

        private void EnsureGlowRenderer()
        {
            if (_glowRenderer != null) return;

            var glowObj = new GameObject("ConcienciaGlow");
            glowObj.transform.SetParent(transform, false);
            glowObj.transform.localPosition = Vector3.zero;

            _glowRenderer = glowObj.AddComponent<SpriteRenderer>();
            _glowRenderer.sprite = GetOrCreateRadialGlowSprite();
            _glowRenderer.sortingOrder = 11; // Just behind Player sprite (12) and above floor/tiles
            if (PixelArtFactory.SpriteMaterial != null)
            {
                _glowRenderer.sharedMaterial = PixelArtFactory.SpriteMaterial;
            }
            _glowRenderer.enabled = false;
        }

        private void EnsureParticleSystem()
        {
            if (_particleSystem != null) return;

            var psObj = new GameObject("ConcienciaParticles");
            psObj.transform.SetParent(transform, false);
            psObj.transform.localPosition = Vector3.zero;

            _particleSystem = psObj.AddComponent<ParticleSystem>();

            var main = _particleSystem.main;
            main.playOnAwake = false;
            main.loop = true;
            main.prewarm = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSize3D = false;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.15f);
            main.maxParticles = 40;

            var emission = _particleSystem.emission;
            emission.enabled = false;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(18f);

            var shape = _particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.28f;

            var col = _particleSystem.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.20f),
                    new GradientAlphaKey(0.85f, 0.75f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var sizeOverLifetime = _particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            var sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.6f);
            sizeCurve.AddKey(0.3f, 1.0f);
            sizeCurve.AddKey(1.0f, 0.2f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var psr = _particleSystem.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                psr.renderMode = ParticleSystemRenderMode.Billboard;
                psr.sortingOrder = 13; // Slightly in front of player to remain clearly visible
                Material mat = GetOrCreateParticleMaterial();
                if (mat != null)
                {
                    psr.sharedMaterial = mat;
                }
            }

            _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static Sprite GetOrCreateRadialGlowSprite()
        {
            if (_radialGlowSprite != null) return _radialGlowSprite;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            float center = (size - 1) * 0.5f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                    float a = Mathf.Clamp01(1f - dist);
                    // Smooth Hermite falloff
                    a = a * a * (3f - 2f * a);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);

            _radialGlowSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                48f);
            _radialGlowSprite.name = "ConcienciaRadialGlow";
            return _radialGlowSprite;
        }

        private static Material GetOrCreateParticleMaterial()
        {
            if (_particleMaterial != null) return _particleMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                _particleMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                var sprite = GetOrCreateRadialGlowSprite();
                if (sprite != null && sprite.texture != null)
                {
                    if (_particleMaterial.HasProperty("_BaseMap")) _particleMaterial.SetTexture("_BaseMap", sprite.texture);
                    if (_particleMaterial.HasProperty("_MainTex")) _particleMaterial.SetTexture("_MainTex", sprite.texture);
                }
            }

            return _particleMaterial;
        }
    }
}
