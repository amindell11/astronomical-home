using System.Collections;
using Combat.Weapons.Arsenal;
using Substrate.Presentation;
using UnityEngine;

namespace Combat.Weapons
{
    /// <summary>
    /// Presentation for a <see cref="Railguns"/> on its one line renderer: while the weapon charges,
    /// a thin aim line to where the shot would first strike, brightening with the charge; on a shot,
    /// a flash along the beam that fades out. Purely cosmetic — damage is applied by the weapon's cast.
    /// </summary>
    [RequireComponent(typeof(Railguns), typeof(LineRenderer))]
    public sealed class RailBeamVisual : MonoBehaviour, IPresentationPart
    {
        [Tooltip("Seconds the beam takes to fade out after a shot.")]
        [SerializeField, Min(0.01f)] private float fadeTime = 0.15f;

        [Header("Aim Line")]
        [Tooltip("Aim line width, as a fraction of the shot's beam width.")]
        [SerializeField, Range(0f, 1f)] private float aimWidth = 0.5f;
        [Tooltip("Aim line opacity, as a fraction of the beam's, as the charge starts.")]
        [SerializeField, Range(0f, 1f)] private float aimOpacityAtStart = 0.15f;
        [Tooltip("Aim line opacity, as a fraction of the beam's, at full charge.")]
        [SerializeField, Range(0f, 1f)] private float aimOpacityAtFull = 0.6f;

        private Railguns weapon;
        private LineRenderer line;
        private Color beamStartColor;
        private Color beamEndColor;
        private float beamWidth;
        private Coroutine fade;

        private void Awake()
        {
            weapon = GetComponent<Railguns>();
            line = GetComponent<LineRenderer>();
            line.positionCount = 2;
            beamStartColor = line.startColor;
            beamEndColor = line.endColor;
            beamWidth = line.widthMultiplier;
        }

        public void ApplyPresentation(bool visible)
        {
            enabled = visible;
            // Only a charge or a shot lights the line; the field is unset until Awake caches it.
            if (line) line.enabled = false;
        }

        private void OnEnable()
        {
            weapon.OnBeamFired += ShowBeam;
            line.enabled = false;
        }

        private void OnDisable()
        {
            weapon.OnBeamFired -= ShowBeam;
            if (fade != null) StopCoroutine(fade);
            fade = null;
            line.enabled = false;
        }

        private void LateUpdate()
        {
            if (fade != null) return;

            var charge = weapon.Charge.ChargePct;
            line.enabled = charge > 0f;
            if (!line.enabled) return;

            Draw(weapon.FireOrigin, weapon.FirstStrikePoint(), aimWidth,
                Mathf.Lerp(aimOpacityAtStart, aimOpacityAtFull, charge));
        }

        private void ShowBeam(Vector3 start, Vector3 end)
        {
            Draw(start, end, 1f, 1f);
            line.enabled = true;

            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(FadeOut());
        }

        private void Draw(Vector3 start, Vector3 end, float width, float opacity)
        {
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.widthMultiplier = beamWidth * width;
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
                elapsed += Time.deltaTime;
                SetOpacity(1f - Mathf.Clamp01(elapsed / fadeTime));
                yield return null;
            }

            line.enabled = false;
            fade = null;
        }
    }
}
