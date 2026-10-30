using System;
using System.Collections;
using Combat.Weapons.Arsenal;
using Substrate.Presentation;
using UnityEngine;

namespace Combat.Weapons
{
    /// <summary>
    /// Owns the railgun's one line renderer: inward charge dashes and a contracting SVG lance.
    /// The weapon supplies the exact shot endpoints; this component changes only presentation.
    /// </summary>
    [RequireComponent(typeof(Railguns), typeof(LineRenderer))]
    public sealed class RailBeamVisual : MonoBehaviour, IPresentationPart
    {
        [Tooltip("Seconds the beam takes to fade out after a shot.")]
        [SerializeField, Min(0.01f)] private float fadeTime = 0.34f;
        [SerializeField, Min(0f)] private float flashTime = 0.06f;

        [Header("Aim Line")]
        [Tooltip("Maximum dash width in world units.")]
        [SerializeField, Min(0.01f)] private float aimWidth = 0.12f;
        [Tooltip("Aim line opacity, as a fraction of the beam's, at full charge.")]
        [SerializeField, Range(0f, 1f)] private float aimOpacityAtFull = 0.68f;
        [SerializeField, Min(1f)] private float aimOpacityPower = 2.2f;
        [Tooltip("Distance between successive dash tips, independent of target distance.")]
        [SerializeField, Min(0.01f)] private float dashSpacing = 6f;
        [SerializeField, Range(0.01f, 0.99f)] private float dashGap = 0.3f;
        [Tooltip("Inward speed in world units per second; accelerates with charge squared.")]
        [SerializeField, Min(0f)] private float dashSpeedAtStart = 10.8f;
        [SerializeField, Min(0f)] private float dashSpeedAtFull = 46.8f;

        private static readonly int ShotId = Shader.PropertyToID("_Shot");
        private static readonly int ShotAgeId = Shader.PropertyToID("_ShotAge");
        private static readonly int BeamLengthId = Shader.PropertyToID("_BeamLength");
        private static readonly int LineWidthId = Shader.PropertyToID("_LineWidth");
        private static readonly int DashPhaseId = Shader.PropertyToID("_DashPhase");
        private static readonly int DashSpacingId = Shader.PropertyToID("_DashSpacing");
        private static readonly int DashGapId = Shader.PropertyToID("_DashGap");

        private Railguns weapon;
        private LineRenderer line;
        private Color beamStartColor;
        private Color beamEndColor;
        private float beamWidth;
        private float dashPhase;
        private MaterialPropertyBlock properties;
        private Coroutine fade;

        private void Awake()
        {
            ValidateSettings();
            weapon = GetComponent<Railguns>();
            line = GetComponent<LineRenderer>();
            line.positionCount = 2;
            beamStartColor = line.startColor;
            beamEndColor = line.endColor;
            beamWidth = line.widthMultiplier;
            properties = new MaterialPropertyBlock();
            properties.SetFloat(DashSpacingId, dashSpacing);
            properties.SetFloat(DashGapId, dashGap);
            Clear();
        }

        private void OnValidate() => ValidateSettings();

        private void ValidateSettings()
        {
            if (!(fadeTime > 0f && flashTime >= 0f && flashTime < fadeTime && aimWidth > 0f &&
                  aimOpacityAtFull >= 0f && aimOpacityAtFull <= 1f && aimOpacityPower >= 1f &&
                  dashSpacing > 0f && dashGap > 0f && dashGap < 1f &&
                  dashSpeedAtStart >= 0f && dashSpeedAtFull > dashSpeedAtStart))
                throw new InvalidOperationException($"{nameof(RailBeamVisual)} has invalid charge or shot settings.");
        }

        public void ApplyPresentation(bool visible)
        {
            enabled = visible;
            // Presentation can be applied before Awake.
            if (line) line.enabled = false;
        }

        private void OnEnable()
        {
            weapon.OnBeamFired += ShowBeam;
            weapon.Charge.OnChargeChanged += ChargeChanged;
            Clear();
        }

        private void OnDisable()
        {
            weapon.OnBeamFired -= ShowBeam;
            weapon.Charge.OnChargeChanged -= ChargeChanged;
            if (fade != null) StopCoroutine(fade);
            fade = null;
            Clear();
        }

        private void ChargeChanged(float charge)
        {
            if (charge > 0f) return;
            dashPhase = 0f;
            properties.SetFloat(DashPhaseId, 0f);
            if (fade == null) Clear();
        }

        private void Clear()
        {
            dashPhase = 0f;
            properties.SetFloat(DashPhaseId, 0f);
            properties.SetFloat(ShotId, 0f);
            properties.SetFloat(ShotAgeId, 0f);
            line.SetPropertyBlock(properties);
            SetOpacity(0f);
            line.enabled = false;
        }

        private void LateUpdate()
        {
            if (fade != null) return;

            var charge = weapon.Charge.ChargePct;
            line.enabled = charge > 0f;
            if (!line.enabled) return;

            dashPhase = Mathf.Repeat(dashPhase + Mathf.Lerp(dashSpeedAtStart, dashSpeedAtFull,
                charge * charge) * Time.deltaTime, dashSpacing);
            properties.SetFloat(ShotId, 0f);
            properties.SetFloat(DashPhaseId, dashPhase);
            Draw(weapon.FireOrigin, weapon.FirstStrikePoint(), aimWidth,
                aimOpacityAtFull * Mathf.Pow(charge, aimOpacityPower));
        }

        private void ShowBeam(Vector3 start, Vector3 end)
        {
            properties.SetFloat(ShotId, 1f);
            properties.SetFloat(ShotAgeId, 0f);
            Draw(start, end, beamWidth * 1.54f, 1f);
            line.enabled = true;

            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(FadeOut());
        }

        private void Draw(Vector3 start, Vector3 end, float width, float opacity)
        {
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.widthMultiplier = width;
            properties.SetFloat(BeamLengthId, Vector3.Distance(start, end));
            properties.SetFloat(LineWidthId, width);
            line.SetPropertyBlock(properties);
            SetOpacity(opacity);
        }

        private void SetOpacity(float opacity)
        {
            line.startColor = Faded(beamStartColor, opacity);
            line.endColor = Faded(beamEndColor, opacity);
        }

        private static Color Faded(Color color, float opacity) => new(color.r, color.g, color.b, color.a * opacity);

        private IEnumerator FadeOut()
        {
            var elapsed = 0f;
            while (elapsed < fadeTime)
            {
                yield return null;
                elapsed += Time.deltaTime;
                var decay = Mathf.InverseLerp(flashTime, fadeTime, elapsed);
                var collapse = Mathf.SmoothStep(0f, 1f, decay);
                line.widthMultiplier = beamWidth * (1f + 0.54f * Mathf.Exp(-elapsed * 40f)) *
                                       (1f - 0.92f * collapse);
                properties.SetFloat(LineWidthId, line.widthMultiplier);
                properties.SetFloat(ShotAgeId, elapsed);
                line.SetPropertyBlock(properties);
                SetOpacity(Mathf.Pow(1f - decay, 1.45f));
            }

            fade = null;
            Clear();
        }
    }
}
