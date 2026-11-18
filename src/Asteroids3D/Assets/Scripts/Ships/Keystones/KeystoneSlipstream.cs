using System;
using System.Collections.Generic;
using Substrate;
using Substrate.Presentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ships.Keystones
{
    public sealed class KeystoneSlipstream : MonoBehaviour, IPresentationPart
    {
        [Serializable]
        public struct Ribbon
        {
            public Vector2 anchor;
            public float width;
            public float spread;
            public float lengthScale;
            public int atlasCell;
            public float opacity;
        }

        private struct Pose
        {
            public Vector3 position, right, up;
            public float time, distance;

            public static Pose Lerp(Pose a, Pose b, float t) => new Pose
            {
                position = Vector3.Lerp(a.position, b.position, t),
                right = Vector3.Lerp(a.right, b.right, t),
                up = Vector3.Lerp(a.up, b.up, t),
                time = Mathf.Lerp(a.time, b.time, t),
                distance = Mathf.Lerp(a.distance, b.distance, t)
            };
        }

        private const int Capacity = 128;
        [SerializeField] private Keystone keystone;
        [SerializeField] private Material material;
        [SerializeField] private float localShipLength = 2f;
        [SerializeField] private float lifetime = 0.22f;
        [Tooltip("Maximum retained path length, in ship lengths.")]
        [SerializeField] private float maxLength = 3f;
        [SerializeField] private Ribbon[] ribbons = Array.Empty<Ribbon>();
        [Range(0f, 1f)] [SerializeField] private float tint = 0.35f;

        private readonly Pose[] history = new Pose[Capacity];
        private List<Vector3> vertices;
        private List<Vector2> uvs;
        private List<Color> colors;
        private List<int> triangles;
        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private GameObject surface;
        private int newest, count;
        private bool wasActive;

        private void Awake()
        {
            Validate();
            var vertexCapacity = ribbons.Length * (Capacity + 1) * 2;
            vertices = new List<Vector3>(vertexCapacity);
            uvs = new List<Vector2>(vertexCapacity);
            colors = new List<Color>(vertexCapacity);
            triangles = new List<int>(ribbons.Length * Capacity * 6);
            mesh = new Mesh { name = "Keystone slipstream" };
            mesh.MarkDynamic();
            surface = new GameObject("Slipstream") { layer = gameObject.layer };
            surface.transform.SetParent(transform, false);
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = surface.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void OnDisable()
        {
            count = 0;
            wasActive = false;
            if (meshRenderer) meshRenderer.enabled = false;
            if (mesh) mesh.Clear();
        }

        private void OnDestroy()
        {
            if (mesh) Destroy(mesh);
            if (surface) Destroy(surface);
        }

        private Pose ReadPose() => new Pose
        {
            position = transform.position,
            right = transform.TransformVector(Vector3.right),
            up = transform.TransformVector(Vector3.up),
            time = Time.time
        };

        private Pose At(int age) => history[(newest - age + Capacity) % Capacity];

        private void Append(Pose pose)
        {
            newest = (newest + 1) % Capacity;
            history[newest] = pose;
            count = Mathf.Min(count + 1, Capacity);
        }

        private void LateUpdate()
        {
            var active = keystone.Active;
            var current = ReadPose();
            var shipLength = current.up.magnitude * localShipLength;
            if (active && !wasActive) count = 0;
            if (active)
            {
                if (count == 0) Append(current);
                var last = At(0);
                current.distance = last.distance + Vector3.Distance(current.position, last.position);
                if (current.distance - last.distance >= shipLength * 0.025f) Append(current);
            }
            wasActive = active;
            while (count > 0 && Time.time - At(count - 1).time > lifetime) count--;
            var head = active ? current : count > 0 ? At(0) : current;
            while (count > 1 && head.distance - At(count - 2).distance > shipLength * maxLength) count--;
            Draw(head, shipLength * maxLength);
        }

        private void Draw(Pose head, float maximum)
        {
            vertices.Clear();
            uvs.Clear();
            colors.Clear();
            triangles.Clear();
            var worldToLocal = transform.worldToLocalMatrix;
            if (count > 0)
                foreach (var ribbon in ribbons)
                    DrawRibbon(ribbon, head, maximum, worldToLocal);
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            meshRenderer.enabled = triangles.Count > 0;
        }

        private void DrawRibbon(Ribbon ribbon, Pose head, float maximum, Matrix4x4 worldToLocal)
        {
            var length = Mathf.Min(head.distance - At(count - 1).distance, maximum) * ribbon.lengthScale;
            if (length <= 0f) return;
            var start = vertices.Count;
            AddSection(ribbon, head, 0f, worldToLocal);
            var previous = head;
            for (var i = 0; i < count; i++)
            {
                var pose = At(i);
                var distance = head.distance - pose.distance;
                if (distance <= 0f) continue;
                if (distance >= length)
                {
                    var previousDistance = head.distance - previous.distance;
                    pose = Pose.Lerp(previous, pose, (length - previousDistance) / (distance - previousDistance));
                    AddSection(ribbon, pose, 1f, worldToLocal);
                    break;
                }
                AddSection(ribbon, pose, distance / length, worldToLocal);
                previous = pose;
            }
            var chord = transform.TransformVector(vertices[start] - vertices[vertices.Count - 1]);
            var across = Vector3.Cross(chord, GamePlane.Normal).normalized;
            var halfWidth = worldToLocal.MultiplyVector(across * (head.right.magnitude * ribbon.width * 0.5f));
            for (var i = start; i < vertices.Count; i += 2)
            {
                vertices[i] -= halfWidth;
                vertices[i + 1] += halfWidth;
            }
            for (var i = start; i + 3 < vertices.Count; i += 2)
            {
                triangles.Add(i);
                triangles.Add(i + 2);
                triangles.Add(i + 1);
                triangles.Add(i + 1);
                triangles.Add(i + 2);
                triangles.Add(i + 3);
            }
        }

        private void AddSection(Ribbon ribbon, Pose pose, float along, Matrix4x4 worldToLocal)
        {
            var outward = Mathf.Sign(ribbon.anchor.x) * ribbon.spread * along;
            var center = pose.position + pose.right * (ribbon.anchor.x + outward) + pose.up * ribbon.anchor.y;
            var point = worldToLocal.MultiplyPoint3x4(center);
            vertices.Add(point);
            vertices.Add(point);
            var column = ribbon.atlasCell % 8;
            var row = ribbon.atlasCell / 8;
            var v = (4f - row - along) / 4f;
            uvs.Add(new Vector2(column / 8f, v));
            uvs.Add(new Vector2((column + 1f) / 8f, v));
            var age = Mathf.Clamp01((Time.time - pose.time) / lifetime);
            var alpha = ribbon.opacity * (1f - age * age);
            var color = new Color(tint, tint * 0.2f, 0f, alpha);
            colors.Add(color);
            colors.Add(color);
        }

        private void Validate()
        {
            if (!keystone || !material)
                throw new InvalidOperationException($"{nameof(KeystoneSlipstream)} on '{name}' needs its keystone and material.");
            if (!(localShipLength > 0f) || !(lifetime > 0f) || !(maxLength > 0f))
                throw new InvalidOperationException($"{nameof(KeystoneSlipstream)} on '{name}' needs positive length and lifetime.");
            if (ribbons == null || ribbons.Length == 0)
                throw new InvalidOperationException($"{nameof(KeystoneSlipstream)} on '{name}' needs ribbon descriptors.");
            for (var i = 0; i < ribbons.Length; i++)
            {
                var ribbon = ribbons[i];
                if (!(ribbon.width > 0f) || !(ribbon.lengthScale > 0f && ribbon.lengthScale <= 1f)
                    || !(ribbon.spread >= 0f) || !(ribbon.opacity >= 0f && ribbon.opacity <= 1f)
                    || ribbon.atlasCell < 0 || ribbon.atlasCell > 23)
                    throw new InvalidOperationException($"{nameof(KeystoneSlipstream)} on '{name}' has invalid ribbon {i}.");
            }
        }
    }
}
