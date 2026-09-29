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
            public Transform transform;
            public Vector3 velocity;
            public Vector3 spin;
            public float delay;
        }

        [SerializeField] private Piece[] pieces;
        [SerializeField] private PooledVFX explosionPrefab;
        [SerializeField, Min(.1f)] private float lifetime = 3f;
        [SerializeField, Min(.01f)] private float fadeDuration = .6f;
        private static readonly int Visibility = Shader.PropertyToID("_DebrisVisibility");
        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private Vector3 velocity;
        private float age;

        private void Awake()
        {
            if (!explosionPrefab || pieces == null || pieces.Length == 0 || lifetime <= fadeDuration)
                throw new InvalidOperationException("Ship debris requires pieces, an explosion and lifetime longer than its fade.");
            foreach (var piece in pieces)
                if (!piece.transform || piece.delay < 0 || piece.delay >= lifetime)
                    throw new InvalidOperationException("Ship debris pieces require transforms and release delays within their lifetime.");
            renderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
        }

        public void Initialize(Vector3 inheritedVelocity)
        {
            velocity = inheritedVelocity;
            SimplePool<PooledVFX>.Get(explosionPrefab, transform.position, Quaternion.identity);
        }

        private void Update()
        {
            var previousAge = age;
            age += Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            foreach (var piece in pieces)
            {
                var elapsed = Mathf.Max(0, age - piece.delay) - Mathf.Max(0, previousAge - piece.delay);
                piece.transform.localPosition += piece.velocity * elapsed;
                piece.transform.Rotate(piece.spin * elapsed, Space.Self);
            }

            var visibility = Mathf.Clamp01((lifetime - age) / fadeDuration);
            if (visibility < 1)
            {
                block.SetFloat(Visibility, visibility);
                foreach (var renderer in renderers) renderer.SetPropertyBlock(block);
            }
            if (age >= lifetime) Destroy(gameObject);
        }
    }
}
