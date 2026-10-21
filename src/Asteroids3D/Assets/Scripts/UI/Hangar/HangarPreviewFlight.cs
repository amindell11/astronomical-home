using Ships.Command;
using Ships.Presentation;
using Ships.Visuals.Wings;
using UnityEngine;

namespace UI.Hangar
{
    /// <summary>
    /// Poses one pooled preview clone at a carousel offset d along the formation handoff: +1 starts the
    /// arrival swoop, 0 is the settled idle, −1 ends the departure. A clone latches its lane (arriving or
    /// departing, and from which side) only where every lane meets — settled, or off-frame — so a reversal
    /// mid-flight retraces the path it is on.
    /// </summary>
    public sealed class HangarPreviewFlight : MonoBehaviour
    {
        private const float EntrySeconds = 1.15f;
        internal const float HandoffSeconds = .85f;
        private WingVisuals[] wings;
        private Quaternion orientation;
        private float radius;
        private float entryAge = EntrySeconds;
        private float cruise;
        private float lastOffset;
        private bool arriving;
        private float mirror = 1f;
        private PilotCommand command;

        private void Awake() => wings = GetComponentsInChildren<WingVisuals>(true);

        public void Initialize(float framingRadius, Quaternion baseOrientation)
        {
            radius = framingRadius;
            orientation = baseOrientation;
            foreach (var wing in wings)
                wing.Bind(new ShipView(transform, null, () => command, null, false));
        }

        /// <summary>Plays the hangar-entry swoop from the next <see cref="Place"/>.</summary>
        public void Enter()
        {
            entryAge = 0f;
            cruise = 0f;
        }

        internal void Hide()
        {
            gameObject.SetActive(false);
            cruise = 0f;
        }

        /// <param name="offset">This ship's index minus the carousel position.</param>
        /// <param name="velocity">The carousel's velocity: positive while Next is being followed.</param>
        internal void Place(float offset, float velocity, float dt)
        {
            var distance = Mathf.Abs(offset);
            var settled = distance <= HangarCarousel.SettledOffset;
            if (!gameObject.activeSelf || settled || offset * lastOffset < 0f)
                Latch(offset, velocity);
            gameObject.SetActive(true);
            lastOffset = offset;

            entryAge += dt;
            var entering = entryAge < EntrySeconds;
            if (settled && !entering) cruise += dt;

            var turn = Mathf.Sin(cruise * .85f);
            var position = radius * new Vector3(.018f * Mathf.Sin(cruise * .53f),
                .022f * Mathf.Sin(cruise * 1.1f), 0f);
            var rotation = FlightRotation(14f + 8f * turn, 5f * turn, -6f + 2f * Mathf.Sin(cruise * .67f));
            if (entering)
            {
                var t = entryAge / EntrySeconds;
                var remaining = 1f - t;
                position += radius * new Vector3(-3.4f * remaining * remaining * remaining,
                    -.65f * remaining * remaining * remaining + .9f * t * remaining * remaining,
                    5f * remaining * remaining * remaining);
                rotation = Quaternion.Slerp(FlightRotation(-32f, 22f, 18f), rotation, 1f - remaining * remaining);
            }

            if (arriving)
            {
                var remaining = 1f - HangarCarousel.HandoffTime(1f - distance);
                position += radius * new Vector3(3.4f * mirror * remaining * remaining * remaining,
                    -.2f * remaining * remaining, 1.4f * remaining * remaining * remaining);
                rotation *= Quaternion.Euler(0f, 35f * mirror * remaining * remaining, -12f * mirror * remaining);
            }
            else
            {
                var t = HangarCarousel.HandoffTime(distance);
                position += radius * new Vector3(-4f * mirror * t * t, .35f * t * t, 2f * t * t);
                rotation *= Quaternion.Euler(0f, -48f * mirror * t * t, 12f * mirror * t);
            }

            transform.localPosition = position;
            transform.localRotation = rotation;
            command.thrust = !settled || entering || turn > .92f ? 1f : 0f;
        }

        // d and velocity share a sign while the ship closes on the centre.
        private void Latch(float offset, float velocity)
        {
            if (offset == 0f || velocity == 0f) return;
            arriving = offset * velocity > 0f;
            mirror = arriving ? Mathf.Sign(offset) : -Mathf.Sign(offset);
        }

        private Quaternion FlightRotation(float roll, float yaw, float pitch) =>
            orientation * Quaternion.Euler(pitch, roll, yaw);
    }
}
