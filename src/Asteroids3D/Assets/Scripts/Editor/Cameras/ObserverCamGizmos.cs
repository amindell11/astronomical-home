using Diagnostics;
using UnityEditor;
using UnityEngine;
using Substrate;

namespace Cameras
{
    /// <summary>The observer camera's framing in plane space: subject bounds, and while locked on the
    /// subject its lead chain, fight rings and zoom rects.</summary>
    [InitializeOnLoad]
    internal static class ObserverCamGizmos
    {
        static ObserverCamGizmos()
        {
            GizmoView.Register(typeof(ObserverCam), "bounds", "Camera Bounds",
                "yellow subject-framing rectangle", "Environment");
            GizmoView.Register(typeof(ObserverCam), "lead", "Camera Lead",
                "cyan velocity lead, magenta cursor lead, orange fight pull, white focus cross, faint magenta cursor dead zone",
                "Environment");
            GizmoView.Register(typeof(ObserverCam), "fight", "Camera Fight",
                "orange fight ring, faint orange release ring, red engaged-ship rings, faint red fight box",
                "Environment");
            GizmoView.Register(typeof(ObserverCam), "zoom", "Camera Zoom",
                "faint white live view, green floor, red speed cap and orange fight cap (solid when clipping), target cyan for speed or orange for fight",
                "Environment");
        }

        private const int EllipseSegments = 48;
        private const float FocusCrossSize = 2f;
        private const float EngagedRingRadius = 2f;

