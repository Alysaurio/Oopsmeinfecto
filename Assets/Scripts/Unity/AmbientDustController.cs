using UnityEngine;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Creates and manages an atmospheric ambient dust Particle System as a child of the Main Camera.
    /// Particles appear as tiny floating pixel-like specks with subtle noise oscillation.
    /// </summary>
    public sealed class AmbientDustController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private Color _dustColor = new Color(0.85f, 0.95f, 1.0f, 0.12f); // Desaturated white/cyan, 12% alpha
        [SerializeField] private float _minLifetime = 5f;
        [SerializeField] private float _maxLifetime = 8f;
        [SerializeField] private float _minSpeed = 0.1f;
        [SerializeField] private float _maxSpeed = 0.2f;

        private ParticleSystem _particleSystem;

        public static AmbientDustController AttachToCamera(Camera targetCamera)
        {
            if (targetCamera == null) return null;

            var existing = targetCamera.GetComponentInChildren<AmbientDustController>();
            if (existing != null) return existing;

            var dustObj = new GameObject("AmbientDust");
            dustObj.transform.SetParent(targetCamera.transform, false);
            dustObj.transform.localPosition = new Vector3(0f, 0f, 10f);

            var controller = dustObj.AddComponent<AmbientDustController>();
            controller.Initialize(targetCamera);
            return controller;
        }

        public void Initialize(Camera targetCamera)
        {
            _particleSystem = gameObject.GetComponent<ParticleSystem>();
            if (_particleSystem == null)
            {
                _particleSystem = gameObject.AddComponent<ParticleSystem>();
            }

            var main = _particleSystem.main;
            main.playOnAwake = true;
            main.loop = true;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(_minLifetime, _maxLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(_minSpeed, _maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.10f);
            main.startColor = _dustColor;
            main.maxParticles = 20;

            // Emission: 10-15 particles in screen. Average lifetime = 6.5s => rate = ~2 particles/sec
            var emission = _particleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 2f;

            // Shape: Box covering the visible camera viewport
            var shape = _particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            float height = targetCamera != null ? targetCamera.orthographicSize * 2f : 16f;
            float width = height * (targetCamera != null ? targetCamera.aspect : (16f / 9f));
            shape.scale = new Vector3(Mathf.Max(24f, width * 1.2f), Mathf.Max(16f, height * 1.2f), 0f);

            // Movement: subtle noise module for erratic floating
            var noise = _particleSystem.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.2f;
            noise.damping = true;

            // Renderer: default material with very small max particle size (crisp square pixels)
            var psr = gameObject.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                psr.renderMode = ParticleSystemRenderMode.Billboard;
                psr.maxParticleSize = 0.006f;
                psr.sortingOrder = 8; // Drawn above floor and walls
                if (psr.sharedMaterial == null)
                {
                    psr.sharedMaterial = PixelArtFactory.SpriteMaterial;
                }
            }

            if (!_particleSystem.isPlaying)
            {
                _particleSystem.Play();
            }
        }
    }
}
