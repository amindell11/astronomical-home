using System.Collections;
using UnityEngine;

namespace Combat.Weapons
{
    /// <summary>
    /// Presentation for a <see cref="Railguns"/> shot: flashes a line renderer along the beam
    /// and fades it out. Purely cosmetic — damage is applied by the weapon's raycast.
    /// </summary>
    [RequireComponent(typeof(Railguns), typeof(LineRenderer))]
    public sealed class RailBeamVisual : MonoBehaviour
    {
        [Tooltip("Seconds the beam takes to fade out after a shot.")]
        [SerializeField, Min(0.01f)] private float fadeTime = 0.15f;

        private Railguns weapon;
        private LineRenderer line;
        private Coroutine fade;

        private void Awake()
        {
            weapon = GetComponent<Railguns>();
            line = GetComponent<LineRenderer>();
            line.enabled = false;
        }

        private void OnEnable() => weapon.OnBeamFired += ShowBeam;
        private void OnDisable()
        {
            weapon.OnBeamFired -= ShowBeam;
            if (fade != null) StopCoroutine(fade);
            line.enabled = false;
        }

        private void ShowBeam(Vector3 start, Vector3 end)
        {
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.enabled = true;

            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(FadeOut());
        }

        private IEnumerator FadeOut()
        {
            var elapsed = 0f;
            var startColor = line.startColor;
            var endColor = line.endColor;

            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                var alpha = 1f - Mathf.Clamp01(elapsed / fadeTime);
                line.startColor = new Color(startColor.r, startColor.g, startColor.b, startColor.a * alpha);
                line.endColor = new Color(endColor.r, endColor.g, endColor.b, endColor.a * alpha);
                yield return null;
            }

            line.enabled = false;
            line.startColor = startColor;
            line.endColor = endColor;
            fade = null;
        }
    }
}
