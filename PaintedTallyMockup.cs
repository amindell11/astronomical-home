using System;
using System.Collections.Generic;
using System.IO;
using Ships;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class PaintedTallyMockup
{
    private static readonly Color Paint = new Color(.88f, .87f, .78f);
    private static readonly Color Muted = new Color(.63f, .70f, .67f);
    private const string Output = "D:/amind/git/agent-3/scratch/scoreboard/painted-tally";
    private static Font font;
    private static readonly string[] Kills = {
        "Valis", "Crimson", "Valis", "Nightshade", "Vanguard", "Crimson", "Valis", "Bulwark", "Nightshade",
        "Valis", "Vanguard", "Crimson", "Nightshade", "Bulwark", "Valis", "Crimson", "Vanguard"
    };

    public static object Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter the game before creating this preview.");
        var previous = GameObject.Find("PaintedTallyMockup");
        if (previous) Object.DestroyImmediate(previous);
        Directory.CreateDirectory(Output);
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var textures = new Dictionary<string, Texture2D>();
        foreach (var shipName in Kills)
            if (!textures.ContainsKey(shipName)) textures.Add(shipName, Silhouette(shipName));

        var root = Box(null, "PaintedTallyMockup", 0, 0, 1280, 720);
        var canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = .5f;
        root.gameObject.AddComponent<GraphicRaycaster>();

        var panel = Box(root, "PaintedPanel", 0, 0, 470, 344);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
        panel.anchoredPosition = Vector2.zero;
        panel.gameObject.AddComponent<Image>().color = new Color(.115f, .16f, .15f, .97f);
        Line(panel, "TopSeam", 14, 13, 442, 1, new Color(.55f, .64f, .57f, .14f));
        Line(panel, "BottomSeam", 14, 330, 442, 1, new Color(.55f, .64f, .57f, .14f));
        foreach (var corner in new[] {new Vector2(14, 14), new Vector2(452, 14), new Vector2(14, 326), new Vector2(452, 326)})
            Line(panel, "Rivet", corner.x, corner.y, 3, 3, new Color(.55f, .64f, .57f, .4f));

        Label(panel, "Outcome", "SHIP LOST", 34, 24, 210, 18, 12, Muted);
        Label(panel, "Total", "17", 31, 42, 92, 57, 48, Paint);
        Label(panel, "Kills", "KILLS", 111, 66, 100, 24, 16, Paint);
        var detail = Label(panel, "ShipName", "", 225, 69, 210, 20, 12, Muted);
        detail.alignment = TextAnchor.MiddleRight;
        for (var i = 0; i < Kills.Length; i++)
        {
            var name = Kills[i];
            var slot = Box(panel, "Kill_" + (i + 1) + "_" + name, 34 + i % 9 * 45, 110 + i / 9 * 45, 40, 40);
            slot.gameObject.AddComponent<Image>().color = Color.clear;
            var mark = Box(slot, "Silhouette", 2, 2, 36, 36).gameObject.AddComponent<RawImage>();
            mark.texture = textures[name];
            mark.color = Paint;
            mark.raycastTarget = false;
            var button = slot.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => detail.text = name);
        }

        Label(panel, "Progress", "2 SECTORS CLEARED", 34, 210, 300, 18, 11, Muted);
        for (var i = 0; i < 3; i++)
        {
            var x = 34 + i * 187;
            var cleared = i < 2;
            var color = cleared ? Paint : new Color(.84f, .65f, .55f);
            var node = Box(panel, "Sector" + (i + 1), x, 238, 26, 24);
            node.gameObject.AddComponent<Image>().color = cleared
                ? new Color(.47f, .56f, .47f, .22f) : new Color(.61f, .35f, .25f, .20f);
            var numeral = Label(node, "Number", (i + 1).ToString("00"), 0, 0, 26, 24, 12, color);
            numeral.alignment = TextAnchor.MiddleCenter;
            if (i < 2) Line(panel, "Route", x + 35, 250, 143, 1, new Color(.63f, .70f, .60f, .3f));
        }
        var exit = Box(panel, "Continue", 34, 283, 122, 34);
        exit.gameObject.AddComponent<Image>().color = Paint;
        var caption = Label(exit, "Label", "CONTINUE", 0, 0, 122, 34, 12, new Color(.13f, .21f, .17f));
        caption.alignment = TextAnchor.MiddleCenter;
        exit.gameObject.AddComponent<Button>().onClick.AddListener(() => {
            Time.timeScale = 1;
            Object.Destroy(root.gameObject);
        });
        Time.timeScale = 0;
        Canvas.ForceUpdateCanvases();
        return new {marks = Kills.Length, ships = textures.Keys, panelWidth = 470, panelHeight = 344};
    }

    private static Texture2D Silhouette(string name)
    {
        var ship = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/" + name + ".prefab");
        if (!ship || !ship.VisualRig) throw new InvalidOperationException(name + " has no visual rig.");
        var renderers = ship.VisualRig.Silhouette;
        if (renderers.Count == 0) throw new InvalidOperationException(name + " has no hull renderers.");
        var bounds = new Bounds(renderers[0].transform.position, Vector3.zero);
        foreach (var renderer in renderers)
        {
            var local = renderer.localBounds;
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var z = -1; z <= 1; z += 2)
                bounds.Encapsulate(renderer.localToWorldMatrix.MultiplyPoint3x4(
                    local.center + Vector3.Scale(local.extents, new Vector3(x, y, z))));
        }
        var span = Mathf.Max(bounds.size.x, bounds.size.y) * 1.12f;
        var normalize = Matrix4x4.Translate(new Vector3(.5f, .5f, 0))
            * Matrix4x4.Scale(new Vector3(1 / span, 1 / span, 0))
            * Matrix4x4.Translate(-bounds.center);
        var shader = Shader.Find("Unlit/Color");
        if (!shader) throw new InvalidOperationException("Unlit/Color is required for silhouette capture.");
        var material = new Material(shader) {color = Color.white};
        material.SetInt("_Cull", (int)CullMode.Off);
        var target = RenderTexture.GetTemporary(128, 128, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        GL.Clear(true, true, Color.clear);
        GL.PushMatrix();
        GL.LoadOrtho();
        foreach (var renderer in renderers)
        {
            Mesh mesh;
            var skinned = renderer as SkinnedMeshRenderer;
            if (skinned)
            {
                mesh = new Mesh();
                skinned.BakeMesh(mesh);
            }
            else mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            material.SetPass(0);
            for (var sub = 0; sub < mesh.subMeshCount; sub++)
                Graphics.DrawMeshNow(mesh, normalize * renderer.localToWorldMatrix, sub);
            if (skinned) Object.DestroyImmediate(mesh);
        }
        GL.PopMatrix();
        var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
        texture.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
        Object.DestroyImmediate(material);
        File.WriteAllBytes(Output + "/silhouette-" + name + ".png", texture.EncodeToPNG());
        return texture;
    }

    private static RectTransform Box(Transform parent, string name, float x, float y, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    private static Text Label(Transform parent, string name, string value, float x, float y, float width, float height, int size, Color color)
    {
        var label = Box(parent, name, x, y, width, height).gameObject.AddComponent<Text>();
        label.font = font;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAnchor.MiddleLeft;
        label.raycastTarget = false;
        label.text = value;
        return label;
    }

    private static void Line(Transform parent, string name, float x, float y, float width, float height, Color color)
    {
        var image = Box(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }
}
