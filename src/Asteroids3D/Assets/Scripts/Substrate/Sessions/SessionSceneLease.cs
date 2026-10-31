using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Substrate.Sessions
{
    /// <summary>
    /// Ties a session scene to the GameObject that owns the session. Releasing it, or destroying the owner without a
    /// release, makes the previously active scene active again, then unloads the session scene. The previous scene is
    /// restored only while the session scene is still the active one and the previous scene is still loaded; a session
    /// scene that is no longer loaded (play-mode exit, a finished release) is left alone.
    /// </summary>
    public sealed class SessionSceneLease : MonoBehaviour
    {
        private Scene scene;
        private Scene previous;

        public static SessionSceneLease Attach(GameObject owner, Scene scene, Scene previous)
        {
            var lease = owner.AddComponent<SessionSceneLease>();
            lease.scene = scene;
            lease.previous = previous;
            return lease;
        }

        public IEnumerator Release()
        {
            yield return Unload();
            Destroy(this);
        }

        private void OnDestroy() => Unload();

        private AsyncOperation Unload()
        {
            if (!scene.isLoaded) return null;
            if (SceneManager.GetActiveScene() == scene && previous.isLoaded)
                SceneManager.SetActiveScene(previous);
            return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
