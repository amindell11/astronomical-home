using System;
using System.Collections;
using System.Collections.Generic;
using Combat.Weapons;
using Combat.Weapons.Conditions;
using NUnit.Framework;
using Ships.Command;
using UI.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

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
        private AimCrosshair crosshair;
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
            ammo = new Ammo();
            crosshair = AimCrosshair.Create(root.transform);
            crosshair.Initialize(new WeaponSources(ammo));
            crosshair.gameObject.SetActive(true);
        }

        public override void TearDown()
        {
            Object.DestroyImmediate(root);
            Cursor.visible = cursorWasVisible;
            base.TearDown();
        }

        private Mesh Draw()
        {
            crosshair.SendMessage("LateUpdate");
            Canvas.ForceUpdateCanvases();
            crosshair.Rebuild(CanvasUpdate.PreRender);
            return Object.Instantiate(crosshair.canvasRenderer.GetMesh());
        }

        [Test]
        public void TriggerReadouts_AreIndependent_AndRebindClearsTheirSources()
        {
            using var idle = new MeshLease(Draw());
            Assert.That(idle.Value.bounds.min.x, Is.GreaterThan(-12f));
            Press(mouse.leftButton);
            using var primary = new MeshLease(Draw());
            Assert.That(primary.Value.bounds.min.x, Is.LessThan(-18f));
            Assert.That(primary.Value.bounds.max.x, Is.LessThan(12f));
            Press(mouse.rightButton);
            using var both = new MeshLease(Draw());
            Assert.That(both.Value.bounds.max.x, Is.GreaterThan(18f));
            crosshair.Initialize(null);
            using var unarmed = new MeshLease(Draw());
            Assert.That(unarmed.Value.bounds.size.x, Is.EqualTo(idle.Value.bounds.size.x).Within(.01f));
        }

        [UnityTest]
        public IEnumerator Release_LingersThenHides_AndDisableRestoresCursor()
        {
            Assert.IsFalse(Cursor.visible);
            Press(mouse.rightButton);
            yield return null;
            using var held = new MeshLease(Draw());
            Assert.That(held.Value.bounds.max.x, Is.GreaterThan(18f));
            Release(mouse.rightButton);
            yield return null;
            using var released = new MeshLease(Draw());
            Assert.That(released.Value.bounds.max.x, Is.GreaterThan(18f));
            yield return new WaitForSecondsRealtime(.45f);
            using var idle = new MeshLease(Draw());
            Assert.That(idle.Value.bounds.max.x, Is.LessThan(12f));
            crosshair.gameObject.SetActive(false);
            Assert.IsTrue(Cursor.visible);
            crosshair.gameObject.SetActive(true);
            using var reenabled = new MeshLease(Draw());
            Assert.That(reenabled.Value.bounds.max.x, Is.LessThan(12f));
        }

        [Test]
        public void ReloadProgress_AddsAnArc_AndEmptyAmmoChangesTheResourceInk()
        {
            Press(mouse.rightButton);
            using var ready = new MeshLease(Draw());
            var readyColors = ready.Value.colors32;
            ammo.AmmoCount = 0;
            using var empty = new MeshLease(Draw());
            Assert.That(empty.Value.colors32, Is.Not.EqualTo(readyColors));
            ammo.IsReloading = true;
            using var reload = new MeshLease(Draw());
            Assert.That(reload.Value.bounds.max.x, Is.GreaterThan(ready.Value.bounds.max.x + 4f));
        }

        [Test]
        public void AimPoint_TracksTheMouse_AtFixedPixelSizeUnderCanvasScaling()
        {
            var canvas = root.GetComponent<Canvas>();
            canvas.scaleFactor = 2f;
            var aim = new Vector2(Screen.width * .4f, Screen.height * .6f);
            Set(mouse.position, aim);
            using var mesh = new MeshLease(Draw());
            var actual = RectTransformUtility.WorldToScreenPoint(null, crosshair.rectTransform.TransformPoint(Vector3.zero));
            Assert.That(Vector2.Distance(actual, aim), Is.LessThan(.1f));
            Assert.That(crosshair.rectTransform.localScale.x * canvas.scaleFactor, Is.EqualTo(1f));
            Assert.That(mesh.Value.vertices, Has.Some.EqualTo(Vector3.zero));
        }

        private sealed class MeshLease : IDisposable
        {
            public Mesh Value { get; }
            public MeshLease(Mesh value) => Value = value;
            public void Dispose() => Object.DestroyImmediate(Value);
        }
    }
}
