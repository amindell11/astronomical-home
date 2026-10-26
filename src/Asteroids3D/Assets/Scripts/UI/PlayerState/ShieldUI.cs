using System;
using Combat.Targeting;
using Damage;
using Ships.Damage;
using Ships.Presentation;
using Substrate;
using UnityEngine;
using UnityEngine.UI;

namespace UI.PlayerState
{
    [RequireComponent(typeof(Image))]
    public sealed class ShieldUI : MonoBehaviour, IShipVisual
    {
        [SerializeField, Min(0.1f)] private float fullHold = 1f;

        private static readonly int ArcLength = Shader.PropertyToID("_ArcLength");
        private static readonly int TipStrength = Shader.PropertyToID("_TipStrength");
        private static readonly int RingRotation = Shader.PropertyToID("_RingRotation");
        private IDamageEvents source;
        private ShipVisualRig rig;
        private Transform root;
        private LockChannel enemyLock;
        private Image ring;
        private Material material;
        private bool subscribed;
        private float hitAngle;
        private float hitAge = float.PositiveInfinity;
        private float breakAge = float.PositiveInfinity;
        private float fullAge = float.PositiveInfinity;

        private void Awake()
        {
            ring = GetComponent<Image>();
            rig = GetComponentInParent<ShipVisualRig>();
            if (!rig) throw new InvalidOperationException("Shield ring requires a ship visual rig.");
            if (!ring.material || !ring.material.HasProperty(ArcLength))
                throw new InvalidOperationException("Shield ring requires its ShieldGlow material.");
            material = new Material(ring.material);
            ring.material = material;
            Clear();
        }

        public void Bind(in ShipView view)
        {
            Unsubscribe();
            source = view.Damage ?? throw new ArgumentException("Shield ring requires damage resources.");
            root = view.Root;
            enemyLock = view.IsPlayer ? null : view.Lock;
            transform.SetPositionAndRotation(root.position - Vector3.forward * 0.05f, GamePlane.Rotation);
            var radius = 0f;
            foreach (var surface in rig.Silhouette)
            {
                var bounds = surface.localBounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var offset = transform.InverseTransformVector(surface.transform.TransformPoint(corner) - root.position);
                    radius = Mathf.Max(radius, new Vector2(offset.x, offset.y).magnitude);
                }
            }
            if (radius <= 0f) throw new InvalidOperationException("Shield ring requires a nonempty hull silhouette.");
            ring.rectTransform.sizeDelta = Vector2.one * (radius * 2f * 1.12f / 0.9f);
            Clear();
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable()
        {
            if (source != null) Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            Clear();
        }

        private void OnDestroy()
        {
            if (material) Destroy(material);
        }

        private void Subscribe()
        {
            if (subscribed) return;
            source.Shield.OnValueChanged += OnShieldChanged;
            source.Shield.OnFullyRegenerated += OnFullyRegenerated;
            source.OnDamaged += OnDamaged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            source.Shield.OnValueChanged -= OnShieldChanged;
            source.Shield.OnFullyRegenerated -= OnFullyRegenerated;
            source.OnDamaged -= OnDamaged;
            subscribed = false;
        }

        private void Clear()
        {
            hitAge = breakAge = fullAge = float.PositiveInfinity;
            if (ring) ring.enabled = false;
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
            if (source.Shield.CurrentValue <= 0f)
            {
                breakAge = 0f;
                hitAge = float.PositiveInfinity;
            }
        }

        private void LateUpdate()
        {
            if (source == null) return;
            hitAge += Time.deltaTime;
            breakAge += Time.deltaTime;
            fullAge += Time.deltaTime;
            ring.enabled = false;
            if (enemyLock is { IsConcealed: true }) return;

            if (breakAge < 0.42f)
            {
                var progress = breakAge / 0.42f;
                var fill = 1f - Smooth(progress);
                Draw(fill, hitAngle + Mathf.PI * (1f - fill),
                    Smooth(breakAge / 0.025f) * (1f - Smooth((progress - 0.65f) / 0.35f)), 1f);
            }
            else if (hitAge < 0.48f)
            {
                Draw(1.5f / (Mathf.PI * 2f), hitAngle - 0.75f,
                    Smooth(hitAge / 0.035f) * (1f - Smooth((hitAge - 0.06f) / 0.42f)), 0f);
            }
            else if (source.Shield.IsRegenerating)
            {
                var fill = source.Shield.Pct;
                Draw(fill, hitAngle + Mathf.PI * (1f - fill), 1f, 1f - Smooth((fill - 0.92f) / 0.08f));
            }
            else if (fullAge < fullHold + 0.38f)
            {
                Draw(1f, hitAngle, 1f - Smooth((fullAge - fullHold) / 0.38f), 0f);
            }
        }

        private void Draw(float fill, float start, float alpha, float tips)
        {
            transform.SetPositionAndRotation(root.position - Vector3.forward * 0.05f,
                GamePlane.Rotation * Quaternion.AngleAxis(start * Mathf.Rad2Deg - 90f, Vector3.forward));
            ring.fillAmount = fill;
            ring.color = new Color(1f, 1f, 1f, alpha);
            material.SetFloat(ArcLength, fill * Mathf.PI * 2f);
            material.SetFloat(TipStrength, tips);
            material.SetFloat(RingRotation, start - Mathf.PI * 0.5f);
            ring.enabled = alpha > 0f && fill > 0f;
        }

        private static float Smooth(float value) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value));
    }
}
