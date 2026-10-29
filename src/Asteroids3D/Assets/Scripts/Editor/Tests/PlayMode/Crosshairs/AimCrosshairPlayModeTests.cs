using System;
using System.Collections;
using System.Collections.Generic;
using Combat.Weapons;
using Combat.Weapons.Conditions;
using NUnit.Framework;
using Ships.Command;
using Tests.Common;
using UI.PlayerState;
using UI.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tests.PlayMode.Crosshairs
{
    [Category("Weapons")]
    public sealed class AimCrosshairPlayModeTests : InputTestFixture
    {
        private sealed class Ammo : IAmmoReadout
        {
            public int AmmoCount { get; set; } = 1;
            public int MaxAmmo => 2;
            public bool IsReloading { get; set; }
            public float ReloadProgress => .5f;
            public event Action<int> OnAmmoCountChanged { add { } remove { } }
            public event Action OnReloadStarted { add { } remove { } }
            public event Action OnReloadCompleted { add { } remove { } }
        }

        private sealed class WeaponSources : IWeaponReadouts
        {
            private readonly IWeaponReadout[] ammo;
            public WeaponSources(Ammo value) => ammo = new IWeaponReadout[] { value };
            public IReadOnlyList<WeaponSlot> Slots { get; } = new[] { WeaponSlot.Primary, WeaponSlot.Secondary };
            public string DisplayName(WeaponSlot slot) => slot.ToString();
            public IReadOnlyList<IWeaponReadout> Readouts(WeaponSlot slot) => ammo;
        }

        private GameObject root;
        private GameObject shipRoot;
        private Camera gameplayCamera;
        private StubShipStatus ship;
        private AimCrosshair crosshair;
        private WeaponResourceHud weaponHud;
        private Mouse mouse;
        private Ammo ammo;
        private bool cursorWasVisible;

        public override void Setup()
        {
            base.Setup();
            cursorWasVisible = Cursor.visible;
            Cursor.visible = true;
            mouse = InputSystem.AddDevice<Mouse>();
            root = new GameObject("Crosshair test", typeof(RectTransform), typeof(Canvas));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            gameplayCamera = new GameObject("Gameplay camera", typeof(Camera)).GetComponent<Camera>();
            gameplayCamera.enabled = false;
            gameplayCamera.orthographic = true;
            gameplayCamera.orthographicSize = 10f;
            gameplayCamera.transform.position = new Vector3(0f, 0f, -10f);
            shipRoot = new GameObject("Player ship");
            shipRoot.transform.position = new Vector3(2f, -1f, 0f);
            ship = new StubShipStatus { transform = shipRoot.transform };
            ammo = new Ammo();
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<AimCrosshair>("Assets/Prefabs/UI/Crosshair/AimCrosshair.prefab");
            crosshair = Object.Instantiate(prefab, root.transform);
            crosshair.Initialize(ship, gameplayCamera);
            crosshair.gameObject.SetActive(true);
            weaponHud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<WeaponResourceHud>(
                "Assets/Prefabs/UI/Crosshair/WeaponKeystoneHud.prefab"), root.transform);
            weaponHud.Initialize(new WeaponSources(ammo));
            weaponHud.GetComponentInChildren<BoostGaugeUI>(true).Initialize(ship);
            weaponHud.gameObject.SetActive(true);
#else
            Assert.Ignore("Requires the authored crosshair prefab.");
#endif
        }

        public override void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(shipRoot);
            if (gameplayCamera) Object.DestroyImmediate(gameplayCamera.gameObject);
            Cursor.visible = cursorWasVisible;
            base.TearDown();
        }

        private void Draw()
        {
            crosshair.SendMessage("LateUpdate");
            weaponHud.SendMessage("LateUpdate");
            Canvas.ForceUpdateCanvases();
        }

        private Image ImageAt(string path) => (path.StartsWith("Primary") || path.StartsWith("Secondary")
            ? weaponHud.transform : crosshair.transform).Find(path).GetComponent<Image>();
        private bool Visible(string name) => weaponHud.transform.Find(name).gameObject.activeSelf;

        [Test]
        public void TriggerReadouts_AreIndependent_AndRebindClearsTheirSources()
        {
            Draw();
            Assert.IsFalse(Visible("PrimaryResources"));
            Assert.IsFalse(Visible("SecondaryResources"));
            Press(mouse.leftButton);
            Draw();
            Assert.IsTrue(Visible("PrimaryResources"));
            Assert.IsFalse(Visible("SecondaryResources"));
            Press(mouse.rightButton);
            Draw();
            Assert.IsTrue(Visible("SecondaryResources"));
            weaponHud.Initialize(null);
            Draw();
            Assert.IsFalse(Visible("PrimaryResources"));
            Assert.IsFalse(Visible("SecondaryResources"));
        }

        [UnityTest]
        public IEnumerator Release_LingersThenHides_AndDisableRestoresCursor()
        {
            Assert.IsFalse(Cursor.visible);
            Press(mouse.rightButton);
            yield return null;
            Draw();
            Assert.IsTrue(Visible("SecondaryResources"));
            Release(mouse.rightButton);
            yield return null;
            Draw();
            Assert.IsTrue(Visible("SecondaryResources"));
            yield return new WaitForSecondsRealtime(.45f);
            Draw();
            Assert.IsFalse(Visible("SecondaryResources"));
            crosshair.gameObject.SetActive(false);
            weaponHud.gameObject.SetActive(false);
            Assert.IsTrue(Cursor.visible);
            crosshair.gameObject.SetActive(true);
            weaponHud.gameObject.SetActive(true);
            Draw();
            Assert.IsFalse(Visible("SecondaryResources"));
        }

        [Test]
        public void ReloadProgress_UsesItsOwnArc_AndEmptyAmmoKeepsTheSpentRoundVisible()
        {
            Press(mouse.rightButton);
            Draw();
            var round = ImageAt("SecondaryResources/Round0");
            var spentInk = ImageAt("SecondaryResources/Round1").color;
            Assert.That(round.color, Is.Not.EqualTo(spentInk));
            ammo.AmmoCount = 0;
            Draw();
            Assert.That(round.color, Is.EqualTo(spentInk));
            Assert.IsTrue(round.isActiveAndEnabled);
            ammo.IsReloading = true;
            Draw();
            var reload = ImageAt("SecondaryResources/ReloadFill");
            Assert.IsTrue(reload.isActiveAndEnabled);
            Assert.That(reload.fillAmount, Is.EqualTo(120f / 720f).Within(.001f));
        }

        [Test]
        public void Pointer_FollowsShipToAimDirection_WhileItsTipAndCircleStayOnAim()
        {
            var aim = (Vector2)gameplayCamera.WorldToScreenPoint(shipRoot.transform.position) + new Vector2(100f, 30f);
            Set(mouse.position, aim);
            Draw();
            AssertPointer(aim);
            var fixedPosition = weaponHud.transform.position;
            shipRoot.transform.position += Vector3.up * 3f;
            gameplayCamera.transform.SetPositionAndRotation(new Vector3(1f, 3f, -10f), Quaternion.Euler(0f, 0f, 25f));
            Draw();
            AssertPointer(aim);
            Assert.That(ImageAt("Circle").rectTransform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(weaponHud.transform.position, Is.EqualTo(fixedPosition));
            Assert.That(weaponHud.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(((RectTransform)weaponHud.transform).anchorMin, Is.EqualTo(new Vector2(.5f, 0f)));
            Assert.That(((RectTransform)weaponHud.transform).anchorMax, Is.EqualTo(new Vector2(.5f, 0f)));
        }

        [Test]
        public void KeystoneRing_FillsAsCooldownExpires_AndRebindsToTheNewShip()
        {
            var gauge = weaponHud.GetComponentInChildren<BoostGaugeUI>();
            var fill = gauge.GetComponent<Image>();
            Assert.That(fill.fillMethod, Is.EqualTo(Image.FillMethod.Radial360));
            Assert.That(fill.fillAmount, Is.EqualTo(1f));
            ship.BoostAvailable = false;
            ship.BoostCooldownPct = .75f;
            gauge.SendMessage("Update");
            Assert.That(fill.fillAmount, Is.EqualTo(.25f));
            ship.BoostCooldownPct = .25f;
            gauge.SendMessage("Update");
            Assert.That(fill.fillAmount, Is.EqualTo(.75f));
            gauge.Initialize(new StubShipStatus());
            Assert.That(fill.fillAmount, Is.EqualTo(1f));
            Assert.That(((RectTransform)weaponHud.transform).sizeDelta, Is.EqualTo(new Vector2(92f, 92f)));
            Assert.That(((RectTransform)weaponHud.transform).anchoredPosition, Is.EqualTo(new Vector2(0f, 58.5f)));
        }

        private void AssertPointer(Vector2 aim)
        {
            var pointer = ImageAt("Pointer").rectTransform;
            var tip = RectTransformUtility.WorldToScreenPoint(null, pointer.TransformPoint(Vector3.zero));
            var expected = (aim - (Vector2)gameplayCamera.WorldToScreenPoint(shipRoot.transform.position)).normalized;
            Assert.That(Vector2.Distance(tip, aim), Is.LessThan(.1f));
            Assert.That(Vector2.Dot(pointer.up, expected), Is.GreaterThan(.999f));
        }

        [Test]
        public void AimPoint_StaysAccurate_WhenTheArtworkScalesWithTheHud()
        {
            var canvas = root.GetComponent<Canvas>();
            var aim = new Vector2(Screen.width * .4f, Screen.height * .6f);
            Set(mouse.position, aim);
            Draw();
            var circle = ImageAt("Circle").rectTransform;
            var before = RectTransformUtility.WorldToScreenPoint(null, circle.TransformPoint(Vector3.right * 10f)) - aim;
            canvas.scaleFactor = 2f;
            Draw();
            var after = RectTransformUtility.WorldToScreenPoint(null, circle.TransformPoint(Vector3.right * 10f)) - aim;
            Assert.That(after.magnitude, Is.EqualTo(before.magnitude * 2f).Within(.1f));
            AssertPointer(aim);
        }

        [Test]
        public void ImportedSprites_KeepTheArtboardPivot_AndSourceSilhouettes()
        {
            var circle = ImageAt("Circle").sprite;
            var pointer = ImageAt("Pointer").sprite;
            Assert.That(circle.rect.size, Is.EqualTo(new Vector2(256f, 256f)));
            Assert.That(pointer.pivot, Is.EqualTo(new Vector2(128f, 128f)));
#if UNITY_EDITOR
            var decoded = new Texture2D(2, 2);
            try
            {
                decoded.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(circle)));
                Assert.That(decoded.GetPixel(128, 128).a, Is.EqualTo(0f));
                Assert.That(decoded.GetPixel(128, 172).a, Is.GreaterThan(.9f));
                decoded.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(pointer)));
                Assert.That(decoded.GetPixel(128, 110).a, Is.GreaterThan(.9f));
                Assert.That(decoded.GetPixel(128, 128).a, Is.EqualTo(0f));
                var resourceRing = weaponHud.transform.Find("ResourceTemplate").GetComponent<Image>().sprite;
                decoded.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(resourceRing)));
                Assert.That(decoded.GetPixel(223, 128).a, Is.GreaterThan(.9f));
                Assert.That(decoded.GetPixel(235, 128).a, Is.GreaterThan(.9f));
                Assert.That(decoded.GetPixel(221, 128).a, Is.EqualTo(0f));
                Assert.That(decoded.GetPixel(237, 128).a, Is.EqualTo(0f));
            }
            finally { Object.DestroyImmediate(decoded); }
#endif
        }
    }
}
