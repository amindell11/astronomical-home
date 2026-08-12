using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Capture
{
    public static class CaptureFraming
    {
        private const float CameraHeight = 60f;

        public static void Apply(Camera camera, CaptureConfig config, IReadOnlyList<Vector2> subjects)
        {
            if (!camera) throw new ArgumentNullException(nameof(camera));
            ValidateSubjects(subjects);

            var min = subjects[0];
            var max = subjects[0];
            for (var i = 1; i < subjects.Count; i++)
            {
                min = Vector2.Min(min, subjects[i]);
                max = Vector2.Max(max, subjects[i]);
            }

            var center = 0.5f * (min + max);
            var xExtent = 0.5f * (max.x - min.x);
            var yExtent = 0.5f * (max.y - min.y);
            var halfHeight = Mathf.Max(
                yExtent + config.padding,
                (xExtent + config.padding) * config.height / config.width,
                config.minHalfHeight);

            var normal = GamePlane.Rotation * Vector3.forward;
            camera.transform.position = GamePlane.PlanePointToWorld(center) + normal * CameraHeight;
            camera.transform.rotation = Quaternion.LookRotation(-normal, GamePlane.Rotation * Vector3.up);
            camera.orthographicSize = halfHeight;
        }

        private static void ValidateSubjects(IReadOnlyList<Vector2> subjects)
        {
            if (subjects == null || subjects.Count == 0)
                throw new ArgumentException("[Capture] Step needs at least one subject to frame");
            for (var i = 0; i < subjects.Count; i++)
            {
                var subject = subjects[i];
                if (!float.IsFinite(subject.x) || !float.IsFinite(subject.y))
                    throw new ArgumentException(
                        $"[Capture] subject {i} is not finite ({subject}) — was its source destroyed?");
            }
        }
    }
}
