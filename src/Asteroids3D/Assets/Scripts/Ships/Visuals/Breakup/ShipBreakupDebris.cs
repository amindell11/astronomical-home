using System;
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
        [SerializeField] private PooledVFX explosionPrefab;
        [SerializeField] private Animation motion;
        [SerializeField, Min(.1f)] private float lifetime = 1.4f;
        [SerializeField, Min(.01f)] private float fadeDuration = .25f;
        private static readonly int Visibility = Shader.PropertyToID("_DebrisVisibility");
        private MaterialPropertyBlock block;
        private Vector3 velocity;
        private float age;

        private void Awake()
        {
            if (!explosionPrefab || !motion || !motion.clip || pieces == null || pieces.Length == 0 || lifetime <= fadeDuration)
                throw new InvalidOperationException("Ship debris requires pieces, motion, an explosion and lifetime longer than its fade.");
            foreach (var piece in pieces)
                if (!piece.renderer || piece.lifetime <= fadeDuration || piece.lifetime > lifetime)
                    throw new InvalidOperationException("Ship debris pieces require renderers and lifetimes within their parent's lifetime.");
            block = new MaterialPropertyBlock();
        }

        public void Initialize(Vector3 inheritedVelocity)
        {
            velocity = inheritedVelocity;
            motion.Play();
            SimplePool<PooledVFX>.Get(explosionPrefab, transform.position, Quaternion.identity);
        }

        private void Update()
        {
            age += Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            foreach (var piece in pieces)
            {
                var visibility = Mathf.Clamp01((piece.lifetime - age) / fadeDuration);
                block.SetFloat(Visibility, visibility);
                piece.renderer.SetPropertyBlock(block);
            }
            if (age >= lifetime) Destroy(gameObject);
        }
    }
}
