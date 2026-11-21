using System;
using System.Collections.Generic;
using Cameras.Shake;
using Damage;
using Ships.Command;
using Ships.Damage;
using Ships.Registry;
using UnityEngine;
using Substrate;
using Object = UnityEngine.Object;

namespace Cameras
{
    /// <summary>
    /// Follows the player (the flight camera), framing the nearest enemies being fought
    /// unless the player chose the manual camera (<see cref="CameraSettings"/>).
    /// Fits all active ships when unlocked or without a subject.
    /// Manual framing remains available after the last ship deactivates.
    /// Shakes its view on the subject's hits, death and own shakes without moving its transform.
    /// </summary>
    public class ObserverCam : SmoothCamera
    {
        [Header("Focus Behavior")]
        [SerializeField] private bool lockCameraToSubject;
        [SerializeField] private bool lockZoomToSubject;
        [Tooltip("Adaptive zoom, velocity lead, cursor lead and fight framing while locked on the subject.")]
        [SerializeField] private FlightCameraProfile feel;

        [Header("Player Tracking")]
        [Tooltip("If true, camera will shift to keep the player within the view frustum.")]
        [SerializeField] private bool keepSubjectInView = true;

        [Header("Player Zoom")]
        [Tooltip("Factor the manual camera's framing scales by per scroll-wheel notch.")]
        [SerializeField, Min(1f)] private float zoomStep = 1.1f;
        [Tooltip("Closest the manual camera can zoom in, as a multiple of its framing.")]
        [SerializeField] private float minZoomScale = 0.6f;
        [Tooltip("Widest the manual camera can zoom out, as a multiple of its framing.")]
        [SerializeField] private float maxZoomScale = 2f;

        private HashSet<Transform> secondarySubjects;
        private Transform subject;
        private Rigidbody leadBody;
        private IShipStatus hull;
        private IDamageEvents damage;
        private ShakeChannel shake;
        private readonly ShakeLayers layers = new();
        private float trauma;
        private Vector2 lead;
        private Vector2 leadVelocity;
        private Vector2? cursor;
        private Vector2 cursorLead;
        private Vector2 cursorLeadVelocity;
        private Vector2 fightShift;
        private Vector2 fightShiftVelocity;
        private HashSet<Transform> engaged;
        private HashSet<Transform> nextEngaged;
        private List<Transform> fightCandidates;
        private Comparison<Transform> byDistanceFromSubject;
        private float lockedZoom;
        private float lockedZoomVelocity;
        private Vector2? manualCenter;
        private float? manualZoom;
        private float zoomScale = 1f;

        public bool LockCameraToSubject => lockCameraToSubject;
        public Transform Subject => subject;
        public Vector2? ManualCenter => manualCenter;
        public float? ManualZoom => manualZoom;
        public float ZoomScale => zoomScale;
        internal float AppliedZoomScale => CameraSettings.ManualCamera ? zoomScale : 1f;

        protected override void Awake()
        {
            if (!feel)
                throw new InvalidOperationException($"{name}'s ObserverCam has no FlightCameraProfile assigned to feel.");
            lockedZoom = feel.zoom;
            base.Awake();
            secondarySubjects = new HashSet<Transform>();
            engaged = new HashSet<Transform>();
            nextEngaged = new HashSet<Transform>();
            fightCandidates = new List<Transform>();
            byDistanceFromSubject = CompareDistanceFromSubject;
        }

        // A disabled camera must neither freeze a shaken frame nor resume a stale shake.
        private void OnDisable()
        {
            trauma = 0f;
            layers.Clear();
            Cam.ResetWorldToCameraMatrix();
        }

        private void OnDestroy()
        {
            BindDamage(null);
            BindShake(null);
        }

        protected override void Update()
        {
            base.Update();
            ApplyShake();
        }

