using System;
using System.Collections.Generic;
using UnityEngine;
using Substrate;

namespace Cameras
{
    /// <summary>
    /// Multi-target camera that follows all active ships with optional player-lock modes.
    /// Manual framing remains available after the last ship deactivates.
    /// </summary>
    public class ObserverCam : SmoothCamera
    {
        [Header("Focus Behavior")]
        [SerializeField] private bool lockCameraToSubject;
        [SerializeField] private bool lockZoomToSubject;
        [Tooltip("Adaptive zoom and velocity lead while locked on the subject.")]
        [SerializeField] private FlightCameraProfile feel;

        [Header("Player Tracking")]
        [Tooltip("If true, camera will shift to keep the player within the view frustum.")]
        [SerializeField] private bool keepSubjectInView = true;

        [Header("Player Zoom")]
        [Tooltip("Factor the auto-fit or lock-zoom framing scales by per scroll-wheel notch.")]
        [SerializeField, Min(1f)] private float zoomStep = 1.1f;
        [Tooltip("Closest the player can zoom in, as a multiple of the auto-fit or lock-zoom framing.")]
        [SerializeField] private float minZoomScale = 0.6f;
        [Tooltip("Widest the player can zoom out, as a multiple of the auto-fit or lock-zoom framing.")]
        [SerializeField] private float maxZoomScale = 2f;

        private HashSet<Transform> secondarySubjects;
        private Transform subject;
        private Rigidbody leadBody;
        private Vector2 lead;
        private Vector2 leadVelocity;
        private HashSet<Transform> engaged;
        private HashSet<Transform> nextEngaged;
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

        protected override void Awake()
        {
            if (!feel)
                throw new InvalidOperationException($"{name}'s ObserverCam has no FlightCameraProfile assigned to feel.");
            lockedZoom = feel.zoom;
            base.Awake();
            secondarySubjects = new HashSet<Transform>();
            engaged = new HashSet<Transform>();
            nextEngaged = new HashSet<Transform>();
        }

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
            var center2D = ComputeFocusCenter(min2D, max2D);
            desiredSize = ComputeDesiredZoom(center2D, min2D, max2D);
            desiredCenter = ApplyViewConstraints(center2D, desiredSize.Value);
        }

        private Vector2 ComputeFocusCenter(Vector2 boundsMin, Vector2 boundsMax)
        {
            if (lockCameraToSubject && subject)
                return GamePlane.WorldPointToPlane(subject.position) + StepLead();
            return CameraUtils.BoundsCenter(boundsMin, boundsMax);
        }

        private Vector2 StepLead()
        {
            var target = Vector2.ClampMagnitude(LeadBodyVelocity * feel.leadSeconds, feel.maxLead);
            lead = Vector2.SmoothDamp(lead, target, ref leadVelocity, feel.leadSmoothTime,
                float.PositiveInfinity, Time.unscaledDeltaTime);
            return lead;
        }

        private Vector2 LeadBodyVelocity => leadBody && leadBody.gameObject.activeInHierarchy
            ? GamePlane.WorldDirToPlane(leadBody.linearVelocity)
            : Vector2.zero;

        private float ComputeDesiredZoom(Vector2 center, Vector2 boundsMin, Vector2 boundsMax)
        {
            if (manualZoom.HasValue)
                return manualZoom.Value;
            if (lockZoomToSubject && lockCameraToSubject)
                return StepLockedZoom(center) * zoomScale;
            var preferredSize = CameraUtils.ZoomToFitBounds(center, boundsMin, boundsMax, padding, Cam.aspect);
            return ClampZoom(preferredSize) * zoomScale;
        }

        private float StepLockedZoom(Vector2 center)
        {
            StepEngaged();
            var speedZoom = feel.zoom + feel.zoomPerSpeed * LeadBodyVelocity.magnitude;
            var target = Mathf.Clamp(Mathf.Max(speedZoom, FightZoom(center)), feel.zoom, feel.zoom + feel.maxWiden);
            var smoothTime = target > lockedZoom ? feel.widenTime : feel.narrowTime;
            lockedZoom = Mathf.SmoothDamp(lockedZoom, target, ref lockedZoomVelocity, smoothTime,
                float.PositiveInfinity, Time.unscaledDeltaTime);
            return lockedZoom;
        }

        private void StepEngaged()
        {
            nextEngaged.Clear();
            foreach (var ship in secondarySubjects)
            {
                if (IsEngaged(ship))
                    nextEngaged.Add(ship);
            }
            (engaged, nextEngaged) = (nextEngaged, engaged);
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

        private float FightZoom(Vector2 center)
        {
            if (engaged.Count == 0)
                return 0f;
            CameraUtils.InitEmptyBounds(out var min, out var max);
            CameraUtils.ExpandBounds(ref min, ref max, GamePlane.WorldPointToPlane(subject.position));
            foreach (var ship in engaged)
                CameraUtils.ExpandBounds(ref min, ref max, GamePlane.WorldPointToPlane(ship.position));
            return CameraUtils.ZoomToFitBounds(center, min, max, padding, Cam.aspect);
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
        public void SetSubject(Transform newSubject, Rigidbody newLeadBody = null, bool removeOldSubject = true)
        {
            if (subject && removeOldSubject)
                secondarySubjects.Remove(subject);
            
            subject = newSubject;
            leadBody = newLeadBody;
            
            if (newSubject)
                secondarySubjects.Add(newSubject);
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

        public void StepZoom(float notchesIn) =>
            zoomScale = Mathf.Clamp(zoomScale * Mathf.Pow(zoomStep, -notchesIn), minZoomScale, maxZoomScale);

        public Vector2 CurrentCenter2D => manualCenter
            ?? (subject ? GamePlane.WorldPointToPlane(subject.position) : Vector2.zero);
    }
}
