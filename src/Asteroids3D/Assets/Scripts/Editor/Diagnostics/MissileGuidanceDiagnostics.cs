using System.Collections.Generic;
using System.Reflection;
using Combat;
using Combat.Projectile;
using Game;
using UnityEditor;
using UnityEngine;
using Utils;

namespace Diagnostics
{
    public class MissileGuidanceDiagnostics : MonoBehaviour
    {
        [Header("Scenario Parameters")]
        [SerializeField] private Vector2 shooterVelocity;
        [SerializeField] private float targetDistance = 20f;
        [SerializeField] private float targetAngle;
        [SerializeField] private Vector2 targetVelocity;

        [Header("Missile Overrides (0 = use prefab default)")]
        [SerializeField] private float homingSpeed;
        [SerializeField] private float homingTurnRate;
        [SerializeField] private float initialSpeed;
        [SerializeField] private float acceleration;

        [Header("Time")]
        [SerializeField, Range(0.01f, 1f)] private float timeScale = 1f;

        [Header("Visualization")]
        [SerializeField] private bool showFlightPath = true;
        [SerializeField] private bool showVelocityHeadingDelta = true;
        [SerializeField] private bool showShooterOrigin = true;
        [SerializeField] private bool showTargetInfo = true;
        [SerializeField] private bool logTelemetry = true;
        [SerializeField] private int logInterval = 10;

        [Header("Runtime State (Read-Only)")]
        [SerializeField] private float currentDistance;
        [SerializeField] private float closingSpeed;
        [SerializeField] private float headingError;
        [SerializeField] private float turnRateUtilization;

        private Missile activeMissile;
        private GameObject targetMarker;
        private StubShooter stubShooter;
        private Vector3 shooterOrigin;
        private Vector3 shooterVelocityWorld;
        private float prevTimeScale;
        private readonly List<Vector3> flightPath = new();
        private float prevDistance;
        private int fixedFrameCount;

        private class StubShooter : MonoBehaviour, IShooter
        {
            public Vector3 Velocity { get; set; }
        }

        [ContextMenu("Spawn Missile Test")]
        public void SpawnMissileTest()
        {
            ClearActiveTest();

            if (!EnsureGamePlane()) return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/MissileProjectile.prefab");
            if (!prefab)
            {
                Debug.LogError("[MissileGuidanceDiagnostics] MissileProjectile.prefab not found at expected path.");
                return;
            }

            var missileGo = Instantiate(prefab, transform.position, transform.rotation);
            activeMissile = missileGo.GetComponent<Missile>();
            if (!activeMissile)
            {
                Debug.LogError("[MissileGuidanceDiagnostics] Prefab does not contain a Missile component.");
                DestroyImmediate(missileGo);
                return;
            }

            // Apply tuning overrides via reflection
            ApplyOverride(activeMissile, "homingSpeed", homingSpeed);
            ApplyOverride(activeMissile, "homingTurnRate", homingTurnRate);
            ApplyOverride(activeMissile, "initialSpeed", initialSpeed);
            ApplyOverride(activeMissile, "acceleration", acceleration);
            SetFieldHierarchy(activeMissile, "maxDistance", 500f);

            // Create target marker
            var angleRad = targetAngle * Mathf.Deg2Rad;
            var planeDir = new Vector2(Mathf.Sin(angleRad), Mathf.Cos(angleRad));
            var missileOriginPlane = GamePlane.WorldPointToPlane(transform.position);
            var targetPlane = missileOriginPlane + planeDir * targetDistance;

            targetMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            targetMarker.name = "DiagTarget";
            targetMarker.transform.position = GamePlane.PlanePointToWorld(targetPlane);
            targetMarker.transform.localScale = Vector3.one * 0.5f;
            var col = targetMarker.GetComponent<Collider>();
            if (col) col.enabled = false;
            var renderer = targetMarker.GetComponent<Renderer>();
            if (renderer) renderer.material.color = Color.red;

            if (targetVelocity.sqrMagnitude > 0.01f)
            {
                var targetRb = targetMarker.AddComponent<Rigidbody>();
                targetRb.useGravity = false;
                targetRb.linearVelocity = GamePlane.PlaneDirToWorld(targetVelocity);
            }

            // Create stub shooter
            var shooterGo = new GameObject("DiagStubShooter");
            stubShooter = shooterGo.AddComponent<StubShooter>();
            if (shooterVelocity.sqrMagnitude > 0.01f)
                stubShooter.Velocity = GamePlane.PlaneDirToWorld(shooterVelocity);

            shooterOrigin = transform.position;
            shooterVelocityWorld = stubShooter.Velocity;

            activeMissile.SetTarget(targetMarker.transform);
            activeMissile.Initialize(stubShooter);
            var launchDir = GamePlane.PlaneDirToWorld(Vector2.up);
            activeMissile.Launch(launchDir);

            // Detect immediate deactivation (returned to pool)
            activeMissile.ReturnedToPool += () =>
                Debug.LogWarning("[MissileGuidanceDiagnostics] Missile was returned to pool!");

            var rb = activeMissile.GetComponent<Rigidbody>();
            Debug.Log($"[MissileGuidanceDiagnostics] Post-launch state:\n" +
                      $"  active={activeMissile.gameObject.activeInHierarchy}\n" +
                      $"  pos={activeMissile.transform.position}\n" +
                      $"  vel={rb?.linearVelocity}\n" +
                      $"  target pos={targetMarker.transform.position}\n" +
                      $"  GamePlane normal={GamePlane.Normal} forward={GamePlane.Forward} origin={GamePlane.Origin}");

            flightPath.Clear();
            prevDistance = Vector3.Distance(activeMissile.transform.position, targetMarker.transform.position);
            fixedFrameCount = 0;

            prevTimeScale = Time.timeScale;
            Time.timeScale = timeScale;

            Debug.Log($"[MissileGuidanceDiagnostics] Test spawned — target at {targetDistance}u, angle {targetAngle}°, shooter vel {shooterVelocity}, timeScale {timeScale}");
        }

