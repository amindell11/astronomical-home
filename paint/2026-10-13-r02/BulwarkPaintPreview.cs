using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Ships.Loadout;
using Ships.Presentation;
using Ships.Visuals;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BulwarkPaintPreview
{
    private const string Output = "D:/amind/git/agent-3/results/bulwark/paint/2026-10-13-r02/lineup";
    private const string MeshPath = "D:/amind/git/agent-3/results/bulwark/paint/2026-10-13-r02/Bulwark-preview-mesh.json";

    public static object Capture()
    {
        Directory.CreateDirectory(Output);
        var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(ItemCatalog.AssetPath);
        var rows = new List<object>();
        foreach (var ship in catalog.Chassis)
        {
            if (!new[] { "Crimson", "Vanguard", "Nightshade", "Valis" }.Contains(ship.name)) continue;
            var draws = ReadPrefab(ship.gameObject);
            try
            {
                Render(draws, ship.name, false);
                Render(draws, ship.name, true);
                rows.Add(new { ship = ship.name, source = AssetDatabase.GetAssetPath(ship), renderers = draws.Count,
                    materials = draws.SelectMany(d => d.Materials).Select(m => new {
                        path = AssetDatabase.GetAssetPath(m), shader = m.shader.name,
                        paperColor = m.HasProperty("_PaperColor") ? m.GetColor("_PaperColor").ToString() : "texture" }).ToArray() });
            }
            finally { DisposeMeshes(draws); }
        }
        var temporaryMaterials = new List<Material>();
        var temporaryTextures = new List<Texture2D>();
        var bulwark = ReadBulwark(temporaryMaterials, temporaryTextures);
        try
        {
            Render(bulwark, "Bulwark-concept", false);
            Render(bulwark, "Bulwark-concept", true);
            Render(bulwark, "Bulwark-painted-large", true, 1400);
        }
        finally
        {
            DisposeMeshes(bulwark);
            foreach (var material in temporaryMaterials) UnityEngine.Object.DestroyImmediate(material);
            foreach (var texture in temporaryTextures) UnityEngine.Object.DestroyImmediate(texture);
        }
        var report = new { engine = Application.unityVersion, project = Application.dataPath,
            colorSpace = QualitySettings.activeColorSpace.ToString(), roster = rows,
            bulwark = "Locked r08 geometry and approved r02 UVs with separate grayscale paint masks; temporary materials, no gameplay prefab.",
            framing = "Equal maximum model dimension; shared orthographic camera and neutral lighting.",
            sceneOrAssetSaved = false, capturedUtc = DateTime.UtcNow.ToString("o") };
        File.WriteAllText(Path.Combine(Output, "capture-report.json"), JsonConvert.SerializeObject(report, Formatting.Indented));
        return report;
    }

    private static List<Draw> ReadPrefab(GameObject prefab)
    {
        var rig = prefab.GetComponentInChildren<ShipVisualRig>(true);
        if (!rig && prefab.name != "Junker_1") throw new InvalidOperationException(prefab.name + " has no ShipVisualRig.");
        var visualRoot = prefab.name == "Junker_1" ? prefab.transform : rig.transform;
        var draws = new List<Draw>();
        foreach (var renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled || renderer.GetComponentInParent<MinimapShipMarker>(true)) continue;
            var active = true;
            for (var t = renderer.transform; t; t = t.parent)
                if (!t.gameObject.activeSelf) active = false;
            if (!active) continue;
            Mesh mesh;
            var owns = false;
            if (renderer is SkinnedMeshRenderer skinned)
            {
                mesh = new Mesh { name = renderer.name + " preview", hideFlags = HideFlags.HideAndDontSave };
                skinned.BakeMesh(mesh);
                owns = true;
            }
            else if (renderer is MeshRenderer)
                mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            else continue;
            if (!mesh) throw new InvalidOperationException(renderer.name + " has no mesh.");
            draws.Add(new Draw { Mesh = mesh, Matrix = visualRoot.worldToLocalMatrix * renderer.localToWorldMatrix,
                Materials = renderer.sharedMaterials, OwnsMesh = owns });
        }
        if (draws.Count == 0) throw new InvalidOperationException(prefab.name + " has no visible meshes.");
        return draws;
    }

    private static List<Draw> ReadBulwark(List<Material> temporaryMaterials, List<Texture2D> temporaryTextures)
    {
        var data = JsonConvert.DeserializeObject<PreviewMesh>(File.ReadAllText(MeshPath));
        if (data.sourceSha256 != "96a9e782c7dc5cb59eb24ce8f84248474246b4065a2c6bc76f5ef36a2b400a50")
            throw new InvalidOperationException("Bulwark UV checkpoint mismatch.");
        var palette = new[] { "#66505C", "#D8CDB4", "#302E34", "#718591", "#66505C" };
        var template = AssetDatabase.LoadAssetAtPath<Material>("Assets/Visuals/Ships/Valis/Materials/jade-iris/Graphite.mat");
        var shadow = LoadMask("Shadow", temporaryTextures);
        var light = LoadMask("Light", temporaryTextures);
        var ink = LoadMask("Ink", temporaryTextures);
        var regionMask = LoadMask("BaseRegion", temporaryTextures);
        var materials = new Material[6];
        for (var i = 0; i < palette.Length; i++)
        {
            ColorUtility.TryParseHtmlString(palette[i], out var color);
            ColorUtility.TryParseHtmlString(i == 1 ? "#66505C" : "#D8CDB4", out var alternate);
            ColorUtility.TryParseHtmlString("#29222F", out var inkColor);
            ColorUtility.TryParseHtmlString("#E3D8CB", out var lightColor);
            var material = new Material(template) { name = "Bulwark paint " + i, hideFlags = HideFlags.HideAndDontSave };
            material.EnableKeyword("_PAINT_LAYERS");
            material.SetFloat("_PaintLayers", 1);
            material.SetTexture("_PaintShadowMap", shadow);
            material.SetTexture("_PaintLightMap", light);
            material.SetTexture("_PaintInkMap", ink);
            material.SetTexture("_EmissionMap", regionMask);
            material.SetFloat("_PaintShadowStrength", .85f);
            material.SetFloat("_PaintLightStrength", .5f);
            material.SetFloat("_PaintInkStrength", .9f);
            material.SetFloat("_PaintSaturation", 1);
            material.SetColor("_PaintInkColor", inkColor);
            material.SetColor("_PaintLightColor", lightColor);
            material.SetFloat("_TextureStrength", 0);
            material.SetFloat("_LineStrength", 0);
            material.SetFloat("_WearStrength", 0);
            material.SetFloat("_PaintEmissionBlend", i == 1 || i == 4 ? 1 : 0);
            material.SetColor("_EmissionColor", alternate.linear);
            material.SetFloat("_EmissionStrength", 0);
            material.SetColor("_PaperColor", color);
            material.SetColor("_BaseColor", Color.white);
            materials[i] = material;
            temporaryMaterials.Add(material);
        }
        materials[5] = AssetDatabase.LoadAssetAtPath<Material>("Assets/Visuals/Ships/_Shared/Illustrated/Ship contour.mat");
        var draws = new List<Draw>();
        foreach (var part in data.parts)
        {
            var vertices = Vectors(part.vertices);
            var mesh = new Mesh { name = part.name, hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices.Concat(vertices).ToArray();
            mesh.normals = Vectors(part.normals).Concat(Vectors(part.contourNormals)).ToArray();
            var uv = new Vector2[vertices.Length];
            for (var i = 0; i < uv.Length; i++) uv[i] = new Vector2(part.uv[i * 2], part.uv[i * 2 + 1]);
            mesh.uv = uv.Concat(uv).ToArray();
            mesh.subMeshCount = 6;
            for (var region = 0; region < 5; region++)
            {
                var indices = new List<int>();
                for (var t = 0; t < part.regions.Length; t++)
                    if (part.regions[t] == region)
                        indices.AddRange(new[] { part.triangles[t * 3], part.triangles[t * 3 + 1], part.triangles[t * 3 + 2] });
                mesh.SetTriangles(indices, region);
            }
            mesh.SetTriangles(part.triangles.Select(i => i + vertices.Length).ToArray(), 5);
            mesh.RecalculateBounds();
            draws.Add(new Draw { Mesh = mesh, Matrix = Matrix4x4.identity, Materials = materials, OwnsMesh = true });
        }
        return draws;
    }

    private static Texture2D LoadMask(string name, List<Texture2D> textures)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, true) { name = name, hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
        if (!texture.LoadImage(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(MeshPath), "layers", name + ".png"))))
            throw new InvalidOperationException("Cannot decode " + name);
        textures.Add(texture);
        return texture;
    }

    private static void Render(List<Draw> draws, string name, bool quarter, int size = 640)
    {
        var bounds = new Bounds();
        var first = true;
        foreach (var draw in draws)
        {
            var b = draw.Mesh.bounds;
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var z = -1; z <= 1; z += 2)
            {
                var point = draw.Matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z)));
                if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                else bounds.Encapsulate(point);
            }
        }
        var scale = 6f / Mathf.Max(bounds.size.x, bounds.size.y);
        var normalize = Matrix4x4.Scale(Vector3.one * scale) * Matrix4x4.Translate(-bounds.center);
        var preview = new PreviewRenderUtility();
        Texture2D pixels = null;
        var prior = RenderTexture.active;
        RenderTexture target = null;
        try
        {
            preview.ambientColor = new Color(0.22f, 0.22f, 0.24f, 1f);
            preview.lights[0].intensity = 1.6f;
            preview.lights[0].transform.rotation = Quaternion.Euler(30f, -20f, 0f);
            preview.lights[1].intensity = 0.25f;
            preview.lights[1].transform.rotation = Quaternion.Euler(-20f, 130f, 0f);
            var camera = preview.camera;
            camera.orthographic = true;
            camera.orthographicSize = quarter ? 4.6f : 3.7f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.07f, 0.095f, 1f);
            camera.transform.position = quarter ? new Vector3(9f, 12f, -20f) : new Vector3(0f, 0f, -25f);
            camera.transform.rotation = Quaternion.LookRotation(-camera.transform.position, Vector3.up);
            preview.BeginPreview(new Rect(0, 0, size, size), GUIStyle.none);
            foreach (var draw in draws)
            for (var s = 0; s < draw.Mesh.subMeshCount; s++)
            {
                var material = draw.Materials[Mathf.Min(s, draw.Materials.Length - 1)];
                if (!material) throw new InvalidOperationException(draw.Mesh.name + " has a missing material.");
                preview.DrawMesh(draw.Mesh, normalize * draw.Matrix, material, s);
            }
            preview.Render(true);
            var texture = preview.EndPreview();
            target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(texture, target);
            RenderTexture.active = target;
            pixels = new Texture2D(size, size, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(Path.Combine(Output, name + (quarter ? "-quarter.png" : "-top.png")), pixels.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = prior;
            if (target) RenderTexture.ReleaseTemporary(target);
            if (pixels) UnityEngine.Object.DestroyImmediate(pixels);
            preview.Cleanup();
        }
    }

    private static Vector3[] Vectors(float[] data)
    {
        var result = new Vector3[data.Length / 3];
        for (var i = 0; i < result.Length; i++) result[i] = new Vector3(data[i * 3], data[i * 3 + 1], data[i * 3 + 2]);
        return result;
    }

    private static void DisposeMeshes(List<Draw> draws)
    {
        foreach (var draw in draws) if (draw.OwnsMesh) UnityEngine.Object.DestroyImmediate(draw.Mesh);
    }

    private sealed class Draw
    {
        public Mesh Mesh;
        public Matrix4x4 Matrix;
        public Material[] Materials;
        public bool OwnsMesh;
    }

    private sealed class PreviewMesh
    {
        public string sourceSha256;
        public Part[] parts;
    }

    private sealed class Part
    {
        public string name;
        public float[] vertices;
        public float[] normals;
        public float[] contourNormals;
        public float[] uv;
        public int[] triangles;
        public int[] regions;
    }
}
