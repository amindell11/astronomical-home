#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using AI;
using Damage;
using NUnit.Framework;
using RL.Opponents;
using Ships;
using Ships.Keystones;
using Ships.Keystones.Kits;
using Substrate;
using Tests.PlayMode.Common;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Keystones
{
    [Category("AI")]
    public class RamSightPlayModeTests : AIIntegrationFixture
    {
        private const string BulwarkPath = "Assets/Prefabs/Ships/Bulwark.prefab";
        private const string VanguardPath = "Assets/Prefabs/Ships/Vanguard.prefab";
        private const float SimSeconds = 20f;

        private readonly List<Ship> spawned = new();

        public override void TearDown()
        {
            foreach (var ship in spawned)
                if (ship) Object.DestroyImmediate(ship.gameObject);
            spawned.Clear();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator ScriptedBulwark_RamsThroughItsSight_AndFliesAtTheTargetWhileTheRamIsLive()
        {
            var target = Object.Instantiate(TestAssets.Load<Ship>(VanguardPath), GamePlane.PlanePointToWorld(new Vector2(0f, 14f)), GamePlane.Rotation);
            spawned.Add(target);
            target.Initialize(1, 0, Projectiles);
            registry.ActiveShips.Add(target);
            var rammed = new List<DamageInfo>();
            target.Damage.OnDamaged += hit => { if (hit.Kind == DamageKind.Collision) rammed.Add(hit); };

            var bulwark = ShipTestFactory.CreateShip(TestAssets.Load<Ship>(BulwarkPath), TestAssets.LoadTestPilotMpc(), Projectiles,
                team: 0, position: Vector3.zero, rotation: GamePlane.Rotation);
            spawned.Add(bulwark);
            registry.ActiveShips.Add(bulwark);
            var commander = bulwark.GetComponentInChildren<AICommander>();
            commander.InstallBrain<ArchetypeBrain>().Configure(target, OpponentArchetype.Aggressor,
                new OpponentDraw { desiredRange = 3f, speedFraction = 0.85f }, jukeSeed: 0, Vector2.zero, borderRadius: 200f);
            commander.SetSensing(registry, null);
            var ram = (RamDash)bulwark.Keystone;

            var exemptWhileLive = true;
            var seenActivations = 0;
            var steps = Mathf.CeilToInt(SimSeconds / Time.fixedDeltaTime);
            for (var i = 0; i < steps && rammed.Count == 0; i++)
            {
                yield return new WaitForFixedUpdate();
                // The commander steers before the kit activates, so it first sees a live ram one tick later.
                var activatedThisTick = ram.Activations != seenActivations;
                seenActivations = ram.Activations;
                if (ram.RamLive && !activatedThisTick && commander.Navigator.AvoidanceExempt != target.Id) exemptWhileLive = false;
            }

            Assert.That(ram.Activations, Is.GreaterThan(0), "the sight fired the ram with no brain request (no missile threat)");
            Assert.That(rammed, Is.Not.Empty, "a ram landed on the target");
            Assert.That(rammed[0].AttackerId, Is.EqualTo(bulwark.Id));
            Assert.That(ram.PaidActivations, Is.GreaterThan(0));
            Assert.That(exemptWhileLive, Is.True, "the navigator left the target out of its obstacles while the ram was live");
        }
    }
}
#endif
