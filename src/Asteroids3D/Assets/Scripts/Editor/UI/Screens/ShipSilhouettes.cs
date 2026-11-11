using System;
using System.Collections.Generic;
using System.IO;
using Ships;
using UI.Hangar.Displays;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace UI.Screens
{
    public static class ShipSilhouettes
    {
        [MenuItem("Tools/UI/Bake recap silhouettes")]
        public static void BakeRecap() => Bake(RunRecapScreen.SilhouetteFolder, 128);

        [MenuItem("Tools/UI/Bake hangar silhouettes")]
        public static void BakeHangar() => Bake(HangarMainDisplayView.SilhouetteFolder, 512);

        // A white top-down silhouette of every ship, one square PNG per ship under Resources.
        public static void Bake(string resourcesFolder, int size)
        {
            var output = "Assets/Resources/" + resourcesFolder;
            Directory.CreateDirectory(output);
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Ships" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ship = AssetDatabase.LoadAssetAtPath<Ship>(path);
                if (!ship || ship.name == "ShipBase") continue;
                BakeShip(ship, output, size);
            }
            AssetDatabase.Refresh();
        }

        private static void BakeShip(Ship ship, string output, int size)
        {
            IReadOnlyList<Renderer> renderers;
            if (ship.VisualRig)
                renderers = ship.VisualRig.Silhouette;
            else if (Array.IndexOf(ShipLegacyList.Chassis, ship.name) >= 0)
                renderers = ship.GetComponentsInChildren<MeshRenderer>(true);
            else
                throw new InvalidOperationException($"{ship.name} needs a visual rig to bake its silhouette.");
            if (renderers.Count == 0)
                throw new InvalidOperationException($"{ship.name} needs hull renderers to bake its silhouette.");
            var toShip = ship.transform.worldToLocalMatrix;
            var bounds = new Bounds(toShip.MultiplyPoint3x4(renderers[0].transform.position), Vector3.zero);
            foreach (var renderer in renderers)
            {
                var local = renderer.localBounds;
                for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                for (var z = -1; z <= 1; z += 2)
                    bounds.Encapsulate((toShip * renderer.localToWorldMatrix).MultiplyPoint3x4(
                        local.center + Vector3.Scale(local.extents, new Vector3(x, y, z))));
            }
            var span = Mathf.Max(bounds.size.x, bounds.size.y) * 1.12f;
            var normalize = Matrix4x4.Translate(new Vector3(.5f, .5f, 0))
                * Matrix4x4.Scale(new Vector3(1 / span, 1 / span, 0))
                * Matrix4x4.Translate(-bounds.center);
            var shader = Shader.Find("Unlit/Color");
            if (!shader) throw new InvalidOperationException("Silhouette baking requires Unlit/Color.");
            var material = new Material(shader) { color = Color.white };
            material.SetInt("_Cull", (int)CullMode.Off);
            var target = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            RenderTexture.active = target;
            GL.PushMatrix();
            try
            {
                GL.Clear(true, true, Color.clear);
                GL.LoadOrtho();
                foreach (var renderer in renderers)
                {
                    var skinned = renderer as SkinnedMeshRenderer;
                    var mesh = skinned ? new Mesh() : renderer.GetComponent<MeshFilter>().sharedMesh;
                    if (skinned) skinned.BakeMesh(mesh);
                    material.SetPass(0);
                    for (var sub = 0; sub < mesh.subMeshCount; sub++)
                        Graphics.DrawMeshNow(mesh, normalize * toShip * renderer.localToWorldMatrix, sub);
                    if (skinned) Object.DestroyImmediate(mesh);
                }
                texture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                texture.Apply();
                var path = output + "/" + ship.name + ".png";
                File.WriteAllBytes(path, texture.EncodeToPNG());
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            finally
            {
                GL.PopMatrix();
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(material);
            }
        }
    }
}
