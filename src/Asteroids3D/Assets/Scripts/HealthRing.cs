using UnityEngine;
using UnityEngine.UI;

public class HealthRing : MonoBehaviour
{
    [SerializeField] Image ringImage;      // drag ShieldRing here
    [SerializeField] Gradient healthColors; // optional color fade
    [SerializeField] ShipHealth source;    // assign player ship health

    void OnEnable()
    {
        if (source == null) source = GetComponentInParent<ShipHealth>();
        if (source != null) source.OnHealthChanged += UpdateRing;
    }
    void OnDisable()
    {
        if (source != null) source.OnHealthChanged -= UpdateRing;
    }

    void UpdateRing(float current, float max)
    {
        float t = current / max;               // 0–1
        ringImage.fillAmount = t;
        if (healthColors != null)              // tint as HP drops
            ringImage.color = healthColors.Evaluate(t);
    }
    void LateUpdate()
    {
        transform.rotation = Quaternion.LookRotation(
            transform.position - Camera.main.transform.position);
    }
}
