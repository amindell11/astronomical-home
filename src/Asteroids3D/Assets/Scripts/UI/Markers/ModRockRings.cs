using System;
using System.Collections.Generic;
using Ships.Mods;
using Substrate;
using UnityEngine;

namespace UI.Markers
{
    /// <summary>
    /// Rings each rock near the HUD ship that counts for a rock mod it holds, one faint ring per mod
    /// (Lie in wait's heavy rocks, Orbit's big ones), so the pilot can plan a route. A few times a second
    /// it gathers the rocks in reach into a pool of rings; between gathers the rings follow their rocks.
    /// Drawn like the charge markers, as world geometry the overlay shows and hides by hand.
    /// </summary>
    public sealed class ModRockRings : MonoBehaviour
    {
        private sealed class Ring
        {
            public LineRenderer Line;
            public Collider Rock;
            public float Radius;
        }

        private const int DefaultLayer = 0;
        private const int Segments = 48;
        private const int MaxRocks = 64;

        [SerializeField] private Material ringMaterial;

        [Tooltip("Ring colour on a rock heavy enough for Lie in wait.")]
        [SerializeField] private Color lieInWaitColor = new(0.3f, 0.9f, 0.85f, 0.3f);

        [Tooltip("Ring colour on a rock big enough for Orbit.")]
        [SerializeField] private Color orbitColor = new(1f, 0.7f, 0.3f, 0.3f);

        [Tooltip("Gap, in units, between a rock's surface and its ring.")]
        [SerializeField, Min(0f)] private float gap = 0.5f;

        [Tooltip("How much wider, in units, the Orbit ring sits on a rock that counts for both mods.")]
        [SerializeField, Min(0f)] private float spacing = 0.35f;

        [SerializeField, Min(0.001f)] private float lineWidth = 0.05f;

        [Tooltip("Distance, in units, from the ship within which rocks get rings; about the camera's view.")]
        [SerializeField, Min(1f)] private float reach = 45f;

        [Tooltip("Seconds between gathers of the rocks in reach.")]
        [SerializeField, Min(0.02f)] private float refreshInterval = 0.25f;

        private readonly Collider[] found = new Collider[MaxRocks];
        private readonly List<Ring> rings = new();
        private readonly Vector3[] circle = new Vector3[Segments];
        private readonly Vector3[] points = new Vector3[Segments];
        private IRockMods mods;
        private Transform ship;
        private int used;
        private float untilGather;

        private void Awake()
        {
            if (!ringMaterial)
                throw new InvalidOperationException($"ModRockRings on '{name}' needs its ring material.");
            for (var i = 0; i < Segments; i++)
            {
                var angle = i * Mathf.PI * 2f / Segments;
                circle[i] = GamePlane.PlaneDirToWorld(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
            }
        }

        public void Initialize(IRockMods mods, Transform ship)
        {
            this.mods = mods;
            this.ship = ship;
            untilGather = 0f;
        }

        private void LateUpdate()
        {
            if (!ship)
            {
                Show(0);
                return;
            }

            untilGather -= Time.deltaTime;
            if (untilGather <= 0f)
            {
                untilGather = refreshInterval;
                Gather();
            }
            Follow();
        }

        private void Gather()
        {
            var count = Physics.OverlapSphereNonAlloc(ship.position, reach, found, 1 << LayerIds.Asteroid,
                QueryTriggerInteraction.Ignore);
            var placed = 0;
            for (var i = 0; i < count; i++)
            {
                var rock = found[i];
                var lies = mods.CountsForLieInWait(rock);
                var orbits = mods.CountsForOrbit(rock);
                if (!lies && !orbits) continue;
                var radius = PlaneRadius(rock.bounds) + gap;
                if (lies) Place(placed++, rock, radius, lieInWaitColor);
                if (orbits) Place(placed++, rock, lies ? radius + spacing : radius, orbitColor);
            }
            Show(placed);
        }

        private void Place(int index, Collider rock, float radius, Color color)
        {
            if (index == rings.Count) rings.Add(NewRing());
            var ring = rings[index];
            ring.Rock = rock;
            ring.Radius = radius;
            ring.Line.startColor = ring.Line.endColor = color;
        }

        private void Show(int count)
        {
            for (var i = 0; i < rings.Count; i++)
            {
                rings[i].Line.enabled = i < count;
                if (i >= count) rings[i].Rock = null;
            }
            used = count;
        }

        // A pooled rock is switched off between gathers rather than destroyed.
        private void Follow()
        {
            for (var r = 0; r < used; r++)
            {
                var ring = rings[r];
                var live = ring.Rock && ring.Rock.gameObject.activeInHierarchy;
                ring.Line.enabled = live;
                if (!live) continue;
                var centre = GamePlane.WorldPointOnPlane(ring.Rock.bounds.center);
                for (var i = 0; i < Segments; i++)
                    points[i] = centre + circle[i] * ring.Radius;
                ring.Line.SetPositions(points);
            }
        }

        private Ring NewRing()
        {
            var root = new GameObject("ModRockRing") { layer = DefaultLayer };
            root.transform.SetParent(transform, false);
            var line = root.AddComponent<LineRenderer>();
            line.sharedMaterial = ringMaterial;
            line.useWorldSpace = true;
            line.loop = true;
            line.widthMultiplier = lineWidth;
            line.positionCount = Segments;
            line.enabled = false;
            return new Ring { Line = line };
        }

        private static float PlaneRadius(Bounds bounds)
        {
            var extents = GamePlane.WorldDirToPlane(bounds.extents);
            return Mathf.Max(Mathf.Abs(extents.x), Mathf.Abs(extents.y));
        }
    }
}
