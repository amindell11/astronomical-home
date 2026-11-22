#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Capture.Recording;
using Capture.Views;
using Game;
using NUnit.Framework;
using Ships;
using Tests.PlayMode.Recording;
using UI.Hangar;
using UI.Hangar.Hardware;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Capture for the hologram tuner (art/ui/hangar-hologram): the hangar plate with no hologram, and each
// offered ship's stage buffers in its own paint. Copy to scratch/capture/ and run it as a capture scenario.
public sealed class HologramBuffersProbe : CaptureScenario
{
    private const string ShaderPath = "Assets/__HologramProbe.shader";
    private const string ShaderSource = @"
Shader ""Hidden/Scratch/HologramProbe""
{
    Properties
    {
        _PaintInkMap (""Ink"", 2D) = ""black"" {}
        _PaintInkStrength (""Ink strength"", Float) = 1
        _IsGlass (""Glass"", Float) = 0
        _Mode (""Mode"", Float) = 0
    }
    SubShader
    {
        Tags { ""RenderPipeline""=""UniversalPipeline"" ""RenderType""=""Opaque"" ""Queue""=""Geometry"" }
        Pass
        {
            Name ""Probe""
            Tags { ""LightMode""=""UniversalForwardOnly"" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
            TEXTURE2D(_PaintInkMap); SAMPLER(sampler_PaintInkMap);
            CBUFFER_START(UnityPerMaterial)
                float _PaintInkStrength, _IsGlass, _Mode;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float eyeDepth : TEXCOORD3;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.eyeDepth = -position.positionVS.z;
                return output;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                if (_Mode < 0.5)
                {
                    float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                    return float4(TransformWorldToViewDir(normalWS, true), saturate(dot(normalWS, view)));
                }
                float ink = SAMPLE_TEXTURE2D(_PaintInkMap, sampler_PaintInkMap, input.uv).r * _PaintInkStrength;
                return float4(input.eyeDepth, saturate(ink) * (1 - _IsGlass), _IsGlass, 1);
            }
            ENDHLSL
        }
    }
}";

    private static readonly int ContourPixelsId = Shader.PropertyToID("_ContourPixels");
    private string outDir;

    public override bool NeedsSession => false;
    public override GizmoCaptureProfile Profile => GizmoCaptureProfile.None;

