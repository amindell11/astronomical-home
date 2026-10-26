using System;
using Cameras;
using Ships;
using Substrate;
using Substrate.Services.Locales;
using Substrate.Services.Units;
using UnityEngine;

namespace Game.Travel
{
    /// <summary>
    /// Owns the persistent ship from an accepted jump until arrival or cancellation.
    /// The host loads locales and runs the recap while this component keeps travel moving.
    /// The physical departure stays visible before blending into the shared jump presentation.
    /// </summary>
    public sealed class SectorJumpFlight : MonoBehaviour
    {
        private Ship ship;
        private PlayerRig rig;
        private IUnitService units;
        private ObserverCam observer;
        private SectorJumpProfile profile;
        private SectorJumpPassage passage;
        private SectorJumpPresentation presentation;
        private Shader shader;
        private bool commanderEnabled;
        private bool movementEnabled;
        private bool observerEnabled;
        private bool[] colliderEnabled;
        private RigidbodyInterpolation interpolation;
        private Vector3 startPosition;
        private Vector3 startVelocity;
        private Vector3 startCamera;
        private Vector3 arrivalPosition;
        private float priorInvulnerability;
        private float committedAt;
        private float elapsed;
        private float held;
        private bool resumed;
        private bool transferred;
        private bool released;

        public SectorJumpState State { get; private set; }
        public bool Holding => !resumed && elapsed >= passage.HoldStartsAt;
        public bool Complete => resumed && elapsed >= passage.DurationWithHold(held);
        public float ArrivalSpeed => ship.MaxSpeed * profile.arrivalSpeedFraction;

        public static SectorJumpFlight Begin(Transform parent, PlayerRig rig, IUnitService units,
            ObserverCam observer, SectorJumpProfile feel, Shader shader, float heading)
        {
            if (!feel || !shader) throw new ArgumentException("Jump travel needs a feel profile and shader.");
            if (!(feel.departureDistance >= 0f) || !(feel.arrivalSpeedFraction >= 0f) ||
                !(feel.arrivalSpeedFraction <= 1f) || !(feel.worldBlendDuration > 0f))
                throw new ArgumentException("Jump flight handoff settings are outside their supported ranges.");
            var root = new GameObject("Sector jump flight");
            root.transform.SetParent(parent, false);
            var flight = root.AddComponent<SectorJumpFlight>();
            flight.Initialize(rig, units, observer, feel, shader, heading);
            return flight;
        }

        private void Initialize(PlayerRig playerRig, IUnitService unitService, ObserverCam camera,
            SectorJumpProfile feel, Shader jumpShader, float heading)
        {
            rig = playerRig;
            ship = rig.Player;
            units = unitService;
            observer = camera;
            shader = jumpShader;
            profile = Instantiate(feel);
            var startingHeading = Vector3.SignedAngle(GamePlane.Forward,
                Vector3.ProjectOnPlane(ship.transform.up, GamePlane.Normal), GamePlane.Normal);
            passage = new SectorJumpPassage(profile, startingHeading, heading);
            startPosition = ship.Body.position;
            startVelocity = ship.Body.linearVelocity;
            startCamera = observer.transform.position;
            commanderEnabled = ship.Commander.enabled;
            movementEnabled = ship.Movement.enabled;
            observerEnabled = observer.enabled;
            interpolation = ship.Body.interpolation;
            priorInvulnerability = ship.Damage.IsInvulnerable ? ship.Damage.InvulTimeLeft : 0f;
            committedAt = Time.time;
            colliderEnabled = new bool[ship.Colliders.Length];
            for (var i = 0; i < colliderEnabled.Length; i++) colliderEnabled[i] = ship.Colliders[i].enabled;
            Protect();
            observer.enabled = false;
            units.OnShipRespawned += OnRespawned;
            State = passage.StateAt(0f, 0f);
        }

        private void Protect()
        {
            ship.Commander.enabled = false;
            ship.Movement.enabled = false;
            ship.Body.interpolation = RigidbodyInterpolation.None;
            ship.Body.linearVelocity = Vector3.zero;
            ship.Body.angularVelocity = Vector3.zero;
            foreach (var collider in ship.Colliders) collider.enabled = false;
            ship.Damage.SetInvulnerability(float.PositiveInfinity);
        }

