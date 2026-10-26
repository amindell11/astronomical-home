using System;
using System.Collections.Generic;
using Combat.Weapons;
using Combat.Weapons.Conditions;
using Game.Player;
using Ships.Command;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Weapons
{
    public sealed class AimCrosshair : MonoBehaviour
    {
        [SerializeField] private RectTransform pointer;
        [SerializeField] private Image resourceTemplate;
        [SerializeField] private Image reloadTemplate;
        [SerializeField] private Color ink = new(.953f, .949f, .875f);
        [SerializeField] private Color track = new(.09f, .145f, .196f, .85f);
        [SerializeField] private Color empty = new(.376f, .455f, .506f);
        [SerializeField] private Color overheat = new(1f, .3f, .16f);

        private sealed class Resource
        {
            public WeaponSlot Slot;
            public IWeaponReadout Source;
            public GameObject Root;
            public Image Fill;
            public Image[] Rounds;
            public Image ReloadTrack;
            public Image ReloadFill;
        }

        private readonly List<Resource> resources = new();
        private PlayerControls controls;
        private Canvas rootCanvas;
        private RectTransform rect;
        private RectTransform parentRect;
        private IShipStatus ship;
        private Camera gameplayCamera;
        private float primaryUntil = float.NegativeInfinity;
        private float secondaryUntil = float.NegativeInfinity;
        private bool previousCursorVisible;

        private void Awake()
        {
            if (!pointer || !resourceTemplate || !reloadTemplate)
                throw new InvalidOperationException("AimCrosshair requires its pointer and resource image templates.");
            rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
            rect = (RectTransform)transform;
            parentRect = (RectTransform)transform.parent;
            controls = new PlayerControls();
        }

        public void Initialize(IShipStatus status, IWeaponReadouts weapons, Camera camera)
        {
            ship = status ?? throw new ArgumentNullException(nameof(status));
            if (!camera) throw new ArgumentNullException(nameof(camera));
            gameplayCamera = camera;
            foreach (var resource in resources)
            {
                resource.Root.SetActive(false);
                Destroy(resource.Root);
            }
            resources.Clear();
            primaryUntil = secondaryUntil = float.NegativeInfinity;
            if (weapons == null) return;
            foreach (var slot in weapons.Slots)
            {
                var index = 0;
                foreach (var readout in weapons.Readouts(slot))
                    if (readout is IHeatReadout or IAmmoReadout)
                        resources.Add(BuildResource(slot, readout, index++));
            }
        }

        private Resource BuildResource(WeaponSlot slot, IWeaponReadout source, int index)
        {
            var go = new GameObject(slot + "Resources", typeof(RectTransform));
            go.SetActive(false);
            var resourceRect = (RectTransform)go.transform;
            resourceRect.SetParent(transform, false);
            resourceRect.anchorMin = Vector2.zero;
            resourceRect.anchorMax = Vector2.one;
            resourceRect.sizeDelta = Vector2.zero;
            resourceRect.localScale = Vector3.one * (1f + index * .5f);
            var left = slot == WeaponSlot.Primary;
            var start = left ? 120f : 60f;
            var resource = new Resource { Slot = slot, Source = source, Root = go };
            if (source is IAmmoReadout { MaxAmmo: <= 8 } ammo)
            {
                var gap = ammo.MaxAmmo == 2 ? 25f : 4f;
                var span = (115f - gap * (ammo.MaxAmmo - 1)) / ammo.MaxAmmo;
                resource.Rounds = new Image[ammo.MaxAmmo];
                for (var i = 0; i < ammo.MaxAmmo; i++)
                    resource.Rounds[i] = AddArc(resourceTemplate, resourceRect, "Round" + i,
                        start + (left ? 1f : -1f) * i * (span + gap), span, left, empty);
            }
            else
            {
                AddArc(resourceTemplate, resourceRect, "Track", start, 115f, left, track);
                resource.Fill = AddArc(resourceTemplate, resourceRect, "Fill", start, 0f, left, ink);
            }
            if (source is IAmmoReadout)
            {
                resource.ReloadTrack = AddArc(reloadTemplate, resourceRect, "ReloadTrack", start, 115f, left, track);
                resource.ReloadFill = AddArc(reloadTemplate, resourceRect, "ReloadFill", start, 0f, left, ink);
                resource.ReloadTrack.gameObject.SetActive(false);
                resource.ReloadFill.gameObject.SetActive(false);
            }
            return resource;
        }

        private static Image AddArc(Image template, Transform parent, string name, float start, float span,
            bool clockwise, Color color)
        {
            var image = Instantiate(template, parent);
            image.name = name;
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -start);
            image.fillClockwise = clockwise;
            image.fillAmount = span / 360f;
            image.color = color;
            image.gameObject.SetActive(true);
            return image;
        }

        private void OnEnable()
        {
            controls.Flight.Enable();
            previousCursorVisible = Cursor.visible;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            controls.Flight.Disable();
            primaryUntil = secondaryUntil = float.NegativeInfinity;
            foreach (var resource in resources) resource.Root.SetActive(false);
            Cursor.visible = previousCursorVisible;
        }

        private void OnDestroy() => controls?.Dispose();

        private void LateUpdate()
        {
            var now = Time.unscaledTime;
            if (controls.Flight.PrimaryFire.IsPressed()) primaryUntil = now + .4f;
            if (controls.Flight.SecondaryFire.IsPressed()) secondaryUntil = now + .4f;
            var aim = controls.Flight.AimPoint.ReadValue<Vector2>();
            var uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, aim, uiCamera, out var point);
            rect.localPosition = point;
            var direction = aim - (Vector2)gameplayCamera.WorldToScreenPoint(ship.Transform.position);
            if (direction.sqrMagnitude > .0001f)
                pointer.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);

            foreach (var resource in resources)
            {
                resource.Root.SetActive(now < (resource.Slot == WeaponSlot.Primary ? primaryUntil : secondaryUntil));
                if (!resource.Root.activeSelf) continue;
                switch (resource.Source)
                {
                    case IHeatReadout heat:
                        resource.Fill.fillAmount = 115f / 360f * heat.HeatPct;
                        resource.Fill.color = heat.Overheated
                            ? Color.Lerp(ink, overheat, .65f + .35f * Mathf.Sin(now * 12f))
                            : ink;
                        break;
                    case IAmmoReadout ammo:
                        if (resource.Rounds != null)
                            for (var i = 0; i < resource.Rounds.Length; i++)
                                resource.Rounds[i].color = i < ammo.AmmoCount ? ink : empty;
                        else
                            resource.Fill.fillAmount = 115f / 360f * ammo.AmmoCount / ammo.MaxAmmo;
                        resource.ReloadTrack.gameObject.SetActive(ammo.IsReloading);
                        resource.ReloadFill.gameObject.SetActive(ammo.IsReloading);
                        resource.ReloadFill.fillAmount = 115f / 360f * ammo.ReloadProgress;
                        break;
                }
            }
        }
    }
}