        [ContextMenu("Clear Active Test")]
        public void ClearActiveTest()
        {
            if (activeMissile) DestroyImmediate(activeMissile.gameObject);
            if (targetMarker) DestroyImmediate(targetMarker);
            if (stubShooter) DestroyImmediate(stubShooter.gameObject);
            activeMissile = null;
            targetMarker = null;
            stubShooter = null;
            flightPath.Clear();
            Time.timeScale = prevTimeScale > 0f ? prevTimeScale : 1f;
        }

        private void OnDisable()
        {
            Time.timeScale = prevTimeScale > 0f ? prevTimeScale : 1f;
        }

        private void FixedUpdate()
        {
            if (!activeMissile || !targetMarker) return;

            if (!Mathf.Approximately(Time.timeScale, timeScale))
                Time.timeScale = timeScale;

            // Record position
            if (flightPath.Count < 500)
                flightPath.Add(activeMissile.transform.position);

            // Compute telemetry
            var dist = Vector3.Distance(activeMissile.transform.position, targetMarker.transform.position);
            closingSpeed = (prevDistance - dist) / Time.fixedDeltaTime;
            currentDistance = dist;
            prevDistance = dist;

            var rb = activeMissile.GetComponent<Rigidbody>();
            if (rb)
            {
                var toTarget = (targetMarker.transform.position - activeMissile.transform.position).normalized;
                var velDir = rb.linearVelocity.normalized;
                headingError = rb.linearVelocity.sqrMagnitude > 0.01f
                    ? Vector3.Angle(velDir, toTarget)
                    : 0f;
            }

            fixedFrameCount++;

            if (logTelemetry && logInterval > 0 && fixedFrameCount % logInterval == 0)
            {
                Debug.Log($"[MissileGuidance] frame={fixedFrameCount} dist={currentDistance:F2} " +
                          $"closing={closingSpeed:F2} heading_err={headingError:F1}° " +
                          $"turn_util={turnRateUtilization:P0}");
            }
        }

