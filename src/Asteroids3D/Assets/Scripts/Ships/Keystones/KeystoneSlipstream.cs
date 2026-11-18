using System;
using Substrate.Presentation;
using UnityEngine;

namespace Ships.Keystones
{
    public sealed class KeystoneSlipstream : MonoBehaviour, IPresentationPart
    {
        private const float ReleaseSeconds = 0.17f;
        private const int Sections = 16;
        private const int Capacity = 64;
        [SerializeField] private Keystone keystone;
        [SerializeField] private ParticleSystem wake;
        [SerializeField] private ParticleSystem crown;
        [SerializeField] private float localShipLength = 2f;
        [Tooltip("Hull lengths the released paint travels down the last flight path.")]
        [SerializeField] private float releaseDrift;

        private readonly ParticleSystem.Particle[] wakeStrokes = new ParticleSystem.Particle[2];
        private readonly ParticleSystem.Particle[] bowStroke = new ParticleSystem.Particle[1];
        private readonly FlowSample[] history = new FlowSample[Capacity];
        private readonly Mesh[] strips = new Mesh[2];
        private readonly Vector3[] vertices = new Vector3[Sections * 2];
        private int newest, count;
        private Vector3 tail;
        private Vector3 lastRoot, releaseTravel;
        private float startedAt, lastActiveAt;
        private bool drawing;

        private struct FlowSample
        {
            public Vector3 position, rear, tail;
            public float distance;
        }

        private void Awake()
        {
            if (!keystone || !wake || !(localShipLength > 0f))
                throw new InvalidOperationException($"{name} needs a keystone, painted wake, and positive hull length.");
            var uv = new Vector2[vertices.Length];
            var triangles = new int[(Sections - 1) * 6];
            for (var i = 0; i < Sections; i++)
            {
                var u = i / (float)(Sections - 1);
                uv[i * 2] = new Vector2(u, 0f);
                uv[i * 2 + 1] = new Vector2(u, 1f);
                if (i == Sections - 1) break;
                var a = i * 2;
                var t = i * 6;
                triangles[t] = a; triangles[t + 1] = a + 1; triangles[t + 2] = a + 2;
                triangles[t + 3] = a + 2; triangles[t + 4] = a + 1; triangles[t + 5] = a + 3;
            }
            for (var i = 0; i < strips.Length; i++)
            {
                var mesh = new Mesh { name = "Painted wake" };
                mesh.MarkDynamic();
                mesh.vertices = vertices;
                mesh.uv = uv;
                mesh.triangles = triangles;
                strips[i] = mesh;
                wakeStrokes[i].SetMeshIndex(i);
            }
            var renderer = wake.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.enableGPUInstancing = false;
            renderer.SetMeshes(strips);
        }

