using System;
using System.Collections.Generic;
using Combat.Projectiles;
using Combat.Weapons.Arsenal;
using Ships.Command;
using Substrate;
using UnityEngine;

namespace UI.Markers
{
    /// <summary>
    /// Draws a target marker for each of the HUD ship's concussion charges in flight: a ring at its
    /// target point and a faint ring at its blast radius, in the game plane. The overlay binds it to
    /// its own ship's weapon readouts, so another ship's charges never get one. Markers are world
    /// geometry drawn by the observer camera, so they follow the overlay's visibility by hand.
    /// </summary>
    public sealed class ChargeTargetMarkers : MonoBehaviour
    {
        private sealed class Marker
        {
            public IChargeFlight Flight;
            public Action OnEnded;
            public GameObject Root;
        }

        private const int DefaultLayer = 0;

        [SerializeField] private Material ringMaterial;
        [SerializeField] private Color pointColor = new(1f, 0.65f, 0.25f, 0.9f);
        [SerializeField] private Color blastColor = new(1f, 0.65f, 0.25f, 0.2f);
        [SerializeField, Min(0.01f)] private float pointRadius = 0.6f;
        [SerializeField, Min(0.001f)] private float lineWidth = 0.08f;
        [SerializeField, Min(8)] private int segments = 48;

        private readonly List<IChargeLaunchReadout> sources = new();
        private readonly List<Marker> markers = new();
        private bool visible = true;

        public void Initialize(IWeaponReadouts weapons)
        {
            Unbind();
            if (weapons == null) return;

            foreach (var slot in weapons.Slots)
            foreach (var readout in weapons.Readouts(slot))
            {
                if (readout is not IChargeLaunchReadout launches) continue;
                launches.Launched += Show;
                sources.Add(launches);
            }
        }

        public void SetVisible(bool value)
        {
            visible = value;
            foreach (var marker in markers)
                marker.Root.SetActive(visible);
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Unbind()
        {
            foreach (var source in sources)
                source.Launched -= Show;
            sources.Clear();
            for (var i = markers.Count - 1; i >= 0; i--)
                Remove(markers[i]);
        }

        private void Show(IChargeFlight flight)
        {
            var root = new GameObject("ChargeTargetMarker") { layer = DefaultLayer };
            root.transform.SetParent(transform, false);
            root.SetActive(visible);
            AddRing(root, "Point", flight.TargetPoint, pointRadius, pointColor);
            AddRing(root, "Blast", flight.TargetPoint, flight.BlastRadius, blastColor);

            var marker = new Marker { Flight = flight, Root = root };
            marker.OnEnded = () => Remove(marker);
            flight.Ended += marker.OnEnded;
            markers.Add(marker);
        }

        private void Remove(Marker marker)
        {
            marker.Flight.Ended -= marker.OnEnded;
            markers.Remove(marker);
            if (marker.Root) Destroy(marker.Root);
        }

        private void AddRing(GameObject root, string ringName, Vector3 centre, float radius, Color color)
        {
            var go = new GameObject(ringName) { layer = root.layer };
            go.transform.SetParent(root.transform, false);
            var ring = go.AddComponent<LineRenderer>();
            ring.sharedMaterial = ringMaterial;
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.widthMultiplier = lineWidth;
            ring.startColor = ring.endColor = color;
            ring.positionCount = segments;
            for (var i = 0; i < segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                ring.SetPosition(i, centre + GamePlane.PlaneDirToWorld(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))) * radius);
            }
        }
    }
}
