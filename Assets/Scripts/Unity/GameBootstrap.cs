using UnityEngine;

namespace Oopsmeinfecto
{
    /// <summary>
    /// The scene can stay empty: entering Play mode creates the game controller automatically.
    /// If a controller already exists in the scene, it is left untouched.
    /// </summary>
    public static class GameBootstrap
    {
        /// <summary>
        /// Domain reload is disabled in this project, so static UnityEngine.Object caches can
        /// survive Stop/Play as destroyed references. Reset them before any scene object asks
        /// for a sprite or material.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            PixelArtFactory.ResetCache();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureGameExists()
        {
            if (Object.FindAnyObjectByType<GameFlowController>() != null) return;

            var gameObject = new GameObject("Oopsmeinfecto");
            gameObject.AddComponent<GameFlowController>();
        }
    }
}
