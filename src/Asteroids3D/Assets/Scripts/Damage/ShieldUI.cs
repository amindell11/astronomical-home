using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ShieldUI : MonoBehaviour
{
    [Header("Timing (seconds)")]
    [SerializeField] float fadeIn  = 0.06f;
    [SerializeField] float fadeOut = 0.40f;

    [Header("Fill & Color")]
    [Tooltip("Color of the shield")]

    [SerializeField] ShipDamageHandler source;

    Image ring;
    Color baseColor;
    Coroutine animCo;
    float prevShield = -1f;

    void Awake()
    {
        ring      = GetComponent<Image>();
        baseColor = ring.color;
        ring.canvasRenderer.SetAlpha(0f);                // start fully transparent
    }

    void OnEnable()
    {
        if (source == null) source = GetComponentInParent<ShipDamageHandler>();
        if (source != null)
        {
            source.OnShieldChanged += OnShieldChanged;
        }
    }

    void OnDisable()
    {
        if (source != null)
        {
            source.OnShieldChanged -= OnShieldChanged;
        }
    }

    void OnShieldChanged(float current, float max)
    {
        Debug.Log("OnShieldChanged: " + current );
        float t = current / max;

        // radial fill
        ring.fillAmount = t;

        // first call just seeds
        if (prevShield < 0f) { prevShield = current; return; }

        ring.canvasRenderer.SetAlpha(1f);
        Debug.Log("FadeImage: " + ring.canvasRenderer.GetAlpha());
        prevShield = current;
        StartFade(0f, fadeOut);
    }

    IEnumerator FadeAlpha(float target, float duration)
    {
        float start = ring.color.a;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(start, target, t / duration);
            ring.color = new Color(baseColor.r, baseColor.g, baseColor.b, a);
            yield return null;
        }
        ring.color = new Color(baseColor.r, baseColor.g, baseColor.b, target);
    }

    void StartFade(float targetAlpha, float duration)
    {
        if (animCo != null) StopCoroutine(animCo);  // cancel regen/full fades if needed
        animCo = StartCoroutine(FadeAlpha(targetAlpha, duration));
    }

    void LateUpdate()
    {
        transform.rotation = Quaternion.LookRotation(
            transform.position - Camera.main.transform.position);
    }

}
