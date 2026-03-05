using System.Collections.Generic;
using System.Linq;
using Game;
using UnityEngine;
using Utils;

namespace Cameras
{
    /// <summary>
    /// Multi-target camera that follows all active ships with optional player-lock modes.
    /// Extends CameraFollowBase with configurable focus and zoom behaviors.
    /// </summary>
    public partial class ObserverCam : SmoothCamera
    {
        [Header("Focus Behavior")]
        [SerializeField] private bool lockCameraToSubject;
        [SerializeField] private bool lockZoomToSubject;
        [SerializeField] private float lockZoomDistance = 10f;

        [Header("Player Tracking")]
        [Tooltip("If true, camera will shift to keep the player within the view frustum.")]
        [SerializeField] private bool keepSubjectInView = true;

        private HashSet<Transform> secondarySubjects;
        private Transform subject;

        public bool LockCameraToSubject => lockCameraToSubject;

        protected override void Awake()
        {
            base.Awake();
            secondarySubjects = new HashSet<Transform>();
        }

        protected override void ComputeDesiredCameraState(out Vector2? desiredCenter, out float? desiredSize)
        {
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
                return GamePlane.WorldPointToPlane(subject.position);
            return CameraUtils.BoundsCenter(boundsMin, boundsMax);
        }

        private float ComputeDesiredZoom(Vector2 center, Vector2 boundsMin, Vector2 boundsMax)
        {
            if (lockZoomToSubject)
                return ClampZoom(lockZoomDistance);
            var preferredSize = CameraUtils.ZoomToFitBounds(center, boundsMin, boundsMax, padding, Cam.aspect);
            return ClampZoom(preferredSize);
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
    
        private bool TryGetBoundaryAroundAllSubjects(out Vector2 min, out Vector2 max)
        {
            CameraUtils.InitEmptyBounds(out min, out max);

            foreach (var t in secondarySubjects.Where(t => t && t.gameObject.activeInHierarchy))
            {
                CameraUtils.ExpandBounds(ref min, ref max, GamePlane.WorldPointToPlane(t.position));
            }

            var valid = CameraUtils.AreBoundsValid(min, max);
            if (!valid)
                min = max = Vector2.zero;
        
            return valid;
        }

        public void SetLockCameraToSubject(bool value) => lockCameraToSubject = value;
        public void SetLockZoomToSubject(bool value) => lockZoomToSubject = value;
        public void SetSubject(Transform newSubject, bool removeOldSubject = true)
        {
            if (subject && removeOldSubject)
                secondarySubjects.Remove(subject);
            
            subject = newSubject;
            
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
    }
}
