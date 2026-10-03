using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class ApplyValisPaint
{
    private const string Folder = "Assets/Visuals/Ships/Valis";
    private static readonly string[] Regions = { "Ivory", "Cool gray", "Lavender", "Graphite", "Edge", "Canopy", "Engine" };

    public static string Main()
    {
        var root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        var art = Path.Combine(root, "art/ships/valis");
        var data = JsonConvert.DeserializeObject<Export>(File.ReadAllText(Path.Combine(root, "results/valis-integration/paint-meshes.json")));
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/Meshes/Valis hull.asset");
        var vertices = mesh.vertices;
        var raw = data.parts.SelectMany(part => part.vertices).ToArray();
        var minY = raw.Min(v => v.y);
        var maxY = raw.Max(v => v.y);
        var center = new Vector3(0, (minY + maxY) * .5f, 0);
        var scale = mesh.bounds.size.y / (maxY - minY);
        var uvs = new List<Vector2>();
        var offset = 0;
        foreach (var part in data.parts)
        {
            for (var copy = 0; copy < 2; copy++)
            {
                for (var i = 0; i < part.vertices.Length; i++)
                    if (Vector3.Distance(vertices[offset + i], (part.vertices[i] - center) * scale) > .00001f)
                        throw new InvalidOperationException("Paint export changed Valis geometry at " + part.name);
                uvs.AddRange(part.uv);
                offset += part.vertices.Length;
            }
        }
        if (offset != mesh.vertexCount)
            throw new InvalidOperationException("Paint export has a different Valis vertex count.");
        mesh.SetUVs(0, uvs);
        mesh.RecalculateTangents();
        EditorUtility.SetDirty(mesh);

        var textures = new Dictionary<string, Texture2D>();
        foreach (var name in new[] { "Shadow", "Light", "Ink" })
        {
            var path = Folder + "/Paint/" + name + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            textures[name] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        var settings = JObject.Parse(File.ReadAllText(Path.Combine(art, "paint-settings.json")));
        var palettes = (JObject)JObject.Parse(File.ReadAllText(Path.Combine(art, "palettes.json")))["palettes"];
        foreach (var palette in palettes.Properties())
        foreach (var region in Regions)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + palette.Name + "/" + region + ".mat");
            material.EnableKeyword("_PAINT_LAYERS");
            material.SetFloat("_PaintLayers", 1);
            foreach (var layer in textures)
            {
                material.SetTexture("_Paint" + layer.Key + "Map", layer.Value);
                material.SetFloat("_Paint" + layer.Key + "Strength", (float)settings[layer.Key.ToLowerInvariant() + "_strength"]);
            }
            material.SetColor("_PaintLightColor", ReadColor(settings["light_color_linear"]).gamma);
            material.SetColor("_PaintInkColor", ReadColor(settings["ink_color_linear"]).gamma);
            material.SetTexture("_EmissionMap", Texture2D.whiteTexture);
            material.SetColor("_EmissionColor", region == "Lavender" && palette.Name != "jade-gray"
                ? (ReadColor(palette.Value[region]) * 2.2f).gamma : Color.black);
            material.SetFloat("_EmissionStrength", .35f);
            EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
        return "Updated authored Valis UVs and 21 palette materials; iris emission enabled.";
    }

    private static Color ReadColor(JToken token) => new Color((float)token[0], (float)token[1], (float)token[2], 1);

    [Serializable]
    public sealed class Part
    {
        public string name;
        public Vector3[] vertices;
        public Vector2[] uv;
    }

    [Serializable]
    public sealed class Export
    {
        public Part[] parts;
    }
}
