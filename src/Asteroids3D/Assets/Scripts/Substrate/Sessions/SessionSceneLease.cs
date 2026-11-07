using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Substrate.Sessions
{
    /// <summary>
    /// Ties a session scene to the GameObject that owns the session. Releasing the lease, or destroying the owner
    /// without a release, unloads the session scene, first handing the active scene back to the previous one if the
    /// session scene still holds it. A scene already unloaded, by a finished release or at play-mode exit, is left alone.
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
