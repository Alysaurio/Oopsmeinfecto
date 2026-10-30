using UnityEngine;

namespace Oopsmeinfecto
{
    /// <summary>
    /// Safety net for scenes that do not contain the game hierarchy. The real scene owns a
    /// persistent "Game" root with the controllers, so this bootstrap normally does nothing.
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
