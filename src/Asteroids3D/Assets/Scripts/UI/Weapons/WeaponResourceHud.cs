using System;
using System.Collections.Generic;
using Combat.Weapons;
using Combat.Weapons.Conditions;
using Ships.Command;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Weapons
{
    public sealed class WeaponResourceHud : MonoBehaviour
    {
        [SerializeField] private Image resourceTemplate;
        [SerializeField] private Color ink = new(.953f, .949f, .875f);
        [SerializeField] private Color track = new(.639f, .651f, .667f, .3f);
        [SerializeField] private Color empty = new(.639f, .651f, .667f, .3f);
        [SerializeField] private Color threshold = new(.04f, .06f, .1f);
        [SerializeField] private Color overheat = new(.98f, .41f, .41f);

        private sealed class Resource
        {
            public IWeaponReadout Source;
            public GameObject Root;
            public Image Fill;
            public Image[] Rounds;
            public float RoundSpan;
            public int Capacity;
        }

        private readonly List<Resource> resources = new();
        private readonly List<Image> icons = new();
        private IWeaponReadouts readouts;

        private void Awake()
        {
            if (!resourceTemplate)
                throw new InvalidOperationException("WeaponResourceHud requires its resource image template.");
        }

        public void Initialize(IWeaponReadouts weapons)
        {
            readouts = weapons;
            foreach (var resource in resources)
            {
                resource.Root.SetActive(false);
                Destroy(resource.Root);
            }
            resources.Clear();
            foreach (var icon in icons)
            {
                icon.gameObject.SetActive(false);
                Destroy(icon.gameObject);
            }
            icons.Clear();
            if (weapons == null) return;
            foreach (var slot in weapons.Slots)
            {
                icons.Add(BuildIcon(slot, weapons.Icon(slot)));
                var index = 0;
                foreach (var readout in weapons.Readouts(slot))
                    if (readout is IHeatReadout or IAmmoReadout or IChargeReadout)
                        resources.Add(BuildResource(slot, readout, index++));
            }
        }

        private Image BuildIcon(WeaponSlot slot, Sprite sprite)
        {
            if (!sprite)
                throw new InvalidOperationException($"WeaponResourceHud requires an icon for the equipped {slot} weapon.");
            var icon = Instantiate(resourceTemplate, transform);
            icon.name = slot + "Icon";
            icon.sprite = sprite;
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.color = ink;
            var rect = icon.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = InstrumentCenter(slot);
            rect.sizeDelta = new Vector2(40f, 40f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            icon.gameObject.SetActive(true);
            return icon;
        }

        private static Vector2 InstrumentCenter(WeaponSlot slot) =>
            new(slot == WeaponSlot.Primary ? -96f : 96f, 0f);

        private Resource BuildResource(WeaponSlot slot, IWeaponReadout source, int index)
        {
            var go = new GameObject(slot + "Resources", typeof(RectTransform));
            var resourceRect = (RectTransform)go.transform;
            resourceRect.SetParent(transform, false);
            resourceRect.anchorMin = resourceRect.anchorMax = new Vector2(.5f, .5f);
            resourceRect.anchoredPosition = InstrumentCenter(slot);
            resourceRect.sizeDelta = new Vector2(92f, 92f);
            resourceRect.localScale = Vector3.one * (1f + index * .5f);
            var left = slot == WeaponSlot.Primary;
            var start = left ? 120f : 60f;
            var resource = new Resource { Source = source, Root = go };
            if (source is IAmmoReadout { MaxAmmo: <= 8, Refill: not Rounds.RefillMode.Sequential } ammo)
            {
                const float gap = 4f;
                var span = (120f - gap * (ammo.MaxAmmo - 1)) / ammo.MaxAmmo;
                resource.RoundSpan = span / 360f;
                resource.Rounds = new Image[ammo.MaxAmmo];
                for (var i = 0; i < ammo.MaxAmmo; i++)
                {
                    var angle = start + (left ? 1f : -1f) * i * (span + gap);
                    AddArc(resourceTemplate, resourceRect, "RoundTrack" + i, angle, span, left, empty);
                    resource.Rounds[i] = AddArc(resourceTemplate, resourceRect, "Round" + i,
                        angle, 0f, left, ink);
                }
            }
            else
            {
                AddArc(resourceTemplate, resourceRect, "Track", start, 120f, left, track);
                resource.Fill = AddArc(resourceTemplate, resourceRect, "Fill", start, 0f, left, ink);
                if (source is IAmmoReadout { Refill: Rounds.RefillMode.Sequential } shared)
                {
                    const float width = 2f;
                    for (var i = 1; i < shared.MaxAmmo; i++)
                    {
                        var angle = start + (left ? 1f : -1f) * (i * 120f / shared.MaxAmmo - width * .5f);
                        AddArc(resourceTemplate, resourceRect, "Threshold" + i, angle, width, left, threshold);
                    }
                }
            }
            if (source is IAmmoReadout capacity) resource.Capacity = capacity.MaxAmmo;
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

        private void LateUpdate()
        {
            if (resources.Exists(resource => resource.Source is IAmmoReadout { Refill: Rounds.RefillMode.Sequential } ammo
                    && resource.Capacity != ammo.MaxAmmo))
                Initialize(readouts);
            var now = Time.unscaledTime;
            foreach (var resource in resources)
            {
                switch (resource.Source)
                {
                    case IChargeReadout charge:
                        resource.Fill.fillAmount = 120f / 360f * charge.ChargePct;
                        break;
                    case IHeatReadout heat:
                        resource.Fill.fillAmount = 120f / 360f * heat.HeatPct;
                        var brightness = .825f + .175f * Mathf.Sin(now * 1.5f * Mathf.PI * 2f);
                        var color = heat.Overheated ? overheat * brightness : ink;
                        color.a = heat.Overheated ? overheat.a : ink.a;
                        resource.Fill.color = color;
                        break;
                    case IAmmoReadout ammo:
                        if (resource.Rounds != null)
                        {
                            for (var i = 0; i < resource.Rounds.Length; i++)
                                resource.Rounds[i].fillAmount = resource.RoundSpan * ammo.RoundProgress(i);
                            break;
                        }
                        var progress = ammo.Refill == Rounds.RefillMode.Sequential
                            ? (ammo.AmmoCount + ammo.ReloadProgress) / ammo.MaxAmmo
                            : ammo.IsReloading ? ammo.ReloadProgress : (float)ammo.AmmoCount / ammo.MaxAmmo;
                        resource.Fill.fillAmount = 120f / 360f * progress;
                        break;
                }
            }
        }
    }
}
