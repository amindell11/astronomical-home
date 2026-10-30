using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Ships;
using Ships.Damage;
using Tests.PlayMode.Common;
using UI.PlayerState;
using UI.Projection;
using UI.Weapons;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Utils;

namespace Tests.PlayMode.PlayerState
{
    [Category("UI")]
    public class ShipStatusOverlayPlayModeTests : PlayModeWorldFixture
    {
        private GameObject root;
        private Ship ship;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            root = new GameObject("Status test");
        }

        [TearDown]
        public override void TearDown()
        {
            DestroyTestObject(root);
            ShipTestFactory.DestroyShip(ship);
            base.TearDown();
        }

        [Test]
        public void ResourceChanges_UpdateFillGraduationsAndNumbers()
        {
            var resource = new Resource(150f);
            resource.ApplyDamage(75f);
            var bar = StatusBarUI.Create(root.transform, resource,
                Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), Color.cyan, 0f,
                TestAssets.Load<Material>("Assets/Visuals/Ui/Projection/CanopyText.mat"));
            var label = bar.GetComponentInChildren<Text>();
            Assert.AreEqual("75/150", label.text);
            using (var mesh = new VertexHelper())
            {
                typeof(StatusBarUI).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                        null, new[] { typeof(VertexHelper) }, null)
                    .Invoke(bar, new object[] { mesh });
                Assert.AreEqual(32, mesh.currentVertCount, "Track, empty, fill and five 25-unit marks");
                var vertex = new UIVertex();
                mesh.PopulateUIVertex(ref vertex, 10);
                Assert.AreEqual(0f, vertex.position.x, .001f, "The remaining fill ends halfway across");
                mesh.PopulateUIVertex(ref vertex, 12);
                Assert.AreEqual(-40f + 80f / 6f, vertex.position.x, .001f, "First mark is 25 of 150");
                mesh.PopulateUIVertex(ref vertex, 24);
                var left = vertex.position.x;
                mesh.PopulateUIVertex(ref vertex, 26);
                Assert.AreEqual(2f, vertex.position.x - left, .001f, "100-unit marks are heavier");
            }
            bar.gameObject.SetActive(false);
            resource.Configure(400f);
            resource.ApplyDamage(200f);
            bar.gameObject.SetActive(true);
            Assert.AreEqual("200/400", label.text);
            resource.ApplyDamage(.2f);
            Assert.AreEqual("200/400", label.text, "Round current value up");
            resource.Configure(0f);
            Assert.AreEqual("0/0", label.text);
        }

        [UnityTest]
        public IEnumerator LowHull_PulsesOnlyTheHealthFill_AndResetRestoresIt()
        {
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform);
            camera.orthographic = true;
            var canvas = new GameObject("HUD").AddComponent<Canvas>();
            canvas.transform.SetParent(root.transform);
            ship = ShipTestFactory.CreateShip(TestAssets.Load<Ship>("Assets/Prefabs/Ships/Vanguard.prefab"),
                null, Projectiles, team: 1);
            var bars = ShipStatusIndicator.Create(canvas, camera, ship,
                Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),
                TestAssets.Load<Material>("Assets/Visuals/Ui/Projection/CanopyText.mat")).GetComponentsInChildren<StatusBarUI>();
            var shieldColor = FillEdge(bars[0]).color;
            var healthColor = FillEdge(bars[1]).color;

            ship.Damage.Health.SetFraction(.2f);
            yield return null;
            var first = FillEdge(bars[1]).color;
            Assert.Greater(first.r, first.g, "Low hull turns the health fill red");
            yield return new WaitForSeconds(.6f);
            var later = FillEdge(bars[1]);
            Assert.Greater(later.color.r, first.r + 25, "The fill pulses brighter");
            Assert.AreEqual(-40f + 80f * .2f, later.position.x, .001f, "The pulse never changes the quantity");
            Assert.AreEqual(shieldColor, FillEdge(bars[0]).color, "Shields never pulse");

            ship.ResetShip();
            yield return null;
            Assert.AreEqual(healthColor, FillEdge(bars[1]).color);
        }

        [UnityTest]
        public IEnumerator Indicators_FollowZoomConcealmentResetFadeAndRegistryRemoval()
        {
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform);
            camera.transform.position = new Vector3(0f, 0f, -50f);
            camera.orthographic = true;
            camera.orthographicSize = 17f;
            var ships = new SubscribedSet<Ship>();
            ship = ShipTestFactory.CreateShip(TestAssets.Load<Ship>("Assets/Prefabs/Ships/Vanguard.prefab"),
                null, Projectiles, team: 1);
            ship.Rigidbody.interpolation = RigidbodyInterpolation.None;
            ships.Add(ship);
            var hud = new GameObject("HUD").AddComponent<Canvas>();
            hud.transform.SetParent(root.transform);
            hud.gameObject.layer = LayerMask.NameToLayer("UIBloom");
            hud.worldCamera = camera;
            var mainHud = hud.gameObject.AddComponent<global::UI.Overlay>();
            var crosshair = Object.Instantiate(TestAssets.Load<AimCrosshair>(
                "Assets/Prefabs/UI/Crosshair/AimCrosshair.prefab"), hud.transform);
            crosshair.Initialize(ship, camera);
            var weaponHud = Object.Instantiate(TestAssets.Load<WeaponResourceHud>(
                "Assets/Prefabs/UI/Crosshair/WeaponKeystoneHud.prefab"), hud.transform);
            weaponHud.Initialize(null);
            typeof(global::UI.Overlay).GetField("crosshair", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(mainHud, crosshair);
            typeof(global::UI.Overlay).GetField("weaponResources", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(mainHud, weaponHud);
            typeof(global::UI.Overlay).GetField("lockReticle", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(mainHud, TestAssets.Load<LockOnIndicator>("Assets/Prefabs/UI/UILockOnIndicator.prefab"));
            mainHud.ConfigureCameras(camera, camera);
            mainHud.BindShips(ships, camera);
            var overlay = root.GetComponentInChildren<ShipStatusOverlay>();
            yield return null;

            var worldVisibility = (CanvasGroup)typeof(ShipStatusOverlay)
                .GetField("worldVisibility", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(overlay);
            var reticle = worldVisibility.GetComponentInChildren<LockOnIndicator>();
            var lockVisibility = reticle.GetComponent<CanvasGroup>();
            Assert.IsNull(worldVisibility.transform.parent, "World reticles must not inherit canvas scale");
            Assert.AreEqual(Vector3.one, worldVisibility.transform.lossyScale);
            Assert.AreEqual(Vector2.one * 5f, ((RectTransform)reticle.transform).sizeDelta);
            Assert.AreEqual(Color.red, reticle.GetComponent<Image>().color);
            ship.Lock.RaiseProgress(.5f);
            yield return null;
            Assert.AreEqual(1f, lockVisibility.alpha);
            Assert.That(Vector3.Distance(ship.VisualRig.transform.position + Substrate.GamePlane.Normal * -5f,
                reticle.transform.position), Is.LessThan(.001f));
            var indicator = overlay.GetComponentInChildren<ShipStatusIndicator>();
            var bars = indicator.GetComponentsInChildren<StatusBarUI>();
            Assert.AreSame(bars[0].GetComponentInParent<InstrumentAppear>(), bars[1].GetComponentInParent<InstrumentAppear>());
            var visibility = indicator.GetComponent<CanvasGroup>();
            Assert.IsFalse(indicator.transform.IsChildOf(ship.transform));
            Assert.AreEqual(hud.gameObject.layer, indicator.gameObject.layer);
            Assert.AreEqual(1f, indicator.transform.localScale.x, .001f);
            var before = RectTransformUtility.WorldToScreenPoint(camera, indicator.transform.position);
            ship.transform.position = ship.Rigidbody.position = Vector3.right * 3f;
            Physics.SyncTransforms();
            yield return null;
            var after = RectTransformUtility.WorldToScreenPoint(camera, indicator.transform.position);
            Assert.Greater(after.x, before.x);
            Assert.AreEqual(camera.WorldToScreenPoint(ship.transform.position).x, after.x, 1f);
            camera.transform.position += Vector3.right * 5f;
            yield return null;
            var panned = RectTransformUtility.WorldToScreenPoint(camera, indicator.transform.position);
            Assert.AreEqual(camera.WorldToScreenPoint(ship.transform.position).x, panned.x, 1f,
                "A moving camera does not drag the bar off its ship");
            camera.orthographicSize = 34f;
            yield return null;
            Assert.AreEqual(.8f, indicator.transform.localScale.x, .001f);
            camera.orthographicSize = 8.5f;
            yield return null;
            Assert.AreEqual(1.15f, indicator.transform.localScale.x, .001f);

            ship.Lock.Conceal();
            yield return null;
            Assert.AreEqual(0f, visibility.alpha);
            ship.tag = "Player";
            yield return null;
            Assert.AreEqual(1f, visibility.alpha, "Player bars remain readable while concealed");
            ship.Lock.Reveal();
            mainHud.SetOpacity(.35f);
            Assert.AreEqual(.35f, overlay.GetComponent<CanvasGroup>().alpha, .001f,
                "Travel fades and restores every HUD canvas through the same call");
            mainHud.SetOpacity(1f);
            Assert.AreEqual(1f, overlay.GetComponent<CanvasGroup>().alpha);
            Assert.IsTrue(crosshair.gameObject.activeSelf);
            Assert.IsTrue(weaponHud.gameObject.activeSelf);
            mainHud.SetVisible(false);
            Assert.AreEqual(0f, worldVisibility.alpha, "Ordinary HUD hiding also hides world reticles");
            mainHud.SetOpacity(0f, 1f);
            Assert.IsFalse(crosshair.gameObject.activeSelf);
            Assert.IsFalse(weaponHud.gameObject.activeSelf);
            Assert.IsTrue(overlay.GetComponent<Canvas>().enabled);
            Assert.AreEqual(1f, overlay.GetComponent<CanvasGroup>().alpha);
            mainHud.SetOpacity(0f, .35f);
            Assert.AreEqual(.35f, worldVisibility.alpha, "Recap alpha composes above reticle visibility");
            yield return null;
            Assert.That(reticle.GetComponent<CanvasRenderer>().GetInheritedAlpha(), Is.EqualTo(.35f).Within(.001f));
            Assert.AreEqual(.35f, overlay.GetComponent<CanvasGroup>().alpha, .001f);
            mainHud.SetOpacity(0f, 1f);

            for (var cycle = 0; cycle < 3; cycle++)
            {
                TestDamage.Kill(ship);
                yield return null;
                Assert.AreEqual(0f, visibility.alpha);
                Assert.AreEqual(0f, lockVisibility.alpha);
                ship.ResetShip();
                yield return null;
                Assert.AreEqual(1f, visibility.alpha);
                ship.Lock.RaiseProgress(.5f);
                yield return null;
                Assert.AreEqual(1f, lockVisibility.alpha, "HUD reticles remain subscribed across ship reset");
                ship.Lock.RaiseReleased();
                Assert.AreEqual(0f, lockVisibility.alpha);
                ship.Damage.Health.SetFraction(.5f);
                var labels = indicator.GetComponentsInChildren<Text>();
                Assert.AreEqual($"{Mathf.CeilToInt(ship.Damage.Health.CurrentValue)}/{ship.Damage.Health.MaxValue}",
                    labels[1].text);
            }
            ships.Remove(ship);
            yield return null;
            Assert.IsFalse(indicator);
            Assert.IsFalse(reticle, "Registry removal destroys the HUD-owned reticle");
            ships.Add(ship);
            yield return null;
            Assert.AreEqual(1, overlay.GetComponentsInChildren<ShipStatusIndicator>().Length);
            ships.Remove(ship);
            ShipTestFactory.DestroyShip(ship);
            ship = ShipTestFactory.CreateShip(TestAssets.Load<Ship>("Assets/Prefabs/Ships/Cargo Ships/Junker_1.prefab"),
                null, Projectiles, team: 1);
            Assert.IsFalse(ship.VisualRig, "Exercise legacy ships without a visual rig");
            ships.Add(ship);
            yield return null;
            var cargoIndicator = overlay.GetComponentInChildren<ShipStatusIndicator>();
            Assert.AreEqual(1f, cargoIndicator.GetComponent<CanvasGroup>().alpha);
            Assert.AreEqual("400/400", cargoIndicator.GetComponentsInChildren<Text>()[1].text);
        }

        private static UIVertex FillEdge(StatusBarUI bar)
        {
            using var mesh = new VertexHelper();
            typeof(StatusBarUI).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null)
                .Invoke(bar, new object[] { mesh });
            var vertex = new UIVertex();
            mesh.PopulateUIVertex(ref vertex, 10);
            return vertex;
        }
    }
}

