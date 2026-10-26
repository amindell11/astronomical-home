using System;
using Combat.Targeting;
using Damage;
using Ships.Damage;
using Ships.Presentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ships.Visuals
{
    public sealed class ShieldVisuals : MonoBehaviour, IShipVisual
    {
        [SerializeField] private Material material;
        [SerializeField] private Color color = new(0.4f, 0.86f, 0.91f);
        [SerializeField, Min(0.1f)] private float fullHold = 1f;

        private readonly LineRenderer[] strokes = new LineRenderer[7];
        private ShipVisualRig rig;
        private Transform root;
        private IDamageEvents source;
        private LockChannel enemyLock;
        private bool subscribed;
        private float radius;
        private float hitAngle;
        private float hitAge = float.PositiveInfinity;
        private float breakAge = float.PositiveInfinity;
        private float fullAge = float.PositiveInfinity;
        private float recoveryAngle;

        private void Awake()
        {
            if (!material) throw new InvalidOperationException("Shield visuals require an authored material.");
            rig = GetComponentInParent<ShipVisualRig>();
            if (!rig) throw new InvalidOperationException("Shield visuals require a ship visual rig.");
            for (var i = 0; i < strokes.Length; i++)
            {
                var child = new GameObject($"Shield stroke {i}") { layer = gameObject.layer };
                child.transform.SetParent(transform, false);
                var stroke = child.AddComponent<LineRenderer>();
                stroke.sharedMaterial = material;
                stroke.useWorldSpace = true;
                stroke.alignment = LineAlignment.View;
                stroke.shadowCastingMode = ShadowCastingMode.Off;
                stroke.receiveShadows = false;
                stroke.numCapVertices = 2;
                stroke.enabled = false;
                strokes[i] = stroke;
            }
        }

        public void Bind(in ShipView view)
        {
            Unsubscribe();
            source = view.Damage ?? throw new ArgumentException("Shield visuals require damage resources.");
            root = view.Root;
            enemyLock = view.IsPlayer ? null : view.Lock;
            radius = 0f;
            foreach (var surface in rig.Silhouette)
            {
                var bounds = surface.localBounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var offset = surface.transform.TransformPoint(corner) - root.position;
                    radius = Mathf.Max(radius, new Vector2(offset.x, offset.y).magnitude);
                }
            }
            if (radius <= 0f) throw new InvalidOperationException("Shield visuals require a nonempty hull silhouette.");
            radius *= 1.12f;
            Clear();
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            Unsubscribe();
            Clear();
        }

        private void Subscribe()
        {
            if (subscribed || source == null) return;
            source.OnDamaged += OnDamaged;
            source.Shield.OnValueChanged += OnShieldChanged;
            source.Shield.OnFullyRegenerated += OnFullyRegenerated;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            source.OnDamaged -= OnDamaged;
            source.Shield.OnValueChanged -= OnShieldChanged;
            source.Shield.OnFullyRegenerated -= OnFullyRegenerated;
            subscribed = false;
        }

        private void Clear()
        {
            hitAge = breakAge = fullAge = float.PositiveInfinity;
            recoveryAngle = 0f;
            foreach (var stroke in strokes)
                if (stroke) stroke.enabled = false;
        }

        private void OnShieldChanged(float current, float previous, float max)
        {
            fullAge = float.PositiveInfinity;
            if (current >= max) hitAge = breakAge = float.PositiveInfinity;
        }

        private void OnFullyRegenerated() => fullAge = 0f;

        private void OnDamaged(AppliedDamage damage)
        {
            if (damage.Shield <= 0f) return;
            var offset = damage.Info.HitPoint - root.position;
            hitAngle = Mathf.Atan2(offset.y, offset.x);
            hitAge = 0f;
            if (source.Shield.CurrentValue <= 0f) breakAge = 0f;
        }

        private void LateUpdate()
        {
            if (source == null) return;
            hitAge += Time.deltaTime;
            breakAge += Time.deltaTime;
            fullAge += Time.deltaTime;
            foreach (var stroke in strokes) stroke.enabled = false;
            if (enemyLock is { IsConcealed: true }) return;

            if (source.Shield.IsRegenerating)
            {
                recoveryAngle += Time.deltaTime * 2.3f;
                Draw(strokes[0], radius, recoveryAngle - 1.4f, recoveryAngle, 0f, 0.85f);
            }
            if (fullAge < 0.24f + fullHold + 0.38f)
            {
                var close = Smooth(fullAge / 0.24f);
                var fade = Smooth((fullAge - 0.24f - fullHold) / 0.38f);
                Draw(strokes[0], radius * (1f + 0.1f * (1f - close) + 0.04f * fade),
                    recoveryAngle - 1.4f, recoveryAngle - 1.4f + Mathf.Lerp(1.4f, Mathf.PI * 2f, close),
                    0.8f * (1f - fade), 0.8f * (1f - fade));
            }
            if (hitAge < 0.48f)
            {
                var alpha = Smooth(hitAge / 0.035f) * (1f - Smooth((hitAge - 0.06f) / 0.42f));
                Draw(strokes[0], radius * (1f + 0.06f * hitAge), hitAngle - 0.75f, hitAngle + 0.75f, alpha, alpha);
            }
            if (breakAge >= 0.65f) return;
            var strength = Smooth(breakAge / 0.04f) * (1f - Smooth((breakAge - 0.06f) / 0.55f));
            for (var i = 0; i < 6; i++)
            {
                var angle = i * Mathf.PI / 3f + breakAge * 0.12f;
                Draw(strokes[i + 1], radius * (1f + 0.22f * Smooth(breakAge / 0.65f)),
                    angle + 0.12f + breakAge * 0.12f, angle + 0.68f, strength, strength);
            }
        }

        private void Draw(LineRenderer stroke, float size, float start, float end, float startAlpha, float endAlpha)
        {
            const int count = 65;
            stroke.positionCount = count;
            stroke.widthMultiplier = radius * 0.035f;
            var first = color;
            first.a *= startAlpha;
            var last = color;
            last.a *= endAlpha;
            stroke.startColor = first;
            stroke.endColor = last;
            var center = root.position - Vector3.forward * 0.05f;
            for (var i = 0; i < count; i++)
            {
                var angle = Mathf.Lerp(start, end, i / (float)(count - 1));
                stroke.SetPosition(i, center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * size);
            }
            stroke.enabled = true;
        }

        private static float Smooth(float value) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value));
    }
}
