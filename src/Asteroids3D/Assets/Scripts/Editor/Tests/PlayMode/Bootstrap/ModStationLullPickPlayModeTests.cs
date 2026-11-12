#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cameras;
using Game;
using Game.Moments;
using Game.Runs;
using Loot;
using NUnit.Framework;
using Substrate.Sectors.Elements.Phases;
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
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Bootstrap
{
    /// <summary>
    /// A real host on the trial sector: each mod station claim opens the lull pick once with time stopped and
    /// a taken pick raises mod-pick, a lull beacon claim opens none and raises one cache-claimed with its
    /// bonus key, and a headless host claims stations with no pick. The rig is built without its HUD, so
    /// the presenting host needs no graphics device.
    /// </summary>
    [Category("Bootstrap")]
    [Category("RequiresLfsPng")]
    public class ModStationLullPickPlayModeTests : PlayModeWorldFixture
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string ObserverCamPrefabPath = "Assets/Prefabs/Cameras/Main Camera.prefab";
        private const string LocalePath = "Assets/Prefabs/Locales/IllustratedBlue.prefab";
        private const string RecapFeelPath = "Assets/Settings/UI/RecapFeel.asset";
        private const string LullModsPath = "Assets/Settings/Weapons/Mods/LullMods.asset";
        private const float StepTimeoutSec = 30f;
        private const int SettleFrames = 10;

        private static readonly FieldInfo MomentsField =
            typeof(GameHost).GetField("moments", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly string[] BonusKeys = { "damage", "fire-rate", "shield", BeaconCache.HullKey };

        private GameObject hostGo;
        private GameObject rigTemplateRoot;
        private PlayerRig rig;
        private string dir;

        public override void SetUp()
        {
            base.SetUp();
            dir = Path.Combine(Path.GetTempPath(), "mod-station-host-" + Guid.NewGuid().ToString("N"));
        }

        public override void TearDown()
        {
            if (hostGo) hostGo.GetComponent<UnitService>().Clear();
            DestroyTestObject(hostGo);
            DestroyTestObject(rigTemplateRoot);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Time.timeScale = 1f;
            base.TearDown();
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator EachModStationClaim_OpensTheLullPickOnce_AndABeaconClaimOpensNone()
        {
            yield return BootToTheTrialSector(presentation: true);
            var stations = StationsPlaced();
            yield return WaitFor(() => stations.Stations.Count == 2 && stations.Stations[1].Body.activeSelf,
                "both mod stations are placed");
            Assert.IsFalse(Card(), "no card before a claim");

            yield return FlyInto(stations.Stations[0]);
            yield return WaitFor(() => Card(), "the first claim opens the card");
            Assert.AreEqual(0f, Time.timeScale, "the card stops time");
            Card().Choose(0);
            yield return WaitFor(() => !Card() && Time.timeScale > 0f, "the pick closes the card and time runs on");
            Assert.AreEqual(1, Moments().OfType<ModPicked>().Count(), "a taken pick raises mod-pick");

            yield return Settle();
            Assert.IsFalse(Card(), "a claimed station opens no second card");

            yield return FlyInto(stations.Stations[1]);
            yield return WaitFor(() => Card(), "the second station's claim opens the card again");
            Card().Choose(LullPickScreen.Skip);
            yield return WaitFor(() => !Card(), "the second card closes");
            Assert.AreEqual(1, Moments().OfType<ModPicked>().Count(), "a skip raises no mod-pick");

            yield return FlyInto(stations.Stations[0]);
            Assert.IsFalse(Card(), "flying back into a spent station opens nothing");
            Assert.AreEqual(2, stations.Claims);

            var spine = hostGo.GetComponent<GameHost>().ActiveSector.Module<PhaseSpineModule>();
            typeof(PhaseSpineModule).GetProperty(nameof(PhaseSpineModule.BeaconClaimed))
                .SetValue(spine, true);
            yield return Settle();
            Assert.IsFalse(Card(), "a lull beacon claim no longer opens the card");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator EachCacheClaim_RaisesOneCacheClaimed_WithItsBonusKey()
        {
            yield return BootToTheTrialSector(presentation: true);
            var host = hostGo.GetComponent<GameHost>();
            var spine = host.ActiveSector.Module<PhaseSpineModule>();
            var cache = host.ActiveSector.Module<BeaconCache>();

            yield return ClaimBeacon(spine);
            var first = Moments().OfType<CacheClaimed>().ToList();
            Assert.AreEqual(1, first.Count, "one claim raises one cache-claimed");
            Assert.AreEqual(cache.LastBonus, first[0].Bonus, "the moment carries the rolled bonus");
            Assert.AreEqual(1, first[0].Sector);

            yield return ClaimBeacon(spine);
            var both = Moments().OfType<CacheClaimed>().ToList();
            Assert.AreEqual(2, both.Count, "a second claim in the sector raises a second");
            Assert.AreEqual(cache.LastBonus, both[1].Bonus);
            foreach (var moment in both)
                CollectionAssert.Contains(BonusKeys, moment.Bonus);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator AHeadlessHost_ClaimsAModStation_WithNoCard()
        {
            yield return BootToTheTrialSector(presentation: false);
            var stations = StationsPlaced();
            yield return WaitFor(() => stations.Stations.Count > 0 && stations.Stations[0].Body.activeSelf,
                "the mod stations are placed");

            yield return FlyInto(stations.Stations[0]);
            yield return WaitFor(() => stations.Claims == 1, "the headless player claims the station");
            Assert.IsFalse(Card(), "headless runs never open the card");
            Assert.AreEqual(1f, Time.timeScale, "headless runs never stop time");
        }

        private IEnumerator BootToTheTrialSector(bool presentation)
        {
            var row = TestAssets.TrialRow(0).WithLocale(AssetDatabase.LoadAssetAtPath<LocaleSky>(LocalePath));
            row.modStations = 2;
            hostGo = new GameObject("TestHost");
            hostGo.SetActive(false);
            var host = hostGo.AddComponent<GameHost>();
            host.sessionProfile = new SessionProfile
            {
                run = SectorRun.Create(row),
                presentation = presentation
            };
            host.playerRig = HudlessRig();
            host.observerCamPrefab = AssetDatabase.LoadAssetAtPath<ObserverCam>(ObserverCamPrefabPath);
            host.recapFeel = AssetDatabase.LoadAssetAtPath<RecapFeelProfile>(RecapFeelPath);
            host.lullMods = AssetDatabase.LoadAssetAtPath<LullModPool>(LullModsPath);
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

        // Under an inactive root, so the template never wakes; with no overlay the rig builds no HUD cameras.
        private PlayerRig HudlessRig()
        {
            rigTemplateRoot = new GameObject("RigTemplate");
            rigTemplateRoot.SetActive(false);
            var template = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PlayerRig>(RigPrefabPath),
                rigTemplateRoot.transform);
            using var serialized = new SerializedObject(template);
            serialized.FindProperty("overlayPrefab").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return template;
        }

        // The cache rolls on the claim's rising edge, so each call drops the claim first.
        private static IEnumerator ClaimBeacon(PhaseSpineModule spine)
        {
            var claimed = typeof(PhaseSpineModule).GetProperty(nameof(PhaseSpineModule.BeaconClaimed));
            claimed.SetValue(spine, false);
            yield return Settle();
            claimed.SetValue(spine, true);
            yield return Settle();
        }

        private IReadOnlyList<IRunMoment> Moments() =>
            ((RunMomentStream)MomentsField.GetValue(hostGo.GetComponent<GameHost>())).Moments;

        private ModStation StationsPlaced() => hostGo.GetComponent<GameHost>().ActiveSector.Module<ModStation>();

        private LullPickScreen Card() => hostGo.GetComponentInChildren<LullPickScreen>();

        private IEnumerator FlyInto(ModStation.Station station)
        {
            var player = rig.Player;
            player.transform.position = station.Position;
            player.Body.position = station.Position;
            player.Body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
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