    public override IEnumerator Run()
    {
        outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "results", "hologram-probe"));
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(Application.dataPath, "__HologramProbe.shader"), ShaderSource);
        AssetDatabase.ImportAsset(ShaderPath, ImportAssetOptions.ForceSynchronousImport);
        var probeShader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        Assert.IsTrue(probeShader && !ShaderUtil.ShaderHasError(probeShader), "probe shader compiles");

        var savedPause = AudioListener.pause;
        AudioListener.pause = true;
        var staging = new GameObject("Probe staging");
        staging.SetActive(false);
        var host = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameHost>("Assets/Prefabs/Game/GameHost.prefab"),
            staging.transform);
        host.openOnTitle = false;
        host.runRecordPath = Path.Combine(outDir, "run-" + Guid.NewGuid().ToString("N"), "runs.jsonl");
        host.transform.SetParent(null, false);
        Object.Destroy(staging);
        HangarPreviewStage plain = null;
        try
        {
            HangarConsole console = null;
            var deadline = Time.realtimeSinceStartup + 90f;
            while (!console && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                console = Object.FindFirstObjectByType<HangarConsole>();
            }
            Assert.IsTrue(console, "the host reached the hangar");

            using var recording = Capture.Prepare(new CaptureRequest("results/capture", "hologram-probe", Commit,
                1920, 1080));
            yield return recording.Ready;
            yield return new WaitForSeconds(4f);
            yield return Realtime(1f);
            foreach (var behaviour in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
                if (behaviour.GetType().Name == "FMODRuntimeManagerOnGUIHelper")
                    behaviour.GetType().GetField("TargetRuntimeManager").SetValue(behaviour, null);

            var projector = console.Projector;
            using var wiring = new SerializedObject(projector);
            var surface = (MeshRenderer)wiring.FindProperty("surface").objectReferenceValue;
            var camera = console.HardwareCamera;
            var stage = projector.Stage;
            var size = new Vector2Int(stage.Texture.width, stage.Texture.height);
            var frame = projector.Frame;
            var corners = new[] { new Vector3(-.5f, -.5f), new Vector3(-.5f, .5f), new Vector3(.5f, -.5f), new Vector3(.5f, .5f) }
                .Select(corner => camera.WorldToScreenPoint(surface.transform.TransformPoint(corner)))
                .Select(p => $"[{F(p.x)}, {F(p.y)}]");
            var lens = camera.WorldToScreenPoint(projector.transform.position);
            var ships = console.Selection.Ships.ToArray();
            var meta = new StringBuilder("{\n");
            meta.Append($"  \"screen\": [{Screen.width}, {Screen.height}],\n");
            meta.Append($"  \"stage\": [{size.x}, {size.y}],\n");
            meta.Append($"  \"frame\": [{F(frame.x)}, {F(frame.y)}, {F(frame.width)}, {F(frame.height)}],\n");
            meta.Append($"  \"quad\": [{string.Join(", ", corners)}],\n");
            meta.Append($"  \"projector\": [{F(lens.x)}, {F(lens.y)}],\n");
            meta.Append($"  \"ships\": [{string.Join(", ", ships.Select(ship => $"\"{ship.name}\""))}]\n}}\n");
            File.WriteAllText(Path.Combine(outDir, "meta.json"), meta.ToString());

            // The plate: the hangar with the hologram and its cone hidden.
            foreach (var r in projector.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            yield return Frames(3);
            yield return new WaitForEndOfFrame();
            var plate = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(outDir, "plate.png"), plate.EncodeToPNG());
            Object.DestroyImmediate(plate);

            // The buffers come from a stage of the same size with no restyle and no resolve pass.
            stage.gameObject.SetActive(false);
            plain = HangarPreviewStage.Create(null, size, frame, ships);
            for (var i = 0; i < ships.Length; i++)
            {
                plain.Show(ships[i], backwards: false);
                yield return new WaitForSeconds(3f);
                var savedScale = Time.timeScale;
                Time.timeScale = 0f;
                yield return Frames(2);
                CaptureBuffers(plain, ships[i].name, probeShader);
                Time.timeScale = savedScale;
                yield return Frames(2);
            }
            recording.Start();
            yield return Frames(3);
            Result = recording.Finish();
            Debug.Log($"[HologramBuffersProbe] wrote {outDir}");
        }
        finally
        {
            Time.timeScale = 1f;
            AudioListener.pause = savedPause;
            if (plain) Object.DestroyImmediate(plain.gameObject);
            if (host)
            {
                host.GetComponent<Substrate.Services.Units.UnitService>().Clear();
                Object.DestroyImmediate(host.gameObject);
            }
            AssetDatabase.DeleteAsset(ShaderPath);
        }
    }

    // livery: the ship as the game draws it. paint: the same with no contour and hull parts only.
    // normal: view-space normal and facing. aux: eye depth, panel ink, glass flag, hull coverage.
    private void CaptureBuffers(HangarPreviewStage stage, string shipName, Shader probeShader)
    {
        var camera = stage.GetComponentInChildren<Camera>();
        var source = stage.Texture;
        var clone = stage.GetComponentsInChildren<HangarPreviewFlight>().Single(flight => flight.name == shipName + "_Preview");
        var renderers = clone.GetComponentsInChildren<Renderer>();
        var block = new MaterialPropertyBlock();
        var msaa = RenderTexture.GetTemporary(new RenderTextureDescriptor(source.width, source.height,
            RenderTextureFormat.ARGBHalf, 24) { msaaSamples = 4, sRGB = false });
        var resolved = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGBHalf,
            RenderTextureReadWrite.Linear);
        var readback = new Texture2D(source.width, source.height, TextureFormat.RGBAHalf, false, true);
        var created = new List<Material>();
        var originals = renderers.ToDictionary(r => r, r => r.sharedMaterials);
        var contourWidths = new Dictionary<(Renderer, int), float>();
        var enabledBefore = renderers.ToDictionary(r => r, r => r.enabled);
        try
        {
            Render("livery");
            foreach (var r in renderers)
            {
                var materials = originals[r];
                var drawn = false;
                for (var i = 0; i < materials.Length; i++)
                {
                    if (!materials[i]) continue;
                    var shader = materials[i].shader.name;
                    if (shader.StartsWith("Astronomical/Drawn/")) drawn = true;
                    if (shader != "Astronomical/Drawn/Contour") continue;
                    r.GetPropertyBlock(block, i);
                    contourWidths[(r, i)] = block.HasFloat(ContourPixelsId)
                        ? block.GetFloat(ContourPixelsId) : materials[i].GetFloat(ContourPixelsId);
                    block.SetFloat(ContourPixelsId, 0f);
                    r.SetPropertyBlock(block, i);
                }
                if (!drawn) r.enabled = false;
            }
            Render("paint");
            foreach (var r in renderers)
            {
                var swapped = (Material[])originals[r].Clone();
                for (var i = 0; i < swapped.Length; i++)
                {
                    var material = swapped[i];
                    if (!material) continue;
                    var shader = material.shader.name;
                    if (shader != "Astronomical/Drawn/Surface" && shader != "Astronomical/Drawn/Canopy") continue;
                    var probe = new Material(probeShader);
                    created.Add(probe);
                    if (shader == "Astronomical/Drawn/Canopy") probe.SetFloat("_IsGlass", 1f);
                    else
                    {
                        probe.SetTexture("_PaintInkMap", material.GetTexture("_PaintInkMap"));
                        probe.SetFloat("_PaintInkStrength", material.GetFloat("_PaintInkStrength"));
                    }
                    swapped[i] = probe;
                }
                r.sharedMaterials = swapped;
            }
            foreach (var probe in created) probe.SetFloat("_Mode", 0f);
            Render("normal");
            foreach (var probe in created) probe.SetFloat("_Mode", 1f);
            Render("aux");
        }
        finally
        {
            foreach (var r in renderers)
            {
                r.sharedMaterials = originals[r];
                r.enabled = enabledBefore[r];
            }
            foreach (var entry in contourWidths)
            {
                entry.Key.Item1.GetPropertyBlock(block, entry.Key.Item2);
                block.SetFloat(ContourPixelsId, entry.Value);
                entry.Key.Item1.SetPropertyBlock(block, entry.Key.Item2);
            }
            foreach (var probe in created) Object.DestroyImmediate(probe);
            camera.targetTexture = source;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(msaa);
            RenderTexture.ReleaseTemporary(resolved);
            Object.DestroyImmediate(readback);
        }

        void Render(string label)
        {
            camera.targetTexture = msaa;
            camera.Render();
            camera.targetTexture = source;
            Graphics.Blit(msaa, resolved);
            RenderTexture.active = resolved;
            readback.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(outDir, $"{shipName}-{label}.bin"), readback.GetRawTextureData());
        }
    }

    private static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static IEnumerator Frames(int count)
    {
        for (var i = 0; i < count; i++) yield return null;
    }

    private static IEnumerator Realtime(float seconds)
    {
        var until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until) yield return null;
    }
}
#endif
