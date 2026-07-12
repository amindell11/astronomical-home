#if UNITY_EDITOR
using System.Collections;
using Game.Services;
using NUnit.Framework;
using Player;
using Ships;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;

namespace Tests.PlayMode
{
    /// <summary>
    /// The hangar's ship change is a whole-player rebuild (SessionRig.ApplyLoadout →
    /// RebuildPlayer): the old ship despawns, a fresh build of the chosen prefab takes its place
    /// with the standard wiring re-run, and the injected death callback follows the new instance.
    /// Uses the real SessionRig prefab + a real service container — this is the integration seam the
    /// between-run flow drives.
    /// </summary>
    [Category("RequiresGraphics")]
    public class HangarShipSwapPlayModeTests : PlayModeWorldFixture
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string Ship1Path = "Assets/Prefabs/Ships/Ship_1.prefab";

        private GameObject servicesGo;
        private SessionRig rig;
        private GameServices services;

        public override void SetUp()
        {
            base.SetUp();
            // Headless presentation: no HUD/hangar UI needed to exercise the rebuild seam.
            GameSettings.SetPresentationEnabled(false);
        }

        public override void TearDown()
        {
            GameSettings.SetPresentationEnabled(true);
            if (rig) rig.Teardown();
            services?.ClearAll();
            DestroyTestObject(rig ? rig.gameObject : null);
            DestroyTestObject(servicesGo);
            base.TearDown();
        }

        private IEnumerator BuildRig(System.Action<ShipId, ShipId> onPlayerDeath = null)
        {
            servicesGo = new GameObject("TestServices");
            var unitService = servicesGo.AddComponent<UnitService>();
            var objectiveService = servicesGo.AddComponent<ObjectiveService>();
            services = new GameServices(
                unitService: unitService,
                environmentService: new EnvironmentService(),
                objectiveService: objectiveService,
                cameraService: new CameraService(),
                uiService: new UIService());

            var rigPrefab = AssetDatabase.LoadAssetAtPath<SessionRig>(RigPrefabPath);
            Assert.IsNotNull(rigPrefab, "SessionRig prefab loads");
            rig = Object.Instantiate(rigPrefab);
            yield return rig.Build(services, buildPlayer: true, onPlayerDeath: onPlayerDeath);
            Assert.IsNotNull(rig.Player, "rig built a player");
        }

        [UnityTest]
        public IEnumerator ShipChange_RebuildsPlayer_WithNewTemplateAndWiring()
        {
            yield return BuildRig();

            var oldShip = rig.Player;
            var oldId = oldShip.Id;

            var ship1 = AssetDatabase.LoadAssetAtPath<Ship>(Ship1Path);
            Assert.IsNotNull(ship1, "Ship_1 prefab loads");
            Assert.AreNotEqual("Ship_1(Clone)", oldShip.name, "test premise: rig does not start on Ship_1");

            rig.Loadout.Ship = ship1;
            rig.Loadout.Engine = ship1.Engine;
            rig.Loadout.Shield = ship1.Shield;
            rig.ApplyLoadout();

            Assert.AreNotSame(oldShip, rig.Player, "a new player instance was built");
            Assert.AreEqual("Ship_1(Clone)", rig.Player.name, "new player comes from the chosen template");
            Assert.AreEqual("Player", rig.Player.tag, "player wiring re-ran on the new instance");
            Assert.IsFalse(services.UnitService.Registry.TryGetShip(oldId, out _),
                "old ship left the registry");
            Assert.IsTrue(services.UnitService.Registry.TryGetShip(rig.Player.Id, out _),
                "new ship is registered");

            yield return null; // let Destroy(oldShip) finalize
            Assert.IsTrue(oldShip == null, "old player instance was destroyed");
        }

        [UnityTest]
        public IEnumerator SameShip_LaunchDoesNotRebuild()
        {
            yield return BuildRig();
            var shipBefore = rig.Player;

            rig.ApplyLoadout(); // unedited loadout — same template

            Assert.AreSame(shipBefore, rig.Player, "unchanged ship pick must not rebuild the player");
        }

        [UnityTest]
        public IEnumerator ShipChange_DeathCallbackFollowsNewShip()
        {
            var died = false;
            yield return BuildRig((_, _) => died = true);

            var ship1 = AssetDatabase.LoadAssetAtPath<Ship>(Ship1Path);
            rig.Loadout.Ship = ship1;
            rig.Loadout.Engine = ship1.Engine;
            rig.Loadout.Shield = ship1.Shield;
            rig.ApplyLoadout();

            // Lethal damage on the NEW ship must fire the injected death callback (re-wired across the
            // rebuild). Damage applies to shield OR hull per hit (overflow discarded), so two hits:
            // one to drop the shield, one to kill the hull.
            var lethal = rig.Player.Stats.maxShield + rig.Player.Stats.maxHealth + 100f;
            rig.Player.Damage.TakeDamage(lethal, 0f, Vector3.zero, rig.Player.transform.position, null);
            rig.Player.Damage.TakeDamage(lethal, 0f, Vector3.zero, rig.Player.transform.position, null);

            Assert.IsTrue(died, "injected death callback re-armed on the rebuilt player");
        }
    }
}
#endif
