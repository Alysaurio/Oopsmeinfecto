using UnityEngine;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Visual presentation for a DamageTile (lethal hazard).
    /// Displays a pulsing toxic glow and hosts the skull (efectomuerte) particle system.
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

        private static Material _deathMaterial;

        private void Awake()
        {
            if (_gasParticles == null)
            {
                _gasParticles = GetComponentInChildren<ParticleSystem>(true);
            }
            if (_gasParticles != null)
            {
                ConfigureParticles(_gasParticles);
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

            EnsureParticles(gasParticlePrefab);

            if (_gasParticles != null)
            {
                var main = _gasParticles.main;
                main.playOnAwake = false;
                _gasParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void EnsureParticles(GameObject gasParticlePrefab)
        {
            if (_gasParticles == null)
            {
                _gasParticles = GetComponentInChildren<ParticleSystem>(true);
            }

            if (_gasParticles == null && gasParticlePrefab != null)
            {
                var particles = Instantiate(gasParticlePrefab, transform, false);
                particles.name = "ToxicGasParticles";
                particles.transform.localPosition = Vector3.zero;
                _gasParticles = particles.GetComponent<ParticleSystem>();
            }

            if (_gasParticles == null)
            {
                var particlesObj = new GameObject("ToxicGasParticles");
                particlesObj.transform.SetParent(transform, false);
                particlesObj.transform.localPosition = Vector3.zero;
                _gasParticles = particlesObj.AddComponent<ParticleSystem>();
            }

            ConfigureParticles(_gasParticles);
        }

        public void PlayParticles()
        {
            if (_gasParticles == null)
            {
                EnsureParticles(null);
            }
            if (_gasParticles != null)
            {
                var main = _gasParticles.main;
                main.playOnAwake = false;
                _gasParticles.Play(true);
            }
        }

        public static void ConfigureParticles(ParticleSystem ps)
        {
            if (ps == null) return;

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.prewarm = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            // Start Lifetime: Entre 1.8 y 2.4 segundos
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.4f);

            // Start Speed: Ascendente lenta, entre 0.25f y 0.45f unidades por segundo en el eje Y
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);

            // Start Size: Reducido en 10% (0.35 * 0.9 = 0.315, 0.65 * 0.9 = 0.585)
            // 3D Start Size desactivado para evitar deformacion asimetrica y que las calaveras no se estiren como rayas
            main.startSize3D = false;
            main.startSize = new ParticleSystem.MinMaxCurve(0.315f, 0.585f);
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.315f, 0.585f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.315f, 0.585f);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(0.315f, 0.585f);

            // Orientacion plana respecto a camara para preservar el aspecto cuadrado 16x16
            main.startRotation3D = false;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f);

            main.maxParticles = 20;

            // Emission: Tasa baja y sutil, entre 1.5 y 2.5 particulas por segundo
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(2.0f);

            // Shape: Box o Edge ubicado en la base de la casilla (ancho aproximado de 0.6 casillas, altura 0.1)
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = new Vector3(0f, -0.35f, 0f);
            shape.rotation = new Vector3(-90f, 0f, 0f); // Emite hacia arriba (+Y)
            shape.scale = new Vector3(0.6f, 0.1f, 0.1f);

            // Oscilacion Horizontal (Vaiven): Habilita el modulo Noise (fuerza muy suave entre 0.1f y 0.2f, frecuencia baja)
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.2f;
            noise.damping = true;

            // Color over Lifetime: El canal Alfa debe iniciar al 80%, mantenerse durante su trayecto y desvanecerse suavemente a 0% en el ultimo tercio de su vida util
            var col = ps.colorOverLifetime;
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
                    new GradientAlphaKey(0.80f, 0.0f),
                    new GradientAlphaKey(0.80f, 0.67f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(grad);

            // Particle System Renderer: modo Billboard, Filter Mode Point, Sorting Layer suelo/entidades
            var psr = ps.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                psr.renderMode = ParticleSystemRenderMode.Billboard;
                psr.sortingOrder = 7;
                Material mat = ResolveDeathMaterial();
                if (mat != null)
                {
                    psr.sharedMaterial = mat;
                }
            }
        }

        public static Material ResolveDeathMaterial()
        {
            if (_deathMaterial != null) return _deathMaterial;

            _deathMaterial = Resources.Load<Material>("DamageTileParticleMaterial");
            if (_deathMaterial == null)
            {
                var template = Resources.Load<Material>("ToxicGasParticleMaterial");
                if (template != null) _deathMaterial = new Material(template);
            }

            var sprite = Resources.Load<Sprite>("efectomuerte");
            Texture tex = sprite != null ? sprite.texture : null;
            if (tex == null)
            {
                tex = Resources.Load<Texture2D>("efectomuerte");
            }

            if (_deathMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    _deathMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                }
            }

            if (_deathMaterial != null && tex != null)
            {
                if (_deathMaterial.HasProperty("_BaseMap")) _deathMaterial.SetTexture("_BaseMap", tex);
                if (_deathMaterial.HasProperty("_MainTex")) _deathMaterial.SetTexture("_MainTex", tex);
            }

            return _deathMaterial;
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
