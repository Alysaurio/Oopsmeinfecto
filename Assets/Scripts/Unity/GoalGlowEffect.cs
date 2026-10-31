using UnityEngine;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Visual presentation for the victory switch tile.
    /// Emits a soft, pulsing halo and the floating spiral (efectometa) particle system to clearly distinguish the goal position without altering gameplay logic.
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

        [Header("Particles")]
        [SerializeField] private ParticleSystem _goalParticles;

        public SpriteRenderer GlowRenderer => _glowRenderer;
        public ParticleSystem GoalParticles => _goalParticles;

        private static Material _goalMaterial;

        private void Awake()
        {
            if (_goalParticles == null)
            {
                _goalParticles = GetComponentInChildren<ParticleSystem>(true);
            }
            if (_goalParticles != null)
            {
                ConfigureParticles(_goalParticles);
                var main = _goalParticles.main;
                main.playOnAwake = false;
                _goalParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

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

            EnsureParticles();

            if (_goalParticles != null)
            {
                var main = _goalParticles.main;
                main.playOnAwake = false;
                _goalParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void EnsureParticles()
        {
            if (_goalParticles == null)
            {
                _goalParticles = GetComponentInChildren<ParticleSystem>(true);
            }

            if (_goalParticles == null)
            {
                var particlesObj = new GameObject("GoalParticles");
                particlesObj.transform.SetParent(transform, false);
                particlesObj.transform.localPosition = Vector3.zero;
                _goalParticles = particlesObj.AddComponent<ParticleSystem>();
            }

            ConfigureParticles(_goalParticles);
        }

        public void PlayParticles()
        {
            if (_goalParticles == null)
            {
                EnsureParticles();
            }
            if (_goalParticles != null)
            {
                var main = _goalParticles.main;
                main.playOnAwake = false;
                _goalParticles.Play(true);
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

            // Start Lifetime: Entre 1.5 y 2.2 segundos
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.2f);

            // Start Speed: Acortado a la mitad (0.3 / 2 = 0.15, 0.5 / 2 = 0.25) para que no viajen tan lejos
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.25f);

            // Start Size: Reducido a la mitad (0.4 / 2 = 0.2, 0.7 / 2 = 0.35)
            main.startSize3D = false;
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);

            // Start Rotation: ángulo inicial aleatorio
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);

            main.maxParticles = 20;

            // Emission: Tasa moderada, entre 2.0 y 3.5 particulas por segundo
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(2.8f);

            // Shape: Circle o Box centrado en la casilla (radio 0.3 casillas)
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.position = Vector3.zero;
            shape.rotation = new Vector3(-90f, 0f, 0f); // Emite hacia arriba (+Y)
            shape.radius = 0.3f;

            // Rotation over Lifetime: Velocidad de rotacion angular continua entre 90 y 160 grados/seg (tanto positiva como negativa para variar el sentido de giro)
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.separateAxes = false;
            rot.z = new ParticleSystem.MinMaxCurve(-160f * Mathf.Deg2Rad, 160f * Mathf.Deg2Rad);

            // Oscilacion Horizontal (Vaiven): vaiven sutil en el eje X
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 0.4f;
            noise.scrollSpeed = 0.2f;
            noise.damping = true;

            // Color over Lifetime: Curva de Alfa progresiva y desvanecimiento a 0% a altura maxima. Tinte cian/azul armonizado
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var goalCyan = new Color(0.20f, 0.85f, 1.0f, 1f);
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(goalCyan, 0f),
                    new GradientColorKey(goalCyan, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.85f, 0.25f),
                    new GradientAlphaKey(0.80f, 0.65f),
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
                Material mat = ResolveGoalMaterial();
                if (mat != null)
                {
                    psr.sharedMaterial = mat;
                }
            }
        }

        public static Material ResolveGoalMaterial()
        {
            if (_goalMaterial != null) return _goalMaterial;

            _goalMaterial = Resources.Load<Material>("GoalParticleMaterial");
            if (_goalMaterial == null)
            {
                var template = Resources.Load<Material>("DamageTileParticleMaterial");
                if (template != null) _goalMaterial = new Material(template);
            }

            var sprite = Resources.Load<Sprite>("efectometa");
            Texture tex = sprite != null ? sprite.texture : null;
            if (tex == null)
            {
                tex = Resources.Load<Texture2D>("efectometa");
            }

            if (_goalMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    _goalMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                }
            }

            if (_goalMaterial != null && tex != null)
            {
                if (_goalMaterial.HasProperty("_BaseMap")) _goalMaterial.SetTexture("_BaseMap", tex);
                if (_goalMaterial.HasProperty("_MainTex")) _goalMaterial.SetTexture("_MainTex", tex);
            }

            return _goalMaterial;
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
