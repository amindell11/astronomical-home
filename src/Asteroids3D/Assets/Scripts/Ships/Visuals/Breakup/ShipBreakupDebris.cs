using System;
using System.Collections.Generic;
using UnityEngine;
using Utils;

namespace Ships.Visuals.Breakup
{
    public sealed class ShipBreakupDebris : MonoBehaviour
    {
        [Serializable]
        private struct Piece
        {
            public Renderer renderer;
            public float lifetime;
        }

        [SerializeField] private Piece[] pieces;
        [SerializeField] private Transform[] poseRoots = Array.Empty<Transform>();
        [SerializeField] private Animation motion;
        [SerializeField, Min(.1f)] private float lifetime = 1.4f;
        [SerializeField, Min(.01f)] private float fadeDuration = .25f;
        [Tooltip("Soot on the hull paint this debris wears.")]
        [SerializeField, Range(0f, 1f)] private float soot;
        [Tooltip("Render queue the worn hull paint draws at; above 3000 it draws in front of the death explosion. -1 keeps the paint's own.")]
        [SerializeField] private int paintQueue = -1;
        private static readonly int Visibility = Shader.PropertyToID("_DebrisVisibility");
        private static readonly int Soot = Shader.PropertyToID("_SootStrength");
        private readonly List<Material> copies = new();
        private MaterialPropertyBlock block;
        private Vector3 velocity;
        private float pace;
        private float age;

        public int PoseCount => poseRoots.Length;

        private void Awake()
        {
            if (!motion || !motion.clip || pieces == null || pieces.Length == 0 || lifetime <= fadeDuration)
                throw new InvalidOperationException("Ship debris requires pieces, motion and lifetime longer than its fade.");
            foreach (var piece in pieces)
                if (!piece.renderer || piece.lifetime <= fadeDuration || piece.lifetime > lifetime)
                    throw new InvalidOperationException("Ship debris pieces require renderers and lifetimes within their parent's lifetime.");
            foreach (var pose in poseRoots)
                if (!pose)
                    throw new InvalidOperationException("Ship debris requires every authored pose root.");
            block = new MaterialPropertyBlock();
        }

        public void Initialize(Vector3 inheritedVelocity, Transform[] sourcePose, DeathFeelProfile feel,
            IReadOnlyDictionary<Material, Material> hullPaint)
        {
            WearHullPaint(hullPaint);
            velocity = inheritedVelocity;
            pace = feel.Pace;
            for (var i = 0; i < poseRoots.Length; i++)
                poseRoots[i].localRotation = sourcePose[i].localRotation;
            motion[motion.clip.name].speed = pace;
            motion.Play();
            SimplePool<PooledVFX>.Get(feel.DebrisExplosion, transform.position, Quaternion.identity);
        }

        private void Update()
        {
            age += Time.deltaTime * pace;
            transform.position += velocity * Time.deltaTime;
            foreach (var piece in pieces)
            {
                var visibility = Mathf.Clamp01((piece.lifetime - age) / fadeDuration);
                block.SetFloat(Visibility, visibility);
                piece.renderer.SetPropertyBlock(block);
            }
            if (age >= lifetime) Destroy(gameObject);
        }

        private void WearHullPaint(IReadOnlyDictionary<Material, Material> hullPaint)
        {
            if (hullPaint.Count == 0) return;

            var treated = new Dictionary<Material, Material>();
            foreach (var piece in pieces)
            {
                var materials = piece.renderer.sharedMaterials;
                var dressed = false;
                for (var i = 0; i < materials.Length; i++)
                {
                    if (!hullPaint.TryGetValue(materials[i], out var paint)) continue;
                    materials[i] = Worn(paint, treated);
                    dressed = true;
                }
                if (dressed) piece.renderer.sharedMaterials = materials;
            }
        }

        private Material Worn(Material paint, Dictionary<Material, Material> treated)
        {
            if (treated.TryGetValue(paint, out var worn)) return worn;

            if ((soot <= 0f && paintQueue < 0) || !paint.HasFloat(Soot))
                worn = paint;
            else
            {
                worn = new Material(paint);
                worn.SetFloat(Soot, soot);
                if (paintQueue >= 0) worn.renderQueue = paintQueue;
                copies.Add(worn);
            }

            treated.Add(paint, worn);
            return worn;
        }

        private void OnDestroy()
        {
            foreach (var material in copies)
                Destroy(material);
        }
    }
}
