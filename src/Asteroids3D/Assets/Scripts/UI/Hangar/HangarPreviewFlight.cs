using Ships.Command;
using Ships.Presentation;
using Ships.Visuals.Wings;
using UnityEngine;

namespace UI.Hangar
{
    public sealed class HangarPreviewFlight : MonoBehaviour
    {
        private const float ArrivalSeconds = 1.15f;
        private const float DepartureSeconds = .55f;
        private WingVisuals[] wings;
        private Quaternion orientation;
        private float radius;
        private float age;
        private bool departing;
        private Vector3 departurePosition;
        private Quaternion departureRotation;
        private PilotCommand command;

        private void Awake() => wings = GetComponentsInChildren<WingVisuals>(true);

        public void Initialize(float framingRadius, Quaternion baseOrientation)
        {
            radius = framingRadius;
            orientation = baseOrientation;
            command.thrust = 1f;
            foreach (var wing in wings)
                wing.Bind(new ShipView(transform, null, () => command, null, false));
            PoseArrival();
        }

        public void Depart()
        {
            departing = true;
            age = 0f;
            departurePosition = transform.localPosition;
            departureRotation = transform.localRotation;
            command.thrust = 1f;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (departing)
            {
                var t = Mathf.Clamp01(age / DepartureSeconds);
                transform.localPosition = departurePosition + radius * new Vector3(4f * t * t, .65f * t * t, -2f * t * t);
                transform.localRotation = Quaternion.Slerp(departureRotation,
                    FlightRotation(-30f, -18f, 12f), t * t);
                if (t >= 1f) Destroy(gameObject);
                return;
            }

            PoseArrival();
        }

        private void PoseArrival()
        {
            var cruise = Mathf.Max(0f, age - ArrivalSeconds);
            var turn = Mathf.Sin(cruise * .85f);
            var position = radius * new Vector3(.018f * Mathf.Sin(cruise * .53f),
                .022f * Mathf.Sin(cruise * 1.1f), 0f);
            var rotation = FlightRotation(14f + 8f * turn, 5f * turn,
                -6f + 2f * Mathf.Sin(cruise * .67f));
            var t = Mathf.Clamp01(age / ArrivalSeconds);
            var remaining = 1f - t;
            transform.localPosition = position + radius * new Vector3(
                -3.4f * remaining * remaining * remaining,
                -.65f * remaining * remaining * remaining + .9f * t * remaining * remaining,
                5f * remaining * remaining * remaining);
            transform.localRotation = Quaternion.Slerp(FlightRotation(-32f, 22f, 18f),
                rotation, 1f - remaining * remaining);
            command.thrust = age < .7f || Mathf.Sin(cruise * .85f) > .92f ? 1f : 0f;
        }

        private Quaternion FlightRotation(float roll, float yaw, float pitch) =>
            orientation * Quaternion.Euler(pitch, roll, yaw);
    }
}
