using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Substrate.Sectors.Elements.Hunt
{
    /// <summary>
    /// One patrol's sight on the minimap: a flat cone out to the sight radius and a disc of the close
    /// radius, in the game plane, parented to the ship so it turns with the nose. <see cref="SetSeeing"/>
    /// tints it while that patrol sees the hero.
    /// </summary>
    public sealed class PatrolSightWedge : MonoBehaviour
    {
        private static readonly Color Idle = new(1f, .45f, .4f, .18f);
        private static readonly Color Seeing = new(1f, .12f, .08f, .7f);
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private Material tint;

        public void Build(HuntFeelProfile feel, Material source)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            mesh = Fan(feel.coneDegrees, feel.sightRadius, feel.closeSightRadius);
            mesh.hideFlags = HideFlags.HideAndDontSave;
            meshFilter.sharedMesh = mesh;
            tint = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
            meshRenderer.sharedMaterial = tint;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            gameObject.layer = LayerIds.MinimapEnemy;
            SetSeeing(false);
        }

        public void SetSeeing(bool seeing)
        {
            tint.SetColor(ColorId, seeing ? Seeing : Idle);
        }

        private void OnDestroy()
        {
            if (meshFilter) meshFilter.sharedMesh = null;
            if (meshRenderer) meshRenderer.sharedMaterial = null;
            if (mesh) DestroyImmediate(mesh);
            if (tint) DestroyImmediate(tint);
            mesh = null;
            tint = null;
        }

        private static Mesh Fan(float coneDegrees, float sightRadius, float closeRadius)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            if (sightRadius > 0f && coneDegrees > 0f)
                AddFan(verts, tris, sightRadius, coneDegrees);
            if (closeRadius > 0f)
                AddDisc(verts, tris, closeRadius);
            var mesh = new Mesh { name = "Patrol sight" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Bisector along local +Y, the ship nose.
        private static void AddFan(List<Vector3> verts, List<int> tris, float radius, float coneDegrees)
        {
            var start = verts.Count;
            var half = coneDegrees * .5f * Mathf.Deg2Rad;
            var segments = Mathf.Max(1, Mathf.CeilToInt(coneDegrees / 6f));
            verts.Add(Vector3.zero);
            for (var i = 0; i <= segments; i++)
            {
                var angle = Mathf.Lerp(-half, half, i / (float)segments);
                verts.Add(new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f) * radius);
            }
            for (var i = 0; i < segments; i++)
            {
                tris.Add(start);
                tris.Add(start + 1 + i);
                tris.Add(start + 2 + i);
            }
        }

        private static void AddDisc(List<Vector3> verts, List<int> tris, float radius)
        {
            var start = verts.Count;
            const int segments = 24;
            verts.Add(Vector3.zero);
            for (var i = 0; i < segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                verts.Add(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
            }
            for (var i = 0; i < segments; i++)
            {
                var a = start + 1 + i;
                var b = start + 1 + (i + 1) % segments;
                tris.Add(start);
                tris.Add(a);
                tris.Add(b);
            }
        }
    }
}
