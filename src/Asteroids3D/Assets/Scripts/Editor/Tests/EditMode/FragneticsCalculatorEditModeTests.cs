using System.Collections;
using System.Linq;
using Asteroids.Fragnetics;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class FragneticsCalculatorEditModeTests
    {
        private AsteroidFragSettings asteroidFragSettings;

        [SetUp]
        public void SetUp()
        {
            asteroidFragSettings = ScriptableObject.CreateInstance<AsteroidFragSettings>();
            // Deterministic settings for tests
            asteroidFragSettings.minMass = 5f;
            asteroidFragSettings.minFragments = 2;
            asteroidFragSettings.maxFragments = 6;
            asteroidFragSettings.highCountBias = 0.9f;
            asteroidFragSettings.baseSeparationSpeed = 5f;
            asteroidFragSettings.spinVariation = 0f; // disable jitter for strict assertions
            asteroidFragSettings.momentumCoupling = 1f; // full coupling; conservation is always enforced in code
            asteroidFragSettings.outwardBias = 0.7f;
            asteroidFragSettings.bulletBias = 1.0f;
            asteroidFragSettings.randomBias = 0.0f; // remove randomness from directions for predictability
            asteroidFragSettings.massLossFactor = 0.8f;

            Random.InitState(123456); // make Random deterministic per test run
        }

        [TearDown]
        public void TearDown()
        {
            if (asteroidFragSettings)
            {
                Object.DestroyImmediate(asteroidFragSettings);
            }
        }

        [Test]
        public void GenerateFragments_RespectsMassConservationAndBounds()
        {
            var calc = new Calculator(asteroidFragSettings);
            var astData = new AsteroidData(
                mass: 100f,
                rotation: Quaternion.identity,
                angularVelocity: Vector3.zero,
                velocity: Vector3.zero,
                position: Vector3.zero,
                inertiaTensor: new Vector3(2f, 2f, 2f));

            var frags = calc.GenerateFragments(astData);

            Assert.That(frags.Length, Is.GreaterThanOrEqualTo(asteroidFragSettings.minFragments));
            Assert.That(frags.Length, Is.LessThanOrEqualTo(asteroidFragSettings.maxFragments));

            float sumMass = frags.Sum(f => f.Mass);
            float expectedTotal = astData.Mass * asteroidFragSettings.massLossFactor;

            Debug.Log($"[GenerateFragments] count={frags.Length}, massSum={sumMass:F3}, expected={expectedTotal:F3}, minMass={asteroidFragSettings.minMass}");

            Assert.That(sumMass, Is.EqualTo(expectedTotal).Within(1e-3f));
            Assert.That(frags.Min(f => f.Mass), Is.GreaterThanOrEqualTo(asteroidFragSettings.minMass));

            // Positions should be near parent position (offset <= ~0.5)
            foreach (var f in frags)
            {
                float d = Vector3.Distance(f.Position, astData.Position);
                Assert.That(d, Is.InRange(0f, 0.51f));
            }
        }

        [Test]
        public void CalculateInitialMomentum_ComputesLinearAndAngularCorrectly()
        {
            var calc = new Calculator(asteroidFragSettings);

            // Identity rotation to simplify; custom inertia tensor and angular velocity
            var astData = new AsteroidData(
                mass: 50f,
                rotation: Quaternion.identity,
                angularVelocity: new Vector3(0.1f, 0.2f, 0.3f),
                velocity: new Vector3(4, 0, 0),
                position: new Vector3(1, 2, 3),
                inertiaTensor: new Vector3(2f, 3f, 4f));
            var hit = new HitData(
                projectileMass: 5f,
                projectileVelocity: new Vector3(-2, 1, 0.5f),
                hitPoint: astData.Position + new Vector3(0.5f, -0.25f, 0.75f));

            var (pLin, pAng) = calc.CalculateInitialMomentum(astData, hit);

            var expectedPLin = astData.Mass * astData.Velocity + hit.Mass * hit.Velocity;
            var localW = Quaternion.Inverse(astData.Rotation) * astData.AngularVelocity;
            var localL = Vector3.Scale(astData.InertiaTensor, localW);
            var asteroidL = astData.Rotation * localL;
            var r = hit.HitPoint - astData.Position;
            var projectileL = Vector3.Cross(r, hit.Mass * hit.Velocity);
            var expectedPAng = asteroidL + projectileL;

            Debug.Log($"[InitialMomentum] pLin={pLin}, expected={expectedPLin}; pAng={pAng}, expected={expectedPAng}");

            AssertVec3Approximately(pLin, expectedPLin, 1e-5f);
            AssertVec3Approximately(pAng, expectedPAng, 1e-5f);
        }

        [Test]
        public void PlaceholderPhysics_ProducesReasonableSpeeds_NoExplosions()
        {
            var calc = new Calculator(asteroidFragSettings);
            var astData = new AsteroidData(
                mass: 40f,
                rotation: Quaternion.identity,
                angularVelocity: Vector3.zero,
                velocity: new Vector3(1, 0, 0),
                position: Vector3.zero,
                inertiaTensor: Vector3.one * 2f);
            var frags = calc.GenerateFragments(astData);

            var hit = new HitData(5f, new Vector3(3f, 0, 0), astData.Position + new Vector3(0.2f, 0.1f, 0));

            calc.CalculatePlaceholderPhysics(astData, hit, frags);

            // delta v should be on the order of baseSeparationSpeed
            float avgDelta = frags.Select(f => (f.Velocity - astData.Velocity).magnitude).Average();

            Debug.Log($"[PlaceholderPhysics] avgDelta={avgDelta:F3} (baseSeparationSpeed={asteroidFragSettings.baseSeparationSpeed})");

            Assert.That(avgDelta, Is.GreaterThan(0f));
            Assert.That(avgDelta, Is.LessThanOrEqualTo(asteroidFragSettings.baseSeparationSpeed * 2f));
        }

        [Test]
        public void CoroutinePhysics_ConservesLinearAndAngularMomentum_WhenNoLossNoSpin()
        {
            // spinVariation = 0 already set in SetUp; momentum is strictly conserved in implementation
            var calc = new Calculator(asteroidFragSettings);
            var astData = new AsteroidData(
                mass: 60f,
                rotation: Quaternion.identity,
                angularVelocity: new Vector3(0.2f, -0.1f, 0.3f),
                velocity: new Vector3(2, -1, 0.5f),
                position: new Vector3(0, 0, 0),
                inertiaTensor: new Vector3(3f, 2f, 5f));
            var hit = new HitData(8f, new Vector3(-5f, 2f, 1f), astData.Position + new Vector3(0.3f, 0.2f, -0.4f));

            var frags = calc.GenerateFragments(astData);
            var momentum = calc.CalculateInitialMomentum(astData, hit);

            var co = calc.CoCalculateFragmentPhysics(astData, hit, frags, momentum, null);
            RunToEnd(co);

            // Linear momentum sum after correction should match initial momentum
            var pFinal = SumLinearMomentum(frags);
            Debug.Log($"[CoroutinePhysics] pFinal={pFinal}, expected={momentum.linear}");
            AssertVec3Approximately(pFinal, momentum.linear, 1e-3f);

            // Angular momentum: L_orbit + I_total * omegaBase should match expected
            var center = astData.Position;
            var lOrbit = Vector3.zero;
            float iTotal = 0f;
            foreach (var f in frags)
            {
                var r = f.Position - center;
                lOrbit += Vector3.Cross(r, f.Mass * f.Velocity);
                // spherical approximation consistent with production code
                float radius = Mathf.Pow(f.Mass, 1f / 3f);
                iTotal += 0.4f * f.Mass * radius * radius;
            }

            // All spins should be equal because spinVariation == 0
            Vector3 omegaBase = frags.Length > 0 ? frags[0].Spin : Vector3.zero;
            var lSpin = iTotal * omegaBase;
            var lTotal = lOrbit + lSpin;

            Debug.Log($"[CoroutinePhysics] L_total={lTotal}, expected={momentum.angular} (iTotal={iTotal:F3}, omegaBase={omegaBase})");
            AssertVec3Approximately(lTotal, momentum.angular, 1e-2f);
        }

        [Test]
        public void SeparationSpeed_ScalesWithRelativeImpactVelocity()
        {
            // This test is diagnostic to uncover potential refactor-induced speed inflation.
            // It demonstrates that fragment dispersion speeds scale with the relative projectile speed.
            asteroidFragSettings.randomBias = 0.0f;
            asteroidFragSettings.outwardBias = 0.7f;
            asteroidFragSettings.bulletBias = 1.0f;
            asteroidFragSettings.baseSeparationSpeed = 4f;
            asteroidFragSettings.spinVariation = 0f;

            var calc = new Calculator(asteroidFragSettings);
            var astData = new AsteroidData(
                mass: 80f,
                rotation: Quaternion.identity,
                angularVelocity: Vector3.zero,
                velocity: Vector3.zero,
                position: Vector3.zero,
                inertiaTensor: Vector3.one * 2f);
            var frags1 = calc.GenerateFragments(astData);
            var frags2 = calc.GenerateFragments(astData); // same count distribution statistically with fixed seed

            var hitSlow = new HitData(5f, new Vector3(2f, 0, 0), astData.Position + new Vector3(0.1f, 0, 0));
            var hitFast = new HitData(5f, new Vector3(20f, 0, 0), astData.Position + new Vector3(0.1f, 0, 0));

            var momSlow = calc.CalculateInitialMomentum(astData, hitSlow);
            var momFast = calc.CalculateInitialMomentum(astData, hitFast);

            var co1 = calc.CoCalculateFragmentPhysics(astData, hitSlow, frags1, momSlow, null);
            RunToEnd(co1);
            var co2 = calc.CoCalculateFragmentPhysics(astData, hitFast, frags2, momFast, null);
            RunToEnd(co2);

            // Measure dispersion speeds in the center-of-mass frame to remove uniform momentum correction
            float totalMass1 = frags1.Sum(f => f.Mass);
            float totalMass2 = frags2.Sum(f => f.Mass);
            Vector3 vCom1 = SumLinearMomentum(frags1) / Mathf.Max(1e-4f, totalMass1);
            Vector3 vCom2 = SumLinearMomentum(frags2) / Mathf.Max(1e-4f, totalMass2);
            float avgSpeedSlow = frags1.Select(f => (f.Velocity - vCom1).magnitude).Average();
            float avgSpeedFast = frags2.Select(f => (f.Velocity - vCom2).magnitude).Average();
            float ratio = avgSpeedFast / Mathf.Max(1e-4f, avgSpeedSlow);
            float expectedRatio = hitFast.Velocity.magnitude / Mathf.Max(1e-4f, hitSlow.Velocity.magnitude);

            Debug.Log($"[SpeedScaling] avgSlow={avgSpeedSlow:F3}, avgFast={avgSpeedFast:F3}, ratio={ratio:F2}, expectedRatio≈{expectedRatio:F2}. If this ratio is very high in-game, fragments may be too fast.");

            // Expect approx proportional scaling to relative velocity
            Assert.That(ratio, Is.EqualTo(expectedRatio).Within(0.35f * expectedRatio));
        }

        // Helpers
        // (no GameObject helpers needed)

        private static void RunToEnd(IEnumerator co)
        {
            while (co.MoveNext()) { }
        }

        private static Vector3 SumLinearMomentum(Frag[] frags)
        {
            Vector3 p = Vector3.zero;
            foreach (var f in frags)
            {
                p += f.Mass * f.Velocity;
            }
            return p;
        }

        private static void AssertVec3Approximately(Vector3 a, Vector3 b, float eps)
        {
            Assert.That(a.x, Is.EqualTo(b.x).Within(eps));
            Assert.That(a.y, Is.EqualTo(b.y).Within(eps));
            Assert.That(a.z, Is.EqualTo(b.z).Within(eps));
        }
    }
}


