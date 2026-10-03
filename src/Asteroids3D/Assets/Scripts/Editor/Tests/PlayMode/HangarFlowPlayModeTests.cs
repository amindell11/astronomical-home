#if UNITY_EDITOR
using System.Collections;
using Game;
using NUnit.Framework;
using Substrate.Services;
using Substrate.Services.Objectives;
using Substrate.Services.Units;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    /// <summary>
    /// Guards the hangar's non-interactive path: when presentation is off (headless/RL) the host's
    /// <see cref="GameHost.RunHangar"/> step must apply the standing loadout and finish on its
    /// own — never instantiate the screen or block waiting for a Launch click. Also guards the
    /// rig's parking invariant across that step: the player is parked outside a sector (so nothing
    /// of it renders behind the hangar) and live once the step hands it to the sector load.
    /// </summary>
    [Category("UI")]
    public class HangarFlowPlayModeTests : PlayModeWorldFixture
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";

        private GameObject hostGo;
        private GameObject rigGo;
        private GameObject servicesGo;
        private PlayerRig playerRig;
        private UnitService unitService;

        public override void TearDown()
        {
            if (playerRig) playerRig.Teardown();
            if (unitService) unitService.Clear();
            DestroyTestObject(hostGo);
            DestroyTestObject(rigGo);
            DestroyTestObject(playerRig ? playerRig.gameObject : null);
            DestroyTestObject(servicesGo);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator RunHangar_HeadlessOrNoPlayer_CompletesWithoutScreen()
        {
            // Keep the host inactive so its Awake and flow never run — the hangar flow is
            // pumped in isolation. RequireComponent adds the sibling services on AddComponent.
            hostGo = new GameObject("TestHost");
            hostGo.SetActive(false);
            var host = hostGo.AddComponent<GameHost>();

            // A bare rig: no player was built, no screen prefab assigned — both gate conditions hold.
            rigGo = new GameObject("TestRig");
            var rig = rigGo.AddComponent<PlayerRig>();

            var finished = false;
            var step = host.RunHangar(rig, presentationEnabled: false, hostGo.transform);
            while (step.MoveNext())
                yield return step.Current;
            finished = true;

            Assert.IsTrue(finished, "RunHangar completed without waiting for a Launch click");
            Assert.IsNull(Object.FindFirstObjectByType<UI.HangarScreen>(),
                "no hangar screen was instantiated on the non-interactive path");
        }

        [UnityTest]
        public IEnumerator AlivePlayer_ParkedOutsideSector_LiveAfterHangarStep()
        {
            servicesGo = new GameObject("TestServices");
            unitService = servicesGo.AddComponent<UnitService>();
            var objectiveService = servicesGo.AddComponent<ObjectiveService>();
            ShipServices.Compose(unitService, servicesGo.transform, presentationEnabled: false);

            var rigPrefab = AssetDatabase.LoadAssetAtPath<PlayerRig>(RigPrefabPath);
            Assert.IsNotNull(rigPrefab, "PlayerRig prefab loads");
            playerRig = Object.Instantiate(rigPrefab);

            // No observer and presentation off: the rig builds no camera, so this runs under -nographics.
            var died = false;
            yield return playerRig.Build(unitService, objectiveService, presentationEnabled: false, observer: null,
                servicesGo.transform, new SessionFrame(Vector2.zero), onPlayerDeath: (_, _) => died = true);
            Assert.IsNotNull(playerRig.Player, "rig built a player");
            Assert.IsFalse(playerRig.Player.gameObject.activeSelf, "the first hangar sees the alive player parked");

            hostGo = new GameObject("TestHost");
            hostGo.SetActive(false);
            var host = hostGo.AddComponent<GameHost>();

            yield return host.RunHangar(playerRig, presentationEnabled: false, hostGo.transform);
            Assert.IsTrue(playerRig.Player.gameObject.activeSelf,
                "the hangar step hands the sector load a live player");

            // What the host does after a completed sector unloads, with the player still alive.
            playerRig.Park();
            Assert.IsFalse(playerRig.Player.gameObject.activeSelf,
                "the hangar after a completed sector sees the player parked");
            Assert.IsFalse(died, "parking an alive player does not read as a death");
            Assert.IsTrue(unitService.Registry.TryGetShip(playerRig.Player.Id, out _),
                "a parked player stays registered");

            yield return host.RunHangar(playerRig, presentationEnabled: false, hostGo.transform);
            Assert.IsTrue(playerRig.Player.gameObject.activeSelf, "the next hangar step revives the parked player");
        }
    }
}
#endif
