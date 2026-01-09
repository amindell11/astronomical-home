using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Game;
using UnityEngine.Serialization;

/// <summary>
/// Multi-target camera that follows all active ships with optional player-lock modes.
/// Extends CameraFollowBase with configurable focus and zoom behaviors.
/// </summary>
public partial class CameraFollow : CameraFollowBase
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

    protected override bool HasValidTargets() => secondarySubjects is { Count: > 0 };

    protected override bool TryGetPlaneBounds(out Vector2 min, out Vector2 max)
    {
        min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        var foundAny = false;

        foreach (var p in 
                 (from t in 
                     secondarySubjects where t && t.gameObject.activeInHierarchy 
                     select GamePlane.WorldPointToPlane(t.position)))
        {
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
            foundAny = true;
        }

        if (!foundAny)
            min = max = Vector2.zero;
        
        return foundAny;
    }

    protected override Vector2 ComputeFocusCenter(Vector2 boundsMin, Vector2 boundsMax)
    {
        if (lockCameraToSubject && subject)
        {
            return GamePlane.WorldPointToPlane(subject.position);
        }
        return (boundsMin + boundsMax) * 0.5f;
    }

    protected override float ComputeDesiredZoom(Vector2 center, Vector2 boundsMin, Vector2 boundsMax)
    {
        if (lockZoomToSubject)
        {
            return ClampZoom(lockZoomDistance);
        }

        // Compute extents from center to encompass all targets
        var maxDx = Mathf.Max(center.x - boundsMin.x, boundsMax.x - center.x);
        var maxDy = Mathf.Max(center.y - boundsMin.y, boundsMax.y - center.y);

        var preferredSize = Mathf.Max(maxDy + padding, (maxDx + padding) / Cam.aspect);
        return ClampZoom(preferredSize);
    }

    protected override Vector2 ApplyViewConstraints(Vector2 center, float zoomSize)
    {
        if (lockCameraToSubject || !keepSubjectInView || !subject) return center;
        
        return ShiftCenterToKeepSubjectInView(center, zoomSize);
    }

    private Vector2 ShiftCenterToKeepSubjectInView(Vector2 center, float zoomSize)
    {
        var horizontalExtent = zoomSize * Cam.aspect;
        var verticalExtent = zoomSize;

        var tempWorldCenter = GamePlane.PlanePointToWorld(center);
        var toPlayerWorld = subject.position - tempWorldCenter;
        var toPlayer2D = new Vector2(
            Vector3.Dot(toPlayerWorld, GamePlane.Right),
            Vector3.Dot(toPlayerWorld, GamePlane.Forward));

        if (Mathf.Abs(toPlayer2D.x) > horizontalExtent - padding)
        {
            var shiftX = Mathf.Abs(toPlayer2D.x) - (horizontalExtent - padding);
            center.x += Mathf.Sign(toPlayer2D.x) * shiftX;
        }
        if (Mathf.Abs(toPlayer2D.y) > verticalExtent - padding)
        {
            var shiftY = Mathf.Abs(toPlayer2D.y) - (verticalExtent - padding);
            center.y += Mathf.Sign(toPlayer2D.y) * shiftY;
        }

        return center;
    }

    public void SetLockCameraToSubject(bool value) => lockCameraToSubject = value;
    public void SetLockZoomToSubject(bool value) => lockZoomToSubject = value;
    public void SetSubject(Transform newSubject) => subject = newSubject;
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
