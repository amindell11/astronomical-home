using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Cameras;
using Game;
using Game.Audio;
using Game.Preview;
using Game.Runs;
using Game.Travel;
using NUnit.Framework;
using Substrate.Sectors;
using Substrate.Sectors.Elements;
using Substrate.Services.Locales;
using Substrate.Services.Units;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using Tests.PlayMode.Common.SimulatedInput;
using UI;
using UI.Hangar;
using UI.Recap;
using UI.Projection;
using UI.Screens;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Preview
{
    [Category("Bootstrap"), Category("RequiresGraphics")]
    public sealed class PreviewPlayModeTests : PlayModeWorldFixture
    {
        private PreviewHost host;
        private GameObject template;
        private PlayModeInputTestFixture input;
        private Keyboard keyboard;
        private Mouse mouse;
        private PlayerRig rig;
        private SectorJumpProfile selectedJump;
        private RecapFeelProfile selectedRecap;
        private SectorJumpProfile handedJump;
        private RecapFeelProfile handedRecap;
        private CanopyProjectionProfile handedCanopy;
        private CanopyProjectionProfile selectedCanopy;

        public override void SetUp()
        {
            base.SetUp();
            input = new PlayModeInputTestFixture();
            input.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator ClosePreview()
        {
            if (host)
            {
                yield return host.Shutdown();
                Object.Destroy(host.gameObject);
            }
            if (template) Object.Destroy(template);
            if (selectedJump) Object.Destroy(selectedJump);
            if (selectedRecap) Object.Destroy(selectedRecap);
            if (handedJump) Object.Destroy(handedJump);
            if (handedRecap) Object.Destroy(handedRecap);
            if (handedCanopy) Object.Destroy(handedCanopy);
            if (selectedCanopy) Object.Destroy(selectedCanopy);
            yield return null;
            input.TearDown();
        }

        [UnityTest]
        public IEnumerator FlightAndEnemyDamage_UseProductionControlsRecapAndIsolatedRecords()
        {
            var normalPath = new RunRecordStore().Path;
            var normalContents = File.Exists(normalPath) ? File.ReadAllBytes(normalPath) : null;
            BuildPreview();
            yield return WaitForFlight();
            var game = host.Playtest;
            var player = rig.Player;
            var start = player.transform.position;
            var shots = 0;
            player.Weapons.Fired += () => shots++;
            input.Set(mouse.position, new Vector2(Screen.width * .5f, Screen.height * .8f));
            input.Press(keyboard.wKey);
            input.Press(mouse.leftButton);
            yield return new WaitForSecondsRealtime(.4f);
            input.Release(keyboard.wKey);
            input.Release(mouse.leftButton);
            yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Distance(start, player.transform.position), Is.GreaterThan(.1f));
            Assert.That(shots, Is.GreaterThan(0));
            Assert.That(host.Subject, Is.Null, "inspection never drives the live game");

            var enemy = game.GetComponent<UnitService>().SpawnShip(TestAssets.LoadCrimsonPrefab(),
                TestAssets.LoadTestPilotMpc(), player.teamNumber + 1, player.transform.position + Vector3.right * 40f,
                Quaternion.identity, Field);
            enemy.Commander.enabled = false;
            enemy.Movement.enabled = false;
            enemy.Rigidbody.linearVelocity = Vector3.right * 5f;
            var enemyStart = enemy.transform.position;
            TestDamage.Kill(player, enemy);
            yield return WaitForBanner();
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Vector3.Distance(enemyStart, enemy.transform.position), Is.GreaterThan(.1f));
            Assert.That(File.Exists(game.runRecordPath), Is.True);
            Assert.That(game.runRecordPath, Is.Not.EqualTo(normalPath));
            var recap = game.GetComponentInChildren<RunRecapScreen>();
            yield return AdvanceToBreakdown(recap);
            var button = recap.transform.Find("Panels/Recap/Continue").GetComponent<Button>();
            var rect = (RectTransform)button.transform;
            input.Set(mouse.position, RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)));
            yield return null;
            input.Press(mouse.leftButton);
            yield return null;
            input.Release(mouse.leftButton);
            yield return WaitForFlight();
            Assert.That(recap == null, Is.True, "a real UI click completed the recap");
            Assert.That(File.ReadAllLines(game.runRecordPath).Length, Is.EqualTo(1));
            if (normalContents == null) Assert.That(File.Exists(normalPath), Is.False);
            else Assert.That(File.ReadAllBytes(normalPath), Is.EqualTo(normalContents));
        }

        [UnityTest]
        public IEnumerator RestartAndModeSwitch_ReleaseRecapInputAndInspectionLocaleOwnership()
        {
            BuildPreview();
            yield return WaitForFlight();
            var combatHost = host.Playtest;
            host.RestartPlaytest();
            yield return AsyncAssert.WaitUntil(() => host.Playtest != combatHost, 30f, "combat restart replaces the run");
            yield return WaitForFlight();
            Assert.That(combatHost == null, Is.True);
            foreach (var breakdown in new[] { false, true })
            {
                var previous = host.Playtest;
                var previousPlayer = rig.Player;
                TestDamage.Kill(previousPlayer);
                yield return WaitForBanner();
                if (breakdown)
                    yield return AdvanceToBreakdown(previous.GetComponentInChildren<RunRecapScreen>());
                host.RestartPlaytest();
                yield return AsyncAssert.WaitUntil(() => host.Playtest != previous, 30f, "a fresh GameHost replaces the run");
                yield return WaitForFlight();
                Assert.That(previous == null, Is.True);
                Assert.That(previousPlayer == null, Is.True);
                Assert.That(Object.FindObjectsByType<GameHost>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                Assert.That(InputSystem.ListEnabledActions().Count(action => action.name == "Continue"), Is.Zero);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }

            host.SelectMode(PreviewMode.Inspection);
            yield return AsyncAssert.WaitUntil(() => host.IsReady && host.Subject != null, 30f, "jump inspection composes");
            Assert.That(LocaleInstances.Loaded("Locale_1"), Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            host.Transport.Playing = false;
            host.Transport.Scrub(0, 3f);
            yield return null;
            var state = ((SectorJumpPreviewSubject)host.Subject).State;
            host.Transport.Scrub(0, 1f);
            yield return null;
            host.Transport.Scrub(0, 3f);
            yield return null;
            Assert.That(((SectorJumpPreviewSubject)host.Subject).State, Is.EqualTo(state));

            selectedJump = Object.Instantiate((SectorJumpProfile)host.Subject.Knobs);
            host.Subject.Knobs = selectedJump;
            host.SelectSubject(PreviewSubjectKind.RunRecap);
            yield return AsyncAssert.WaitUntil(() => host.IsReady, 30f, "recap inspection composes");
            selectedRecap = Object.Instantiate((RecapFeelProfile)host.Subject.Knobs);
            host.Subject.Knobs = selectedRecap;
            host.SelectSubject(PreviewSubjectKind.SectorJump);
            yield return AsyncAssert.WaitUntil(() => host.IsReady, 30f, "jump inspection returns");
            Assert.That(host.Subject.Knobs, Is.SameAs(selectedJump));

            host.SelectMode(PreviewMode.Playtest);
            yield return WaitForFlight();
            Assert.That(host.RecapFeel, Is.SameAs(selectedRecap));
            Assert.That(LocaleInstances.Loaded("Locale_1"), Is.False);
            Assert.That(LocaleInstances.Shown(), Is.EqualTo("Locale_3"));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(Object.FindObjectsByType<GameHost>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(InputSystem.ListEnabledActions().Count(action => action.name == "Thrust"), Is.EqualTo(1));
            host.SelectMode(PreviewMode.Inspection);
            yield return AsyncAssert.WaitUntil(() => host.IsReady, 30f, "inspection returns after playtest");
            Assert.That(host.Subject.Knobs, Is.SameAs(selectedJump));
        }

        [UnityTest]
        public IEnumerator Playtest_RunsTheHandedFeels_AndAJumpFeelChangeRestartsCombat()
        {
            BuildPreview();
            yield return WaitForFlight();
            var combat = host.Playtest;
            Assert.That(combat.recapFeel, Is.SameAs(handedRecap), "Playtest runs the feels its builder hands in");
            Assert.That(combat.jumpFeel, Is.SameAs(handedJump));

            var jump = TestAssets.Load<SectorJumpProfile>("Assets/Settings/Game/SectorJumpTuning.asset");
            host.RestartPlaytest(jump);
            yield return AsyncAssert.WaitUntil(() => host.IsReady && host.Playtest != combat, 30f,
                "a jump feel change restarts Combat");
            Assert.That(host.Playtest.jumpFeel, Is.SameAs(jump));
            Assert.That(host.Playtest.recapFeel, Is.SameAs(handedRecap), "the restart keeps the recap feel");
        }

        [UnityTest]
        public IEnumerator CanopyWorkingProfile_ControlsForcedStatesAndBothOffSwitches_AndSelectionRestartsCombat()
        {
            BuildPreview();
            yield return WaitForFlight();
            var combat = host.Playtest;
            var projection = combat.CanopyProjection;
            Assert.That(projection.Profile, Is.SameAs(handedCanopy));
            var authored = rig.AuthoredCanopyProfile;
            var authoredSettings = authored.baseSettings;
            handedCanopy.lowHull.fields = CanopyFields.Glow;
            handedCanopy.lowHull.settings.glow = 0f;
            handedCanopy.lowHull.blendInSeconds = 0f;
            projection.ForceMode = CanopyForceMode.LowHull;
            yield return null;
            yield return null;
            Assert.That(projection.ActiveState, Is.EqualTo(CanopyState.LowHull));
            Assert.That(projection.Settings.glow, Is.Zero);
            Assert.That(projection.Settings.aberration, Is.EqualTo(handedCanopy.baseSettings.aberration));
            Assert.That(authored.baseSettings, Is.EqualTo(authoredSettings));
            handedCanopy.personalityEnabled = false;
            Assert.That(projection.EffectsEnabled, Is.False);
            handedCanopy.personalityEnabled = true;
            projection.PersonalityEnabled = false;
            Assert.That(projection.EffectsEnabled, Is.False);
            projection.PersonalityEnabled = true;
            Assert.That(projection.EffectsEnabled, Is.True);

            selectedCanopy = Object.Instantiate(authored);
            selectedCanopy.baseSettings.glow = .17f;
            host.RestartPlaytest(selectedCanopy);
            yield return AsyncAssert.WaitUntil(() => host.IsReady && host.Playtest != combat, 30f,
                "a canopy profile change restarts Combat");
            yield return WaitForFlight();
            Assert.That(host.CanopyFeel, Is.SameAs(selectedCanopy));
            Assert.That(host.Playtest.CanopyProjection.Profile, Is.SameAs(selectedCanopy));
            Assert.That(host.Playtest.CanopyProjection.Settings.glow, Is.EqualTo(.17f));
            Assert.That(host.Playtest.CanopyProjection.ForceMode, Is.EqualTo(CanopyForceMode.Automatic));
        }

        [UnityTest]
        public IEnumerator InspectionExtractionRecap_LeavesTheHudAndClockToTheJump()
        {
            BuildPreview(PreviewMode.Inspection, PreviewSubjectKind.RunRecap);
            yield return AsyncAssert.WaitUntil(() => host.IsReady, 45f, "recap inspection composes");
            host.Transport.Playing = false;
            var recap = (RecapPreviewSubject)host.Subject;
            var hud = host.GetComponentInChildren<PlayerRig>().Overlay.GetComponent<CanvasGroup>();
            var extractions = Enumerable.Range(0, recap.Fixtures.Count)
                .Where(fixture => recap.Fixtures[fixture].StartsWith("Extraction")).ToArray();
            Assert.That(extractions, Is.Not.Empty);
            foreach (var fixture in extractions)
            {
                recap.SelectFixture(fixture);
                for (var span = 0; span < recap.SpanCount; span++)
                {
                    host.Transport.Scrub(span, host.Transport.Length(recap.Span(span)));
                    yield return null;
                    var at = $"{recap.Fixtures[fixture]}, {recap.Span(span).Name}";
                    Assert.That(Time.timeScale, Is.EqualTo(1f), at);
                    Assert.That(hud.alpha, Is.EqualTo(1f), at);
                }
            }
        }

        [UnityTest]
        public IEnumerator ComposeFault_FaultsTheHost_AndModeSwitchRestartAndShutdownRefuseInsteadOfWaiting()
        {
            LogAssert.Expect(LogType.Exception, new Regex("requires a feel profile and shader"));
            var bootLighting = LightingSnapshot.Capture();
            BuildPreview(PreviewMode.Inspection, PreviewSubjectKind.SectorJump, jumpShader: false);
            host.SelectMode(PreviewMode.Playtest);
            yield return AsyncAssert.WaitUntil(() => host.Fault != null, 45f, "the subject's compose faults the host");
            yield return null;
            Assert.That(host.Fault, Is.TypeOf<System.InvalidOperationException>());
            Assert.That(host.IsReady, Is.False);
            Assert.That(host.Playtest, Is.Null, "the mode switch waiting on the compose stops at its fault");
            Assert.Throws<System.InvalidOperationException>(() => host.SelectMode(PreviewMode.Inspection));
            Assert.Throws<System.InvalidOperationException>(() => host.RestartPlaytest());
            Assert.Throws<System.InvalidOperationException>(() => host.SelectSubject(PreviewSubjectKind.RunRecap));
            var shutdown = host.Shutdown();
            Assert.That(Assert.Catch(() => shutdown.MoveNext()), Is.SameAs(host.Fault));
            // The faulted host never tears its session down, so it never restores the boot lighting.
            var faulted = host.gameObject;
            host = null;
            faulted.GetComponent<UnitService>().Clear();
            Object.Destroy(faulted);
            yield return null;
            bootLighting.Restore();
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Playtest_RunsTheShippedGameHost_AndTheCrowdedGateSwapsOnlyItsSectors()
        {
            var shipped = TestAssets.Load<GameHost>("Assets/Prefabs/Game/GameHost.prefab");
            var root = new GameObject("Shipped playtest test");
            root.SetActive(false);
            host = root.AddComponent<PreviewHost>();
            host.mode = PreviewMode.Playtest;
            host.gameHostPrefab = shipped;
            host.recapFeel = shipped.recapFeel;
            host.jumpFeel = shipped.jumpFeel;
            host.sessionProfile = new SessionProfile();
            host.crowdedJumpSectors = new[] { "CrowdedJumpDeparture", "CrowdedJumpArrival" }.Select(name => new SectorEntry
            {
                prefab = TestAssets.Load<Sector>($"Assets/Prefabs/Sectors/Preview/{name}.prefab"),
                config = TestAssets.Load<SectorSettings>($"Assets/Settings/Game/Preview/{name}.asset")
            }).ToArray();
            root.SetActive(true);

            var combat = host.Playtest;
            yield return LaunchFromHangar(combat);
            Assert.That(combat.sessionProfile.sectors.Select(entry => entry.prefab),
                Is.EqualTo(shipped.sessionProfile.sectors.Select(entry => entry.prefab)));
            Assert.IsTrue(shipped.jumpFeel, "Combat travels between sectors");
            Assert.That(combat.jumpFeel, Is.SameAs(shipped.jumpFeel));
            Assert.IsTrue(combat.GetComponentInChildren<GameMusic>(), "the shipped music plays");
            Assert.IsTrue(combat.GetComponentInChildren<LoadingSplash>(true), "the shipped splash covers loads");
            Assert.That(combat.runRecordPath, Is.Not.EqualTo(new RunRecordStore().Path));
            Assert.That(combat.ActiveSector.name, Does.StartWith(shipped.sessionProfile.sectors[0].prefab.name));

            host.SelectPlaytestSetup(PreviewPlaytestSetup.CrowdedJumpGate);
            yield return AsyncAssert.WaitUntil(() => host.IsReady && host.Playtest != combat, 30f,
                "the crowded gate replaces the run");
            var crowded = host.Playtest;
            yield return LaunchFromHangar(crowded);
            Assert.That(combat == null, Is.True);
            Assert.That(crowded.sessionProfile.sectors, Is.SameAs(host.crowdedJumpSectors));
            Assert.That(crowded.sessionProfile.idleLocale, Is.SameAs(shipped.sessionProfile.idleLocale));
            Assert.That(crowded.ActiveSector.name, Does.StartWith("CrowdedJumpDeparture"));
            Assert.That(Object.FindObjectsByType<GameHost>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }

        private static IEnumerator LaunchFromHangar(GameHost game)
        {
            HangarScreen hangar = null;
            yield return AsyncAssert.WaitUntil(() => (hangar = game.GetComponentInChildren<HangarScreen>()) != null, 45f,
                "the shipped run opens in the hangar");
            Assert.That(LocaleInstances.Shown(), Is.EqualTo(game.sessionProfile.idleLocale.name),
                "the idle locale shows behind the hangar");
            hangar.launchButton.onClick.Invoke();
            yield return AsyncAssert.WaitUntil(() => game.ActiveSector != null, 45f, "Launch loads the first sector");
        }

        private IEnumerator WaitForFlight() => AsyncAssert.WaitUntil(() =>
        {
            if (!host.Playtest) return false;
            rig = host.Playtest.GetComponentInChildren<PlayerRig>();
            return rig && rig.Player && rig.Player.gameObject.activeInHierarchy && rig.Tally.SecondsSurvived > 0f;
        }, 45f, "the production player is flying");

        private IEnumerator WaitForBanner() => AsyncAssert.WaitUntil(() =>
        {
            var screen = host.Playtest.GetComponentInChildren<RunRecapScreen>();
            return screen && screen.transform.Find("OutcomeIntro/ContinueHint").gameObject.activeInHierarchy;
        }, 30f, "the recap waits for a fresh press");

        private IEnumerator AdvanceToBreakdown(RunRecapScreen screen)
        {
            yield return null;
            input.Press(keyboard.tKey);
            yield return null;
            input.Release(keyboard.tKey);
            yield return AsyncAssert.WaitUntil(() => screen.transform.Find("Panels/Recap").GetComponent<CanvasGroup>().interactable,
                15f, "the real recap consumes the fresh press");
        }

        private void BuildPreview(PreviewMode mode = PreviewMode.Playtest,
            PreviewSubjectKind subject = PreviewSubjectKind.SectorJump, bool jumpShader = true)
        {
            template = new GameObject("Preview test templates");
            template.SetActive(false);
            var sectorObject = new GameObject("Playtest selected setup");
            sectorObject.SetActive(false);
            sectorObject.transform.SetParent(template.transform, false);
            var sector = sectorObject.AddComponent<Sector>();
            sector.SetManifest(null, null, System.Array.Empty<SectorModule>());
            var profile = new SessionProfile
            {
                sectors = new[]
                {
                    new SectorEntry
                    {
                        prefab = sector,
                        config = TestAssets.Load<SectorSettings>("Assets/Settings/Game/SurvivalTrialConfig.asset")
                    }
                }
            };
            var game = new GameObject("Playtest game host");
            game.transform.SetParent(template.transform, false);
            var gameHost = game.AddComponent<GameHost>();
            gameHost.sessionProfile = profile;
            gameHost.playerRig = TestAssets.Load<PlayerRig>("Assets/Prefabs/MiscObjects/PlayerRig.prefab");
            gameHost.observerCamPrefab = TestAssets.Load<ObserverCam>("Assets/Prefabs/Cameras/Main Camera.prefab");
            gameHost.recapFeel = TestAssets.Load<RecapFeelProfile>("Assets/Settings/UI/RecapFeel.asset");
            gameHost.jumpFeel = TestAssets.Load<SectorJumpProfile>("Assets/Settings/Game/SectorJumpTuning.asset");
            gameHost.jumpShader = jumpShader ? Shader.Find("Astronomical/Travel/SectorJump") : null;
            var root = new GameObject("Preview lifecycle test");
            root.SetActive(false);
            host = root.AddComponent<PreviewHost>();
            host.mode = mode;
            host.subjectKind = subject;
            host.gameHostPrefab = gameHost;
            host.sessionProfile = profile;
            host.jumpDestination = TestAssets.Load<LocaleSky>("Assets/Prefabs/Locales/Locale_1.prefab");
            host.recapFeel = handedRecap = Object.Instantiate(gameHost.recapFeel);
            host.jumpFeel = handedJump = Object.Instantiate(gameHost.jumpFeel);
            host.canopyFeel = handedCanopy = Object.Instantiate(gameHost.playerRig.AuthoredCanopyProfile);
            root.SetActive(true);
        }
    }
}
