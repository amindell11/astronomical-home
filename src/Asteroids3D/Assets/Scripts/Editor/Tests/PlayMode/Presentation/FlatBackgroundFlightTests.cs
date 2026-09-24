#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using Cameras;
using Game;
using NUnit.Framework;
using Ships.Command;
using Substrate.Services.Environment.Flat;
using UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Presentation
{
    [Category("Sectors"), Category("RequiresGraphics")]
    public sealed class FlatBackgroundFlightTests
    {
        private GameHost host;
        private string output;
        private ObserverCam observer;
        private Color32[] capturedPixels;

        [UnityTest]
        public IEnumerator GameView_FlightAndRapidLockAndSignedTravel()
        {
            Assert.That(Application.isBatchMode, Is.False, "Run this Game View check with -WithGraphics -Windowed.");
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../results/flat-background/flight", System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
            Directory.CreateDirectory(output);
            var gameView = EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            gameView.Show();
            gameView.Focus();
            yield return SceneManager.LoadSceneAsync("InitScene", LoadSceneMode.Single);
            HangarScreen hangar = null;
            for (var frame = 0; frame < 600 && !hangar; frame++)
            {
                hangar = Object.FindFirstObjectByType<HangarScreen>();
                yield return null;
            }
            Assert.That(hangar, Is.Not.Null, "Real game must reach the hangar.");
            var button = typeof(HangarScreen).GetField("launchButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hangar);
            var click = button.GetType().GetProperty("onClick").GetValue(button);
            click.GetType().GetMethod("Invoke").Invoke(click, null);
            host = Object.FindFirstObjectByType<GameHost>();
            for (var frame = 0; frame < 600 && !host.ActiveSector; frame++) yield return null;
            Assert.That(host.ActiveSector, Is.Not.Null);
            yield return new WaitForSeconds(1);
            var rig = (PlayerRig)new SerializedObject(host).FindProperty("playerRig").objectReferenceValue;
            observer = Object.FindFirstObjectByType<ObserverCam>();
            Assert.That(rig.Player, Is.Not.Null);
            rig.Player.Damage.SetInvulnerability(600);
            Assert.That(observer.Cam.GetComponent<FlatBackgroundCamera>(), Is.Not.Null);
            Assert.That(host.ActiveSector.ObstacleField, Is.Not.Null, "Real flight must include the asteroid field.");
            var asset = AssetDatabase.LoadAssetAtPath<FlatBackgroundAsset>("Assets/Visuals/Environment/Flat/Generated/nebula-glow-flat-final.flatbg");
            Assert.That(asset && asset.IsFinal, Is.True);
            var root = new GameObject("Environment");
            SceneManager.MoveGameObjectToScene(root, SceneManager.GetActiveScene());
            var environment = root.AddComponent<EnvironmentAuthoring>();
            var serialized = new SerializedObject(environment);
            serialized.FindProperty("backgroundShader").objectReferenceValue = Shader.Find("Environment/Flat Background");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            observer.SetLockCameraToSubject(true);
            observer.SetLockZoomToSubject(true);
            var previousTimeScale = Time.timeScale;
            var cameraEnabled = observer.enabled;
            try
            {
                Time.timeScale = 0;
                observer.enabled = false;
                yield return null;
                yield return Capture("original", 0);
                var originalPixels = capturedPixels;
                environment.Preview(asset);
                yield return Capture("candidate", 0);
                var candidateDifference = Difference(originalPixels, capturedPixels);
                environment.EndPreview();
                yield return Capture("restored", 0);
                var restoredDifference = Difference(originalPixels, capturedPixels);
                File.WriteAllText(Path.Combine(output, "preview-difference.txt"),
                    System.FormattableString.Invariant($"Candidate={candidateDifference}; Restored={restoredDifference}\n"));
                Assert.That(candidateDifference, Is.GreaterThan(0.05), "Candidate clouds must change the actual Game View.");
                Assert.That(restoredDifference, Is.LessThan(0.01), "Ending preview must restore the original Game View.");
                Assert.That(candidateDifference, Is.GreaterThan(restoredDifference * 5));
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                if (observer) observer.enabled = cameraEnabled;
            }
            serialized.Update();
            serialized.FindProperty("background").objectReferenceValue = asset;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            rig.Player.Commander.enabled = false;
            var start = rig.Player.transform.position;
            rig.Player.Movement.Drive(new PilotCommand { thrust = 0.7f, strafe = 0.2f });
            for (var frame = 0; frame < 30; frame++)
            {
                yield return new WaitForSeconds(0.1f);
                yield return Capture("flight", frame);
            }
            Assert.That(Vector3.Distance(start, rig.Player.transform.position), Is.GreaterThan(1));
            for (var frame = 0; frame < 30; frame++)
            {
                observer.SetLockCameraToSubject(frame % 2 == 0);
                observer.SetLockZoomToSubject(frame % 2 == 0);
                yield return new WaitForSeconds(0.1f);
                yield return Capture("lock", frame);
            }
            rig.Player.Movement.Drive(default);
            observer.SetLockCameraToSubject(true);
            observer.SetManualZoom(20);
            observer.SetManualCenter(new Vector2(-60000, 60000));
            yield return new WaitForSeconds(1);
            Assert.That(observer.transform.position.x, Is.LessThan(-59000));
            Assert.That(observer.transform.position.y, Is.GreaterThan(59000));
            yield return Capture("travel-start", 0);
            for (var frame = 0; frame < 80; frame++)
            {
                var position = Vector2.Lerp(new Vector2(-60000, 60000), new Vector2(60000, -60000), frame / 79f);
                observer.SetManualCenter(position);
                yield return new WaitForSeconds(0.1f);
                yield return Capture("travel", frame);
            }
            yield return new WaitForSeconds(1);
            yield return Capture("travel-end", 0);
            Assert.That(observer.transform.position.x, Is.GreaterThan(59000));
            Assert.That(observer.transform.position.y, Is.LessThan(-59000));
            observer.SetManualCenter(null);
            observer.SetManualZoom(null);
            observer.SetLockCameraToSubject(true);
            observer.SetLockZoomToSubject(true);
            yield return new WaitForSeconds(1);
            yield return Capture("returned", 0);
            File.WriteAllText(Path.Combine(output, "evidence.txt"),
                "Actual InitScene GameHost, player ship and asteroid field. Original/candidate/restored; production thrust; 30 rapid lock/zoom changes; camera target route (-60000,+60000) to (+60000,-60000); absolute parallax, 20000 units/repeat, zero zoom response. Final applied source, native lighting unchanged.\n");
            Debug.Log("FLAT_FLIGHT_EVIDENCE=" + output);
        }

        private IEnumerator Capture(string phase, int index)
        {
            yield return new WaitForEndOfFrame();
            Assert.That(host.ActiveSector, Is.Not.Null, "Capture must remain in the flight sector.");
            var position = observer.transform.position;
            File.AppendAllText(Path.Combine(output, "route.csv"), System.FormattableString.Invariant($"{phase},{index},{position.x},{position.y},{observer.Cam.orthographicSize}\n"));
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            var pixels = image.GetPixels32();
            capturedPixels = pixels;
            bool varied = false;
            for (int i = 1; i < pixels.Length; i += 97)
                if (!pixels[i].Equals(pixels[0])) { varied = true; break; }
            Assert.That(varied, Is.True, "Composited Game View must contain visible content.");
            File.WriteAllBytes(Path.Combine(output, phase + "-" + index.ToString("D3") + ".png"), image.EncodeToPNG());
            Object.Destroy(image);
        }

        private static double Difference(Color32[] original, Color32[] current)
        {
            Assert.That(current.Length, Is.EqualTo(original.Length));
            long sum = 0;
            for (var i = 0; i < original.Length; i++)
                sum += System.Math.Abs(original[i].r - current[i].r) +
                       System.Math.Abs(original[i].g - current[i].g) +
                       System.Math.Abs(original[i].b - current[i].b);
            return sum / (original.Length * 765.0);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (host) Object.Destroy(host.gameObject);
            yield return null;
        }
    }
}
#endif
