using System;
using Ships.Keystones.Kits;
using Substrate;
using Substrate.Presentation;
using UnityEngine;

namespace Ships.Keystones.Tells
{
    /// <summary>
    /// Recall's tell: a ring in the game plane where the blink started, drawn like a charge's target
    /// marker, fading out over the recall window and gone once the ship recalls. Rides the kit prefab.
    /// </summary>
    public sealed class RecallMarker : MonoBehaviour, IPresentationPart
    {
        private const int Segments = 48;

        [SerializeField] private Blink keystone;
        [SerializeField] private Material ringMaterial;
        [SerializeField] private Color color = new(1f, 0.65f, 0.25f, 0.9f);
        [SerializeField, Min(0.01f)] private float radius = 1f;
        [SerializeField, Min(0.001f)] private float lineWidth = 0.08f;

        private LineRenderer ring;
        private Vector2? drawnAt;

        private void Awake()
        {
            if (!keystone)
                throw new InvalidOperationException($"RecallMarker on '{name}' needs its keystone.");
            if (!ringMaterial)
                throw new InvalidOperationException($"RecallMarker on '{name}' needs its ring material.");
            var root = new GameObject("RecallMarker");
            root.transform.SetParent(transform, false);
            ring = root.AddComponent<LineRenderer>();
            ring.sharedMaterial = ringMaterial;
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.widthMultiplier = lineWidth;
            ring.positionCount = Segments;
            ring.enabled = false;
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void OnDisable()
        {
            ring.enabled = false;
            drawnAt = null;
        }

        private void LateUpdate()
        {
            if (keystone.Marker is not { } at)
            {
                ring.enabled = false;
                drawnAt = null;
                return;
            }

            if (drawnAt != at) Place(at);
            var faded = color;
            faded.a *= keystone.MarkerLeft;
            ring.startColor = ring.endColor = faded;
            ring.enabled = true;
        }

        private void Place(Vector2 at)
        {
            drawnAt = at;
            for (var i = 0; i < Segments; i++)
            {
                var angle = i * Mathf.PI * 2f / Segments;
                ring.SetPosition(i, GamePlane.PlanePointToWorld(at + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius));
            }
        }
    }
}