        private void ApplyShake()
        {
            trauma = Mathf.Max(0f, trauma - feel.traumaDecay * Time.unscaledDeltaTime);
            Cam.ResetWorldToCameraMatrix();
            var time = Time.unscaledTime;
            var amount = Mathf.Pow(trauma, feel.shakeExponent);
            var reach = amount * feel.maxShakeOffset * Cam.orthographicSize;
            var offset = new Vector2(CameraUtils.ShakeNoise(0, time, feel.shakeFrequency),
                CameraUtils.ShakeNoise(1, time, feel.shakeFrequency)) * reach;
            var roll = CameraUtils.ShakeNoise(2, time, feel.shakeFrequency) * amount * feel.maxShakeRoll;
            layers.Sample(time, Cam.orthographicSize, out var shakeOffset, out var shakeRoll);
            // A stopped clock freezes the kits holding shakes, so their layers would rumble under the pause.
            if (Time.timeScale > 0f)
            {
                offset += shakeOffset;
                roll += shakeRoll;
            }
            CameraUtils.CapShake(ref offset, ref roll, feel.shakeOffsetCap * Cam.orthographicSize, feel.shakeRollCap);
            if (offset == Vector2.zero && roll == 0f) return;
            // The view matrix carries the shake, so follow smoothing, children and listeners never see it.
            Cam.worldToCameraMatrix = CameraUtils.ShakenView(Cam.worldToCameraMatrix, offset, roll);
        }

        private void OnSubjectDamaged(AppliedDamage hit) =>
            trauma = Mathf.Clamp01(trauma + hit.HitSize(damage.Health.MaxValue) * feel.traumaPerHit);

        private void OnSubjectDeath(ShipId victim, DamageInfo blow) => trauma = Mathf.Clamp01(trauma + feel.deathTrauma);

        private void OnShakeRaised(Object source, CameraShakeProfile profile, Vector2 direction) =>
            layers.Start(source, profile, direction, Time.unscaledTime);

        private void OnShakeReleased(Object source) => layers.Release(source, Time.unscaledTime);

        protected override void ComputeDesiredCameraState(out Vector2? desiredCenter, out float? desiredSize)
        {
            if (manualCenter.HasValue)
            {
                desiredCenter = manualCenter.Value;
                desiredSize = manualZoom ?? Cam.orthographicSize;
                return;
            }
            if (!TryGetBoundaryAroundAllSubjects(out var min2D, out var max2D))
            {
                desiredCenter = null;
                desiredSize = null;
                return;
            }
            // Outside the locked branch, so the set empties once the subject is destroyed.
            StepEngaged();
            var center2D = ComputeFocusCenter(min2D, max2D);
            desiredSize = ComputeDesiredZoom(center2D, min2D, max2D);
            desiredCenter = ApplyViewConstraints(center2D, desiredSize.Value);
        }

        private Vector2 ComputeFocusCenter(Vector2 boundsMin, Vector2 boundsMax)
        {
            if (!lockCameraToSubject || !subject)
                return CameraUtils.BoundsCenter(boundsMin, boundsMax);
            var center = GamePlane.WorldPointToPlane(subject.position) + StepLead() + StepCursorLead();
            return center + StepFightShift(center);
        }

        private Vector2 StepLead()
        {
            var target = Vector2.ClampMagnitude(LeadBodyVelocity * feel.leadSeconds, feel.maxLead);
            lead = Vector2.SmoothDamp(lead, target, ref leadVelocity, feel.leadSmoothTime,
                float.PositiveInfinity, Time.unscaledDeltaTime);
            return lead;
        }

        private Vector2 StepCursorLead()
        {
            var target = cursor.HasValue
                ? CameraUtils.CursorLead(Cam.ScreenToViewportPoint(cursor.Value), feel.cursorDeadZone, feel.cursorLead)
                : Vector2.zero;
            cursorLead = Vector2.SmoothDamp(cursorLead, target, ref cursorLeadVelocity, feel.cursorLeadSmoothTime,
                float.PositiveInfinity, Time.unscaledDeltaTime);
            return cursorLead;
        }

        private Vector2 StepFightShift(Vector2 center)
        {
            var target = TryGetFightBounds(out var min, out var max)
                ? FightFocus(center, min, max) - center
                : Vector2.zero;
            fightShift = Vector2.SmoothDamp(fightShift, target, ref fightShiftVelocity, feel.fightShiftTime,
                float.PositiveInfinity, Time.unscaledDeltaTime);
            return fightShift;
        }

        private Vector2 FightFocus(Vector2 center, Vector2 min, Vector2 max)
        {
            var biased = Vector2.Lerp(center, CameraUtils.BoundsCenter(min, max), feel.fightCenterBias);
            var fitted = CameraUtils.ShiftToFitBounds(biased, min, max, feel.maxZoomOut, Cam.aspect, padding);
            var player = GamePlane.WorldPointToPlane(subject.position);
            return CameraUtils.ShiftToKeepPointInView(fitted, player - fitted, feel.maxZoomOut, Cam.aspect, padding);
        }

