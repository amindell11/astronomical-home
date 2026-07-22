using AI.Scanning;
using Movement;
using Movement.MPC;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Tests.EditMode
{
    /// <summary>Mechanism proof that representing an elongated rock as its tighter baked lobe spheres frees the space beside the rod that a single fat circle blocked (fat-axis clearance unchanged). Also pins ConvertObstacles' expansion, kill-switch, and atomic-admit semantics.</summary>
    [Category("MPC")]
    public class MultiSphereObstacleEditModeTests
    {
        // Rod geometry: two lobes on the x-axis at (±d,0), radius r; a gap opens along y that the single covering circle (center 0, radius d+r) still fills.
        private const float D = 1.5f;
        private const float R = 1.0f;
        private const float SingleR = D + R; // 2.5 — mean-vertex-style circle spanning both lobes
        private const float Hull = 0.1f;     // small ship hull radius for the collision test

        private static NativeArray<ObstacleData> Circles(params ObstacleData[] o) =>
            new NativeArray<ObstacleData>(o, Allocator.Temp);

        private static ObstacleData C(float x, float y, float radius) => new ObstacleData
        {
            position = new float2(x, y),
            radius = radius,
            weight = 1f
        };

        [Test]
        public void TwoLobes_FreeTheNotch_ThatSingleCircleBlocks()
        {
            using var lobes = Circles(C(-D, 0f, R), C(D, 0f, R));
            using var single = Circles(C(0f, 0f, SingleR));

            // Notch (0,1.4): sqrt(1.5²+1.4²)=2.05 > R+Hull=1.1 clears both lobes, but 1.4 < SingleR+Hull=2.6 so the fat circle blocks it.
            var notch = new float2(0f, 1.4f);
            Assert.IsFalse(Cost.Collides(notch, lobes, lobes.Length, Hull),
                "Notch point must be collision-free under the two lobes");
            Assert.IsTrue(Cost.Collides(notch, single, single.Length, Hull),
                "Notch point must be inside the single covering circle");
        }

        [Test]
        public void FatAxisClearance_Unchanged_BetweenLobesAndSingle()
        {
            using var lobes = Circles(C(-D, 0f, R), C(D, 0f, R));
            using var single = Circles(C(0f, 0f, SingleR));

            // Well inside the rod tip: both representations block.
            var inside = new float2(2.0f, 0f);
            Assert.IsTrue(Cost.Collides(inside, lobes, lobes.Length, Hull));
            Assert.IsTrue(Cost.Collides(inside, single, single.Length, Hull));

            // Well beyond the rod tip: both representations are clear.
            var outside = new float2(3.0f, 0f);
            Assert.IsFalse(Cost.Collides(outside, lobes, lobes.Length, Hull));
            Assert.IsFalse(Cost.Collides(outside, single, single.Length, Hull));
        }

        private static Dynamics MakeDynamics() => new Dynamics(
            mass: 1f, forwardAcc: 5f, reverseAcc: 5f, maxStrafeAcc: 5f, minStrafeAcc: 5f,
            maxSpeed: 10f, maxYawRate: 180f, yawTorque: 1f, angularDrag: 0.1f,
            linearDrag: 0.1f, yawInertia: 1f, maxBankAngleRad: 0.5f);

        // Builds a K=2 DetectedObstacle with plane lobes at (±d,0); the primary circle is irrelevant to these lobe-row/count assertions, so its world position projects to the origin.
        private static DetectedObstacle Rod(int lobeCount = 2)
        {
            var l0 = new DetectedObstacle.PlaneCircle(new UnityEngine.Vector2(-D, 0f), R);
            var l1 = new DetectedObstacle.PlaneCircle(new UnityEngine.Vector2(D, 0f), R);
            var l2 = new DetectedObstacle.PlaneCircle(new UnityEngine.Vector2(0f, D), R);
            return new DetectedObstacle(UnityEngine.Vector3.zero, SingleR, null, l0, l1, l2, lobeCount);
        }

        // Runs one solve so ConvertObstacles fires, then returns the resulting obstacle count.
        private static int RunConvert(DetectedObstacle[] buffer, int count, bool multiSphere)
        {
            var settings = UnityEngine.ScriptableObject.CreateInstance<MpcSettings>();
            settings.samples = 1;
            settings.horizonSeconds = 0.2f;
            settings.rolloutDt = 0.1f;
            var dynamics = MakeDynamics();
            var cfg = settings.ToConfig();
            cfg.ApplyDynamics(in dynamics);

            var seq = new Control[settings.Horizon];
            var solver = new SolverBuffers(0u);
            try
            {
                var scan = new ObstacleScan(buffer, count);
                var state = new State { pos = float2.zero };
                solver.Solve(state, seq, cfg, dynamics,
                    scan, true, multiSphere,
                    new float2(5f, 0f),
                    float2.zero, float2.zero, float.NaN, 0f, default, 0f,
                    1, 0f, 2, default);
                return solver.ObstacleCount;
            }
            finally
            {
                solver.Dispose();
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void ConvertObstacles_K2_Expands_To2Rows_WhenMultiSphere()
        {
            var buffer = new[] { Rod() };
            Assert.AreEqual(2, RunConvert(buffer, 1, multiSphere: true),
                "A K=2 rock must expand to 2 obstacle rows when multi-sphere is on");
        }

        [Test]
        public void ConvertObstacles_K2_Collapses_To1Row_WhenKillSwitchOff()
        {
            var buffer = new[] { Rod() };
            Assert.AreEqual(1, RunConvert(buffer, 1, multiSphere: false),
                "Kill switch off must write one single-circle row per rock");
        }

        [Test]
        public void ConvertObstacles_AtomicAdmit_LastK2RockAllOrNothing()
        {
            // 48 K=2 rocks fill the 96-row buffer exactly; a 49th (98 > 96) must be rejected whole — count stays 96, never 97.
            var buffer = new DetectedObstacle[49];
            for (var i = 0; i < buffer.Length; i++) buffer[i] = Rod();
            var count = RunConvert(buffer, 49, multiSphere: true);
            Assert.AreEqual(96, count, "Overflowing rock must not be admitted at all");
            Assert.AreEqual(0, count % 2, "No partial rock: count stays a multiple of the lobe count");
        }

        [Test]
        public void ConvertObstacles_AtomicAdmit_K3RockRejectedWhole_EvenWhenSomeLobesFit()
        {
            // 47 K=2 rocks = 94 rows, then a K=3 rock (94+3=97 > 96) is dropped whole even though 2 of its 3 lobes would fit — admission is atomic.
            var buffer = new DetectedObstacle[48];
            for (var i = 0; i < 47; i++) buffer[i] = Rod();
            buffer[47] = Rod(lobeCount: 3);
            Assert.AreEqual(94, RunConvert(buffer, 48, multiSphere: true),
                "K=3 rock must be rejected wholesale, not partially, at the buffer boundary");
        }
    }
}
