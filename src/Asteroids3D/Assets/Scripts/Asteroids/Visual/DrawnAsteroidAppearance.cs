using System;
using UnityEngine;

namespace Asteroids.Visual
{
    [RequireComponent(typeof(AsteroidController), typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class DrawnAsteroidAppearance : MonoBehaviour
    {
        [Serializable]
        private struct Appearance
        {
            public Mesh surface;
            public Mesh drawing;
            public Material paint;
        }

        [SerializeField] private Appearance[] shapes;
        [SerializeField] private MeshFilter drawing;
        [SerializeField] private MeshFilter contour;
        private AsteroidController asteroid;
        private MeshFilter surface;
        private MeshRenderer paint;

        private void Awake()
        {
            asteroid = GetComponent<AsteroidController>();
            surface = GetComponent<MeshFilter>();
            paint = GetComponent<MeshRenderer>();
            if (!drawing || !contour || shapes == null || shapes.Length == 0)
                throw new InvalidOperationException("Drawn asteroid requires drawing, contour, and shape appearances.");
            foreach (var shape in shapes)
                if (!shape.surface || !shape.drawing || !shape.paint)
                    throw new InvalidOperationException("Every drawn asteroid shape requires surface, drawing, and paint assets.");
        }

        private void OnEnable() => asteroid.OnInitialized += Apply;
        private void OnDisable() => asteroid.OnInitialized -= Apply;

        private void Apply()
        {
            var shape = shapes[asteroid.MeshIndex];
            surface.sharedMesh = shape.surface;
            paint.sharedMaterial = shape.paint;
            drawing.sharedMesh = shape.drawing;
            contour.sharedMesh = shape.surface;
        }
    }
}