        private Vector2 LeadBodyVelocity => leadBody && leadBody.gameObject.activeInHierarchy
            ? GamePlane.WorldDirToPlane(leadBody.linearVelocity)
            : Vector2.zero;

        private float ComputeDesiredZoom(Vector2 center, Vector2 boundsMin, Vector2 boundsMax)
        {
            if (manualZoom.HasValue)
                return manualZoom.Value;
            if (lockZoomToSubject && lockCameraToSubject)
                return StepLockedZoom(center) * AppliedZoomScale;
            var preferredSize = CameraUtils.ZoomToFitBounds(center, boundsMin, boundsMax, padding, Cam.aspect);
            return ClampZoom(preferredSize) * AppliedZoomScale;
        }

        private float StepLockedZoom(Vector2 center)
        {
            var target = Mathf.Min(Mathf.Max(Mathf.Clamp(SpeedZoom, BaseZoom, BaseZoom + feel.maxWiden),
                FightZoom(center)), feel.maxZoomOut);
            lockedZoom = CameraUtils.SmoothZoom(lockedZoom, target, ref lockedZoomVelocity,
                feel.widenTime, feel.narrowTime, Time.unscaledDeltaTime);
            return lockedZoom;
        }

        private void StepEngaged()
        {
            nextEngaged.Clear();
            if (!CameraSettings.ManualCamera && subject)
            {
                fightCandidates.Clear();
                foreach (var ship in secondarySubjects)
                {
                    if (IsEngaged(ship))
                        fightCandidates.Add(ship);
                }
                fightCandidates.Sort(byDistanceFromSubject);
                var count = Mathf.Min(fightCandidates.Count, feel.fightCount);
                for (var i = 0; i < count; i++)
                    nextEngaged.Add(fightCandidates[i]);
            }
            (engaged, nextEngaged) = (nextEngaged, engaged);
        }

        private int CompareDistanceFromSubject(Transform a, Transform b)
        {
            var origin = GamePlane.WorldPointToPlane(subject.position);
            var aDistance = Vector2.Distance(origin, GamePlane.WorldPointToPlane(a.position));
            var bDistance = Vector2.Distance(origin, GamePlane.WorldPointToPlane(b.position));
            return aDistance.CompareTo(bDistance);
        }

        private bool IsEngaged(Transform ship)
        {
            if (!subject || !ship || ship == subject || !ship.gameObject.activeInHierarchy)
                return false;
            var distance = Vector2.Distance(GamePlane.WorldPointToPlane(subject.position), GamePlane.WorldPointToPlane(ship.position));
            return engaged.Contains(ship)
                ? distance <= feel.fightRadius + feel.releaseMargin
                : distance < feel.fightRadius;
        }

        internal float FightZoom(Vector2 center) => TryGetFightBounds(out var min, out var max)
            ? CameraUtils.ZoomToFitBounds(center, min, max, padding, Cam.aspect)
            : 0f;

        private bool TryGetFightBounds(out Vector2 min, out Vector2 max)
        {
            CameraUtils.InitEmptyBounds(out min, out max);
            if (engaged.Count == 0)
                return false;
            CameraUtils.ExpandBounds(ref min, ref max, GamePlane.WorldPointToPlane(subject.position));
            foreach (var ship in engaged)
                CameraUtils.ExpandBounds(ref min, ref max, GamePlane.WorldPointToPlane(ship.position));
            return true;
        }

        private Vector2 ApplyViewConstraints(Vector2 center, float zoomSize)
        {
            if (lockCameraToSubject || !keepSubjectInView || !subject) return center;
            return ShiftCenterToKeepSubjectInView(center, zoomSize);
        }

        private Vector2 ShiftCenterToKeepSubjectInView(Vector2 center, float zoomSize)
        {
            var tempWorldCenter = GamePlane.PlanePointToWorld(center);
            var toPlayerWorld = subject.position - tempWorldCenter;
            var toPlayer2D = new Vector2(
                Vector3.Dot(toPlayerWorld, GamePlane.Right),
                Vector3.Dot(toPlayerWorld, GamePlane.Forward));

            return CameraUtils.ShiftToKeepPointInView(center, toPlayer2D, zoomSize, Cam.aspect, padding);
        }