        private static readonly Color VelocityLeadLine = Color.cyan;
        private static readonly Color CursorLeadLine = Color.magenta;
        private static readonly Color FightShiftLine = new(1f, 0.5f, 0f);
        private static readonly Color FocusCross = Color.white;
        private static readonly Color DeadZone = new(1f, 0f, 1f, 0.35f);
        private static readonly Color FightRing = new(1f, 0.5f, 0f);
        private static readonly Color ReleaseRing = new(1f, 0.5f, 0f, 0.35f);
        private static readonly Color EngagedRing = Color.red;
        private static readonly Color FightBox = new(1f, 0f, 0f, 0.35f);
        private static readonly Color LiveView = new(1f, 1f, 1f, 0.35f);
        private static readonly Color ZoomFloor = new(0f, 1f, 0f, 0.5f);
        private static readonly Color SpeedCap = new(1f, 0f, 0f, 0.35f);
        private static readonly Color SpeedCapClipping = Color.red;
        private static readonly Color FightCap = new(1f, 0.5f, 0f, 0.35f);
        private static readonly Color FightCapClipping = new(1f, 0.5f, 0f);
        private static readonly Color SpeedTarget = Color.cyan;
        private static readonly Color FightTarget = new(1f, 0.5f, 0f);

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(ObserverCam))]
        private static void DrawSubjectBounds(ObserverCam cam, GizmoType gizmoType)
        {
            if (!GizmoView.IsOn(typeof(ObserverCam), "bounds") || !GizmoView.InScope(cam)) return;
            if (!Application.isPlaying) return;
            if (!cam.TryGetBoundaryAroundAllSubjects(out var min, out var max)) return;

            Gizmos.color = Color.yellow;
            var bl = new Vector2(min.x, min.y);
            var br = new Vector2(max.x, min.y);
            var tr = new Vector2(max.x, max.y);
            var tl = new Vector2(min.x, max.y);
            Line(bl, br);
            Line(br, tr);
            Line(tr, tl);
            Line(tl, bl);
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(ObserverCam))]
        private static void DrawLead(ObserverCam cam, GizmoType gizmoType)
        {
            if (!ShowsLocked(cam, "lead")) return;

            var s = GamePlane.WorldPointToPlane(cam.Subject.position);
            var v = s + cam.VelocityLead;
            var c = v + cam.CursorLead;
            var p = c + cam.FightShift;
            Gizmos.color = VelocityLeadLine;
            Line(s, v);
            Gizmos.color = CursorLeadLine;
            Line(v, c);
            Gizmos.color = FightShiftLine;
            Line(c, p);
            Cross(p, FocusCrossSize, FocusCross);

            // The dead zone is screen-space, so it sits on the live view, not the focus.
            var radii = new Vector2(cam.Cam.aspect, 1f) * (cam.Cam.orthographicSize * cam.Feel.cursorDeadZone);
            Ellipse(GamePlane.WorldPointToPlane(cam.transform.position), radii, DeadZone);
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(ObserverCam))]
        private static void DrawFight(ObserverCam cam, GizmoType gizmoType)
        {
            if (!ShowsLocked(cam, "fight")) return;

            var feel = cam.Feel;
            var s = GamePlane.WorldPointToPlane(cam.Subject.position);
            Ring(s, feel.fightRadius, FightRing);
            Ring(s, feel.fightRadius + feel.releaseMargin, ReleaseRing);
            if (cam.Engaged.Count == 0) return;

            CameraUtils.InitEmptyBounds(out var min, out var max);
            CameraUtils.ExpandBounds(ref min, ref max, s);
            foreach (var ship in cam.Engaged)
            {
                if (!ship) continue;
                var p = GamePlane.WorldPointToPlane(ship.position);
                Ring(p, EngagedRingRadius, EngagedRing);
                CameraUtils.ExpandBounds(ref min, ref max, p);
            }
            Rect(CameraUtils.BoundsCenter(min, max), (max - min) * 0.5f, FightBox);
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected, typeof(ObserverCam))]
        private static void DrawZoom(ObserverCam cam, GizmoType gizmoType)
        {
            // A manual zoom replaces the locked zoom, so these rects would show a target not in use.
            if (!ShowsLocked(cam, "zoom") || cam.ManualZoom.HasValue) return;

            var feel = cam.Feel;
            var shape = new Vector2(cam.Cam.aspect, 1f);
            Rect(GamePlane.WorldPointToPlane(cam.transform.position), shape * cam.Cam.orthographicSize, LiveView);

            var p = GamePlane.WorldPointToPlane(cam.Subject.position) + cam.VelocityLead + cam.CursorLead + cam.FightShift;
            var floor = feel.zoom;
            var speedCap = feel.zoom + feel.maxWiden;
            var fightCap = feel.zoom + feel.fightMaxWiden;
            var fightZoom = cam.FightZoom(p);
            var speed = Mathf.Clamp(cam.SpeedZoom, floor, speedCap);
            var fight = Mathf.Min(fightZoom, fightCap);
            var scale = shape * cam.ZoomScale;
            Rect(p, scale * floor, ZoomFloor);
            Rect(p, scale * speedCap, cam.SpeedZoom > speedCap ? SpeedCapClipping : SpeedCap);
            if (cam.Engaged.Count > 0)
                Rect(p, scale * fightCap, fightZoom > fightCap ? FightCapClipping : FightCap);
            Rect(p, scale * Mathf.Max(speed, fight), speed >= fight ? SpeedTarget : FightTarget);
        }

        private static bool ShowsLocked(ObserverCam cam, string key) =>
            GizmoView.IsOn(typeof(ObserverCam), key) && GizmoView.InScope(cam) && Application.isPlaying
            && cam.LockCameraToSubject && cam.Subject && !cam.ManualCenter.HasValue;

        private static void Rect(Vector2 center, Vector2 half, Color color)
        {
            Gizmos.color = color;
            var bl = center - half;
            var tr = center + half;
            var br = new Vector2(tr.x, bl.y);
            var tl = new Vector2(bl.x, tr.y);
            Line(bl, br);
            Line(br, tr);
            Line(tr, tl);
            Line(tl, bl);
        }

        private static void Ellipse(Vector2 center, Vector2 radii, Color color)
        {
            Gizmos.color = color;
            var previous = center + new Vector2(radii.x, 0f);
            for (var i = 1; i <= EllipseSegments; i++)
            {
                var angle = i * 2f * Mathf.PI / EllipseSegments;
                var next = center + new Vector2(Mathf.Cos(angle) * radii.x, Mathf.Sin(angle) * radii.y);
                Line(previous, next);
                previous = next;
            }
        }

        private static void Ring(Vector2 center, float radius, Color color) =>
            Ellipse(center, new Vector2(radius, radius), color);

        private static void Cross(Vector2 center, float size, Color color)
        {
            Gizmos.color = color;
            var arm = size * 0.5f;
            Line(center - new Vector2(arm, 0f), center + new Vector2(arm, 0f));
            Line(center - new Vector2(0f, arm), center + new Vector2(0f, arm));
        }

        private static void Line(Vector2 a, Vector2 b) =>
            Gizmos.DrawLine(GamePlane.PlanePointToWorld(a), GamePlane.PlanePointToWorld(b));
    }
}
