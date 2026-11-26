using System;
using Cameras;
using Damage;
using Ships.Damage;
using Ships.Registry;
using UI.Placement;
using UI.Projection;
using UI.Screens.Windows;
using UnityEngine;
using UnityEngine.UI;

namespace UI.PlayerState.Alerts
{
    /// <summary>
    /// The player's HULL CRITICAL alert: a coral glow and corner brackets at the screen edges, and a
    /// labelled band above the ship, shown while <see cref="HullCritical"/> holds. Both breathe slowly,
    /// scaled by the player's screen-shake setting; the profile's hull critical knobs set their strength.
    /// </summary>
    public sealed class HullCriticalAlert : MonoBehaviour
    {
        // Canvas units on the HUD's 800-wide canvas. The band clears the bottom-centre readouts below
        // and the top-right minimap, which reaches lower than the top-centre timer, above.
        internal const float BottomReserve = 96f;
        internal const float TopReserve = 182f;
        internal const float SideMargin = 12f;
        internal static readonly Vector2 BandSize = new(88f, 12f);
        private const float BandLift = 30f;

        private const float GlowReach = 22f;
        private const float GlowAlpha = .5f;
        private const float BracketInset = 8f;
        private const float BracketArm = 26f;
        private const float BracketChamfer = 6f;
        private const float StrokeWidth = .75f;
        private const float HairlineWidth = .5f;
        private const float LabelHalfWidth = 25f;

        private const float OnsetSeconds = .3f;
        private const float RestLevel = .7f;
        private const float PulseDepth = .3f;

        private static readonly Color Ink = WindowKit.Threat;

        private CanopyProjection projection;
        private Func<bool> isVisible;
        private Canvas canvas;
        private RectTransform area;
        private CanvasGroup edges;
        private CanvasGroup band;
        private RectTransform bandRect;
        private IDamageEvents damage;
        private Transform ship;
        private Camera worldCamera;
        private bool critical;
        private float criticalSince;

        public static HullCriticalAlert Create(Transform parent, Font font, Material material,
            CanopyProjection projection, Func<bool> isVisible)
        {
            if (!projection) throw new ArgumentNullException(nameof(projection));
            if (isVisible == null) throw new ArgumentNullException(nameof(isVisible));

            var go = new GameObject("HullCriticalAlert", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            InstrumentAppear.Attach(go, projection, isVisible);
            var alert = go.AddComponent<HullCriticalAlert>();
            alert.projection = projection;
            alert.isVisible = isVisible;

            alert.edges = Layer(go.transform, "Edges");
            AlertInk.Create(alert.edges.transform, "Edge ink", DrawEdges);

            alert.band = Layer(go.transform, "Band");
            alert.bandRect = (RectTransform)alert.band.transform;
            alert.bandRect.anchorMin = alert.bandRect.anchorMax = Vector2.zero;
            alert.bandRect.sizeDelta = BandSize;
            AlertInk.Create(alert.band.transform, "Band ink", DrawBand);
            var label = new GameObject("Label", typeof(RectTransform)).AddComponent<Text>();
            label.gameObject.layer = go.layer;
            label.transform.SetParent(alert.band.transform, false);
            label.font = font;
            label.material = material;
            label.fontSize = 6;
            label.color = Ink;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.rectTransform.sizeDelta = BandSize;
            label.text = "HULL CRITICAL";
            return alert;

            CanvasGroup Layer(Transform owner, string layerName)
            {
                var layer = new GameObject(layerName, typeof(RectTransform)).AddComponent<CanvasGroup>();
                layer.gameObject.layer = owner.gameObject.layer;
                layer.transform.SetParent(owner, false);
                var layerRect = (RectTransform)layer.transform;
                layerRect.anchorMin = Vector2.zero;
                layerRect.anchorMax = Vector2.one;
                layerRect.sizeDelta = Vector2.zero;
                layer.alpha = 0f;
                layer.interactable = layer.blocksRaycasts = false;
                return layer;
            }
        }

        private void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
            area = (RectTransform)transform;
        }

        public void Initialize(IDamageEvents damageEvents, Transform playerShip, Camera camera)
        {
            if (damageEvents == null) throw new ArgumentNullException(nameof(damageEvents));
            if (!playerShip) throw new ArgumentNullException(nameof(playerShip));
            if (!camera) throw new ArgumentNullException(nameof(camera));
            Unsubscribe();
            damage = damageEvents;
            ship = playerShip;
            worldCamera = camera;
            damage.Health.OnValueChanged += OnResourceChanged;
            damage.Shield.OnValueChanged += OnResourceChanged;
            damage.OnDeath += OnDeath;
            critical = false;
            Evaluate();
        }

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (damage == null) return;
            damage.Health.OnValueChanged -= OnResourceChanged;
            damage.Shield.OnValueChanged -= OnResourceChanged;
            damage.OnDeath -= OnDeath;
        }

