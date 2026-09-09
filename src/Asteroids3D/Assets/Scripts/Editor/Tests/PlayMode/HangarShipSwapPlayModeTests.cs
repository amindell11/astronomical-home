#if UNITY_EDITOR
using System.Collections;
using Cameras;
using Game;
using Substrate.Sessions;
using NUnit.Framework;
using Ships;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Utils;
using Ships.Registry;
using Substrate.Services.Units;
using Substrate.Services.Projectiles;
using Substrate.Services.Objectives;

namespace Tests.PlayMode
{
    /// <summary>
    /// The hangar's ship change is a whole-player rebuild (PlayerRig.ApplyLoadout →
    /// RebuildPlayer): the old ship despawns, a fresh build of the chosen prefab takes its place
    /// with the standard wiring re-run, and the injected death callback follows the new instance.
    /// Uses the real PlayerRig prefab + a real unit service — this is the integration seam the
    /// between-run flow drives.
    /// </summary>
    // Real PlayerRig cameras: URP render loop cannot create RTs under -nographics.
    [Category("RequiresGraphics")]
    public class HangarShipSwapPlayModeTests : PlayModeWorldFixture
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string Ship1Path = "Assets/Prefabs/Ships/Ship_1.prefab";

        private GameObject servicesGo;
        private PlayerRig rig;
        private ObserverCam observer;
        private UnitService unitService;

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
            unitService?.Clear();
            DestroyTestObject(rig ? rig.gameObject : null);
            DestroyTestObject(observer ? observer.gameObject : null);
            DestroyTestObject(servicesGo);
            base.TearDown();
        }

        private IEnumerator BuildRig(System.Action<ShipId, Damage.DamageInfo> onPlayerDeath = null)
        {
            servicesGo = new GameObject("TestServices");
            unitService = servicesGo.AddComponent<UnitService>();
            var objectiveService = servicesGo.AddComponent<ObjectiveService>();
            unitService.SetProjectiles(new ProjectileService(servicesGo.transform));

            observer = TestAssets.NewObserverCam();
            var rigPrefab = AssetDatabase.LoadAssetAtPath<PlayerRig>(RigPrefabPath);
            Assert.IsNotNull(rigPrefab, "PlayerRig prefab loads");
            rig = Object.Instantiate(rigPrefab);
            yield return rig.Build(unitService, objectiveService, presentationEnabled: false, observer,
                new SessionFrame(Vector2.zero), onPlayerDeath: onPlayerDeath);
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
            Assert.IsFalse(unitService.Registry.TryGetShip(oldId, out _),
                "old ship left the registry");
            Assert.IsTrue(unitService.Registry.TryGetShip(rig.Player.Id, out _),
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

            var lethal = rig.Player.Stats.maxShield + rig.Player.Stats.maxHealth + 100f;
            rig.Player.Damage.TakeDamage(new Damage.DamageInfo(lethal, Damage.DamageKind.Laser,
                ShipId.Invalid, 0f, Vector3.zero, rig.Player.transform.position));

            Assert.IsTrue(died, "injected death callback re-armed on the rebuilt player");
        }
    }
}
#endif
