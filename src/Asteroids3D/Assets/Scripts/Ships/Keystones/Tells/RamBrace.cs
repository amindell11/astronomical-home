using System;
using Ships.Keystones.Kits;
using Substrate;
using Substrate.Presentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ships.Keystones.Tells
{
    /// <summary>
    /// The ram's brace tell: while the ram brakes, armour plates unfold across the arc it braces, so
    /// a hit from ahead visibly lands on armour. A braced hit flashes its plate and glances sparks off.
    /// The arc spans the kit's own brace angle, so the drawing stays true to the stat.
    /// </summary>
    public sealed class RamBrace : MonoBehaviour, IPresentationPart
    {
        private static readonly int HalfAngleId = Shader.PropertyToID("_HalfAngle");
        private static readonly int ExtentId = Shader.PropertyToID("_Extent");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int ElapsedId = Shader.PropertyToID("_Elapsed");
        private static readonly int HitAngleId = Shader.PropertyToID("_HitAngle");
        private static readonly int HitAgeId = Shader.PropertyToID("_HitAge");
        private static Mesh quad;

        [SerializeField] private RamDash ram;
        [SerializeField] private Material material;

        [Tooltip("World units from the ship's centre to the plates' front edge.")]
        [SerializeField] private float radius = 2f;

        [Tooltip("Local Z offset in world units; negative draws over the hull.")]
        [SerializeField] private float depth = -0.5f;

        [Tooltip("Seconds the plates take to unfold across the arc.")]
        [SerializeField] private float unfold = 0.08f;

        [Tooltip("Seconds the plates take to fade once the brace ends.")]
        [SerializeField] private float fadeOut = 0.12f;

        private Transform arc;
        private MeshRenderer arcRenderer;
        private MaterialPropertyBlock block;
        private float bracedFor;
        private float releasedFor;
        private int seenBracedHits;
        private float hitAt = float.NegativeInfinity;
        private float hitAngle;

        private void Awake()
        {
            if (!ram) throw new InvalidOperationException($"RamBrace on '{name}' needs its ram.");
            if (!material) throw new InvalidOperationException($"RamBrace on '{name}' needs its material.");
            if (radius <= 0f) throw new InvalidOperationException($"RamBrace on '{name}' needs a positive radius.");

            if (!quad) quad = CreateQuad();
            var go = new GameObject("Ram brace");
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            arcRenderer = go.AddComponent<MeshRenderer>();
            arcRenderer.sharedMaterial = material;
            arcRenderer.shadowCastingMode = ShadowCastingMode.Off;
            arcRenderer.receiveShadows = false;
            arcRenderer.enabled = false;
            arc = go.transform;
            arc.SetParent(transform, false);
            arc.localScale = Vector3.one * radius;
            block = new MaterialPropertyBlock();
            seenBracedHits = ram.BracedHits;
            releasedFor = fadeOut;
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void OnEnable()
        {
            // Presentation enables child renderers; the arc draws only while it shows.
            if (arcRenderer) arcRenderer.enabled = false;
        }

        private void OnDisable()
        {
            if (arcRenderer) arcRenderer.enabled = false;
            bracedFor = 0f;
            releasedFor = fadeOut;
        }

        private void LateUpdate()
        {
            var heading = Vector3.ProjectOnPlane(transform.up, GamePlane.Normal);
            var pose = GamePlane.PlanePose(GamePlane.Normal, heading);
            var centre = GamePlane.WorldPointOnPlane(ram.Body.worldCenterOfMass);

            if (ram.BracedHits != seenBracedHits)
            {
                seenBracedHits = ram.BracedHits;
                hitAt = Time.time;
                var toHit = ram.LastBracedPoint - centre;
                hitAngle = Mathf.Atan2(Vector3.Dot(toHit, pose * Vector3.right), Vector3.Dot(toHit, pose * Vector3.up));
            }

            if (ram.Braking)
            {
                if (releasedFor > 0f) bracedFor = 0f;
                bracedFor += Time.deltaTime;
                releasedFor = 0f;
            }
            else releasedFor += Time.deltaTime;

            var fade = ram.Braking ? 1f : fadeOut > 0f ? 1f - releasedFor / fadeOut : 0f;
            if (fade <= 0f)
            {
                arcRenderer.enabled = false;
                return;
            }

            arc.SetPositionAndRotation(centre + pose * new Vector3(0f, 0f, depth), pose);
            block.SetFloat(HalfAngleId, ram.BraceAngle * Mathf.Deg2Rad);
            block.SetFloat(ExtentId, unfold > 0f ? Mathf.Clamp01(bracedFor / unfold) : 1f);
            block.SetFloat(FadeId, fade);
            block.SetFloat(ProgressId, ram.HoldProgress);
            block.SetFloat(ElapsedId, bracedFor);
            block.SetFloat(HitAngleId, hitAngle);
            block.SetFloat(HitAgeId, Time.time - hitAt);
            arcRenderer.SetPropertyBlock(block);
            arcRenderer.enabled = true;
        }

        // Spans every angle a brace can take, in arc radii with the nose along +y.
        private static Mesh CreateQuad()
        {
            var mesh = new Mesh { name = "Ram brace quad" };
            mesh.vertices = new[]
            {
                new Vector3(-1.7f, -0.2f, 0f),
                new Vector3(1.7f, -0.2f, 0f),
                new Vector3(-1.7f, 1.7f, 0f),
                new Vector3(1.7f, 1.7f, 0f),
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            mesh.hideFlags = HideFlags.HideAndDontSave;
            return mesh;
        }
    }
}
