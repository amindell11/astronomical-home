using NUnit.Framework;
using Ships;
using Ships.Loadout;
using Ships.Mods;
using UnityEditor;

namespace Tests.EditMode.Ships
{
    [TestFixture]
    [Category("Ships")]
    public class ShipScaleEditModeTests
    {
        private const string VanguardPath = "Assets/Prefabs/Ships/Vanguard.prefab";

        [Test]
        public void Resolve_WithAScale_ScalesMassForcesTorquesAndHull_AndLeavesTheRestAuthored()
        {
            var ship = AssetDatabase.LoadAssetAtPath<Ship>(VanguardPath);
            Assert.IsNotNull(ship, "Vanguard prefab loads");

            var authored = ship.ResolveStats();
            var scaled = ResolvedShipStats.Resolve(ship, ship.Engine, ship.Shield, ShipMods.None,
                new ShipScale(size: 2f, mass: 3f, thrust: 4f, torque: 5f, hull: 6f));

            Assert.AreEqual(authored.mass * 3f, scaled.mass, 1e-3f);
            Assert.AreEqual(authored.forwardForce * 4f, scaled.forwardForce, 1e-3f);
            Assert.AreEqual(authored.reverseForce * 4f, scaled.reverseForce, 1e-3f);
            Assert.AreEqual(authored.minStrafeForce * 4f, scaled.minStrafeForce, 1e-3f);
            Assert.AreEqual(authored.maxStrafeForce * 4f, scaled.maxStrafeForce, 1e-3f);
            Assert.AreEqual(authored.yawTorque * 5f, scaled.yawTorque, 1e-3f);
            Assert.AreEqual(authored.bankTorque * 5f, scaled.bankTorque, 1e-3f);
            Assert.AreEqual(authored.bankDamping * 5f, scaled.bankDamping, 1e-3f);
            Assert.AreEqual(authored.maxHealth * 6f, scaled.maxHealth, 1e-3f);

            Assert.AreEqual(authored.maxSpeed, scaled.maxSpeed, 1e-4f, "top speed is the engine's, not the size's");
            Assert.AreEqual(authored.maxYawRate, scaled.maxYawRate, 1e-4f, "the turn-rate cap stays authored");
            Assert.AreEqual(authored.angularDrag, scaled.angularDrag, 1e-4f);
            Assert.AreEqual(authored.linearDrag, scaled.linearDrag, 1e-4f);
            Assert.AreEqual(authored.maxBankAngle, scaled.maxBankAngle, 1e-4f);
            Assert.AreEqual(authored.maxShield, scaled.maxShield, 1e-4f);
            Assert.AreEqual(authored.shieldRegenRate, scaled.shieldRegenRate, 1e-4f);
            Assert.AreEqual(authored.shieldRegenDelay, scaled.shieldRegenDelay, 1e-4f);
            Assert.AreEqual(authored.startingLives, scaled.startingLives);
            Assert.AreEqual(authored.mass, ship.ResolveStats().mass, 1e-4f, "the hangar read stays unscaled");
        }
    }
}
