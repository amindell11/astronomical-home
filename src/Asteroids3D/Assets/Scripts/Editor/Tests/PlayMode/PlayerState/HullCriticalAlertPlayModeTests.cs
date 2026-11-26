using System;
using System.Collections;
using System.Reflection;
using Cameras;
using Damage;
using NUnit.Framework;
using Ships.Damage;
using Ships.Registry;
using UI.PlayerState.Alerts;
using UI.Projection;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.PlayerState
{
    [Category("UI")]
    public class HullCriticalAlertPlayModeTests
    {
        private GameObject root;
        private HullCriticalAlert alert;
        private FakeDamage damage;
        private Camera camera;
        private Transform ship;
        private CanvasGroup edges;
        private CanvasGroup band;
        private bool hudVisible;
        private float previousShakeScale;
        private CanopyProjectionProfile profile;

        [SetUp]
        public void SetUp()
        {
            previousShakeScale = CameraSettings.ShakeScale;
            CameraSettings.ShakeScale = 1f;
            hudVisible = true;
            root = new GameObject("Hull critical alert");
            var canvas = new GameObject("HUD", typeof(RectTransform)).AddComponent<Canvas>();
            canvas.transform.SetParent(root.transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            camera = new GameObject("World camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 10f;
            ship = new GameObject("Player ship").transform;
            ship.SetParent(root.transform, false);
            damage = new FakeDamage();
            alert = HullCriticalAlert.Create(canvas.transform,
                Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), null, Projection(), () => hudVisible);
            alert.Initialize(damage, ship, camera);
            edges = alert.transform.Find("Edges").GetComponent<CanvasGroup>();
            band = alert.transform.Find("Band").GetComponent<CanvasGroup>();
        }

        [TearDown]
        public void TearDown()
        {
            CameraSettings.ShakeScale = previousShakeScale;
            if (root) Object.DestroyImmediate(root);
            if (profile) Object.DestroyImmediate(profile);
        }

        [UnityTest]
        public IEnumerator ShowsOnlyWhileHullAndShieldAreBothCritical()
        {
            yield return null;
            Assert.AreEqual(0f, band.alpha, "A healthy ship shows no alert");

            damage.Shield.SetFraction(.05f);
            yield return null;
            Assert.AreEqual(0f, band.alpha, "A critical shield alone shows no alert");

            damage.Health.SetFraction(.2f);
            yield return new WaitForSecondsRealtime(.35f);
            Assert.Greater(band.alpha, 0f, "Hull and shield both critical show the band");
            Assert.Greater(edges.alpha, 0f, "Hull and shield both critical light the screen edges");

            damage.Shield.SetFraction(.5f);
            yield return null;
            Assert.AreEqual(0f, band.alpha, "A shield recharge clears the band");
            Assert.AreEqual(0f, edges.alpha, "A shield recharge clears the edges");

            damage.Shield.SetFraction(.05f);
            damage.Health.SetFraction(.3f);
            yield return null;
            Assert.AreEqual(0f, band.alpha, "A heal past a quarter clears the alert");
        }

        [UnityTest]
        public IEnumerator ClearsOnDeath_AndWhileTheHudIsHidden()
        {
            Critical();
            yield return new WaitForSecondsRealtime(.35f);
            Assert.Greater(band.alpha, 0f);

            hudVisible = false;
            yield return null;
            Assert.AreEqual(0f, band.alpha, "A hidden HUD hides the band");
            Assert.AreEqual(0f, edges.alpha, "A hidden HUD hides the edges");

            hudVisible = true;
            yield return null;
            Assert.Greater(band.alpha, 0f);

            damage.Die();
            yield return null;
            Assert.AreEqual(0f, band.alpha, "Death clears the band");
            Assert.AreEqual(0f, edges.alpha, "Death clears the edges");
        }

        [UnityTest]
        public IEnumerator Band_StaysInsideTheSafeArea_AtEveryScreenEdge()
        {
            Critical();
            yield return null;
            var area = ((RectTransform)alert.transform).rect.size;
            Assert.Greater(area.y, HullCriticalAlert.BottomReserve + HullCriticalAlert.TopReserve
                + HullCriticalAlert.BandSize.y, "The test screen leaves room between the reserves");
            var rect = (RectTransform)band.transform;
            var screen = new Vector2(camera.pixelWidth, camera.pixelHeight);
            var points = new[]
            {
                new Vector2(0f, screen.y * .5f), new Vector2(screen.x, screen.y * .5f),
                new Vector2(screen.x * .5f, 0f), new Vector2(screen.x * .5f, screen.y),
                Vector2.zero, screen, new Vector2(0f, screen.y), new Vector2(screen.x, 0f)
            };
            foreach (var point in points)
            {
                ship.position = camera.ScreenToWorldPoint(new Vector3(point.x, point.y, 10f));
                yield return null;
                var half = rect.rect.size * .5f;
                var centre = rect.anchoredPosition;
                Assert.GreaterOrEqual(centre.x - half.x, 0f, $"Band left edge on screen with the ship at {point}");
                Assert.LessOrEqual(centre.x + half.x, area.x, $"Band right edge on screen with the ship at {point}");
                Assert.GreaterOrEqual(centre.y - half.y, HullCriticalAlert.BottomReserve,
                    $"Band clears the bottom readouts with the ship at {point}");
                Assert.LessOrEqual(centre.y + half.y, area.y - HullCriticalAlert.TopReserve,
                    $"Band clears the timer and minimap with the ship at {point}");
            }

            ship.position = Vector3.zero;
            yield return null;
            Assert.AreEqual(area.x * .5f, rect.anchoredPosition.x, 1f, "Away from the edges the band centres on the ship");
            Assert.Greater(rect.anchoredPosition.y, area.y * .5f, "The band sits above the ship");
        }

        [UnityTest]
        public IEnumerator WithoutScreenShake_HoldsASteadyLevel()
        {
            CameraSettings.ShakeScale = 0f;
            Critical();
            yield return new WaitForSecondsRealtime(.35f);
            var held = band.alpha;
            var edge = edges.alpha;
            Assert.Greater(held, 0f);
            var until = Time.unscaledTime + 1f;
            while (Time.unscaledTime < until)
            {
                yield return null;
                Assert.AreEqual(held, band.alpha, 1e-5f, "The band never pulses without screen shake");
                Assert.AreEqual(edge, edges.alpha, 1e-5f, "The edges never pulse without screen shake");
            }
        }

        private void Critical()
        {
            damage.Shield.SetFraction(.05f);
            damage.Health.SetFraction(.2f);
        }

        private CanopyProjection Projection()
        {
            // Inactive, so the projection never wakes to demand its render feature.
            var host = new GameObject("Canopy projection");
            host.SetActive(false);
            host.transform.SetParent(root.transform, false);
            var projection = host.AddComponent<CanopyProjection>();
            projection.PersonalityEnabled = false;
            profile = ScriptableObject.CreateInstance<CanopyProjectionProfile>();
            typeof(CanopyProjection).GetField("profile", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(projection, profile);
            return projection;
        }

        private sealed class FakeDamage : IDamageEvents
        {
            public event Action<AppliedDamage> OnDamaged { add { } remove { } }
            public event Action<ShipId, DamageInfo> OnDeath;
            public Resource Health { get; } = new Resource(400f);
            public RegenResource Shield { get; } = new RegenResource(50f, 0f, 999f);
            public void Die() => OnDeath?.Invoke(default, default);
        }
    }
}
