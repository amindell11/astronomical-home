using System;
using Substrate.Presentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ships.Keystones.Tells
{
    /// <summary>
    /// While the kit is live, leaves flat-coloured copies of the ship's hull where it was: every
    /// interval it records where each silhouette mesh sits, and each record draws for a short life
    /// at an alpha that drops in steps. Rides the kit prefab.
    /// </summary>
    public sealed class KeystoneGhosts : MonoBehaviour, IPresentationPart
    {
        private const float SkippedLifeShare = 0.12f;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Tooltip("The kit whose live window leaves ghosts.")]
        [SerializeField] private Keystone keystone;

        [Tooltip("Flat material every ghost draws with. Its colour comes from this part.")]
        [SerializeField] private Material material;

        [Tooltip("Seconds between ghosts while the kit is live.")]
        [SerializeField] private float interval = 0.17f;

        [Tooltip("Seconds a ghost lasts.")]
        [SerializeField] private float life = 0.22f;

        [Tooltip("Colour of every ghost.")]
        [SerializeField] private Color colour = new(0.561f, 0.965f, 1f);

        [Tooltip("Opacity of a ghost when it first shows.")]
        [SerializeField, Range(0f, 1f)] private float peakAlpha = 0.62f;

        [Tooltip("Equal steps a ghost's opacity drops through over its life.")]
        [SerializeField, Min(1)] private int alphaSteps = 4;

        private Mesh[] meshes;
        private Transform[] hull;
        private Matrix4x4[] poses;
        private float[] recordedAt;
        private MaterialPropertyBlock block;
        private int nextRecord;
        private float nextRecordAt;

        private void Awake()
        {
            if (!keystone)
                throw new InvalidOperationException($"KeystoneGhosts on '{name}' needs its keystone.");
            if (!material)
                throw new InvalidOperationException($"KeystoneGhosts on '{name}' needs its material.");
            if (interval <= 0f)
                throw new InvalidOperationException($"KeystoneGhosts on '{name}' needs a positive interval.");
            if (life <= 0f)
                throw new InvalidOperationException($"KeystoneGhosts on '{name}' needs a positive life.");

            var ship = GetComponentInParent<Ship>();
            if (!ship || !ship.VisualRig) return;
            var silhouette = ship.VisualRig.Silhouette;
            meshes = new Mesh[silhouette.Count];
            hull = new Transform[silhouette.Count];
            for (var i = 0; i < silhouette.Count; i++)
            {
                meshes[i] = silhouette[i].GetComponent<MeshFilter>().sharedMesh;
                hull[i] = silhouette[i].transform;
            }

            recordedAt = new float[Mathf.CeilToInt(life / interval) + 1];
            poses = new Matrix4x4[recordedAt.Length * meshes.Length];
            block = new MaterialPropertyBlock();
            DropRecords();
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void OnDisable()
        {
            if (recordedAt != null) DropRecords();
        }

        private void LateUpdate()
        {
            if (meshes == null) return;

            if (keystone.Active && Time.time >= nextRecordAt)
            {
                nextRecordAt = Time.time + interval;
                recordedAt[nextRecord] = Time.time;
                for (var i = 0; i < hull.Length; i++)
                    poses[nextRecord * hull.Length + i] = hull[i].localToWorldMatrix;
                nextRecord = (nextRecord + 1) % recordedAt.Length;
            }

            for (var r = 0; r < recordedAt.Length; r++)
            {
                var age = (Time.time - recordedAt[r]) / life;
                if (age < SkippedLifeShare || age >= 1f) continue;

                var step = Mathf.Min(Mathf.FloorToInt(age * alphaSteps), alphaSteps - 1);
                var tint = colour;
                tint.a = peakAlpha * (alphaSteps - step) / alphaSteps;
                block.SetColor(ColorId, tint);
                for (var i = 0; i < meshes.Length; i++)
                {
                    var mesh = meshes[i];
                    for (var sub = 0; sub < mesh.subMeshCount; sub++)
                        Graphics.DrawMesh(mesh, poses[r * meshes.Length + i], material, gameObject.layer, null, sub,
                            block, ShadowCastingMode.Off, false);
                }
            }
        }

        private void DropRecords()
        {
            for (var r = 0; r < recordedAt.Length; r++)
                recordedAt[r] = float.NegativeInfinity;
            nextRecordAt = 0f;
        }
    }
}