        private void OnDrawGizmos()
        {
            if (!activeMissile || !targetMarker || !GamePlane.IsConfigured) return;

            // Flight path trail
            if (showFlightPath && flightPath.Count > 1)
            {
                for (var i = 1; i < flightPath.Count; i++)
                {
                    var t = i / (float)flightPath.Count;
                    Gizmos.color = Color.Lerp(Color.blue, Color.red, t);
                    Gizmos.DrawLine(flightPath[i - 1], flightPath[i]);
                }
            }

            // Velocity and heading arrows
            if (showVelocityHeadingDelta)
            {
                var rb = activeMissile.GetComponent<Rigidbody>();
                if (rb)
                {
                    SuperGizmos.DrawArrow(activeMissile.transform.position, rb.linearVelocity.normalized,
                        color: Color.cyan, scale: 2f);
                    SuperGizmos.DrawArrow(activeMissile.transform.position, activeMissile.transform.up,
                        color: Color.green, scale: 2f);
                }
            }

            // Target marker, line, and info
            if (showTargetInfo)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(targetMarker.transform.position, 0.6f);
                Gizmos.DrawWireSphere(targetMarker.transform.position, 0.3f);
                Gizmos.DrawLine(activeMissile.transform.position, targetMarker.transform.position);

                var targetRb = targetMarker.GetComponent<Rigidbody>();
                if (targetRb && targetRb.linearVelocity.sqrMagnitude > 0.01f)
                {
                    SuperGizmos.DrawArrow(targetMarker.transform.position, targetRb.linearVelocity.normalized,
                        color: Color.red, scale: 2f, headScale: 0.6f);
                    Handles.Label(targetMarker.transform.position + Vector3.up * 0.8f,
                        $"target vel: {targetRb.linearVelocity.magnitude:F1}");
                }

                var midpoint = (activeMissile.transform.position + targetMarker.transform.position) * 0.5f;
                Handles.Label(midpoint, $"dist: {currentDistance:F1}  closing: {closingSpeed:F1}");
            }

            // Shooter origin marker
            if (showShooterOrigin)
            {
                Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
                Gizmos.DrawWireCube(shooterOrigin, Vector3.one * 0.4f);
                Handles.Label(shooterOrigin + Vector3.up * 0.5f, "shooter");

                if (shooterVelocityWorld.sqrMagnitude > 0.01f)
                {
                    SuperGizmos.DrawArrow(shooterOrigin, shooterVelocityWorld.normalized,
                        color: new Color(0.2f, 0.8f, 1f), scale: 2f, headScale: 0.6f);
                    Handles.Label(shooterOrigin + Vector3.up * 0.9f,
                        $"shooter vel: {shooterVelocityWorld.magnitude:F1}");
                }
            }

            // Turn rate arc
            if (showVelocityHeadingDelta)
            {
                var toTarget = (targetMarker.transform.position - activeMissile.transform.position).normalized;
                var angle = Vector3.Angle(activeMissile.transform.up, toTarget);
                var maxAngle = 90f;
                turnRateUtilization = Mathf.Clamp01(angle / maxAngle);
                var arcColor = Color.Lerp(Color.green, Color.red, turnRateUtilization);
                Gizmos.color = arcColor;
                SuperGizmos.DrawWireArc(activeMissile.transform.position, GamePlane.Normal,
                    activeMissile.transform.up, angle, 1.5f);
            }
        }

        private static bool EnsureGamePlane()
        {
            if (GamePlane.IsConfigured) return true;

            GamePlane.Configure(PlaneAxis.Y);
            Debug.Log("[MissileGuidanceDiagnostics] GamePlane was not configured — " +
                      "auto-configured with PlaneAxis.Y.");
            return true;
        }

        private static void ApplyOverride(object obj, string fieldName, float value)
        {
            if (Mathf.Approximately(value, 0f)) return;
            SetFieldHierarchy(obj, fieldName, value);
        }

        private static void SetFieldHierarchy(object obj, string fieldName, object value)
        {
            var type = obj.GetType();
            while (type != null)
            {
                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    field.SetValue(obj, value);
                    return;
                }
                type = type.BaseType;
            }
            Debug.LogWarning($"[MissileGuidanceDiagnostics] Field '{fieldName}' not found on {obj.GetType().Name}");
        }
    }
}