        private void OnResourceChanged(float current, float previous, float max) => Evaluate();

        private void OnDeath(ShipId victim, DamageInfo killingBlow) => critical = false;

        private void Evaluate()
        {
            var holds = HullCritical.Holds(damage);
            if (holds && !critical) criticalSince = Time.unscaledTime;
            critical = holds;
        }

        private void LateUpdate()
        {
            if (!critical || !isVisible())
            {
                edges.alpha = band.alpha = 0f;
                return;
            }

            var profile = projection.Profile;
            var level = Level(Time.unscaledTime - criticalSince, profile.hullCriticalPulseSeconds,
                CameraSettings.ShakeScale);
            edges.alpha = profile.hullCriticalEdgeIntensity * level;
            band.alpha = profile.hullCriticalBandIntensity * level;
            var point = (Vector2)worldCamera.WorldToScreenPoint(ship.position) / canvas.scaleFactor;
            ScreenPin.Place(bandRect, canvas,
                BandCentre(point + Vector2.up * BandLift, area.rect.size) * canvas.scaleFactor);
        }

        internal static float Level(float age, float period, float swing) =>
            Mathf.Clamp01(age / OnsetSeconds) *
            (RestLevel + PulseDepth * swing * Mathf.Sin(2f * Mathf.PI * age / period));

        internal static Vector2 BandCentre(Vector2 wanted, Vector2 canvasSize)
        {
            var half = BandSize * .5f;
            var minX = SideMargin + half.x;
            var minY = BottomReserve + half.y;
            // A screen too short for both reserves pins the band to the readouts' edge.
            return new Vector2(
                Mathf.Clamp(wanted.x, minX, Mathf.Max(minX, canvasSize.x - SideMargin - half.x)),
                Mathf.Clamp(wanted.y, minY, Mathf.Max(minY, canvasSize.y - TopReserve - half.y)));
        }

        private static void DrawEdges(VertexHelper mesh, Rect area)
        {
            var lit = new Color(Ink.r, Ink.g, Ink.b, GlowAlpha);
            var clear = new Color(Ink.r, Ink.g, Ink.b, 0f);
            var bl = area.min;
            var br = new Vector2(area.xMax, area.yMin);
            var tr = area.max;
            var tl = new Vector2(area.xMin, area.yMax);
            var reach = Vector2.one * GlowReach;
            var ibl = bl + reach;
            var itr = tr - reach;
            var ibr = new Vector2(itr.x, ibl.y);
            var itl = new Vector2(ibl.x, itr.y);
            AlertInk.Quad(mesh, bl, br, ibr, ibl, lit, clear);
            AlertInk.Quad(mesh, br, tr, itr, ibr, lit, clear);
            AlertInk.Quad(mesh, tr, tl, itl, itr, lit, clear);
            AlertInk.Quad(mesh, tl, bl, ibl, itl, lit, clear);
            Bracket(bl, 1f, 1f);
            Bracket(br, -1f, 1f);
            Bracket(tr, -1f, -1f);
            Bracket(tl, 1f, -1f);

            void Bracket(Vector2 corner, float x, float y)
            {
                var arm = corner + new Vector2(x * BracketInset, y * (BracketInset + BracketArm));
                var bevelStart = corner + new Vector2(x * BracketInset, y * (BracketInset + BracketChamfer));
                var bevelEnd = corner + new Vector2(x * (BracketInset + BracketChamfer), y * BracketInset);
                var foot = corner + new Vector2(x * (BracketInset + BracketArm), y * BracketInset);
                AlertInk.Stroke(mesh, arm, bevelStart, StrokeWidth, Ink);
                AlertInk.Stroke(mesh, bevelStart, bevelEnd, StrokeWidth, Ink);
                AlertInk.Stroke(mesh, bevelEnd, foot, StrokeWidth, Ink);
            }
        }

        private static void DrawBand(VertexHelper mesh, Rect area)
        {
            var hairline = new Color(Ink.r, Ink.g, Ink.b, .55f);
            AlertInk.Stroke(mesh, new Vector2(area.xMin, area.yMax), area.max, HairlineWidth, hairline);
            AlertInk.Stroke(mesh, area.min, new Vector2(area.xMax, area.yMin), HairlineWidth, hairline);
            var rise = area.height * .25f;
            for (var tick = 0; tick < 3; tick++)
            {
                var x = LabelHalfWidth + 3f + tick * 4f;
                Tick(x);
                Tick(-x - 2.5f);
            }

            void Tick(float x) => AlertInk.Stroke(mesh, new Vector2(x, area.center.y - rise),
                new Vector2(x + 2.5f, area.center.y + rise), StrokeWidth, Ink);
        }
    }
}
