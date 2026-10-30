using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Asteroids;
using Cameras;
using Combat.Projectiles;
using Game;
using Game.Preview;
using Game.Preview.Fixtures;
using Game.Travel;
using NUnit.Framework;
using Objectives;
using Ships;
using Substrate;
using Substrate.Sectors;
using Substrate.Services.Locales;
using Substrate.Services.Objectives;
using Substrate.Services.Units;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using Tests.PlayMode.Common.SimulatedInput;
using UI.Recap;
using UI.Screens;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Preview
{
    [Category("Sectors"), Category("RequiresGraphics")]
    public sealed class CrowdedJumpPlayModeTests : PlayModeWorldFixture
    {
        private PreviewHost host;
        private PlayerRig rig;
        private SectorJumpProfile feel;
        private PlayModeInputTestFixture input;
        private Keyboard keyboard;
        private Mouse mouse;
        private readonly List<GameObject> extras = new();

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
            foreach (var extra in extras)
                if (extra) Object.Destroy(extra);
            extras.Clear();
            if (feel) Object.Destroy(feel);
            yield return null;
            input.TearDown();
        }

        [UnityTest]
        public IEnumerator FreshPressCommitsImmediately_AndIncomingShotAndRockPassThrough()
        {
            BuildPreview(turnSeconds: 3f);
            yield return WaitForFlight();
            var player = rig.Player;
            var zone = host.Playtest.ActiveSector.GetComponentInChildren<ExtractionZone>();
            MovePlayer(zone.transform.position + Vector3.down * 75f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(zone.IsPlayerInZone, Is.False);
            input.Press(keyboard.fKey);
            InputSystem.Update();
            Assert.That(host.Playtest.Jump, Is.Null, "F outside cannot commit");
            MovePlayer(zone.transform.position + Vector3.down * 18f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(zone.IsPlayerInZone, Is.True);
            Assert.That(host.Playtest.Jump, Is.Null, "entering while F is held needs a fresh press");
            Assert.That(host.Playtest.GetComponent<ObjectiveService>().SpineTracker.CurrentStatusLine,
                Is.EqualTo("PRESS F TO JUMP"));
            input.Release(keyboard.fKey);
            InputSystem.Update();
            var hull = player.Damage.Health.CurrentValue;
            var shield = player.Damage.Shield.CurrentValue;
            input.Press(keyboard.fKey);
            InputSystem.Update();
            Assert.That(host.Playtest.Jump, Is.Not.Null, "commit is synchronous with the input callback");
            AssertProtected(player);
            input.Release(keyboard.fKey);
            InputSystem.Update();

            var units = host.Playtest.GetComponent<UnitService>();
            var enemy = units.ActiveRegistry.ActiveShips.First(ship => ship != player);
            var crossing = player.Body.position;
            var shot = Object.Instantiate(TestAssets.Load<Laser>("Assets/Prefabs/Weapons/Projectiles/Laser.prefab"),
                crossing - Vector3.right * 8f, GamePlane.Rotation);
            extras.Add(shot.gameObject);
            shot.Initialize(enemy);
            shot.Launch(Vector3.right, crossing);
            shot.rb.linearVelocity = Vector3.right * 800f;
            var hitPlayer = false;
            shot.Hit += (_, target) => hitPlayer |= ReferenceEquals(target, player.Damage);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(shot.transform.position.x, Is.GreaterThan(crossing.x + 3f),
                "the shot crosses the player before any impact farther into the asteroid field");
            Assert.That(hitPlayer, Is.False);
            Object.Destroy(shot.gameObject);

            var missile = Object.Instantiate(TestAssets.Load<Missile>("Assets/Prefabs/Weapons/Projectiles/MissileProjectile.prefab"),
                crossing - Vector3.right * 8f, Quaternion.LookRotation(GamePlane.Normal, Vector3.right));
            extras.Add(missile.gameObject);
            missile.Initialize(enemy);
            missile.SetTarget(player);
            missile.Launch(Vector3.right, crossing);
            var missileHitPlayer = false;
            missile.Hit += (_, target) => missileHitPlayer |= ReferenceEquals(target, player.Damage);
            yield return AsyncAssert.WaitUntil(() => missileHitPlayer || missile.transform.position.x > crossing.x + 3f,
                2f, "the missile crosses the protected ship", useFixedUpdate: true);
            Assert.That(missileHitPlayer, Is.False, "the missile must not detonate against the protected ship");
            Object.Destroy(missile.gameObject);

            var rock = host.Playtest.ActiveSector.GetComponentInChildren<AsteroidController>();
            Assert.That(rock.SimpleCollider.enabled, Is.True, "the obstruction remains physically solid");
            rock.transform.position = crossing - Vector3.right * 10f;
            rock.Rb.position = rock.transform.position;
            rock.UpdateKinematics(Vector3.right * 100f, Vector3.zero);
            Physics.SyncTransforms();
            yield return AsyncAssert.WaitUntil(() => rock.Rb.position.x > crossing.x + 10f,
                2f, "the solid asteroid crosses the protected ship", useFixedUpdate: true);
            Assert.That(player.Damage.Health.CurrentValue, Is.EqualTo(hull));
            Assert.That(player.Damage.Shield.CurrentValue, Is.GreaterThanOrEqualTo(shield));
            Assert.That(Vector3.Distance(player.Body.position, crossing), Is.LessThan(.1f),
                "the passing asteroid cannot shove the turning ship");
        }

        [UnityTest]
        public IEnumerator Inspection_ArrivalSpeedChangesTheVisibleCoast()
        {
            BuildPreview();
            yield return WaitForFlight();
            host.SelectMode(PreviewMode.Inspection);
            yield return AsyncAssert.WaitUntil(() => host.IsReady, 30f, "jump inspection composes");
            host.Transport.Playing = false;
            var camera = host.GetComponentsInChildren<Camera>().Single(item => item.name == "Jump sky camera");
            var subject = host.Subject;
            var settled = feel.worldBlendDuration + feel.settleDuration;
            feel.arrivalSpeedFraction = 0f;
            subject.Knobs = feel;
            subject.Show(1, settled - .1f, 0f);
            var slowStart = camera.transform.position;
            subject.Show(1, settled, 0f);
            var slowDistance = Vector3.Distance(slowStart, camera.transform.position);
            feel.arrivalSpeedFraction = 1f;
            subject.Knobs = feel;
            subject.Show(1, settled - .1f, 0f);
            var fastStart = camera.transform.position;
            subject.Show(1, settled, 0f);
            var fastDistance = Vector3.Distance(fastStart, camera.transform.position);
            Assert.That(fastDistance, Is.GreaterThan(slowDistance + .1f),
                "Inspection must show the arrival speed selected in the feel profile");
        }

        [UnityTest]
        public IEnumerator Inspection_PlaysTheGamesJumpStates_ForTheGamesHold()
        {
            BuildPreview();
            yield return WaitForFlight();
            var game = host.Playtest;
            PressJump();
            var flight = game.Jump;
            var played = new List<SectorJumpState>();
            IEnumerator Record()
            {
                while (flight)
                {
                    played.Add(flight.State);
                    yield return null;
                }
            }
            host.StartCoroutine(Record());
            yield return WaitForHold();
            yield return ClickContinue(game.GetComponentInChildren<RunRecapScreen>());
            yield return AsyncAssert.WaitUntil(() => !flight, 30f, "travel finishes after Continue");

            host.SelectMode(PreviewMode.Inspection);
            yield return AsyncAssert.WaitUntil(() => host.IsReady, 30f, "jump inspection composes");
            host.Transport.Playing = false;
            var subject = (SectorJumpPreviewSubject)host.Subject;
            var holdStarts = subject.Span(0).Seconds;
            // Every held state repeats the hold's travel progress; the last one is the frame before Continue.
            var holding = played.First(state => state.Seconds >= holdStarts).TravelProgress;
            var resume = played.Last(state => state.Seconds >= holdStarts && state.TravelProgress == holding).Seconds;
            host.Transport.HoldSeconds = resume - holdStarts;
            Assert.That(host.Transport.HoldSeconds, Is.GreaterThan(0f), "the game held in hyperspace");
            foreach (var expected in played)
            {
                if (expected.Seconds <= resume) host.Transport.Scrub(0, expected.Seconds);
                else host.Transport.Scrub(1, expected.Seconds - resume);
                yield return null;
                AssertSameTimeline(expected, subject.State);
            }
        }

        [UnityTest]
        public IEnumerator Motion_KeepsDriftingThroughCharge_AndArrivesMovingBeforeControlReturns()
        {
            BuildPreview(turnSeconds: .15f);
            feel.chargeDuration = 1f;
            feel.worldBlendDuration = .5f;
            feel.settleDuration = .6f;
            feel.arrivalSpeedFraction = .5f;
            yield return WaitForFlight();
            var player = rig.Player;
            var incoming = Vector3.down * 6f;
            player.Body.linearVelocity = incoming;
            PressJump();
            var committedVelocity = player.Body.linearVelocity;
            yield return new WaitForSecondsRealtime(.3f);
            var duringCharge = player.Body.position;
            yield return new WaitForSecondsRealtime(.15f);
            var chargeDrift = Vector3.Dot(player.Body.position - duringCharge, incoming.normalized);
            yield return WaitForHold();
            yield return ClickContinue(host.Playtest.GetComponentInChildren<RunRecapScreen>());
            yield return new WaitForSecondsRealtime(.3f);
            var arrivalVelocity = player.Body.linearVelocity.magnitude;
            var protectedArrival = !player.Commander.enabled;
            var lastVelocity = arrivalVelocity;
            while (host.Playtest.Jump)
            {
                lastVelocity = player.Body.linearVelocity.magnitude;
                yield return null;
            }
            var failures = new List<string>();
            if (Vector3.Distance(committedVelocity, incoming) > .1f) failures.Add("commit discarded incoming velocity");
            if (chargeDrift <= .1f) failures.Add("drift stopped before charging finished");
            if (!protectedArrival || arrivalVelocity < 1f) failures.Add("arrival is stationary before control returns");
            if (Mathf.Abs(player.Body.linearVelocity.magnitude - lastVelocity) > .5f)
                failures.Add("control return changed velocity abruptly");
            Assert.That(failures, Is.Empty, string.Join("; ", failures));
        }

        [UnityTest]
        public IEnumerator HyperspaceHoldsUntilRealContinue_ThenArrivesWithSameHullAndMomentum()
        {
            BuildPreview();
            yield return WaitForFlight();
            var game = host.Playtest;
            var player = rig.Player;
            var colliderStates = player.Colliders.Select(collider => collider.enabled).ToArray();
            var enemies = game.GetComponent<UnitService>().ActiveRegistry.ActiveShips.Where(ship => ship != player).ToArray();
            player.Damage.Health.SetFraction(.65f);
            var hull = player.Damage.Health.CurrentValue;
            PressJump();
            yield return WaitForHold();
            var flight = game.Jump;
            var distance = flight.State.Distance;
            var direction = GamePlane.PlaneDirToWorld(flight.State.JumpDirection);
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(game.Jump, Is.SameAs(flight));
            Assert.That(flight.Holding, Is.True);
            Assert.That(flight.State.Distance, Is.GreaterThan(distance));
            Assert.That(flight.State.LocaleBlend, Is.EqualTo(1f));
            Assert.That(LocaleInstances.Shown(), Is.EqualTo("Locale_1"));
            AssertProtected(player);

            var recap = game.GetComponentInChildren<RunRecapScreen>();
            yield return ClickContinue(recap);
            yield return AsyncAssert.WaitUntil(() => !game.Jump, 30f, "travel finishes after Continue");
            yield return null;
            Assert.That(rig.Player, Is.SameAs(player));
            Assert.That(player.Damage.Health.CurrentValue, Is.EqualTo(hull).Within(.001f));
            Assert.That(Vector3.Dot(player.Body.linearVelocity, direction), Is.GreaterThan(1f));
            Assert.That(Vector3.Dot(player.transform.up, direction), Is.GreaterThan(.99f));
            Assert.That(player.Commander.enabled && player.Movement.enabled, Is.True);
            Assert.That(player.Colliders.Select(collider => collider.enabled), Is.EqualTo(colliderStates));
            Assert.That(player.Damage.IsInvulnerable, Is.False);
            Assert.That(rig.Overlay.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(enemies.All(enemy => !enemy), Is.True);
            Assert.That(game.ActiveSector.GetComponent<CrowdedJumpGateModule>(), Is.Null);
            Assert.That(recap == null, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(LocaleInstances.Shown(), Is.EqualTo("Locale_1"));
            yield return AsyncAssert.WaitUntil(() => !LocaleInstances.Loaded("Locale_3"),
                15f, "the outgoing locale is destroyed after arrival");
        }

        [UnityTest]
        public IEnumerator PreloadedDestination_GradesTheWorldOnlyAfterArrival()
        {
            BuildPreview();
            yield return WaitForFlight();
            var game = host.Playtest;
            PressJump();
            var flight = game.Jump;
            var preloaded = false;
            var strays = new HashSet<string>();
            IEnumerator Watch()
            {
                while (flight)
                {
                    var shown = LocaleInstances.Shown();
                    preloaded |= shown == "Locale_3" && LocaleInstances.Loaded("Locale_1");
                    foreach (var locale in LocaleInstances.All())
                        if (!locale.Shown && locale.GetComponentsInChildren<Volume>().Any(volume => volume.enabled))
                            strays.Add($"{locale.name} while {shown} is shown");
                    yield return null;
                }
            }
            host.StartCoroutine(Watch());
            yield return WaitForHold();
            yield return ClickContinue(game.GetComponentInChildren<RunRecapScreen>());
            yield return AsyncAssert.WaitUntil(() => !flight, 30f, "travel finishes after Continue");
            Assert.That(preloaded, Is.True, "test premise: the destination loads before the locale switch");
            Assert.That(strays, Is.Empty, "only the shown locale's volume grades the world");
            Assert.That(LocaleInstances.All().Where(locale => locale.name == "Locale_1")
                    .SelectMany(locale => locale.GetComponentsInChildren<Volume>()).Select(volume => volume.enabled),
                Is.EqualTo(new[] { true }), "the destination's volume grades the world after arrival");
        }

        [UnityTest]
        public IEnumerator Handoff_FadesTheCard_PreparesTheDestination_AndPreservesCameraScale()
        {
            BuildPreview();
            feel.travelDuration = 4f;
            feel.worldBlendDuration = .4f;
            yield return WaitForFlight();
            var game = host.Playtest;
            var observer = game.GetComponentInChildren<ObserverCam>();
            PressJump();
            yield return WaitForHold();
            var recap = game.GetComponentInChildren<RunRecapScreen>();
            var card = recap.GetComponentInChildren<CanvasGroup>();
            var fadesIn = card.alpha < .99f;
            yield return new WaitForSecondsRealtime(1f);
            var prepared = game.ActiveSector.GetComponent<CrowdedJumpGateModule>() == null;
            var blended = game.Jump.State.LocaleBlend;
            var strayLights = LocaleInstances.All().Where(locale => locale.name == "Locale_3")
                .SelectMany(locale => locale.GetComponentsInChildren<Light>()).Where(light => light.enabled).ToArray();
            var clickedAt = Time.realtimeSinceStartup;
            yield return ClickContinue(recap);
            var fadesOut = false;
            var sizeBeforeRelease = observer.Cam.orthographicSize;
            while (game.Jump && Time.realtimeSinceStartup - clickedAt < 10f)
            {
                if (card && card.alpha > 0f && card.alpha < .99f) fadesOut = true;
                sizeBeforeRelease = observer.Cam.orthographicSize;
                yield return null;
            }
            var exitSeconds = Time.realtimeSinceStartup - clickedAt;
            yield return new WaitForSecondsRealtime(.4f);
            var failures = new List<string>();
            if (!fadesIn) failures.Add("the extraction card must start transparent");
            if (!prepared) failures.Add("destination content must load while the card appears");
            if (blended != 1f) failures.Add("the locale blend must complete during the hold");
            if (strayLights.Length != 0) failures.Add("outgoing lights must not leak into the destination");
            if (!fadesOut) failures.Add("Continue must fade the card");
            if (exitSeconds >= 1.4f) failures.Add($"exit took {exitSeconds:F2}s after Continue");
            if (Mathf.Abs(observer.Cam.orthographicSize - sizeBeforeRelease) > .1f)
                failures.Add($"controls changed camera size from {sizeBeforeRelease:F2} to {observer.Cam.orthographicSize:F2}");
            Assert.That(failures, Is.Empty, string.Join("; ", failures));
        }

        [UnityTest]
        public IEnumerator RestartDuringDepartureAndHold_ThenShutdownDuringArrival_ReleasesOwnedState()
        {
            BuildPreview();
            yield return WaitForFlight();
            for (var hold = 0; hold < 2; hold++)
            {
                var previous = host.Playtest;
                PressJump();
                if (hold == 1) yield return WaitForHold();
                host.RestartPlaytest();
                yield return AsyncAssert.WaitUntil(() => host.Playtest != previous, 30f, "the interrupted run is replaced");
                yield return WaitForFlight();
                Assert.That(previous == null, Is.True);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(LocaleInstances.Loaded("Locale_1"), Is.False);
                Assert.That(LocaleInstances.Shown(), Is.EqualTo("Locale_3"));
                Assert.That(InputSystem.ListEnabledActions().Count(action => action.name == "Jump"), Is.EqualTo(1));
                Assert.That(Object.FindObjectsByType<GameHost>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            }

            PressJump();
            yield return WaitForHold();
            yield return ClickContinue(host.Playtest.GetComponentInChildren<RunRecapScreen>());
            yield return AsyncAssert.WaitUntil(() => host.Playtest.Jump && !host.Playtest.Jump.Holding &&
                LocaleInstances.Shown() == "Locale_1", 30f, "arrival has begun");
            AssertProtected(rig.Player);
            yield return host.Shutdown();
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(LocaleInstances.Loaded("Locale_1"), Is.False);
            Assert.That(LocaleInstances.Loaded("Locale_3"), Is.False);
            Assert.That(Object.FindObjectsByType<GameHost>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<ObserverCam>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<SectorJumpFlight>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(InputSystem.ListEnabledActions().Count(action =>
                action.name is "Jump" or "Continue" or "Thrust"), Is.Zero);
        }

        // Headings are left out: the game's come from the gate, Inspection's from its fixture.
        private static void AssertSameTimeline(SectorJumpState expected, SectorJumpState shown)
        {
            const float tolerance = 1e-4f;
            var at = $"at {expected.Seconds:F3}s";
            Assert.That(shown.Seconds, Is.EqualTo(expected.Seconds).Within(tolerance), at);
            Assert.That(shown.TravelProgress, Is.EqualTo(expected.TravelProgress).Within(tolerance), at);
            Assert.That(shown.TravelFraction, Is.EqualTo(expected.TravelFraction).Within(tolerance), at);
            Assert.That(shown.Speed, Is.EqualTo(expected.Speed).Within(tolerance), at);
            Assert.That(shown.Distance, Is.EqualTo(expected.Distance).Within(tolerance), at);
            Assert.That(shown.LocaleBlend, Is.EqualTo(expected.LocaleBlend).Within(tolerance), at);
            Assert.That(shown.Charge, Is.EqualTo(expected.Charge).Within(tolerance), at);
            Assert.That(shown.HudAlpha, Is.EqualTo(expected.HudAlpha).Within(tolerance), at);
            Assert.That(shown.ShipBlurStrength, Is.EqualTo(expected.ShipBlurStrength).Within(tolerance), at);
            Assert.That(shown.ShipBlurLength, Is.EqualTo(expected.ShipBlurLength).Within(tolerance), at);
        }

        private static void AssertProtected(Ship player)
        {
            Assert.That(player.Commander.enabled || player.Movement.enabled, Is.False);
            Assert.That(player.Colliders.Any(collider => collider.enabled), Is.False);
            Assert.That(player.Damage.IsInvulnerable, Is.True);
        }

        private void MovePlayer(Vector3 position)
        {
            rig.Player.transform.position = position;
            rig.Player.Body.position = position;
            rig.Player.Body.linearVelocity = Vector3.zero;
            rig.Player.Body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        private void PressJump()
        {
            input.Press(keyboard.fKey);
            InputSystem.Update();
            Assert.That(host.Playtest.Jump, Is.Not.Null);
            input.Release(keyboard.fKey);
            InputSystem.Update();
        }

        private IEnumerator WaitForFlight() => AsyncAssert.WaitUntil(() =>
        {
            if (!host.Playtest) return false;
            rig = host.Playtest.GetComponentInChildren<PlayerRig>();
            return rig && rig.Player && rig.Tally.SecondsSurvived > 0f &&
                   host.Playtest.ActiveSector.GetComponentInChildren<ExtractionZone>().IsPlayerInZone;
        }, 45f, "the crowded departure is ready for a fresh jump press");

        private IEnumerator WaitForHold() => AsyncAssert.WaitUntil(() => host.Playtest.Jump &&
            host.Playtest.Jump.Holding && host.Playtest.GetComponentInChildren<RunRecapScreen>(),
            30f, "the extraction recap holds in hyperspace");

        private IEnumerator ClickContinue(RunRecapScreen screen)
        {
            var button = screen.GetComponentInChildren<Button>();
            yield return AsyncAssert.WaitUntil(button.IsInteractable, 10f, "the faded-in card is ready for Continue");
            var rect = (RectTransform)button.transform;
            Canvas.ForceUpdateCanvases();
            input.Set(mouse.position, RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)));
            yield return null;
            input.Press(mouse.leftButton);
            yield return null;
            input.Release(mouse.leftButton);
            yield return null;
        }

        private void BuildPreview(float turnSeconds = .35f)
        {
            feel = Object.Instantiate(TestAssets.Load<SectorJumpProfile>("Assets/Settings/Game/SectorJumpTuning.asset"));
            feel.turnDuration = turnSeconds;
            feel.chargeDuration = .1f;
            feel.travelDuration = .8f;
            feel.settleDuration = .25f;
            feel.worldBlendDuration = .1f;
            feel.hudFadeOut = .1f;
            feel.hudFadeIn = .2f;
            var profile = new SessionProfile
            {
                sectors = new[] { Entry("CrowdedJumpDeparture"), Entry("CrowdedJumpArrival") }
            };
            var template = new GameObject("Crowded jump game host template");
            template.SetActive(false);
            extras.Add(template);
            var game = new GameObject("Crowded jump game host");
            game.transform.SetParent(template.transform, false);
            var gameHost = game.AddComponent<GameHost>();
            gameHost.sessionProfile = profile;
            gameHost.playerRig = TestAssets.Load<PlayerRig>("Assets/Prefabs/MiscObjects/PlayerRig.prefab");
            gameHost.observerCamPrefab = TestAssets.Load<ObserverCam>("Assets/Prefabs/Cameras/Main Camera.prefab");
            gameHost.recapFeel = TestAssets.Load<RecapFeelProfile>("Assets/Settings/UI/RecapFeel.asset");
            gameHost.jumpFeel = feel;
            gameHost.jumpShader = Shader.Find("Astronomical/Travel/SectorJump");
            var root = new GameObject("Crowded jump test");
            root.SetActive(false);
            host = root.AddComponent<PreviewHost>();
            host.mode = PreviewMode.Playtest;
            host.subjectKind = PreviewSubjectKind.SectorJump;
            host.playtestSetup = PreviewPlaytestSetup.CrowdedJumpGate;
            host.gameHostPrefab = gameHost;
            host.recapFeel = gameHost.recapFeel;
            host.jumpFeel = feel;
            host.sessionProfile = profile;
            host.crowdedJumpSectors = profile.sectors;
            host.jumpDestination = TestAssets.Load<LocaleSky>("Assets/Prefabs/Locales/Locale_1.prefab");
            root.SetActive(true);
        }

        private static SectorEntry Entry(string name)
        {
            var entry = new SectorEntry
            {
                prefab = TestAssets.Load<Sector>($"Assets/Prefabs/Sectors/Preview/{name}.prefab"),
                config = TestAssets.Load<SectorSettings>($"Assets/Settings/Game/Preview/{name}.asset")
            };
            Assert.That(entry.prefab && entry.config, Is.True, "Crowded jump fixture assets must exist before PlayMode");
            return entry;
        }
    }
}
