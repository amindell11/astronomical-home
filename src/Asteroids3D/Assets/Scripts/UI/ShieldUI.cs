using System.Collections;
using Game;
using Ships;
using Ships.Damage;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    [RequireComponent(typeof(Image))]
    public class ShieldUI : MonoBehaviour
    {
        [Header("Timing (seconds)")]
        //[SerializeField] float fadeIn  = 0.06f;      // quick pop-in for hit flash
        [SerializeField] float linger  = 0.30f;      // visible while it "shimmers"
        //[SerializeField] float fadeOut = 0.40f;      // dissolve back to invisible

        [Header("Shimmer")]
        [SerializeField] float shimmerFreq = 20f;    // Hz of scale flicker
        [SerializeField] float shimmerAmp  = 0.08f;  // 8 % size wobble

        //[Header("Regen Fade")]
        //[SerializeField] float regenFadeIn = 0.3f;   // fade-in when shield starts regenerating from 0

        [Header("Fill & Color")]
        [Tooltip("Optional gradient to tint ring based on remaining shield")] 
        [SerializeField] Gradient shieldColors;

        [SerializeField] DamageController source;    // assign the ship whose shield flashes

        Image   ring;
        Color   baseColor;       // original tint without alpha
        bool flashActive;
        float flashElapsed;
        Vector3 baseScale;

        void Awake()
        {
            ring       = GetComponent<Image>();
            baseColor  = ring.color;
            baseScale  = transform.localScale;
            // start fully transparent until first event
            ring.canvasRenderer.SetAlpha(1f);
        }

        private void OnEnable()
        {
            if (!source) source = GetComponentInParent<DamageController>();
            if (!source)
            {
                Debug.LogWarning($"[{nameof(ShieldUI)}] No {nameof(DamageController)} source found for {name}", this);
                return;
            }

            source.Shield.OnValueChanged += OnShieldChanged;
        }

        void OnDisable()
        {
            if (source) {
                source.Shield.OnValueChanged -= OnShieldChanged;
            }

            flashActive = false;
            transform.localScale = baseScale;
        }

        void LateUpdate()
        {
            if (ring && ring.canvasRenderer.GetAlpha() <= 0f) return;
            transform.rotation = GamePlane.IsConfigured
                ? Quaternion.LookRotation(GamePlane.Normal, GamePlane.Forward)
                : Quaternion.Euler(90, 0, 0);
        }


        void OnShieldChanged(float current, float previous, float max)
        {
            if (!ring || max <= 0f) return;

            // Update radial fill
            ring.fillAmount = current / max;
            if(current<previous) TriggerFlash();

        }
        void TriggerFlash()
        {
            flashActive = true;
            flashElapsed = 0f;
        }

        void Update()
        {
            if (!flashActive) return;

            flashElapsed += Time.unscaledDeltaTime;
            if (flashElapsed >= linger)
            {
                flashActive = false;
                transform.localScale = baseScale;
                return;
            }

            var wobble = 1f + Mathf.Sin(flashElapsed * shimmerFreq * Mathf.PI * 2) * shimmerAmp;
            transform.localScale = baseScale * wobble;
        }

    }
}


