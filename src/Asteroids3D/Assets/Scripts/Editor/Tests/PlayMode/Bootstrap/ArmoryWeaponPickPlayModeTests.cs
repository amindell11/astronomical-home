#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cameras;
using Combat.Weapons;
using Game;
using Game.Moments;
using Game.Runs;
using NUnit.Framework;
using Ships.Loadout;
using Substrate;
using Substrate.Sectors;
using Substrate.Sectors.Elements.Stations;
using Substrate.Services.Locales;
using Substrate.Services.Units;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using UI.Recap;
using UI.Screens;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Bootstrap
{
    /// <summary>
    /// A real host on a trial sector whose armory places one station: a fly-in opens the weapon pick with time
    /// stopped and controls off, offering nothing equipped; a take fills the matching slot, spends the armory and
    /// raises armory-pick, a skip leaves it open for another fly-in, and a headless host claims it with no pick. The rig is built without its HUD, so the presenting host needs no
    /// graphics device.
    /// </summary>
    [Category("Bootstrap")]
    [Category("RequiresLfsPng")]
    public class ArmoryWeaponPickPlayModeTests : PlayModeWorldFixture
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string ObserverCamPrefabPath = "Assets/Prefabs/Cameras/Main Camera.prefab";
        private const string SectorPrefabPath = "Assets/Prefabs/Sectors/TrialSector.prefab";
        private const string LocalePath = "Assets/Prefabs/Locales/IllustratedBlue.prefab";
        private const string RecapFeelPath = "Assets/Settings/UI/RecapFeel.asset";
        private const string PoolPath = "Assets/Settings/Ships/PlayerLoadout.asset";
        private const float StepTimeoutSec = 30f;
        private const int SettleFrames = 10;

        private static readonly FieldInfo MomentsField =
            typeof(GameHost).GetField("moments", BindingFlags.Instance | BindingFlags.NonPublic);

        private GameObject hostGo;
        private GameObject templateRoot;
        private ArmoryFeelProfile feel;
        private PlayerRig rig;
        private string dir;

        public override void SetUp()
        {
            base.SetUp();
            dir = Path.Combine(Path.GetTempPath(), "armory-host-" + Guid.NewGuid().ToString("N"));
        }

        public override void TearDown()
        {
            if (hostGo) hostGo.GetComponent<UnitService>().Clear();
            DestroyTestObject(hostGo);
            DestroyTestObject(templateRoot);
            if (feel) Object.DestroyImmediate(feel);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Time.timeScale = 1f;
            base.TearDown();
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator AFlyIn_OpensThePick_AndATakeFillsItsSlotAndSpendsTheArmory()
        {
            yield return BootToTheTrialSector(presentation: true);
            var armory = ArmoryPlaced();
            var pool = AssetDatabase.LoadAssetAtPath<ItemSubset>(PoolPath);
            yield return WaitFor(() => armory.Stations.Count == 1 && armory.Stations[0].Body.activeSelf,
                "the armory is placed");
            Assert.IsFalse(Pick(), "no pick before a fly-in");
            var carried = Equipped();

            yield return FlyInto(armory.Stations[0]);
            yield return WaitFor(() => Pick(), "the fly-in opens the weapon pick");
            Assert.AreEqual(0f, Time.timeScale, "the pick stops time");
            Assert.IsFalse(rig.Player.Commander.enabled, "the pick holds the controls");
            var offered = Offered();
            foreach (var weapon in carried)
                CollectionAssert.DoesNotContain(offered, Name(weapon), "nothing equipped is offered");
            var picked = pool.weapons.Single(weapon => Name(weapon) == offered[0]);

            Pick().ChooseOffer(0);
            yield return WaitFor(() => !Pick() && Time.timeScale > 0f, "the take closes the pick and time runs on");
            Assert.IsTrue(rig.Player.Commander.enabled, "controls come back");
            var kind = WeaponsSlot(picked) == Ships.Command.WeaponSlot.Primary ? 0 : 1;
            var mounted = Equipped();
            Assert.AreSame(picked, mounted[kind], "the pick replaces the slot its kind fills");
            Assert.AreSame(carried[1 - kind], mounted[1 - kind], "the other slot keeps its mount");
            Assert.IsTrue(armory.Stations[0].Spent, "a take spends the armory");
            var moment = Moments().OfType<ArmoryPicked>().Single();
            Assert.AreSame(picked, moment.Weapon);
            Assert.AreEqual(1, moment.Sector);

            yield return FlyOut(armory.Stations[0]);
            yield return FlyInto(armory.Stations[0]);
            Assert.IsFalse(Pick(), "a spent armory opens nothing");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator ASkip_LeavesTheArmoryOpen_AndTheNextFlyInReopensThePick()
        {
            yield return BootToTheTrialSector(presentation: true);
            var armory = ArmoryPlaced();
            yield return WaitFor(() => armory.Stations.Count == 1 && armory.Stations[0].Body.activeSelf,
                "the armory is placed");
            var kit = Equipped();

            yield return FlyInto(armory.Stations[0]);
            yield return WaitFor(() => Pick(), "the fly-in opens the weapon pick");
            Pick().ChooseSkip();
            yield return WaitFor(() => !Pick() && Time.timeScale > 0f, "the skip closes the pick");
            CollectionAssert.AreEqual(kit, Equipped(), "a skip swaps nothing");
            Assert.IsFalse(armory.Stations[0].Spent, "a skip leaves the armory open");
            Assert.IsEmpty(Moments().OfType<ArmoryPicked>(), "a skip raises no armory-pick");

            yield return Settle();
            Assert.IsFalse(Pick(), "sitting on in the armory opens no second pick");

            yield return FlyOut(armory.Stations[0]);
            yield return FlyInto(armory.Stations[0]);
            yield return WaitFor(() => Pick(), "flying back in reopens the pick");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator AHeadlessHost_ClaimsTheArmory_WithNoPick()
        {
            yield return BootToTheTrialSector(presentation: false);
            var armory = ArmoryPlaced();
            yield return WaitFor(() => armory.Stations.Count == 1 && armory.Stations[0].Body.activeSelf,
                "the armory is placed");

            yield return FlyInto(armory.Stations[0]);
            yield return WaitFor(() => armory.Claims == 1, "the headless player claims the armory");
            Assert.IsFalse(Pick(), "headless runs never open the pick");
            Assert.AreEqual(1f, Time.timeScale, "headless runs never stop time");
        }

        private IEnumerator BootToTheTrialSector(bool presentation)
        {
            hostGo = new GameObject("TestHost");
            hostGo.SetActive(false);
            var host = hostGo.AddComponent<GameHost>();
            host.sessionProfile = new SessionProfile
            {
                run = SectorRun.Create(new SectorEntry
                {
                    prefab = SectorWithOneArmory(),
                    locale = AssetDatabase.LoadAssetAtPath<LocaleSky>(LocalePath)
                }),
                presentation = presentation
            };
            host.playerRig = HudlessRig();
            host.observerCamPrefab = AssetDatabase.LoadAssetAtPath<ObserverCam>(ObserverCamPrefabPath);
            host.recapFeel = AssetDatabase.LoadAssetAtPath<RecapFeelProfile>(RecapFeelPath);
            host.hangarOffer = AssetDatabase.LoadAssetAtPath<ItemSubset>(PoolPath);
            host.runRecordPath = Path.Combine(dir, "run-records.jsonl");
            host.buildIdentity = (out BuildIdentity identity, out string failure) =>
            {
                identity = new BuildIdentity { commit = "test-commit" };
                failure = null;
                return true;
            };
            hostGo.SetActive(true);

            yield return WaitFor(() =>
            {
                rig = hostGo.GetComponentInChildren<PlayerRig>();
                return rig && rig.Tally.SecondsSurvived > 0f && host.ActiveSector;
            }, "the host started a run in the trial sector");
        }

        // The first sector places no armory as shipped, and the hunt's alarm would darken it mid-test.
        private Sector SectorWithOneArmory()
        {
            feel = ScriptableObject.CreateInstance<ArmoryFeelProfile>();
            feel.stationsPerSector = new[] { 1 };
            var sector = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Sector>(SectorPrefabPath), TemplateRoot());
            using var serialized = new SerializedObject(sector.Modules.OfType<Armory>().Single());
            serialized.FindProperty("feel").objectReferenceValue = feel;
            serialized.FindProperty("hunt").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return sector;
        }

        // Under an inactive root, so the templates never wake; with no overlay the rig builds no HUD cameras.
        private PlayerRig HudlessRig()
        {
            var template = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PlayerRig>(RigPrefabPath), TemplateRoot());
            using var serialized = new SerializedObject(template);
            serialized.FindProperty("overlayPrefab").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return template;
        }

        private Transform TemplateRoot()
        {
            if (templateRoot) return templateRoot.transform;
            templateRoot = new GameObject("Templates");
            templateRoot.SetActive(false);
            return templateRoot.transform;
        }

        private Sector ActiveSector() => hostGo.GetComponent<GameHost>().ActiveSector;

        private Armory ArmoryPlaced() => ActiveSector().Module<Armory>();

        private WeaponComponent[] Equipped() =>
            new[] { rig.Player.Weapons.PrimaryMountPrefab, rig.Player.Weapons.SecondaryMountPrefab };

        private static Ships.Command.WeaponSlot WeaponsSlot(WeaponComponent weapon) =>
            Ships.Weapons.WeaponsController.SlotFor(weapon);

        private static string Name(WeaponComponent weapon) => weapon.DisplayName.ToUpperInvariant();

        private string[] Offered() => Pick().transform.Find("Offers/Cards").Cast<Transform>()
            .Select(card => card.Find("Name").GetComponent<Text>().text).ToArray();

        private IReadOnlyList<IRunMoment> Moments() =>
            ((RunMomentStream)MomentsField.GetValue(hostGo.GetComponent<GameHost>())).Moments;

        private WeaponPickScreen Pick() => hostGo.GetComponentInChildren<WeaponPickScreen>();

        private IEnumerator FlyInto(FindStation.Station station) => MoveTo(station.Position);

        private IEnumerator FlyOut(FindStation.Station station) => MoveTo(station.Position + GamePlane.Right * 100f);

        private IEnumerator MoveTo(Vector3 position)
        {
            var player = rig.Player;
            player.transform.position = position;
            player.Body.position = position;
            player.Body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            // The zone sees an exit or entry only on a physics step, which ten fast frames can miss.
            yield return new WaitForFixedUpdate();
            yield return Settle();
        }

        private static IEnumerator Settle()
        {
            for (var i = 0; i < SettleFrames; i++) yield return null;
        }

        private static IEnumerator WaitFor(Func<bool> condition, string what) =>
            AsyncAssert.WaitUntil(condition, StepTimeoutSec, what);
    }
}
#endif
