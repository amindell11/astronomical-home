using Game.Bootstrap;
using Movement.MPC.Field;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tests.EditMode
{
    /// <summary>
    /// Guards the PR3 scene surgery: the session-root GameObject in each bootstrap scene must carry
    /// BOTH halves of the cleaved session tier — the above-seam <see cref="GameDriver"/> and the
    /// below-seam <see cref="SessionHost"/> — as siblings on the same object. A driver with no host to
    /// drive would NRE at first transition, so this is a load-bearing wiring invariant.
    /// </summary>
    [Category("Bootstrap")]
    public class SceneWiringEditModeTests
    {
        [TestCase("Assets/Scenes/InitScene.unity")]
        [TestCase("Assets/Scenes/TestScene.unity")]
        public void SessionRoot_HasDriverAndHostSiblings(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                GameDriver driver = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    driver = root.GetComponentInChildren<GameDriver>(true);
                    if (driver) break;
                }

                Assert.IsNotNull(driver, $"{scenePath} must contain a GameDriver");
                Assert.IsNotNull(driver.GetComponent<SessionHost>(),
                    $"{scenePath}: the GameDriver's GameObject must also carry a SessionHost sibling");
                Assert.IsNotNull(driver.GetComponent<NavFieldService>(),
                    $"{scenePath}: the SessionHost's GameObject must carry a NavFieldService sibling");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