        private void OnDestroy()
        {
            foreach (var mesh in strips)
                if (mesh) Destroy(mesh);
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void OnDisable()
        {
            if (!drawing) return;
            Clear();
        }

        private void Clear()
        {
            wake.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (crown) crown.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            drawing = false;
        }

        private void LateUpdate()
        {
            if (keystone.Active)
            {
                if (!drawing)
                {
                    count = 0;
                    tail = -transform.up;
                    startedAt = Time.time;
                    wake.Play(false);
                    if (crown) crown.Play(false);
                    drawing = true;
                }
                lastActiveAt = Time.time;
                DrawActive(Time.time - startedAt);
            }
            else
            {
                if (!drawing) return;
                var release = (Time.time - lastActiveAt) / ReleaseSeconds;
                if (release >= 1f)
                {
                    Clear();
                    return;
                }
                var fade = 1f - Mathf.SmoothStep(0f, 1f, release);
                for (var i = 0; i < wakeStrokes.Length; i++)
                {
                    if (releaseDrift > 0f)
                    {
                        wakeStrokes[i].position = lastRoot + releaseTravel * release;
                        wakeStrokes[i].startSize3D = Vector3.one * Mathf.Lerp(1f, 0.72f, release);
                    }
                    SetCel(ref wakeStrokes[i], 3f, fade * (i == 0 ? 1f : 0.64f));
                }
                if (crown) SetCel(ref bowStroke[0], 3f, fade * 0.9f);
            }
            wake.SetParticles(wakeStrokes, wakeStrokes.Length);
            if (crown) crown.SetParticles(bowStroke, bowStroke.Length);
        }

        private void DrawActive(float age)
        {
            var length = transform.TransformVector(Vector3.up * localShipLength).magnitude;
            var velocity = (Vector2)keystone.Body.linearVelocity;
            if (velocity.sqrMagnitude > 0.01f) tail = -velocity.normalized;
            var acrossHull = Vector3.Dot(tail, transform.right);
            var alongHull = Vector3.Dot(tail, transform.up);
            var hullRadius = length / Mathf.Sqrt(acrossHull * acrossHull / (0.32f * 0.32f)
                + alongHull * alongHull / (0.5f * 0.5f));
            var head = new FlowSample { position = transform.position, tail = tail,
                rear = transform.position + tail * (hullRadius * 0.74f) };
            lastRoot = head.rear;
            releaseTravel = tail * (length * releaseDrift);
            var step = count == 0 ? 0f : Vector3.Distance(head.position, history[newest].position);
            head.distance = count == 0 ? 0f : history[newest].distance + step;
            if (count == 0 || step >= length * 0.04f)
            {
                newest = (newest + 1) % Capacity;
                history[newest] = head;
                count = Mathf.Min(count + 1, Capacity);
            }
            DrawWake(0, head, length, age);
            DrawWake(1, head, length, age - 0.035f);
            if (!crown) return;
            var bow = transform.position - tail * (hullRadius * 0.92f);
            var scale = Mathf.Lerp(0.78f, 1f, Mathf.SmoothStep(0f, 1f, age / 0.12f));
            bowStroke[0].position = bow;
            bowStroke[0].rotation3D = BillboardRotation(Mathf.Atan2(tail.x, -tail.y) * Mathf.Rad2Deg);
            bowStroke[0].startSize3D = new Vector3(1.55f, 1.25f, 1f) * (length * scale);
            SetCel(ref bowStroke[0], ActiveCel(age * 0.78f), 0.92f);
        }

        private void DrawWake(int index, FlowSample head, float shipLength, float age)
        {
            var primary = index == 0;
            var length = shipLength * (primary ? 1f : 0.81f);
            var stretch = Mathf.SmoothStep(0.35f, 1f, age / 0.115f);
            var height = length * 1.64f;
            var cursor = 0;
            var near = head;
            for (var i = 0; i < Sections; i++)
            {
                // The painted root lies at (0.12, 0.46) inside each atlas cel.
                var back = (i / (float)(Sections - 1) - 0.12f) * length * 2.5f * stretch;
                var target = head.distance - back;
                while (cursor < count)
                {
                    var next = history[(newest - cursor + Capacity) % Capacity];
                    if (next.distance < target) break;
                    near = next;
                    cursor++;
                }
                var center = near.rear + near.tail * (near.distance - target);
                var direction = near.tail;
                if (back > 0f && cursor < count)
                {
                    var far = history[(newest - cursor + Capacity) % Capacity];
                    var t = (near.distance - target) / (near.distance - far.distance);
                    center = Vector3.Lerp(near.rear, far.rear, t);
                    direction = Vector3.Lerp(near.tail, far.tail, t).normalized;
                }
                var across = new Vector3(-direction.y, direction.x, 0f);
                var spread = primary ? -0.17f * shipLength - 0.052f * back : 0.17f * shipLength + 0.087f * back;
                center += across * spread - head.rear;
                vertices[i * 2] = center - across * (height * 0.46f);
                vertices[i * 2 + 1] = center + across * (height * 0.54f);
            }
            strips[index].vertices = vertices;
            strips[index].RecalculateBounds();
            wakeStrokes[index].position = head.rear;
            wakeStrokes[index].startSize3D = Vector3.one;
            SetCel(ref wakeStrokes[index], ActiveCel(age), age < 0f ? 0f : primary ? 1f : 0.64f);
        }

        // World-aligned particle billboards rotate clockwise in the game plane.
        private static Vector3 BillboardRotation(float worldDegrees) => new(0f, 0f, -worldDegrees);

        private static float ActiveCel(float age)
        {
            if (age < 0.06f) return 0f;
            if (age < 0.105f) return Mathf.InverseLerp(0.06f, 0.105f, age);
            var beat = Mathf.Repeat(age - 0.105f, 0.28f);
            return 1f + Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.07f, 0.105f, beat))
                      - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.205f, 0.25f, beat));
        }

        private static void SetCel(ref ParticleSystem.Particle stroke, float cel, float alpha)
        {
            // Particle age selects the painted flipbook pose.
            stroke.startLifetime = 100f;
            stroke.remainingLifetime = 100f * (1f - cel * 0.25f);
            stroke.startColor = new Color(1f, 1f, 1f, alpha);
            stroke.randomSeed = 1;
        }
    }
}
