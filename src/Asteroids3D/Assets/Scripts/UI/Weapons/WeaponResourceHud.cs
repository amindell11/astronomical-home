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
        [SerializeField] private Color overheat = new(1f, .3f, .16f);

        private sealed class Resource
        {
            public IWeaponReadout Source;
            public GameObject Root;
            public Image Fill;
            public Image[] Rounds;
            public float RoundSpan;
        }

        private readonly List<Resource> resources = new();

        private void Awake()
        {
            if (!resourceTemplate)
                throw new InvalidOperationException("WeaponResourceHud requires its resource image template.");
        }

        public void Initialize(IWeaponReadouts weapons)
        {
            foreach (var resource in resources)
            {
                resource.Root.SetActive(false);
                Destroy(resource.Root);
            }
            resources.Clear();
            if (weapons == null) return;
            foreach (var slot in weapons.Slots)
            {
                var index = 0;
                foreach (var readout in weapons.Readouts(slot))
                    if (readout is IHeatReadout or IAmmoReadout or IChargeReadout)
                        resources.Add(BuildResource(slot, readout, index++));
            }
        }

        private Resource BuildResource(WeaponSlot slot, IWeaponReadout source, int index)
        {
            var go = new GameObject(slot + "Resources", typeof(RectTransform));
            var resourceRect = (RectTransform)go.transform;
            resourceRect.SetParent(transform, false);
            resourceRect.anchorMin = Vector2.zero;
            resourceRect.anchorMax = Vector2.one;
            resourceRect.sizeDelta = Vector2.zero;
            resourceRect.localScale = Vector3.one * (1f + index * .5f);
            var left = slot == WeaponSlot.Primary;
            var start = left ? 120f : 60f;
            var resource = new Resource { Source = source, Root = go };
            if (source is IAmmoReadout { MaxAmmo: <= 8 } ammo)
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

        private void LateUpdate()
        {
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
                        resource.Fill.color = heat.Overheated
                            ? Color.Lerp(ink, overheat, .65f + .35f * Mathf.Sin(now * 12f))
                            : ink;
                        break;
                    case IAmmoReadout ammo:
                        if (resource.Rounds != null)
                        {
                            for (var i = 0; i < resource.Rounds.Length; i++)
                                resource.Rounds[i].fillAmount = resource.RoundSpan * ammo.RoundProgress(i);
                            break;
                        }
                        resource.Fill.fillAmount = 120f / 360f * (ammo.IsReloading
                            ? ammo.ReloadProgress
                            : (float)ammo.AmmoCount / ammo.MaxAmmo);
                        break;
                }
            }
        }
    }
}