        private void OnRespawned(Ship respawned)
        {
            if (respawned != ship) return;
            Protect();
            arrivalPosition = ship.Body.position;
            transferred = true;
            observer.enabled = observerEnabled;
        }

        public void BindBackdrop(Transform ui, LocaleBackdropPair backdrop)
        {
            presentation = SectorJumpPresentation.Create(ui, ship, rig.Overlay,
                units.ActiveRegistry.ActiveShips, observer, backdrop, shader, false);
            presentation.SetWorldBlend(0f);
        }

        public void ResumeArrival()
        {
            if (!Holding) throw new InvalidOperationException("Arrival requires the hyperspace hold.");
            resumed = true;
            arrivalPosition = ship.Body.position;
        }

        private void LateUpdate()
        {
            elapsed += Time.unscaledDeltaTime;
            var departure = profile.turnDuration + profile.chargeDuration;
            if (!presentation) elapsed = Mathf.Min(elapsed, departure);
            if (!resumed) held = Mathf.Max(0f, elapsed - passage.HoldStartsAt);
            State = passage.StateAt(elapsed, held);
            var sequenceTime = elapsed - held;
            Vector3 position;
            if (transferred)
            {
                position = arrivalPosition + GamePlane.PlaneDirToWorld(State.JumpDirection) *
                    (ArrivalSpeed * Mathf.Max(0f, elapsed - passage.ExitStartsAt(held)));
            }
            else
            {
                var u = Mathf.Clamp01(elapsed / profile.turnDuration);
                var drift = profile.turnDuration * (u - u * u + u * u * u / 3f);
                var departureFraction = SectorJumpProfile.Ease((sequenceTime - departure) /
                    Mathf.Max(profile.worldBlendDuration, 0.01f));
                position = startPosition + startVelocity * drift +
                    GamePlane.PlaneDirToWorld(State.JumpDirection) * (profile.departureDistance * departureFraction);
            }
            var rotation = GamePlane.Rotation * Quaternion.AngleAxis(State.HeadingDegrees, Vector3.forward);
            ship.transform.SetPositionAndRotation(position, rotation);
            ship.Body.position = position;
            ship.Body.rotation = rotation;
            var cameraPosition = new Vector3(position.x, position.y, startCamera.z);
            observer.transform.position = resumed ? cameraPosition :
                Vector3.Lerp(startCamera, cameraPosition, SectorJumpProfile.Ease(elapsed / profile.turnDuration));
            rig.Overlay.SetOpacity(State.HudAlpha);
            foreach (var active in units.ActiveRegistry.ActiveShips)
                if (active && active.VisualRig) active.VisualRig.SetHudOpacity(State.HudAlpha);
            if (!presentation) return;
            var blend = SectorJumpProfile.Ease((sequenceTime - departure) / profile.worldBlendDuration) *
                (1f - SectorJumpProfile.Ease(passage.ExitProgress(elapsed, held)));
            presentation.SetWorldBlend(blend);
            presentation.Apply(profile, State, 1099, false,
                Vector3.Lerp(startPosition, arrivalPosition, State.LocaleBlend));
        }

        public void Release(bool arrived)
        {
            if (released) return;
            released = true;
            enabled = false;
            units.OnShipRespawned -= OnRespawned;
            if (presentation) Destroy(presentation.gameObject);
            if (ship)
            {
                for (var i = 0; i < colliderEnabled.Length; i++)
                    ship.Colliders[i].enabled = colliderEnabled[i];
                ship.Body.interpolation = interpolation;
                ship.Body.linearVelocity = arrived
                    ? GamePlane.PlaneDirToWorld(State.JumpDirection) * ArrivalSpeed : Vector3.zero;
                ship.Body.angularVelocity = Vector3.zero;
                ship.Damage.SetInvulnerability(Mathf.Max(0f, priorInvulnerability - (Time.time - committedAt)));
                ship.Movement.enabled = movementEnabled;
                ship.Commander.enabled = commanderEnabled;
            }
            if (observer) observer.enabled = observerEnabled;
            if (rig.Overlay) rig.Overlay.SetOpacity(1f);
            foreach (var active in units.ActiveRegistry.ActiveShips)
                if (active && active.VisualRig) active.VisualRig.SetHudOpacity(1f);
            Destroy(profile);
        }

        private void OnDestroy() => Release(false);
    }
}
