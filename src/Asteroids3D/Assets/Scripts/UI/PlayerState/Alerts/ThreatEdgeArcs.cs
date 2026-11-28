using System.Collections.Generic;
using Combat.Targeting;
using Ships;
using UI.Weapons;
using UnityEngine;
using UnityEngine.UI;
using Utils;

namespace UI.PlayerState.Alerts
{
    /// <summary>
    /// The player's edge arcs: a bold, pulsing arc and chevron in the alert ink on the screen edge, sliding
    /// with the bearing, toward each off-screen missile tracking the player and each off-screen enemy mid-lock
    /// on the player.
    /// It polls the player's lock channel and the ships each frame; whatever leaves play drops out.
    /// </summary>
    public sealed class ThreatEdgeArcs : MonoBehaviour
    {
        // Canvas units on the overlay's 1080-high canvas, so one unit is a pixel at 1080p.
        internal const float Inset = 30f;
        private const float ArcRadius = 150f;
        private const float ArcHalfDegrees = 30f;
        private const int ArcSegments = 10;
        private const float ArcWidth = 14f;
        private const float ChevronBack = 40f;
        private const float ChevronArm = 32f;
        private const float ChevronWidth = 12f;
        private const float PulseHz = 3f;
        // The canopy adds ink as light, so full-strength alert ink washes out to white.
        private const float RestAlpha = .4f;
        private const float PeakAlpha = .7f;

        private readonly List<(Vector2 point, float degrees)> placed = new();
        private SubscribedSet<Ship> ships;
        private Camera worldCamera;
        private RectTransform area;
        private AlertInk ink;
        private Ship player;
        private int drawn;

        public static ThreatEdgeArcs Create(Transform parent, Camera worldCamera, SubscribedSet<Ship> ships)
        {
            var go = new GameObject("Threat edge arcs", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var arcs = go.AddComponent<ThreatEdgeArcs>();
            arcs.worldCamera = worldCamera;
            arcs.ships = ships;
            arcs.ink = AlertInk.Create(go.transform, "Edge arc ink", arcs.Draw);
            return arcs;
        }

        /// <summary>
        /// Where an off-screen point's edge arc sits: <paramref name="local"/> and the result are relative to
        /// the screen's centre, in the same units as <paramref name="half"/>, the screen's half size. The arc
        /// sits <paramref name="inset"/> inside the edge the bearing crosses; degrees is the bearing, 0 = right.
        /// </summary>
        public static bool TryPlace(Vector2 local, Vector2 half, float inset, out Vector2 point, out float degrees)
        {
            point = default;
            degrees = 0f;
            if (Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y) return false;
            point = local * Mathf.Min((half.x - inset) / Mathf.Abs(local.x), (half.y - inset) / Mathf.Abs(local.y));
            degrees = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
            return true;
        }

        internal IReadOnlyList<(Vector2 point, float degrees)> Placed => placed;

        private void Awake()
        {
            area = (RectTransform)transform;
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.sizeDelta = Vector2.zero;
        }

        public void Present(Ship player) => this.player = player;

        private void LateUpdate()
        {
            placed.Clear();
            // Death leaves trackers registered, so the arcs also need a living player.
            if (player && player.gameObject.activeInHierarchy && player.HealthPct > 0f)
            {
                foreach (var missile in player.TrackingMissiles)
                {
                    if (missile is Object body && !body) continue;
                    Place(Substrate.GamePlane.PlanePointToWorld(missile.Kinematics.pos));
                }
                AddLockingEnemies();
            }
            if (placed.Count == 0 && drawn == 0) return;
            drawn = placed.Count;
            ink.SetVerticesDirty();
        }

        // Separable: the warning before launch, from enemies whose lock-on sensor is on the player.
        private void AddLockingEnemies()
        {
            foreach (var ship in ships)
            {
                if (!ship || ship == player || !ship.gameObject.activeInHierarchy || ship.Lock.IsConcealed) continue;
                var sensor = ship.Targeting;
                if (!sensor || !ReferenceEquals(sensor.CurrentTarget, player)) continue;
                if (sensor.State is LockState.Locking or LockState.Locked) Place(ship.transform.position);
            }
        }

        private void Place(Vector3 world)
        {
            var size = area.rect.size;
            var local = ((Vector2)worldCamera.WorldToViewportPoint(world) - Vector2.one * .5f) * size;
            if (TryPlace(local, size * .5f, Inset, out var point, out var degrees)) placed.Add((point, degrees));
        }

        private void Draw(VertexHelper mesh, Rect rect)
        {
            var tint = LockOnIndicator.ThreatTint;
            tint.a = Mathf.Lerp(RestAlpha, PeakAlpha, .5f + .5f * Mathf.Sin(Time.time * PulseHz * 2f * Mathf.PI));
            foreach (var (point, degrees) in placed)
            {
                var bearing = degrees * Mathf.Deg2Rad;
                var outward = new Vector2(Mathf.Cos(bearing), Mathf.Sin(bearing));
                var side = new Vector2(-outward.y, outward.x);
                var centre = rect.center + point - outward * ArcRadius;
                var from = centre + Arc(bearing, -ArcHalfDegrees);
                for (var i = 1; i <= ArcSegments; i++)
                {
                    var to = centre + Arc(bearing, Mathf.Lerp(-ArcHalfDegrees, ArcHalfDegrees, (float)i / ArcSegments));
                    AlertInk.Stroke(mesh, from, to, ArcWidth, tint);
                    from = to;
                }
                var tip = rect.center + point - outward * ChevronBack;
                var heel = tip - outward * ChevronArm;
                AlertInk.Stroke(mesh, heel + side * ChevronArm, tip, ChevronWidth, tint);
                AlertInk.Stroke(mesh, heel - side * ChevronArm, tip, ChevronWidth, tint);
            }
        }

        private static Vector2 Arc(float bearing, float offsetDegrees)
        {
            var angle = bearing + offsetDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ArcRadius;
        }
    }
}
