#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using Cameras;
using Capture;
using Game;
using Game.Travel;
using NUnit.Framework;
using Ships;
using Substrate;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UI.Hangar;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class SectorJumpAuthoredProbe : CaptureScenario
{
    public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;
    public override CaptureConfig Config => new()
    {
        clipName = "sector-jump-authored",
        width = 960, height = 540, everyFixedSteps = 1,
        configureView = (camera, light) => { camera.enabled = false; light.enabled = false; }
    };

    public override IEnumerator Run()
    {
        var boot = SceneManager.GetActiveScene();
        var clock = Time.timeScale;
        yield return SceneManager.LoadSceneAsync("InitScene", LoadSceneMode.Additive);
        GameHost host = null;
        Material material = null;
        GameObject screen = null;
        Camera bakeCamera = null;
        Texture2D from = null, to = null, shipImage = null;
        Renderer[] renderers = null;
        bool[] rendererEnabled = null;
        MonoBehaviour[] debug = null;
        bool[] debugEnabled = null;
        try
        {
            HangarScreen hangar = null;
            var deadline = Time.realtimeSinceStartup + 60f;
            while (!hangar && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                hangar = Object.FindFirstObjectByType<HangarScreen>();
            }
            Assert.IsTrue(hangar);
            host = Object.FindFirstObjectByType<GameHost>();
            hangar.launchButton.onClick.Invoke();
            while (!host.ActiveSector && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(host.ActiveSector);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            var rig = host.GetComponentInChildren<PlayerRig>();
            var observer = host.GetComponentInChildren<ObserverCam>();
            var ship = rig.Player;
            var start = ship.transform.position;
            var hull = ship.HealthPct;
            ship.Commander.enabled = false;
            observer.GetComponent<ObserverCamInputHandler>().enabled = false;
            observer.enabled = false;
            ship.Rigidbody.isKinematic = true;
            ship.Rigidbody.interpolation = RigidbodyInterpolation.None;
            Place(ship, start, GamePlane.Rotation);
            observer.transform.position = start - GamePlane.Normal * 10;
            observer.Cam.orthographicSize = 12;
            observer.Cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            Time.timeScale = 0;

            var sourceScene = SceneManager.GetActiveScene();
            bakeCamera = new GameObject("Jump source camera").AddComponent<Camera>();
            bakeCamera.CopyFrom(observer.Cam);
            bakeCamera.enabled = false;
            bakeCamera.transform.SetPositionAndRotation(observer.transform.position, observer.transform.rotation);
            bakeCamera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            bakeCamera.cullingMask = 1 << LayerIds.Sky;
            bakeCamera.clearFlags = CameraClearFlags.SolidColor;
            bakeCamera.backgroundColor = new Color(0.005f, 0.01f, 0.025f, 1);
            from = Bake(bakeCamera, 960, 540);

            if (!SceneManager.GetSceneByName("Locale_2").isLoaded)
                yield return SceneManager.LoadSceneAsync("Locale_2", LoadSceneMode.Additive);
            SceneManager.SetActiveScene(SceneManager.GetSceneByName("Locale_2"));
            yield return null;
            to = Bake(bakeCamera, 960, 540);
            SceneManager.SetActiveScene(sourceScene);
            yield return null;

            var shipRenderers = ship.VisualRig.Silhouette.ToArray();
            var layers = shipRenderers.Select(renderer => renderer.gameObject.layer).ToArray();
            var particles = ship.VisualRig.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var particle in particles) particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var previewLayer = LayerMask.NameToLayer("ShipPreview");
            Assert.GreaterOrEqual(previewLayer, 0);
            foreach (var renderer in shipRenderers) renderer.gameObject.layer = previewLayer;
            bakeCamera.cullingMask = 1 << previewLayer;
            bakeCamera.backgroundColor = Color.clear;
            bakeCamera.orthographicSize = 1.7f;
            shipImage = Bake(bakeCamera, 512, 512);
            Assert.Less(shipImage.GetPixel(0, 0).a, 0.01f, "The isolated ship has a transparent surround");
            Assert.Greater(shipImage.GetPixels32().Count(pixel => pixel.a > 128), 100,
                "The isolated ship contains visible hull pixels");
            for (var index = 0; index < shipRenderers.Length; index++)
                shipRenderers[index].gameObject.layer = layers[index];

            Save(from, "LocaleFrom");
            Save(to, "LocaleTo");
            Save(shipImage, "Ship");
            const string profilePath = "Assets/Settings/Game/SectorJumpProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<SectorJumpProfile>(profilePath);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<SectorJumpProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
                AssetDatabase.SaveAssetIfDirty(profile);
            }
            material = new Material(Shader.Find("Astronomical/Travel/SectorJump"));
            material.SetTexture("_FromSky", from);
            material.SetTexture("_ToSky", to);
            material.SetTexture("_ShipImage", shipImage);

            renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            rendererEnabled = renderers.Select(renderer => renderer.enabled).ToArray();
            foreach (var renderer in renderers) renderer.enabled = false;
            debug = Resources.FindObjectsOfTypeAll<MonoBehaviour>()
                .Where(component => component.GetType().Name == "FMODRuntimeManagerOnGUIHelper").ToArray();
            debugEnabled = debug.Select(component => component.enabled).ToArray();
            Assert.IsNotEmpty(debug);
            foreach (var component in debug) component.enabled = false;

            screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "Authored jump screen";
            Object.DestroyImmediate(screen.GetComponent<Collider>());
            screen.transform.SetPositionAndRotation(observer.transform.position + observer.transform.forward * 5,
                observer.transform.rotation);
            screen.transform.localScale = new Vector3(24 * 16f / 9f, 24, 1);
            screen.GetComponent<Renderer>().sharedMaterial = material;

            var canvases = rig.Overlay.GetComponentsInChildren<Canvas>(true)
                .Concat(ship.VisualRig.GetComponentsInChildren<Canvas>(true))
                .Where(canvas => !canvas.transform.parent || !canvas.transform.parent.GetComponentInParent<Canvas>()).ToArray();
            var groups = canvases.Select(canvas => canvas.GetComponent<CanvasGroup>()
                ? canvas.GetComponent<CanvasGroup>() : canvas.gameObject.AddComponent<CanvasGroup>()).ToArray();
            var bars = ship.VisualRig.GetComponentsInChildren<UI.PlayerState.StatusBarUI>(true);
            Assert.IsNotEmpty(bars);
            Assert.IsTrue(bars.All(bar => groups.Contains(bar.GetComponentInParent<CanvasGroup>())));
            Film(ship);
            Time.timeScale = 0;
            var frames = Mathf.CeilToInt((profile.Duration + 1.1f) / 0.02f);
            for (var frame = 0; frame < frames; frame++)
            {
                var time = frame * 0.02f - 0.4f;
                SectorJumpPreview.Apply(material, profile, time, -135, 16f / 9f, 540);
                Place(ship, start, GamePlane.Rotation * Quaternion.AngleAxis(
                    -135 * SectorJumpProfile.Ease(time / profile.turnDuration), Vector3.forward));
                foreach (var group in groups) group.alpha = profile.HudAlpha(time);
                if (time > profile.turnDuration + profile.chargeDuration &&
                    time < profile.turnDuration + profile.chargeDuration + profile.travelDuration)
                    Assert.IsTrue(groups.All(group => group.alpha == 0));
                Assert.AreEqual(hull, ship.HealthPct);
                Assert.AreEqual(start, ship.transform.position);
                FilmStep();
                yield return null;
            }
            Assert.AreEqual(1, material.GetVector("_Motion").z, 0.001f);
            Capture.End();
        }
        finally
        {
            if (debug != null)
                for (var index = 0; index < debug.Length; index++)
                    if (debug[index]) debug[index].enabled = debugEnabled[index];
            if (renderers != null)
                for (var index = 0; index < renderers.Length; index++)
                    if (renderers[index]) renderers[index].enabled = rendererEnabled[index];
            Object.DestroyImmediate(screen);
            Object.DestroyImmediate(material);
            if (bakeCamera) Object.DestroyImmediate(bakeCamera.gameObject);
            Object.DestroyImmediate(from);
            Object.DestroyImmediate(to);
            Object.DestroyImmediate(shipImage);
            if (host)
            {
                host.GetComponent<UnitService>().Clear();
                Object.DestroyImmediate(host.gameObject);
            }
            Time.timeScale = clock;
            SceneManager.SetActiveScene(boot);
        }
        foreach (var name in new[] { "InitScene", "Locale_1", "Locale_2", "Locale_3" })
            if (SceneManager.GetSceneByName(name).isLoaded) yield return SceneManager.UnloadSceneAsync(name);
    }

    private static Texture2D Bake(Camera camera, int width, int height)
    {
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        var prior = RenderTexture.active;
        camera.targetTexture = target;
        camera.aspect = (float)width / height;
        camera.Render();
        RenderTexture.active = target;
        var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();
        RenderTexture.active = prior;
        camera.targetTexture = null;
        RenderTexture.ReleaseTemporary(target);
        return result;
    }

    private static void Save(Texture2D image, string name)
    {
        var path = "Assets/Visuals/Travel/" + name + ".png";
        File.WriteAllBytes(path, image.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.SaveAndReimport();
    }

    private static void Place(Ship ship, Vector3 position, Quaternion rotation)
    {
        ship.transform.SetPositionAndRotation(position, rotation);
        ship.Rigidbody.position = position;
        ship.Rigidbody.rotation = rotation;
    }
}
#endif
