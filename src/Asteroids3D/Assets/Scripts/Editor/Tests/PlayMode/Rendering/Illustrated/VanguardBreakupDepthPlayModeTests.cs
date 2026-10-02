#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
namespace Tests.PlayMode.Rendering.Illustrated
{
[Category("Ships"), Category("RequiresGraphics")]
public sealed class VanguardBreakupDepthPlayModeTests
{
    [UnityTest]
    public IEnumerator WorldOccluderHidesDebrisWithNormalDepthTesting()
    {

        var root = new GameObject("Vanguard depth probe");

        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        var target = new RenderTexture(512, 512, 24);
        var pixels = new Texture2D(512, 512, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Visuals/Ships/Vanguard/Breakup/VanguardBreakup.prefab");
            var debris = Object.Instantiate(source, root.transform);
            debris.transform.localRotation = Quaternion.Euler(0, 0, 180);
            debris.transform.localScale = Vector3.one / 3;
            debris.GetComponent<Animation>().clip.SampleAnimation(debris, .2f);
            foreach (var child in debris.GetComponentsInChildren<Transform>()) child.gameObject.layer = 31;
            var cameraObject = new GameObject("Depth camera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform);
            cameraObject.transform.localPosition = new Vector3(0, 0, -10);
            var camera = cameraObject.GetComponent<Camera>();
            camera.cullingMask = 1 << 31;
            camera.orthographic = true;
            camera.orthographicSize = 3;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.targetTexture = target;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var lightObject = new GameObject("Depth light", typeof(Light));
            lightObject.transform.SetParent(root.transform);
            lightObject.GetComponent<Light>().type = LightType.Directional;
            lightObject.transform.rotation = Quaternion.Euler(25, 15, 0);
            var occluder = GameObject.CreatePrimitive(PrimitiveType.Quad);
            occluder.transform.SetParent(root.transform);
            occluder.layer = 31;
            occluder.transform.localScale = Vector3.one * 8;
            material.SetColor("_BaseColor", Color.magenta);
            occluder.GetComponent<Renderer>().sharedMaterial = material;
            Directory.CreateDirectory("../../results/vanguard-breakup/depth");
            occluder.transform.localPosition = new Vector3(0, 0, -3);
            yield return new WaitForEndOfFrame();
            var front = Read("front.png");
            occluder.transform.localPosition = new Vector3(0, 0, 3);
            yield return new WaitForEndOfFrame();
            var back = Read("back.png");
            Assert.That(front, Is.LessThan(25), "Opaque foreground geometry must hide debris.");
            Assert.That(back, Is.GreaterThan(100), "Debris must render when the same geometry moves behind it.");
            Debug.Log($"Depth occlusion passed: front={front}, back={back} non-magenta pixels.");

            int Read(string name)
            {
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                pixels.Apply();
                File.WriteAllBytes("../../results/vanguard-breakup/depth/" + name, pixels.EncodeToPNG());
                var count = 0;
                foreach (var color in pixels.GetPixels32())
                    if (color.r < 240 || color.g > 15 || color.b < 240) count++;
                return count;
            }
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(pixels);
            Object.DestroyImmediate(target);

        }
    }
}

}
#endif
