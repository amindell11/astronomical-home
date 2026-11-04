using System.IO;
using System.Linq;
using System.Reflection;
using Ships;
using Substrate.Services.Locales;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using UnityEngine.SceneManagement;

public static class CaptureLocales
{
    public static void Run()
    {
        ShaderUtil.allowAsyncCompilation = false;
        var output = Path.GetFullPath("../../scratch/locale-variety/captures");
        Directory.CreateDirectory(output);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Camera>("Assets/Prefabs/Cameras/Main Camera.prefab"));
        camera.transform.position = new Vector3(0, 0, -100);
        camera.transform.rotation = Quaternion.identity;
        camera.orthographic = true;
        camera.orthographicSize = 7;
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 1000;
        camera.aspect = 16f / 9;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.allowHDR = true;
        camera.cullingMask = (1 << LayerMask.NameToLayer("Sky")) | (1 << 31);
        var data = camera.gameObject.GetComponent(System.Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime"));
        var dataFields = new SerializedObject(data);
        dataFields.FindProperty("m_RenderPostProcessing").boolValue = true;
        dataFields.ApplyModifiedPropertiesWithoutUndo();
        var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGBHalf);
        target.Create();
        camera.targetTexture = target;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        var ship = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Valis.prefab");
        var rig = Object.Instantiate(ship.VisualRig);
        rig.transform.position = new Vector3(-2, -.8f, 0);
        rig.transform.rotation = Quaternion.Euler(0, 0, -25);
        foreach (var child in rig.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
        foreach (var behaviour in rig.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
        foreach (var canvas in rig.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;
        foreach (var particle in rig.GetComponentsInChildren<ParticleSystem>(true)) particle.gameObject.SetActive(false);
        foreach (var renderer in rig.GetComponentsInChildren<Renderer>(true)) renderer.enabled = rig.Silhouette.Contains(renderer) || (renderer.sharedMaterial && renderer.sharedMaterial.HasProperty("_ContourColor"));
        for (var id = 4; id <= 7; id++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<LocaleSky>($"Assets/Prefabs/Locales/Locale_{id}.prefab");
            var locale = Object.Instantiate(prefab);
            var lighting = (LocaleLighting)typeof(LocaleSky).GetField("lighting", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(locale);
            lighting.Apply();
            camera.transform.position = new Vector3(0, 0, -100);
            camera.orthographicSize = 7;
            Save(camera, image, target, Path.Combine(output, $"Locale_{id}.png"));
            camera.orthographicSize = 14;
            Save(camera, image, target, Path.Combine(output, $"Locale_{id}-zoom.png"));
            camera.orthographicSize = 7;
            camera.transform.position = new Vector3(120, 60, -100);
            rig.transform.position += new Vector3(120, 60, 0);
            Save(camera, image, target, Path.Combine(output, $"Locale_{id}-travel.png"));
            rig.transform.position -= new Vector3(120, 60, 0);
            Object.DestroyImmediate(locale.gameObject);
        }
        camera.targetTexture = null;
        RenderTexture.active = null;
        target.Release();
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(image);
        Object.DestroyImmediate(rig.gameObject);
        Object.DestroyImmediate(camera.gameObject);
        Debug.Log("LOCALE_CAPTURES=" + output);
    }

    private static void Save(Camera camera, Texture2D image, RenderTexture target, string path)
    {
        camera.Render();
        camera.Render();
        RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
    }
}