        internal FlightCameraProfile Feel => feel;
        internal float Trauma => trauma;
        internal int ShakeLayerCount => layers.Count;
        internal Vector2 VelocityLead => lead;
        internal Vector2 CursorLead => cursorLead;
        internal Vector2 FightShift => fightShift;
        internal IReadOnlyCollection<Transform> Engaged => engaged;
        internal float BaseZoom => hull != null
            ? Mathf.Max(0.01f, feel.zoom + feel.zoomPerHullSpeed * (hull.MaxSpeed - feel.referenceHullSpeed))
            : feel.zoom;

        internal float SpeedZoom => BaseZoom + feel.zoomPerSpeed * LeadBodyVelocity.magnitude;

        internal bool TryGetBoundaryAroundAllSubjects(out Vector2 min, out Vector2 max)
        {
            CameraUtils.InitEmptyBounds(out min, out max);

            foreach (var t in secondarySubjects)
            {
                if (!t || !t.gameObject.activeInHierarchy) continue;
                CameraUtils.ExpandBounds(ref min, ref max, GamePlane.WorldPointToPlane(t.position));
            }

            var valid = CameraUtils.AreBoundsValid(min, max);
            if (!valid)
                min = max = Vector2.zero;
        
            return valid;
        }

        public void SetLockCameraToSubject(bool value) => lockCameraToSubject = value;
        public void SetLockZoomToSubject(bool value) => lockZoomToSubject = value;
        public void SetSubject(Transform newSubject, Rigidbody newLeadBody = null, bool removeOldSubject = true,
            IShipStatus newHull = null, IDamageEvents newDamage = null, ShakeChannel newShake = null)
        {
            if (subject && removeOldSubject)
                secondarySubjects.Remove(subject);

            subject = newSubject;
            leadBody = newLeadBody;
            hull = newHull;

            if (newSubject)
                secondarySubjects.Add(newSubject);

            BindDamage(newDamage);
            BindShake(newShake);
        }

        private void BindDamage(IDamageEvents newDamage)
        {
            if (ReferenceEquals(damage, newDamage)) return;
            if (damage != null)
            {
                damage.OnDamaged -= OnSubjectDamaged;
                damage.OnDeath -= OnSubjectDeath;
            }
            damage = newDamage;
            if (damage != null)
            {
                damage.OnDamaged += OnSubjectDamaged;
                damage.OnDeath += OnSubjectDeath;
            }
        }

        private void BindShake(ShakeChannel newShake)
        {
            if (ReferenceEquals(shake, newShake)) return;
            if (shake != null)
            {
                shake.Raised -= OnShakeRaised;
                shake.Released -= OnShakeReleased;
            }
            shake = newShake;
            if (shake != null)
            {
                shake.Raised += OnShakeRaised;
                shake.Released += OnShakeReleased;
            }
        }

        public void AddSecondarySubject(Transform target)
        {
            if (target) secondarySubjects.Add(target);
        }
        public void AddSecondarySubjects(IEnumerable<Transform> targets)
        {
            foreach (var target in targets)
            {
                if (target) secondarySubjects.Add(target);
            }
        }
        public void RemoveSecondarySubject(Transform target)
        {
            if (target) secondarySubjects.Remove(target);
        }
        public void ClearSecondarySubjects() => secondarySubjects.Clear();

        public void SetVisible(bool visible) => Cam.enabled = visible;

        public void SetManualCenter(Vector2? center) => manualCenter = center;
        public void SetManualZoom(float? zoom) => manualZoom = zoom;
        public void SetCursor(Vector2? screenPoint) => cursor = screenPoint;

        public void ResumeFollow()
        {
            lead = leadVelocity = cursorLead = cursorLeadVelocity = fightShift = fightShiftVelocity = Vector2.zero;
            // Keeps pace with the subject instead of springing toward the stale lead.
            dampVelocity = GamePlane.PlaneDirToWorld(LeadBodyVelocity);
        }

        public void StepZoom(float notchesIn) =>
            zoomScale = Mathf.Clamp(zoomScale * Mathf.Pow(zoomStep, -notchesIn), minZoomScale, maxZoomScale);

        public Vector2 CurrentCenter2D => manualCenter
            ?? (subject ? GamePlane.WorldPointToPlane(subject.position) : Vector2.zero);
    }
}
