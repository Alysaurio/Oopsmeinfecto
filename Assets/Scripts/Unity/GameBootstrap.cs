using UnityEngine;

namespace Oopsmeinfecto
{
    /// <summary>
    /// The scene can stay empty: entering Play mode creates the game controller automatically.
    /// If a controller already exists in the scene, it is left untouched.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureGameExists()
        {
            if (Object.FindAnyObjectByType<GameFlowController>() != null) return;

            var gameObject = new GameObject("Oopsmeinfecto");
            gameObject.AddComponent<GameFlowController>();
        }
    }
}
