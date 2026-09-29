#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Ships;
using Ships.Presentation;
using Ships.Visuals.Breakup;
using Substrate.Services;
using Substrate.Services.Units;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Tests.PlayMode.Rendering.Illustrated
{
    [Category("Ships")]
    public sealed class CrimsonBreakupPlayModeTests : PlayModeWorldFixture
    {
        private GameObject host;
        private UnitService units;

        public override void TearDown()
        {
            if (units) units.Clear();
            DestroyTestObject(host);
            foreach (var debris in Object.FindObjectsByType<ShipBreakupDebris>(FindObjectsSortMode.None))
                DestroyTestObject(debris);
            foreach (var effect in Object.FindObjectsByType<PooledVFX>(FindObjectsSortMode.None))
                DestroyTestObject(effect);
            SimplePool<PooledVFX>.Clear();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Death_ReleasesStagesWithMomentum_CleansUp_AndRearmsOnReset()
        {
            using var pacing = Capture.CapturePacing.Locked();
            var ship = Spawn(true);
            yield return null;
            var hull = ship.GetComponentsInChildren<Transform>().Single(t => t.name == "Crimson");
            var expectedPosition = hull.position;
            var expectedRotation = hull.rotation;
            var expectedScale = hull.lossyScale;
            ship.Body.linearVelocity = new Vector3(2, 1, 0);
            TestDamage.Kill(ship);
            var debris = Object.FindObjectsByType<ShipBreakupDebris>(FindObjectsSortMode.None).Single();
            Assert.That(Vector3.Distance(debris.transform.position, expectedPosition), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(debris.transform.rotation, expectedRotation), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(debris.transform.localScale, expectedScale), Is.LessThan(.0001f));
            Assert.That(Explosions(), Is.EqualTo(1));
            Assert.That(ship.gameObject.activeSelf, Is.False);
            var wing = debris.transform.Find("Wing left");
            var core = debris.transform.Find("Core");
            var wingPosition = wing.localPosition;
            var corePosition = core.localPosition;
            yield return new WaitForSeconds(.15f);
            Assert.That(Vector3.Distance(wing.localPosition, wingPosition), Is.GreaterThan(.001f));
            Assert.That(core.localPosition, Is.EqualTo(corePosition));
            var displacement = debris.transform.position - expectedPosition;
            Assert.That(displacement.x, Is.GreaterThan(.1f));
            Assert.That(displacement.x / displacement.y, Is.EqualTo(2).Within(.001f));
            yield return new WaitForSeconds(.3f);
            Assert.That(Vector3.Distance(core.localPosition, corePosition), Is.GreaterThan(.001f));
            yield return new WaitForSeconds(2.2f);
            var block = new MaterialPropertyBlock();
            debris.GetComponentInChildren<Renderer>().GetPropertyBlock(block);
            Assert.That(block.GetFloat("_DebrisVisibility"), Is.InRange(.01f, .99f));
            yield return new WaitForSeconds(.6f);
            Assert.That(!debris, Is.True, "Debris must expire independently of its disabled ship.");
            var before = Explosions();
            ship.ResetShip();
            yield return null;
            Assert.That(hull.gameObject.activeInHierarchy, Is.True);
            TestDamage.Kill(ship);
            Assert.That(Object.FindObjectsByType<ShipBreakupDebris>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(Explosions(), Is.EqualTo(before + 1));
        }

        [UnityTest]
        public IEnumerator PresentationOff_DeathSpawnsNoBreakupOrExplosion()
        {
            var ship = Spawn(false);
            yield return null;
            Assert.That(ship.GetComponentInChildren<ShipVisualRig>(true).gameObject.activeInHierarchy, Is.False);
            TestDamage.Kill(ship);
            yield return null;
            Assert.That(Object.FindObjectsByType<ShipBreakupDebris>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(Explosions(), Is.Zero);
        }

        private Ship Spawn(bool visible)
        {
            host = new GameObject("Crimson breakup test");
            units = host.AddComponent<UnitService>();
            ShipServices.Compose(units, host.transform, visible);
            var prefab = AssetDatabase.LoadAssetAtPath<Ship>("Assets/Prefabs/Ships/Ship_2.prefab");
            return units.SpawnShip(prefab, null, 0, Vector3.zero, Quaternion.identity, Field);
        }

        private static int Explosions() => Object.FindObjectsByType<PooledVFX>(FindObjectsSortMode.None)
            .Count(effect => effect.name.StartsWith("LayeredAsteroidExplosion"));
    }
}
#endif
