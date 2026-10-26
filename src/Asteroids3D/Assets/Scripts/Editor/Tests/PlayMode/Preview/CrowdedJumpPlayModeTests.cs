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
using Substrate.Services.Objectives;
using Substrate.Services.Units;
using Substrate.Sessions;
using Tests.PlayMode.Common;
using UI.Recap;
using UI.Screens;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
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
        private InputTestFixture input;
        private Keyboard keyboard;
        private Mouse mouse;
        private readonly List<GameObject> extras = new();

        public override void SetUp()
        {
            base.SetUp();
            input = new InputTestFixture();
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
            BuildPreview(turnSeconds: 1f);
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
            Assert.That(flight.State.LocaleBlend, Is.Zero);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Locale_3"));
            AssertProtected(player);

            var recap = game.GetComponentInChildren<RunRecapScreen>();
            yield return ClickContinue(recap);
            yield return AsyncAssert.WaitUntil(() => !game.Jump, 30f, "travel finishes after Continue");
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
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Locale_1"));
            yield return AsyncAssert.WaitUntil(() => !SceneManager.GetSceneByName("Locale_3").isLoaded,
                15f, "the outgoing locale unloads after arrival");
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
                Assert.That(SceneManager.GetSceneByName("Locale_1").isLoaded, Is.False);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Locale_3"));
                Assert.That(InputSystem.ListEnabledActions().Count(action => action.name == "Jump"), Is.EqualTo(1));
                Assert.That(Object.FindObjectsByType<GameHost>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            }

            PressJump();
            yield return WaitForHold();
            yield return ClickContinue(host.Playtest.GetComponentInChildren<RunRecapScreen>());
            yield return AsyncAssert.WaitUntil(() => host.Playtest.Jump && !host.Playtest.Jump.Holding &&
                SceneManager.GetActiveScene().name == "Locale_1", 30f, "arrival has begun");
            AssertProtected(rig.Player);
            yield return host.Shutdown();
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(SceneManager.GetSceneByName("Locale_1").isLoaded, Is.False);
            Assert.That(SceneManager.GetSceneByName("Locale_3").isLoaded, Is.False);
            Assert.That(Object.FindObjectsByType<GameHost>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<ObserverCam>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<SectorJumpFlight>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(InputSystem.ListEnabledActions().Count(action =>
                action.name is "Jump" or "Continue" or "Thrust"), Is.Zero);
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
            Assert.That(button.interactable, Is.True);
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
            var root = new GameObject("Crowded jump test");
            root.SetActive(false);
            host = root.AddComponent<PreviewHost>();
            host.mode = PreviewMode.Playtest;
            host.playtestSetup = PreviewPlaytestSetup.CrowdedJumpGate;
            host.sessionProfile = profile;
            host.jumpPlaytestProfile = profile;
            host.playerRigPrefab = TestAssets.Load<PlayerRig>("Assets/Prefabs/MiscObjects/PlayerRig.prefab");
            host.observerCamPrefab = TestAssets.Load<ObserverCam>("Assets/Prefabs/Cameras/Main Camera.prefab");
            host.recapFeel = TestAssets.Load<RecapFeelProfile>("Assets/Settings/UI/RecapFeel.asset");
            host.jumpFeel = feel;
            host.jumpShader = Shader.Find("Astronomical/Travel/SectorJump");
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
