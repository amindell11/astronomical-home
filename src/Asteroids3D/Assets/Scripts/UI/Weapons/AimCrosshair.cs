using System.Collections.Generic;
using Combat.Weapons;
using Combat.Weapons.Conditions;
using Game.Player;
using Ships.Command;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Weapons
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AimCrosshair : MaskableGraphic
    {
        private static readonly Color Ink = new(.953f, .949f, .875f);
        private static readonly Color Track = new(.09f, .145f, .196f, .85f);
        private static readonly Color Empty = new(.376f, .455f, .506f);
        private static readonly Color Shadow = new(.027f, .063f, .114f);
        private readonly List<(WeaponSlot Slot, IWeaponReadout Readout)> resources = new();
        private PlayerControls controls;
        private Canvas rootCanvas;
        private RectTransform parentRect;
        private float primaryUntil = float.NegativeInfinity;
        private float secondaryUntil = float.NegativeInfinity;
        private bool previousCursorVisible;
        private bool primaryVisible;
        private bool secondaryVisible;

        public static AimCrosshair Create(Transform parent)
        {
            var go = new GameObject("AimCrosshair", typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(96f, 96f);
            return go.AddComponent<AimCrosshair>();
        }

        protected override void Awake()
        {
            base.Awake();
            rootCanvas = GetComponentInParent<Canvas>().rootCanvas;
            parentRect = (RectTransform)transform.parent;
            controls = new PlayerControls();
            raycastTarget = false;
        }

        public void Initialize(IWeaponReadouts weapons)
        {
            resources.Clear();
            primaryUntil = secondaryUntil = float.NegativeInfinity;
            primaryVisible = secondaryVisible = false;
            if (weapons != null)
                foreach (var slot in weapons.Slots)
                foreach (var readout in weapons.Readouts(slot))
                    if (readout is IHeatReadout or IAmmoReadout)
                        resources.Add((slot, readout));
            SetVerticesDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            controls.Flight.Enable();
            previousCursorVisible = Cursor.visible;
            Cursor.visible = false;
        }

        protected override void OnDisable()
        {
            controls.Flight.Disable();
            primaryUntil = secondaryUntil = float.NegativeInfinity;
            primaryVisible = secondaryVisible = false;
            Cursor.visible = previousCursorVisible;
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            controls?.Dispose();
            base.OnDestroy();
        }

        private void LateUpdate()
        {
            var now = Time.unscaledTime;
            if (controls.Flight.PrimaryFire.IsPressed()) primaryUntil = now + .4f;
            if (controls.Flight.SecondaryFire.IsPressed()) secondaryUntil = now + .4f;
            primaryVisible = now < primaryUntil;
            secondaryVisible = now < secondaryUntil;
            var camera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect,
                controls.Flight.AimPoint.ReadValue<Vector2>(), camera, out var point);
            rectTransform.localPosition = point;
            rectTransform.localScale = Vector3.one / rootCanvas.scaleFactor;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Triangle(mesh, 3.5f, 2.2f, new Vector2(0f, .7f), Shadow);
            Triangle(mesh, 3f, 1.5f, Vector2.zero, Ink);
            Arc(mesh, 9f, 120f, 215f, 4.5f, Shadow);
            Arc(mesh, 9f, -35f, 60f, 4.5f, Shadow);
            Arc(mesh, 9f, 120f, 215f, 3f, Ink);
            Arc(mesh, 9f, -35f, 60f, 3f, Ink);
            var leftRadius = 20f;
            var rightRadius = 20f;
            foreach (var (slot, readout) in resources)
            {
                var left = slot == WeaponSlot.Primary;
                if (!(left ? primaryVisible : secondaryVisible)) continue;
                var radius = left ? leftRadius : rightRadius;
                var start = left ? 120f : 60f;
                var end = left ? 235f : -55f;
                switch (readout)
                {
                    case IHeatReadout heat:
                        Arc(mesh, radius, start, end, 4f, Track);
                        var heatInk = heat.Overheated
                            ? Color.Lerp(Ink, new Color(1f, .3f, .16f), .65f + .35f * Mathf.Sin(Time.unscaledTime * 12f))
                            : Ink;
                        Arc(mesh, radius, start, Mathf.Lerp(start, end, heat.HeatPct), 4f, heatInk);
                        break;
                    case IAmmoReadout ammo:
                        Ammo(mesh, ammo, radius, start, end);
                        break;
                }
                if (left) leftRadius += 12f;
                else rightRadius += 12f;
            }
        }

        private static void Ammo(VertexHelper mesh, IAmmoReadout ammo, float radius, float start, float end)
        {
            if (ammo.MaxAmmo <= 8)
            {
                var gap = ammo.MaxAmmo == 2 ? 25f : 4f;
                var span = (115f - gap * (ammo.MaxAmmo - 1)) / ammo.MaxAmmo;
                var direction = Mathf.Sign(end - start);
                for (var i = 0; i < ammo.MaxAmmo; i++)
                {
                    var segmentStart = start + direction * i * (span + gap);
                    Arc(mesh, radius, segmentStart, segmentStart + direction * span,
                        4f, i < ammo.AmmoCount ? Ink : Empty);
                }
            }
            else
            {
                Arc(mesh, radius, start, end, 4f, Track);
                Arc(mesh, radius, start, Mathf.Lerp(start, end, (float)ammo.AmmoCount / ammo.MaxAmmo), 4f, Ink);
            }
            if (!ammo.IsReloading) return;
            Arc(mesh, radius + 6f, start, end, 2f, Track);
            Arc(mesh, radius + 6f, start, Mathf.Lerp(start, end, ammo.ReloadProgress), 2f, Ink);
        }

        private static void Triangle(VertexHelper mesh, float halfBase, float thickness, Vector2 origin, Color ink)
        {
            var height = halfBase * Mathf.Sqrt(3f);
            var inset = thickness * Mathf.Sqrt(3f) / 2f;
            for (var side = -1; side <= 1; side += 2)
                Quad(mesh, origin, origin + new Vector2(side * halfBase, -height),
                    origin + new Vector2(side * (halfBase - inset), -height - thickness / 2f),
                    origin + new Vector2(0f, -thickness * 2f), ink);
        }

        private static void Arc(VertexHelper mesh, float radius, float start, float end, float width, Color ink)
        {
            var segments = Mathf.CeilToInt(Mathf.Abs(end - start) / 4f);
            for (var i = 0; i < segments; i++)
            {
                var a = Mathf.Lerp(start, end, (float)i / segments) * Mathf.Deg2Rad;
                var b = Mathf.Lerp(start, end, (float)(i + 1) / segments) * Mathf.Deg2Rad;
                var first = new Vector2(Mathf.Cos(a), -Mathf.Sin(a));
                var second = new Vector2(Mathf.Cos(b), -Mathf.Sin(b));
                Quad(mesh, first * (radius + width / 2f), second * (radius + width / 2f),
                    second * (radius - width / 2f), first * (radius - width / 2f), ink);
            }
        }

        private static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color ink)
        {
            var n = mesh.currentVertCount;
            mesh.AddVert(a, ink, Vector2.zero);
            mesh.AddVert(b, ink, Vector2.zero);
            mesh.AddVert(c, ink, Vector2.zero);
            mesh.AddVert(d, ink, Vector2.zero);
            mesh.AddTriangle(n, n + 1, n + 2);
            mesh.AddTriangle(n, n + 2, n + 3);
        }
    }
}
