#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Cameras;
using Combat.Weapons;
using Combat.Weapons.Arsenal;
using Combat.Weapons.Mods;
using Game;
using Game.Runs;
using Game.Travel;
using Objectives;
using Ships.Command;
using Ships.Loadout;
using Substrate.Services.Objectives;
using UI.Hangar;
using NUnit.Framework;
using Substrate;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Sessions;
using Substrate.Services.Locales;
using Substrate.Services.Units;
using UI.Recap;
using UI.Screens;
using Tests.PlayMode.Common;
using Tests.PlayMode.Common.SimulatedInput;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests.PlayMode.Bootstrap
{
    /// <summary>
    /// A real host over two sectors checks run records, carried hull, the mid-run weapon swap and the return
    /// to play. Graphics cases cover recap timing, the weapon pick in hyperspace, death effects, HUD
    /// visibility and camera restoration.
    /// </summary>
    [Category("Bootstrap")]
    public class MultiSectorRunPlayModeTests : PlayModeWorldFixture
    {
        private const string RigPrefabPath = "Assets/Prefabs/MiscObjects/PlayerRig.prefab";
        private const string ObserverCamPrefabPath = "Assets/Prefabs/Cameras/Main Camera.prefab";
        private const string SectorConfigPath = "Assets/Settings/Game/DefaultSectorConfig.asset";
        private const string RecapFeelPath = "Assets/Settings/UI/RecapFeel.asset";
        private const float StepTimeoutSec = 30f;

        private readonly PlayModeInputTestFixture input = new();
        private GameObject hostGo;
        private GameHost host;
        private GameObject[] templates;
        private string dir;
        private string recordPath;

        public override void SetUp()
        {
            input.Setup();
            base.SetUp();
            dir = Path.Combine(Path.GetTempPath(), "multi-sector-run-" + Guid.NewGuid().ToString("N"));
            recordPath = Path.Combine(dir, "run-records.jsonl");
        }

        public override void TearDown()
        {
            if (hostGo) hostGo.GetComponent<UnitService>().Clear();
            DestroyTestObject(hostGo);
            if (templates != null)
                foreach (var template in templates) DestroyTestObject(template);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            base.TearDown();
            input.TearDown();
        }

        [UnityTest]
        public IEnumerator ExtractingChainsTheSectors_KeepingClockAndHull_AndTheLastOneWins()
        {
            PlayerRig rig = null;
            yield return BootToTheFirstSector(built => rig = built);

            rig.Player.Damage.Health.SetFraction(0.4f);
            var clockBefore = rig.Tally.SecondsSurvived;
            host.ActiveSector.GetComponent<ExtractOnCueModule>().Extract();

            yield return WaitFor(() => InSector("SectorB"), "extracting from the first sector loads the second");
            // The load resets the ship first; the host puts the run's hull back once the load returns.
            yield return WaitFor(() => Mathf.Abs(rig.Player.Damage.Health.Pct - 0.4f) < 1e-3f,
                "hull carries into the next sector");
            Assert.IsTrue(rig.Player.gameObject.activeSelf, "the player flies on without a hangar stop");
            Assert.Greater(rig.Tally.SecondsSurvived, clockBefore, "the run clock keeps running across sectors");
            Assert.IsFalse(File.Exists(recordPath), "no record until the run ends");

            host.ActiveSector.GetComponent<ExtractOnCueModule>().Extract();
            yield return WaitFor(() => File.Exists(recordPath), "extracting from the last sector records the run");
            var record = JsonUtility.FromJson<RunRecord>(File.ReadAllLines(recordPath)[0]);
            Assert.AreEqual(RunRecord.Extracted, record.outcome);
            Assert.AreEqual(2, record.sectorsCleared);
            Assert.AreEqual("SectorB", record.sector);
            Assert.AreEqual(RunRecord.NoSpawn, record.killingBlow.spawn);

            yield return WaitFor(() => InSector("SectorA"), "the next run starts at the first sector");
            Assert.AreEqual(1f, rig.Player.Damage.Health.Pct, 1e-3f, "a new run starts at full hull");
        }

        [UnityTest]
        public IEnumerator SwapWeapon_MidRun_KeepsHullShieldAndClocks_AndRehandsTheRunsMods()
        {
            PlayerRig rig = null;
            yield return BootToTheFirstSector(built => rig = built);
            var railgun = AssetDatabase.LoadAssetAtPath<WeaponComponent>("Assets/Prefabs/Weapons/Railgun.prefab");
            var hot = AssetDatabase.LoadAssetAtPath<WeaponMod>("Assets/Settings/Weapons/Mods/HotRounds.asset");
            rig.PickMod(hot);
            rig.Player.Damage.Health.SetFraction(.4f);
            rig.Player.Damage.Shield.SetFraction(.3f);
            var secondary = rig.Player.Weapons.Secondary;
            var hangarPrimary = rig.Loadout.PrimaryWeapon;
            var clockBefore = rig.Tally.SecondsSurvived;

            rig.SwapWeapon(WeaponSlot.Primary, railgun);

            Assert.AreSame(railgun, rig.Player.Weapons.PrimaryMountPrefab);
            Assert.IsInstanceOf<Railguns>(rig.Player.Weapons.Primary);
            Assert.AreSame(secondary, rig.Player.Weapons.Secondary, "the kept mount keeps its live instance");
            Assert.AreEqual(1f + hot.Amount, rig.Player.Weapons.Primary.Mods.Factor(WeaponModField.Damage), 1e-5f,
                "the run's mods ride onto the new mount");
            Assert.AreEqual(.4f, rig.Player.Damage.Health.Pct, 1e-3f, "no hull refill");
            Assert.AreEqual(.3f, rig.Player.Damage.Shield.Pct, 1e-3f, "no shield refill");
            Assert.AreSame(hangarPrimary, rig.Loadout.PrimaryWeapon, "the hangar kit stays for the next run");
            yield return null;
            Assert.Greater(rig.Tally.SecondsSurvived, clockBefore, "the run clock runs on");
        }

        [UnityTest]
        public IEnumerator DyingInALaterSector_RecordsTheSectorsCleared()
        {
            PlayerRig rig = null;
            yield return BootToTheFirstSector(built => rig = built);

            host.ActiveSector.GetComponent<ExtractOnCueModule>().Extract();
            yield return WaitFor(() => InSector("SectorB"), "extracting from the first sector loads the second");

            TestDamage.Kill(rig.Player);
            yield return WaitFor(() => File.Exists(recordPath), "the death records the run");
            var record = JsonUtility.FromJson<RunRecord>(File.ReadAllLines(recordPath)[0]);
            Assert.AreEqual(RunRecord.Died, record.outcome);
            Assert.AreEqual(1, record.sectorsCleared);
            Assert.AreEqual("SectorB", record.sector);
        }

        [UnityTest]
        [Category("RequiresGraphics")]
        public IEnumerator Recaps_PauseBeforeUnloading_AndFinalExtractionShowsVictoryDirectly()
        {
            PlayerRig rig = null;
            yield return BootToTheFirstSector(built => rig = built, presentation: true);
            rig.Player.Damage.Health.SetFraction(.4f);
            host.ActiveSector.GetComponent<ExtractOnCueModule>().Extract();
            yield return WaitFor(() => hostGo.GetComponentInChildren<RunRecapScreen>(), "sector recap appears");
            var recap = hostGo.GetComponentInChildren<RunRecapScreen>();
            Assert.AreEqual("EXTRACTED", recap.transform.Find("Panels/Recap/Outcome").GetComponent<Text>().text);
            Assert.IsTrue(InSector("SectorA"));
            Assert.AreEqual(0f, Time.timeScale);
            var elapsed = rig.Tally.SecondsSurvived;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.AreEqual(elapsed, rig.Tally.SecondsSurvived);
            Assert.IsFalse(File.Exists(recordPath));
            recap.transform.Find("Panels/Recap/Continue").GetComponent<Button>().onClick.Invoke();

            yield return WaitFor(() => InSector("SectorB") && Mathf.Abs(rig.Player.Damage.Health.Pct - .4f) < 1e-3f,
                "next sector finishes loading after dismissal");
            Assert.AreEqual(1f, Time.timeScale);
            host.ActiveSector.GetComponent<ExtractOnCueModule>().Extract();
            yield return WaitFor(() => File.Exists(recordPath), "victory is recorded");
            Assert.AreEqual(0f, Time.timeScale, "victory freezes combat before fading the HUD");
            yield return WaitFor(() => hostGo.GetComponentInChildren<RunRecapScreen>(), "final recap appears");
            recap = hostGo.GetComponentInChildren<RunRecapScreen>();
            Assert.AreEqual("VICTORY", recap.transform.Find("Panels/Recap/Outcome").GetComponent<Text>().text);
            Assert.IsNotNull(recap.transform.Find("Panels/Recap/Scoreboard/CurrentRun"));
            Assert.AreEqual(1, File.ReadAllLines(recordPath).Length);
            UnityEngine.Object.Destroy(hostGo);
            yield return null;
            Assert.AreEqual(1f, Time.timeScale, "tearing down a paused host restores the clock");
        }

        [UnityTest]
        [Category("RequiresGraphics")]
        public IEnumerator ProductionJump_LaunchesNormally_HoldsRecap_ArrivesMoving_ThenWinsWithoutAnotherJump()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            PlayerRig rig = null;
            yield return BootToTheFirstSector(built => rig = built, presentation: true, productionJump: true);
            Assert.IsNull(host.Jump, "Hangar launch never enters hyperspace.");
            Assert.IsTrue(rig.Player.Commander.enabled);
            rig.Player.Damage.Health.SetFraction(.6f);
            var first = host.ActiveSector;
            yield return EnterProductionZone(rig);
            Assert.IsNull(host.Jump, "Zone occupancy does not commit extraction.");
            input.Press(keyboard.fKey);
            InputSystem.Update();
            Assert.IsNotNull(host.Jump, "The production spine commits in the F callback.");
            Assert.IsFalse(rig.Player.Commander.enabled);
            input.Release(keyboard.fKey);
            InputSystem.Update();
            yield return WaitFor(() => host.Jump.Holding && hostGo.GetComponentInChildren<RunRecapScreen>(),
                "the extracted recap appears in hyperspace");
            var recap = hostGo.GetComponentInChildren<RunRecapScreen>();
            Assert.AreEqual("EXTRACTED", recap.transform.Find("Panels/Recap/Outcome").GetComponent<Text>().text);
            var button = recap.transform.Find("Panels/Recap/Continue").GetComponent<Button>();
            yield return WaitFor(button.IsInteractable, "Continue waits for the destination and its locale blend");
            Assert.AreNotSame(first, host.ActiveSector);
            Assert.AreEqual(1f, host.Jump.State.LocaleBlend);
            Assert.AreEqual(0f, Time.timeScale);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.IsTrue(host.Jump.Holding, "Hyperspace waits for the player.");
            button.onClick.Invoke();
            yield return WaitFor(() => !host.Jump, "Continue completes arrival");
            Assert.IsTrue(rig.Player.Commander.enabled);
            Assert.Greater(rig.Player.Body.linearVelocity.magnitude, 1f, "Control returns with momentum.");
            Assert.AreEqual(.6f, rig.Player.Damage.Health.Pct, .02f);
            yield return EnterProductionZone(rig);
            input.Press(keyboard.fKey);
            InputSystem.Update();
            Assert.IsNull(host.Jump, "Final extraction goes directly to victory.");
            input.Release(keyboard.fKey);
            InputSystem.Update();
            yield return WaitFor(() => hostGo.GetComponentInChildren<RunRecapScreen>(), "victory appears");
            recap = hostGo.GetComponentInChildren<RunRecapScreen>();
            Assert.AreEqual("VICTORY", recap.transform.Find("Panels/Recap/Outcome").GetComponent<Text>().text);
            var record = JsonUtility.FromJson<RunRecord>(File.ReadAllLines(recordPath)[0]);
            Assert.AreEqual(2, record.sectorsCleared);
            yield return host.Shutdown();
        }

        [UnityTest]
        [Category("RequiresGraphics")]
        public IEnumerator JumpTravel_WithAWeaponPool_PicksAfterTheRecap_AndArrivesWithTheSwap()
        {
            PlayerRig rig = null;
            var pool = AssetDatabase.LoadAssetAtPath<ItemSubset>("Assets/Settings/Ships/PlayerLoadout.asset");
            yield return BootToTheFirstSector(built => rig = built, presentation: true, jumpTravel: true,
                hangarOffer: pool);
            rig.Player.Damage.Health.SetFraction(.6f);
            var secondary = rig.Player.Weapons.SecondaryMountPrefab;
            var carried = new[] { rig.Player.Weapons.PrimaryMountPrefab, secondary };
            host.ActiveSector.GetComponent<ExtractOnCueModule>().Extract();
            yield return WaitFor(() => host.Jump && host.Jump.Holding && hostGo.GetComponentInChildren<RunRecapScreen>(),
                "the extracted recap appears in hyperspace");
            var button = hostGo.GetComponentInChildren<RunRecapScreen>().transform.Find("Panels/Recap/Continue")
                .GetComponent<Button>();
            yield return WaitFor(button.IsInteractable, "Continue waits for the destination");
            button.onClick.Invoke();

            yield return WaitFor(() => hostGo.GetComponentInChildren<WeaponPickScreen>(), "the weapon pick follows the recap");
            var pick = hostGo.GetComponentInChildren<WeaponPickScreen>();
            Assert.IsTrue(host.Jump.Holding, "arrival waits for the pick");
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsNull(pick.transform.Find("Ships"), "a sector with no wave director names no threat");
            var offered = pick.transform.Find("Offers/Cards/Card1/Name").GetComponent<Text>().text;
            pick.ChooseOffer(0);
            pick.ChooseSlot(WeaponSlot.Primary);

            yield return WaitFor(() => !host.Jump, "the pick resumes arrival");
            var mounted = rig.Player.Weapons.PrimaryMountPrefab;
            CollectionAssert.DoesNotContain(carried, mounted, "offers are weapons the player does not carry");
            CollectionAssert.Contains(pool.weapons, mounted);
            Assert.AreEqual(offered, mounted.DisplayName.ToUpperInvariant());
            Assert.AreSame(secondary, rig.Player.Weapons.SecondaryMountPrefab);
            Assert.AreEqual(.6f, rig.Player.Damage.Health.Pct, .02f, "hull carries across the swap");
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(hostGo.GetComponentInChildren<WeaponPickScreen>());
        }

        [UnityTest]
        [Category("RequiresGraphics")]
        public IEnumerator FailedSectorWithJumpTravel_TakesTheRecapPathWithoutAJump()
        {
            PlayerRig rig = null;
            yield return BootToTheFirstSector(built => rig = built, presentation: true, jumpTravel: true);
            host.ActiveSector.GetComponent<ExtractOnCueModule>().Fail();
            Assert.IsNull(host.Jump, "a failed result never starts a jump");
            Assert.IsTrue(rig.Player.Commander.enabled);
            yield return WaitFor(() => hostGo.GetComponentInChildren<RunRecapScreen>(), "the sector recap appears");
            hostGo.GetComponentInChildren<RunRecapScreen>().transform.Find("Panels/Recap/Continue")
                .GetComponent<Button>().onClick.Invoke();
            yield return WaitFor(() => InSector("SectorB"), "the next sector loads after the recap");
            Assert.IsNull(host.Jump);
        }

        [UnityTest]
        [Category("RequiresGraphics")]
        public IEnumerator JumpThatFaultsPartway_ReleasesTheShip_AndShutdownSurfacesTheFault()
        {
            PlayerRig rig = null;
            yield return BootToTheFirstSector(built => rig = built, presentation: true, jumpTravel: true);
            var destination = templates[1];
            destination.GetComponent<Sector>().SetManifest(null, null,
                new SectorModule[] { destination.AddComponent<ThrowingSetupModule>() });
            var player = rig.Player;
            var colliders = player.Colliders.Select(collider => collider.enabled).ToArray();
            LogAssert.Expect(LogType.Exception, new Regex(Regex.Escape(ThrowingSetupModule.Fault.Message)));
            host.ActiveSector.GetComponent<ExtractOnCueModule>().Extract();
            Assert.IsNotNull(host.Jump, "extraction commits the jump");
            ThrowingSetupModule fault = null;
            yield return WaitFor(() =>
            {
                fault = host.ActiveSector ? host.ActiveSector.GetComponent<ThrowingSetupModule>() : null;
                return fault && fault.Entered;
            }, "the jump transfers into the destination's setup");
            Assert.AreEqual(0f, Time.timeScale, "the hyperspace recap holds the clock");
            Assert.IsTrue(hostGo.GetComponentInChildren<LocaleBackdropPair>(), "the backdrop is built");
            Assert.IsFalse(player.Commander.enabled, "the jump owns the ship");
            fault.AllowThrow = true;
            yield return WaitFor(() => !host.Jump, "the destination setup faults and the jump exits");
            yield return WaitFor(() => !hostGo.GetComponentInChildren<RunRecapScreen>(), "the hyperspace recap closes");
            Assert.IsTrue(player.Commander.enabled && player.Movement.enabled);
            Assert.That(player.Colliders.Select(collider => collider.enabled), Is.EqualTo(colliders));
            Assert.IsFalse(player.Damage.IsInvulnerable);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(hostGo.GetComponentInChildren<LocaleBackdropPair>(), "the backdrop is destroyed");
            var shutdown = host.Shutdown();
            Assert.That(Assert.Catch(() => shutdown.MoveNext()), Is.SameAs(ThrowingSetupModule.Fault));
            // The faulted host never tears its session down, so its locale stays loaded.
            hostGo.GetComponent<UnitService>().Clear();
            DestroyTestObject(hostGo);
            if (SceneManager.GetSceneByName("Locale_3").isLoaded)
                yield return SceneManager.UnloadSceneAsync("Locale_3");
        }

        private IEnumerator EnterProductionZone(PlayerRig rig)
        {
            var director = host.ActiveSector.GetComponentInChildren<WaveDirector>();
            director.BeginSchedule(Time.time - 500f);
            director.Advance(Time.time);
            host.GetComponent<ObjectiveService>().Tick(0f);
            var zone = host.ActiveSector.GetComponentInChildren<ExtractionZone>();
            Assert.IsNotNull(zone, "Completed production phases expose the extraction zone.");
            Assert.IsTrue(zone.gameObject.activeInHierarchy, "The production zone is active in the loaded sector.");
            rig.Player.transform.position = zone.transform.position;
            rig.Player.Body.position = zone.transform.position;
            rig.Player.Body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(zone.IsPlayerInZone);
        }

        private bool InSector(string templateName) =>
            host.ActiveSector && host.ActiveSector.name.StartsWith(templateName, StringComparison.Ordinal);

        // The screen exists before the HUD fades; showing means a visible banner or card.
        private bool RecapShowing()
        {
            var recap = hostGo.GetComponentInChildren<RunRecapScreen>();
            if (!recap) return false;
            var banner = recap.transform.Find("OutcomeIntro");
            return recap.transform.Find("Panels/Recap").GetComponent<CanvasGroup>().alpha > 0f
                   || banner && banner.gameObject.activeSelf;
        }

        [UnityTest]
        [Category("RequiresGraphics")]
        public IEnumerator Death_CentersTheCameraAndKeepsShipsFlying_ThenRestoresTheNextRun()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            PlayerRig rig = null;
            yield return BootToTheFirstSector(built => rig = built, presentation: true);
            var observer = hostGo.GetComponentInChildren<ObserverCam>();
            var units = hostGo.GetComponent<UnitService>();
            var enemy = units.SpawnShip(TestAssets.LoadCrimsonPrefab(), null, 1,
                rig.Player.transform.position + GamePlane.Right * 50f, GamePlane.Rotation, null);
            observer.SetManualZoom(12f);
            yield return new WaitForSecondsRealtime(.5f);
            var deathPosition = rig.Player.transform.position;
            observer.transform.position = deathPosition + GamePlane.Right * 30f - GamePlane.Normal * 10f;
            var initialZoom = observer.Cam.orthographicSize;
            TestDamage.Kill(rig.Player);
            yield return null;
            yield return new WaitForSecondsRealtime(.4f);
            Assert.AreEqual(0f, rig.Overlay.GetComponent<CanvasGroup>().alpha);
            Assert.Less(Vector2.Distance(GamePlane.WorldPointToPlane(observer.transform.position),
                GamePlane.WorldPointToPlane(deathPosition)), 9f, "the camera moves onto the death location");
            Assert.AreEqual(1f, Time.timeScale, "the explosion keeps simulating");
            Assert.IsFalse(RecapShowing(), "the explosion gets an unobstructed beat");
            yield return AsyncAssert.WaitUntil(RecapShowing, 1.1f, "the banner starts before particle cleanup ends");
            var recap = hostGo.GetComponentInChildren<RunRecapScreen>();
            yield return new WaitForSecondsRealtime(host.recapFeel.BannerInSeconds + .1f);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(initialZoom, observer.Cam.orthographicSize, .1f);
            Assert.Less(Vector2.Distance(GamePlane.WorldPointToPlane(observer.transform.position),
                GamePlane.WorldPointToPlane(deathPosition)), .1f);
            enemy.Body.linearVelocity = GamePlane.Right * 10f;
            var enemyPosition = enemy.transform.position;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.Greater(Vector3.Distance(enemyPosition, enemy.transform.position), .5f,
                "surviving ships keep flying behind the banner");
            var details = recap.transform.Find("Panels/Recap").GetComponent<CanvasGroup>();
            Assert.AreEqual(0f, details.alpha);
            input.Press(keyboard.enterKey);
            yield return null;
            input.Release(keyboard.enterKey);
            yield return AsyncAssert.WaitUntil(() => details.interactable, 1f, "the tuned transition reveals the breakdown");
            Assert.IsFalse(recap.Continued, "the advancing press does not submit the result");
            Assert.AreEqual(1f, Time.timeScale);
            enemy.Body.linearVelocity = GamePlane.Right * 10f;
            enemyPosition = enemy.transform.position;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.Greater(Vector3.Distance(enemyPosition, enemy.transform.position), .5f,
                "surviving ships keep flying behind the breakdown");
            TestDamage.Kill(enemy);
            observer.transform.position += GamePlane.Right * 5f;
            yield return new WaitForSecondsRealtime(.5f);
            Assert.Less(Vector2.Distance(GamePlane.WorldPointToPlane(observer.transform.position),
                GamePlane.WorldPointToPlane(deathPosition)), .1f, "death framing works without active subjects");
            recap.transform.Find("Panels/Recap/Continue").GetComponent<Button>().onClick.Invoke();
            yield return WaitFor(() => rig.Player.gameObject.activeInHierarchy && rig.Tally.SecondsSurvived > 0f,
                "the next run starts");
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsTrue(observer.enabled);
            Assert.AreEqual(12f, observer.ManualZoom);
            Assert.AreEqual(1f, rig.Overlay.GetComponent<CanvasGroup>().alpha);
            Assert.IsTrue(rig.Overlay.GetComponent<Canvas>().enabled);
        }

        private IEnumerator BootToTheFirstSector(Action<PlayerRig> onBuilt, bool presentation = false,
            bool productionJump = false, bool jumpTravel = false, ItemSubset hangarOffer = null)
        {
            var config = AssetDatabase.LoadAssetAtPath<SectorSettings>(productionJump
                ? "Assets/Settings/Game/SurvivalTrialConfig.asset" : SectorConfigPath);
            hostGo = new GameObject("TestHost");
            hostGo.SetActive(false);
            templates = productionJump
                ? Array.Empty<GameObject>()
                : new[] { NewSectorTemplate("SectorA"), NewSectorTemplate("SectorB") };
            foreach (var template in templates) template.SetActive(false);
            foreach (var template in templates) template.transform.SetParent(hostGo.transform);
            host = hostGo.AddComponent<GameHost>();
            host.sessionProfile = new SessionProfile
            {
                sectors = productionJump
                    ? new[] { "TrialSector", "TrialSector2" }.Select(name => new SectorEntry
                    {
                        prefab = AssetDatabase.LoadAssetAtPath<Sector>($"Assets/Prefabs/Sectors/{name}.prefab"),
                        config = config
                    }).ToArray()
                    : Array.ConvertAll(templates, t => new SectorEntry { prefab = t.GetComponent<Sector>(), config = config }),
                presentation = presentation
            };
            host.playerRig = AssetDatabase.LoadAssetAtPath<PlayerRig>(RigPrefabPath);
            host.observerCamPrefab = AssetDatabase.LoadAssetAtPath<ObserverCam>(ObserverCamPrefabPath);
            host.recapFeel = AssetDatabase.LoadAssetAtPath<RecapFeelProfile>(RecapFeelPath);
            host.runRecordPath = recordPath;
            host.hangarOffer = hangarOffer;
            host.buildIdentity = (out BuildIdentity identity, out string failure) =>
            {
                identity = new BuildIdentity { commit = "test-commit" };
                failure = null;
                return true;
            };
            if (productionJump || jumpTravel)
            {
                host.jumpFeel = AssetDatabase.LoadAssetAtPath<SectorJumpProfile>("Assets/Settings/Game/SectorJumpTuning.asset");
                host.jumpFeature = AssetDatabase.LoadAssetAtPath<SectorJumpFeature>("Assets/Settings/Rendering/SectorJumpFeature.asset");
            }
            if (productionJump)
                host.hangarScreenPrefab = AssetDatabase.LoadAssetAtPath<HangarScreen>("Assets/Prefabs/UI/HangarScreen.prefab");
            hostGo.SetActive(true);
            if (productionJump)
            {
                yield return WaitFor(() => hostGo.GetComponentInChildren<HangarScreen>(), "the hangar opens");
                Assert.IsNull(host.Jump);
                hostGo.GetComponentInChildren<HangarScreen>().launchButton.onClick.Invoke();
            }

            PlayerRig rig = null;
            yield return WaitFor(() =>
            {
                rig = hostGo.GetComponentInChildren<PlayerRig>();
                return rig && rig.Tally.SecondsSurvived > 0f && InSector(productionJump ? "TrialSector" : "SectorA");
            }, "the host started a run in the first sector");
            onBuilt(rig);
        }

        // Inactive, so the template never runs; the session instantiates it like a sector prefab.
        private static GameObject NewSectorTemplate(string name)
        {
            var go = new GameObject(name);
            go.SetActive(false);
            var sector = go.AddComponent<Sector>();
            sector.SetManifest(null, null, new SectorModule[] { go.AddComponent<ExtractOnCueModule>() });
            return go;
        }

        private static IEnumerator WaitFor(Func<bool> condition, string what) =>
            AsyncAssert.WaitUntil(condition, StepTimeoutSec, what);
    }
}
#endif
