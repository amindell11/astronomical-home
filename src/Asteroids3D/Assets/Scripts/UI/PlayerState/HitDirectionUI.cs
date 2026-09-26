using System.Collections.Generic;
using Damage;
using Ships.Command;
using Ships.Damage;
using Substrate;
using UnityEngine;
using UnityEngine.UI;

namespace UI.PlayerState
{
    /// <summary>
    /// Screen-edge arc pips toward whatever last hit the player: one arc per hit on an ellipse inset
    /// from the screen rect, fading out over <see cref="fadeSeconds"/>. Bearing is the hit velocity
    /// reversed (a fast bolt's reported hit point can already lie past the ship's center), falling back
    /// to the ship-to-hit-point direction for a resting contact; game-plane direction reads as screen
    /// direction because the observer camera keeps the plane axes screen-aligned.
    /// </summary>
    public sealed class HitDirectionUI : MaskableGraphic
    {
        [Tooltip("Seconds a pip takes to fade out after its hit.")]
        [SerializeField] private float fadeSeconds = 0.8f;

        [Tooltip("Inset of the pip ellipse from the screen edge, as a fraction of each side.")]
        [SerializeField, Range(0f, 0.4f)] private float edgeInset = 0.06f;

        [Tooltip("Half of the arc each pip spans, in degrees.")]
        [SerializeField] private float arcHalfAngleDeg = 10f;

        [Tooltip("Radial thickness of a pip in canvas units.")]
        [SerializeField] private float thickness = 14f;

        [Tooltip("Most pips shown at once; a newer hit evicts the oldest.")]
        [SerializeField] private int maxPips = 8;

        private const int ArcSegments = 8;
        private const float MinBearingSqr = 1e-4f;

        private readonly struct Pip
        {
            public readonly float AngleDeg;
            public readonly float BornAt;

            public Pip(float angleDeg, float bornAt)
            {
                AngleDeg = angleDeg;
                BornAt = bornAt;
            }
        }

        private readonly List<Pip> pips = new();
        private IShipStatus status;
        private IDamageEvents damage;
        private bool subscribed;

        internal int PipCount => pips.Count;
        internal float PipAngleDeg(int index) => pips[index].AngleDeg;

        /// <summary>Re-bindable: the persistent HUD re-Initializes when the player is rebuilt.</summary>
        public void Initialize(IShipStatus shipStatus, IDamageEvents damageEvents)
        {
            Unsubscribe();
            status = shipStatus;
            damage = damageEvents;
            pips.Clear();
            if (isActiveAndEnabled) Subscribe();
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            Subscribe();
        }

        protected override void OnDisable()
        {
            Unsubscribe();
            pips.Clear();
            base.OnDisable();
        }

        private void Subscribe()
        {
            if (subscribed || damage == null) return;
            damage.OnDamaged += OnDamaged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            damage.OnDamaged -= OnDamaged;
            subscribed = false;
        }

        private void OnDamaged(DamageInfo hit)
        {
            if (status == null) return;
            var bearing = -GamePlane.WorldDirToPlane(hit.HitVelocity);
            if (bearing.sqrMagnitude < MinBearingSqr)
                bearing = GamePlane.WorldPointToPlane(hit.HitPoint) - status.Kinematics.pos;
            if (bearing.sqrMagnitude < MinBearingSqr) return;

            if (pips.Count >= maxPips) pips.RemoveAt(0);
            pips.Add(new Pip(Mathf.Atan2(bearing.y, bearing.x) * Mathf.Rad2Deg, Time.time));
            SetVerticesDirty();
        }

        private void Update()
        {
            if (pips.Count == 0) return;
            while (pips.Count > 0 && Time.time - pips[0].BornAt >= fadeSeconds) pips.RemoveAt(0);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            var radii = new Vector2(rect.width, rect.height) * (0.5f - edgeInset);
            foreach (var pip in pips)
            {
                var alpha = 1f - Mathf.Clamp01((Time.time - pip.BornAt) / fadeSeconds);
                AddArc(vh, rect.center, radii, pip.AngleDeg, alpha);
            }
        }

        private void AddArc(VertexHelper vh, Vector2 center, Vector2 radii, float bearingDeg, float alpha)
        {
            var tint = color;
            tint.a *= alpha;
            var bearing = bearingDeg * Mathf.Deg2Rad;
            // The ellipse point in the bearing's true direction has parameter atan2(rx·sin, ry·cos).
            var mid = Mathf.Atan2(radii.x * Mathf.Sin(bearing), radii.y * Mathf.Cos(bearing));
            var half = arcHalfAngleDeg * Mathf.Deg2Rad;
            var inner = radii - Vector2.one * thickness;
            var first = vh.currentVertCount;
            for (var i = 0; i <= ArcSegments; i++)
            {
                var t = mid - half + 2f * half * i / ArcSegments;
                var dir = new Vector2(Mathf.Cos(t), Mathf.Sin(t));
                vh.AddVert(center + dir * inner, tint, Vector2.zero);
                vh.AddVert(center + dir * radii, tint, Vector2.zero);
            }
            for (var i = 0; i < ArcSegments; i++)
            {
                var a = first + i * 2;
                vh.AddTriangle(a, a + 1, a + 3);
                vh.AddTriangle(a, a + 3, a + 2);
            }
        }
    }
}
