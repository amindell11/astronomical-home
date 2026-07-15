using AI;
using Combat;
using Combat.Projectile;
using Combat.Weapons;
using Game;
using Ships;
using Ships.Command;
using UnityEngine;
using UnityEditor;

namespace Game.RLHarness
{
    /// <summary>Diagnostic overlay for recorded episodes, drawn with LineRenderers so it survives offscreen camera renders where editor Gizmos never appear. Per ship: velocity vector, intercept-aim line (green = primary weapon in envelope), laser-range ring; plus ship-to-ship LOS (red = blocked) and bright trails for live projectiles.</summary>
    public sealed class RecorderOverlay : System.IDisposable
    {
        private const float LineWidth = 0.28f;
        private const float VelocitySecondsShown = 0.6f;
        private const int CircleSegments = 48;
        private const int ProjectilePool = 24;
        private const float ProjectileTrail = 2.5f;
        private const float LiftAbovePlane = 3f;

        private static readonly Color RangerColor = new(1f, 0.55f, 0.15f);
        private static readonly Color BaselineColor = new(0.2f, 0.85f, 1f);
        private static readonly Color EnvelopeOpen = new(0.25f, 1f, 0.3f);
        private static readonly Color EnvelopeClosed = new(0.55f, 0.55f, 0.55f);
        private static readonly Color LosClear = new(1f, 1f, 1f, 0.35f);
        private static readonly Color LosBlocked = new(1f, 0.25f, 0.2f);
        private static readonly Color BoltColor = new(1f, 0.95f, 0.2f);

        private readonly Transform root;
        private readonly Material lineMaterial;
        private readonly LineRenderer velocityA, velocityB, aimA, aimB, losLine, ringA, ringB;
        private readonly LineRenderer[] bolts;
        private float ringRadiusA = -1f, ringRadiusB = -1f;

        public RecorderOverlay(Transform parent)
        {
            root = new GameObject("[RecorderOverlay]").transform;
            root.SetParent(parent, false);
            lineMaterial = new Material(FindLineShader());

            velocityA = MakeLine("velA", RangerColor, 2);
            velocityB = MakeLine("velB", BaselineColor, 2);
            aimA = MakeLine("aimA", EnvelopeClosed, 2);
            aimB = MakeLine("aimB", EnvelopeClosed, 2);
            losLine = MakeLine("los", LosClear, 2);
            ringA = MakeLine("ringA", RangerColor * 0.55f, CircleSegments + 1);
            ringB = MakeLine("ringB", BaselineColor * 0.55f, CircleSegments + 1);
            ringA.loop = ringB.loop = true;

            bolts = new LineRenderer[ProjectilePool];
            for (var i = 0; i < bolts.Length; i++)
            {
                bolts[i] = MakeLine($"bolt{i}", BoltColor, 2);
                bolts[i].widthMultiplier = LineWidth * 1.6f;
            }
        }

        private static Shader FindLineShader()
        {
            var shader = Shader.Find("Hidden/Internal-Colored");
            if (!shader) shader = Shader.Find("Sprites/Default");
            if (!shader) shader = Shader.Find("Universal Render Pipeline/Unlit");
            return shader;
        }

        private LineRenderer MakeLine(string name, Color color, int positions)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = lineMaterial;
            line.widthMultiplier = LineWidth;
            line.positionCount = positions;
            line.startColor = line.endColor = color;
            line.enabled = false;
            return line;
        }

        public void Sync(Ship ranger, Ship baseline)
        {
            SyncShip(ranger, baseline, velocityA, aimA, ringA, ref ringRadiusA);
            SyncShip(baseline, ranger, velocityB, aimB, ringB, ref ringRadiusB);
            SyncLos(ranger, baseline);
            SyncProjectiles();
        }

        private static Vector3 Lift(Vector3 world) =>
            world + GamePlane.Rotation * Vector3.forward * LiftAbovePlane;

        private void SyncShip(Ship ship, Ship enemy, LineRenderer velocity, LineRenderer aim,
            LineRenderer ring, ref float ringRadius)
        {
            if (!ship || !enemy) { velocity.enabled = aim.enabled = ring.enabled = false; return; }

            var k = ship.Kinematics;
            var pos = Lift(GamePlane.PlanePointToWorld(k.pos));

            velocity.enabled = true;
            velocity.SetPosition(0, pos);
            velocity.SetPosition(1, Lift(GamePlane.PlanePointToWorld(k.pos + k.vel * VelocitySecondsShown)));

            var context = ship.Weapons ? ship.Weapons.Context : null;
            var sight = context?.Sight(WeaponSlot.Primary);
            if (sight == null) { aim.enabled = ring.enabled = false; return; }

            var ek = enemy.Kinematics;
            var aimPlane = Gunner.AimPoint(in k, ek.pos, ek.vel, context.ProjectileSpeed(WeaponSlot.Primary));
            var aimWorld = GamePlane.PlanePointToWorld(aimPlane);
            aim.enabled = true;
            aim.startColor = aim.endColor = sight.InEnvelope(aimWorld) ? EnvelopeOpen : EnvelopeClosed;
            aim.SetPosition(0, pos);
            aim.SetPosition(1, Lift(aimWorld));

            if (ringRadius < 0f)
                ringRadius = ReadFireDistance(ship);
            if (ringRadius > 0f)
            {
                ring.enabled = true;
                for (var i = 0; i <= CircleSegments; i++)
                {
                    var angle = i * 2f * Mathf.PI / CircleSegments;
                    var rim = k.pos + ringRadius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    ring.SetPosition(i % (CircleSegments + 1), Lift(GamePlane.PlanePointToWorld(rim)));
                }
            }
        }

        // SerializedObject read because weapon envelope fields are inspector-only; overlay lives in an editor assembly.
        private static float ReadFireDistance(Ship ship)
        {
            var lasers = ship.GetComponentInChildren<Lasers>();
            if (!lasers) return -1f;
            var property = new SerializedObject(lasers).FindProperty("fireDistance");
            return property != null ? property.floatValue : -1f;
        }

        private void SyncLos(Ship a, Ship b)
        {
            if (!a || !b) { losLine.enabled = false; return; }
            var from = GamePlane.PlanePointToWorld(a.Kinematics.pos);
            var to = GamePlane.PlanePointToWorld(b.Kinematics.pos);
            var clear = TargetingMath.IsLineClear(from, to);
            losLine.enabled = true;
            losLine.startColor = losLine.endColor = clear ? LosClear : LosBlocked;
            losLine.SetPosition(0, Lift(from));
            losLine.SetPosition(1, Lift(to));
        }

        private void SyncProjectiles()
        {
            var live = Object.FindObjectsByType<ProjectileBase>(FindObjectsSortMode.None);
            for (var i = 0; i < bolts.Length; i++)
            {
                if (i >= live.Length) { bolts[i].enabled = false; continue; }
                var projectile = live[i];
                var rb = projectile.GetComponentInParent<Rigidbody>();
                var dir = rb && rb.linearVelocity.sqrMagnitude > 1e-4f
                    ? rb.linearVelocity.normalized
                    : projectile.transform.up;
                var head = Lift(projectile.transform.position);
                bolts[i].enabled = true;
                bolts[i].SetPosition(0, head - dir * ProjectileTrail);
                bolts[i].SetPosition(1, head);
            }
        }

        public void Dispose()
        {
            if (lineMaterial) Object.DestroyImmediate(lineMaterial);
            if (root) Object.DestroyImmediate(root.gameObject);
        }
    }
}
