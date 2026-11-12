// TEMPORARY batch one-shot for #1495: wires FindStationBody.marker on the three station prefabs, reimports the
// whitened find PNGs, and renders a before/after still. Deleted before commit.
using System;
using System.IO;
using Substrate.Sectors.Elements.Stations;
using UnityEditor;
using UnityEngine;

public static class FindColoursOneShot
{
    private static readonly string[] Paths =
        { "Assets/Prefabs/Objectives/Mod Station.prefab", "Assets/Prefabs/Objectives/Repair Station.prefab", "Assets/Prefabs/Objectives/Armory.prefab" };

    private static readonly string[] Pngs = { "ModFind", "RepairFind", "ArmoryFind" };

    public static void Run()
    {
        var args = Environment.GetCommandLineArgs();
        string outPath = null, beforeDir = null;
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-stillOut") outPath = args[i + 1];
            if (args[i] == "-beforeDir") beforeDir = args[i + 1];
        }
        try
        {
            Wire();
            foreach (var png in Pngs)
                AssetDatabase.ImportAsset("Assets/Visuals/Ui/Minimap/Finds/" + png + ".png", ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            Render(outPath, beforeDir);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    private static void Wire()
    {
        var findLayer = LayerMask.NameToLayer("Minimap_Find");
        foreach (var path in Paths)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var body = root.GetComponentInChildren<FindStationBody>(true);
                var markers = Array.FindAll(body.Lit.GetComponentsInChildren<MeshRenderer>(true), r => r.gameObject.layer == findLayer);
                if (markers.Length != 1) throw new Exception(path + ": expected one find-layer marker under Lit, found " + markers.Length);
                var so = new SerializedObject(body);
                so.FindProperty("marker").objectReferenceValue = markers[0];
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("FINDCOLOURS wired " + path + " -> " + markers[0].name);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static void Render(string outPath, string beforeDir)
    {
        ShaderUtil.allowAsyncCompilation = false;
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var baseColor = Shader.PropertyToID("_BaseColor");
        var root = new GameObject("FIND_TOUR");
        void Panel(float y, float height, Color c)
        {
            var p = new GameObject("panel");
            p.transform.SetParent(root.transform);
            p.transform.position = new Vector3(33f, y, 5f);
            p.transform.localScale = new Vector3(70f, height, 1f);
            p.AddComponent<MeshFilter>().sharedMesh = quad;
            var m = new Material(unlit);
            m.SetColor(baseColor, c);
            p.AddComponent<MeshRenderer>().sharedMaterial = m;
        }
        void Marker(Material material, Color colour, float x, float y, float size)
        {
            var q = new GameObject("marker");
            q.transform.SetParent(root.transform);
            q.transform.position = new Vector3(x, y, 0f);
            q.transform.localScale = Vector3.one * size;
            q.AddComponent<MeshFilter>().sharedMesh = quad;
            var r = q.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            var block = new MaterialPropertyBlock();
            block.SetColor(baseColor, colour);
            r.SetPropertyBlock(block);
        }
        // rows top to bottom: before on the minimap field, after on the minimap field, after on a sky tone
        Panel(10.5f, 29f, new Color(0.02f, 0.03f, 0.05f));
        Panel(-11.5f, 15f, new Color(0.10f, 0.12f, 0.20f));
        var report = "";
        for (var i = 0; i < Paths.Length; i++)
        {
            var station = AssetDatabase.LoadAssetAtPath<GameObject>(Paths[i]).GetComponent<FindStation>();
            var body = (FindStationBody)new SerializedObject(station).FindProperty("body").objectReferenceValue;
            var material = body.Marker.sharedMaterial;
            var colour = FindColours.Of(station.Kind);
            report += station.Kind + "=#" + ColorUtility.ToHtmlStringRGB(colour) + " ";
            var x = 6f + i * 23f;
            var before = new Material(material);
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(Path.Combine(beforeDir, Pngs[i] + ".png")));
            before.SetTexture("_BaseMap", tex);
            Marker(before, Color.white, x, 17f, 12f);
            Marker(before, Color.white, x + 11f, 17f, 4f);
            Marker(material, colour, x, 3f, 12f);
            Marker(material, colour, x + 11f, 3f, 4f);
            Marker(material, colour, x, -12f, 12f);
            Marker(material, colour, x + 11f, -12f, 4f);
        }
        var cgo = new GameObject("cam");
        cgo.transform.SetParent(root.transform);
        var cam = cgo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 22f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.nearClipPlane = .1f;
        cam.farClipPlane = 200f;
        cgo.transform.position = new Vector3(33f, 3f, -50f);
        const int w = 1400, h = 880;
        var rt = new RenderTexture(w, h, 24);
        cam.targetTexture = rt;
        cam.Render();
        cam.Render();
        RenderTexture.active = rt;
        var shot = new Texture2D(w, h, TextureFormat.RGB24, false);
        shot.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        shot.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        File.WriteAllBytes(outPath, shot.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(root);
        Debug.Log("FINDCOLOURS still " + outPath + " " + report);
    }
}
