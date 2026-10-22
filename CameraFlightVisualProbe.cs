#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using Cameras;
using Game;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Cameras
{
    [Category("Camera"), Category("RequiresGraphics")]
    public class CameraFlightVisualProbe : InputTestFixture
    {
        [UnityTest]
        public IEnumerator FlyAndCaptureThreeViews()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/InitScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
            UI.Hangar.HangarScreen hangar = null;
            var deadline = Time.realtimeSinceStartup + 60f;
            while (!hangar && Time.realtimeSinceStartup < deadline)
            {
                hangar = Object.FindFirstObjectByType<UI.Hangar.HangarScreen>();
                yield return null;
            }
            Assert.IsTrue(hangar, "hangar appeared");
            hangar.launchButton.onClick.Invoke();
            var host = Object.FindFirstObjectByType<GameHost>();
            while (!host.ActiveSector && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(host.ActiveSector, "sector loaded");
            var rig = Object.FindFirstObjectByType<PlayerRig>();
            var observer = Object.FindFirstObjectByType<ObserverCam>();
            Assert.IsTrue(rig.Player.gameObject.activeInHierarchy);
            var target = new RenderTexture(1280, 720, 24);
            target.Create();
            observer.Cam.targetTexture = target;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../results/camera-flight"));
            Directory.CreateDirectory(outDir);
            try
            {
                Set(mouse.position, new Vector2(750, 500));
                var initial = rig.Player.transform.position;
                Press(keyboard.wKey);
                yield return new WaitForSeconds(1f);
                Release(keyboard.wKey);
                Assert.That(Vector3.Distance(initial, rig.Player.transform.position), Is.GreaterThan(0.1f), "player flies");
                Time.timeScale = 0f;
                for (var i = 0; i < 3; i++)
                {
                    if (i > 0)
                    {
                        Press(keyboard.f6Key);
                        yield return null;
                        Release(keyboard.f6Key);
                    }
                    for (var frame = 0; frame < 20; frame++) yield return null;
                    Assert.AreEqual((FlightView)i, observer.View);
                    observer.Cam.Render();
                    var previous = RenderTexture.active;
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                    image.Apply();
                    RenderTexture.active = previous;
                    var pixels = image.GetPixels32();
                    Assert.IsTrue(pixels.Any(p => p.r != pixels[0].r || p.g != pixels[0].g || p.b != pixels[0].b));
                    File.WriteAllBytes(Path.Combine(outDir, observer.View + ".png"), image.EncodeToPNG());
                    var commander = (Game.Player.PlayerCommander)rig.Player.Commander;
                    var mousePoint = commander.playerInput.GetMouseWorldPosition();
                    var projected = observer.Cam.WorldToScreenPoint(mousePoint);
                    Assert.That(Vector2.Distance(projected, mouse.position.ReadValue()), Is.LessThan(0.02f));
                    Debug.Log($"CAMERA_FLIGHT mode={observer.View} player={rig.Player.transform.position} aim={mousePoint}");
                }
            }
            finally
            {
                Time.timeScale = 1f;
                observer.Cam.targetTexture = null;
                Object.Destroy(image);
                target.Release();
                Object.Destroy(target);
                Object.Destroy(host.gameObject);
            }
        }
    }
}
#endif
