using System.Linq;
using Asteroids.Fragnetics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>
    /// Drives the fragment calculator with the shipped FragSettings asset and a typical rock + laser
    /// hit, so the numbers a player sees are the numbers under test: the fragment cluster keeps the
    /// parent's velocity (the lost mass leaves with its momentum share) and separates visibly faster
    /// than it drifts, whatever the hit's momentum. Arc: #575.
    /// </summary>
    [Category("Physics")]
    public class FragneticsShippedSettingsEditModeTests
    {
        private const string FragSettingsPath = "Assets/Settings/Asteroids/FragSettings.asset";

        // A mid-field rock (density 170 × massScale ~1) and the shipped Laser.prefab projectile.
        private const float RockMass = 5000f;
        private const float LaserMass = 0.01f;
        private const float LaserSpeed = 50f;

        private AsteroidFragSettings settings;
        private Calculator calc;

        [SetUp]
        public void SetUp()
        {
            settings = AssetDatabase.LoadAssetAtPath<AsteroidFragSettings>(FragSettingsPath);
            Assert.IsNotNull(settings, $"Failed to load {FragSettingsPath}");
            calc = new Calculator(settings);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void CoMovingHit_ClusterKeepsTheParentVelocity(int seed)
        {
            Random.InitState(seed);
            var rock = Rock(velocity: new Vector3(10f, 0f, 0f));
            var hit = new HitData(LaserMass, rock.Velocity, rock.Position + new Vector3(0.5f, 0f, 0f));

            var frags = Fragment(rock, hit);
            var vCom = CentreOfMassVelocity(frags);
            var ratio = vCom.magnitude / rock.Velocity.magnitude;

            TestContext.WriteLine($"seed={seed} n={frags.Length} |v_com|/|v_ast|={ratio:F4} (massLossFactor={settings.massLossFactor})");
            Assert.That(ratio, Is.EqualTo(1f).Within(1e-3f),
                "Fragments must not gain speed from the mass the split discards.");
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void LaserHitOnRestingRock_SeparatesFasterThanItDrifts(int seed)
        {
            Random.InitState(seed);
            var rock = Rock(velocity: Vector3.zero);
            var hit = new HitData(LaserMass, new Vector3(LaserSpeed, 0f, 0f), rock.Position + new Vector3(-0.5f, 0f, 0f));

            var frags = Fragment(rock, hit);
            var vCom = CentreOfMassVelocity(frags);
            var drift = (vCom - rock.Velocity).magnitude;
            var spread = frags.Max(f => (f.Velocity - vCom).magnitude);

            TestContext.WriteLine($"seed={seed} n={frags.Length} drift={drift:E3} m/s spread={spread:E3} m/s (minSeparationSpeed={settings.minSeparationSpeed})");
            Assert.That(spread, Is.GreaterThan(drift),
                "The cluster must separate faster than it drifts, or the fragments fly off together.");
            Assert.That(spread, Is.InRange(0.25f * settings.minSeparationSpeed, 2.4f * settings.minSeparationSpeed),
                "A laser's momentum is negligible, so the separation floor sets the spread.");
        }

        private static AsteroidData Rock(Vector3 velocity) => new(
            mass: RockMass,
            rotation: Quaternion.identity,
            angularVelocity: Vector3.zero,
            velocity: velocity,
            position: Vector3.zero,
            inertiaTensor: Vector3.one * 2000f);

        private Frag[] Fragment(AsteroidData rock, HitData hit)
        {
            var frags = calc.GenerateFragments(rock);
            Assert.That(frags.Length, Is.GreaterThanOrEqualTo(2));
            var momentum = calc.CalculateInitialMomentum(rock, hit);
            var co = calc.CoCalculateFragmentPhysics(rock, hit, frags, momentum, null);
            while (co.MoveNext()) { }
            return frags;
        }

        private static Vector3 CentreOfMassVelocity(Frag[] frags)
        {
            var p = Vector3.zero;
            var m = 0f;
            foreach (var f in frags)
            {
                p += f.Mass * f.Velocity;
                m += f.Mass;
            }
            return p / m;
        }
    }
}
