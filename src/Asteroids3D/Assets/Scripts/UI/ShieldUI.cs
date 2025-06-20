using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

    [SerializeField] ShipDamageHandler source;    // assign the ship whose shield flashes

    Image   ring;
    Color   baseColor;       // original tint without alpha
    Coroutine animCo;

    void Awake()
    {
        ring       = GetComponent<Image>();
        baseColor  = ring.color;
        // start fully transparent until first event
        ring.canvasRenderer.SetAlpha(1f);
    }

    void OnEnable()
    {
        if (source == null) source = GetComponentInParent<ShipDamageHandler>();
        if (source != null) {
            source.OnShieldChanged += OnShieldChanged;
            source.OnShieldDamaged += TriggerFlash;
        }
    }

    void OnDisable()
    {
        if (source != null) {
            source.OnShieldChanged -= OnShieldChanged;
            source.OnShieldDamaged -= TriggerFlash;
        }
    }

    void OnShieldChanged(float current, float max)
    {
        // Update radial fill
        ring.fillAmount = current / max;

    }

    IEnumerator FlashRoutine()
    {
        // ---------- 1  fade in ----------
        //yield return Fade(0, 1, fadeIn);

        // ---------- 2  shimmer while fully visible ----------
        float t = 0;
        Vector3 baseScale = Vector3.one;
        while (t < linger)
        {
            t += Time.unscaledDeltaTime;
            float wobble = 1f + Mathf.Sin(t * shimmerFreq * Mathf.PI * 2) * shimmerAmp;
            transform.localScale = baseScale * wobble;
            yield return null;
        }
        transform.localScale = baseScale;

        // ---------- 3  fade out ----------
        //yield return Fade(1, 0, fadeOut);
    }

    IEnumerator Fade(float aFrom, float aTo, float dur)
    {
        float elapsed = 0f;
        Color c = ring.color;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / dur);
            c.a = Mathf.Lerp(aFrom, aTo, k);
            ring.color = c;
            yield return null;
        }
        c.a = aTo;
        ring.color = c;
    }

    void LateUpdate()
    {
        transform.rotation = Quaternion.Euler(90, 0, 0);
    }

    void TriggerFlash(float dmg, Vector3 hitPt)
    {
        // damage to shield triggers ring flash and sparks at hit point
        if (animCo != null) StopCoroutine(animCo);
        animCo = StartCoroutine(FlashRoutine());
    }
}


