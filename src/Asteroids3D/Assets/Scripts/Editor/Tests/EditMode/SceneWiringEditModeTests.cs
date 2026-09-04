using Game.Play;
using Game.Services;
using NUnit.Framework;
using UnityEditor.SceneManagement;

namespace Tests.EditMode
{
    /// <summary>
    /// Each bootstrap scene must carry a session root the host can actually compose against: a
    /// <see cref="GameSessionHost"/> whose GameObject also holds the two services the session
    /// constructor requires. A host without them NREs at first transition, so this is a
    /// load-bearing wiring invariant.
    /// </summary>
    [Category("Bootstrap")]
    public class SceneWiringEditModeTests
    {
        [TestCase("Assets/Scenes/InitScene.unity")]
        [TestCase("Assets/Scenes/TestScene.unity")]
        public void SessionRoot_HasHostAndItsServices(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                GameSessionHost host = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    host = root.GetComponentInChildren<GameSessionHost>(true);
                    if (host) break;
                }

                Assert.IsNotNull(host, $"{scenePath} must contain a GameSessionHost");
                Assert.IsNotNull(host.GetComponent<UnitService>(),
                    $"{scenePath}: the GameSessionHost's GameObject must also carry a UnitService");
                Assert.IsNotNull(host.GetComponent<ObjectiveService>(),
                    $"{scenePath}: the GameSessionHost's GameObject must also carry an ObjectiveService");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
