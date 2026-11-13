using Damage;
using Ships;
using Substrate;
using System.Collections.Generic;
using UnityEngine;
using UI.Placement;
using UI.Projection;
using System;

namespace UI.PlayerState
{
    public sealed class ShipStatusIndicator : MonoBehaviour
    {
        private Ship ship;
        private Camera worldCamera;
        private Canvas canvas;
        private RectTransform rect;
        private CanvasGroup visibility;
        private IReadOnlyList<Renderer> silhouette;
        private StatusBarUI shieldBar;
        private StatusBarUI healthBar;
        private Ship player;
        private bool numbersShown = true;
        private float playerHitAt = float.NegativeInfinity;
        private float focus;

        public static ShipStatusIndicator Create(Canvas canvas, Camera worldCamera, Ship ship, Font font, Material textMaterial,
            CanopyProjection projection = null, Func<bool> isVisible = null)
        {
            var root = new GameObject("Ship status", typeof(RectTransform), typeof(CanvasGroup));
            root.SetActive(false);
            root.layer = canvas.gameObject.layer;
            root.transform.SetParent(canvas.transform, false);
            var indicator = root.AddComponent<ShipStatusIndicator>();
            indicator.ship = ship;
            indicator.worldCamera = worldCamera;
            indicator.canvas = canvas;
            var appearance = InstrumentAppear.Create(root.transform, projection,
                () => indicator.visibility.alpha > 0f && (isVisible?.Invoke() ?? true));
            indicator.shieldBar = StatusBarUI.Create(appearance.transform, ship.Damage.Shield, font, new Color(0f, .8f, 1f), 18f, textMaterial);
            indicator.healthBar = StatusBarUI.Create(appearance.transform, ship.Damage.Health, font, new Color(.22f, 1f, .15f), 0f, textMaterial, pulsesWhenLow: true);
            root.SetActive(true);
            return indicator;
        }

        private void Awake()
        {
            rect = GetComponent<RectTransform>();
            visibility = GetComponent<CanvasGroup>();
            visibility.blocksRaycasts = false;
            visibility.interactable = false;
            silhouette = ship.VisualRig && ship.VisualRig.Silhouette.Count > 0
                ? ship.VisualRig.Silhouette
                : ship.GetComponentsInChildren<MeshRenderer>(true);
            ship.Damage.OnDamaged += RecordHit;
        }

        private void OnDestroy()
        {
            if (ship) ship.Damage.OnDamaged -= RecordHit;
        }

        public void Present(Ship player, bool targeted, CanopyProjectionProfile feel)
        {
            this.player = player;
            var own = ship == player;
            var shown = own || targeted || Time.time - playerHitAt < feel.hitBarsSeconds;
            var fade = shown ? feel.barsFadeInSeconds : feel.barsFadeOutSeconds;
            focus = own ? 1f : Mathf.MoveTowards(focus, shown ? 1f : 0f, fade > 0f ? Time.deltaTime / fade : 1f);
            var numbers = own || targeted;
            if (numbers == numbersShown) return;
            numbersShown = numbers;
            shieldBar.ShowNumbers(numbers);
            healthBar.ShowNumbers(numbers);
        }

        private void RecordHit(AppliedDamage hit)
        {
            if (player && hit.Info.AttackerId == player.Id) playerHitAt = Time.time;
        }

        private void LateUpdate()
        {
            var visible = ship && ship.gameObject.activeInHierarchy &&
                (!ship.VisualRig || ship.VisualRig.gameObject.activeInHierarchy) &&
                (ship.CompareTag(TagNames.Player) || !ship.Lock.IsConcealed);
            visibility.alpha = visible ? focus : 0f;
            if (visibility.alpha <= 0f) return;

            var point = worldCamera.WorldToScreenPoint(ship.transform.position);
            if (point.z < 0f)
            {
                visibility.alpha = 0f;
                return;
            }

            var top = point.y;
            foreach (var renderer in silhouette)
            {
                var bounds = renderer.bounds;
                top = Mathf.Max(top, worldCamera.WorldToScreenPoint(
                    new Vector3(bounds.center.x, bounds.max.y, bounds.center.z)).y);
            }
            point.y = top + 12f;
            ScreenPin.Place(rect, canvas, point);
            rect.localScale = Vector3.one * Mathf.Clamp(17f / worldCamera.orthographicSize, .8f, 1.15f);
        }
    }
}
