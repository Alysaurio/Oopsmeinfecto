using System.Collections;
using UnityEngine;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Spawns a ghost silhouette of the player when moving in the infected/zombie state.
    /// Fades out the alpha from 1.0 to 0.0 in exactly 0.3 seconds using Mathf.Lerp, then destroys the GameObject.
    /// </summary>
    public sealed class RetroGhostTrail : MonoBehaviour
    {
        private static readonly Color ToxicGreen = new Color(38f / 255f, 115f / 255f, 51f / 255f, 1f); // #267333

        private SpriteRenderer _spriteRenderer;

        public static RetroGhostTrail Spawn(Vector3 position, Sprite sprite, bool flipX, int sortingOrder = 9, Material material = null)
        {
            var ghostObj = new GameObject("RetroGhostTrail");
            ghostObj.transform.position = position;

            var sr = ghostObj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.flipX = flipX;
            sr.sortingOrder = sortingOrder;
            if (material != null)
            {
                sr.sharedMaterial = material;
            }

            var trail = ghostObj.AddComponent<RetroGhostTrail>();
            trail.Initialize(sr);
            return trail;
        }

        public void Initialize(SpriteRenderer spriteRenderer)
        {
            _spriteRenderer = spriteRenderer;
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = ToxicGreen;
            }
            StartCoroutine(FadeOutRoutine());
        }

        private IEnumerator FadeOutRoutine()
        {
            float duration = 0.3f;
            float elapsed = 0f;
            Color color = ToxicGreen;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float alpha = Mathf.Lerp(1.0f, 0.0f, t);

                if (_spriteRenderer != null)
                {
                    color.a = alpha;
                    _spriteRenderer.color = color;
                }
                yield return null;
            }

            if (_spriteRenderer != null)
            {
                color.a = 0f;
                _spriteRenderer.color = color;
            }

            Destroy(gameObject);
        }
    }
}
