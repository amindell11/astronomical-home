#if UNITY_EDITOR
using AI.Debug;
using Game;
using Ships.Command;
using UnityEngine;

namespace AI
{
    public partial class Gunner
    {
        private AICommander cachedCommander;
        private AIDebugSettings CachedSettings
        {
            get
            {
                if (!cachedCommander)
                    cachedCommander = GetComponent<AICommander>();
                return cachedCommander ? cachedCommander.DebugSettings : null;
            }
        }

        private void OnDrawGizmos() => DrawGizmosImpl(false);
        private void OnDrawGizmosSelected() => DrawGizmosImpl(true);

        void DrawGizmosImpl(bool isSelected)
        {
            var settings = CachedSettings;
            if (settings == null || !settings.ShouldDraw(isSelected)) return;
            if (!settings.IsActive(AIDebugChannel.Targeting)) return;

            DrawTargetingGizmos();
            DrawLineOfSightGizmos();
        }

        void DrawTargetingGizmos()
        {
            if (!HasTarget) return;

            var pos = transform.position;
            var targetPos = Target;
            Vector3 forward = pose != null ? (Vector2)pose().Forward : Vector2.up;
            forward = new Vector3(forward.x, forward.y, 0f);

            // Line to target
            Gizmos.color = Color.gray;
            Gizmos.DrawLine(pos, targetPos);

            // Target marker
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(targetPos, Vector3.one * 2f);

            // Angle indicator
            Gizmos.color = Color.red;
            var dirToTarget = (targetPos - pos).normalized;
            Gizmos.DrawRay(pos, dirToTarget * 5f);
        }

        void DrawLineOfSightGizmos()
        {
            var sight = weapons?.Sight(WeaponSlot.Primary);
            if (!HasTarget || sight == null) return;

            var firePos = sight.FirePoint;
            var targetPos = Target;

            var hasLOS = Combat.TargetingMath.IsLineClear(firePos, targetPos);
            Gizmos.color = hasLOS ? Color.green : Color.red;
            Gizmos.DrawLine(firePos, targetPos);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(firePos, 0.5f);
        }

        void DrawAngleCone(Vector3 origin, Vector3 forward, float angleInDegrees, float range)
        {
            var halfAngle = angleInDegrees * 0.5f;
            var forward3D = forward.normalized;
            var leftRotation = Quaternion.AngleAxis(-halfAngle, Vector3.forward);
            var rightRotation = Quaternion.AngleAxis(halfAngle, Vector3.forward);
            var leftDirection = leftRotation * forward3D;
            var rightDirection = rightRotation * forward3D;

            Gizmos.DrawRay(origin, leftDirection * range);
            Gizmos.DrawRay(origin, rightDirection * range);

            var segments = Mathf.Max(3, Mathf.RoundToInt(angleInDegrees / 5f));
            var prevPoint = origin + leftDirection * range;

            for (var i = 1; i <= segments; i++)
            {
                var t = (float)i / segments;
                var currentAngle = Mathf.Lerp(-halfAngle, halfAngle, t);
                var rotation = Quaternion.AngleAxis(currentAngle, Vector3.forward);
                var direction = rotation * forward3D;
                var point = origin + direction * range;
                Gizmos.DrawLine(prevPoint, point);
                prevPoint = point;
            }
        }
    }
}
#endif
