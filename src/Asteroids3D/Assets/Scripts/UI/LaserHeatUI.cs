using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the current heat level of a LaserGun using an Image with Fill mode.
/// Place the UI as a vertical bar near the aiming reticle and assign the references
/// in the inspector. Works in both world-space and screen-space canvases.
/// </summary>
public sealed class LaserHeatUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("LaserGun whose heat we visualise.")]
    [SerializeField] private LaserGun laserGun;

    [Tooltip("Image component whose FillAmount represents heat (0-1). Should use a Vertical fill method.")]
    [SerializeField] private Image fillImage;

    [Header("Overheat Flash")]
    [Tooltip("Optional animator that has a bool parameter named 'overheated'.")] 
    [SerializeField] private Animator animator;

    void Awake()
    {
        // Attempt auto-wiring if not set.
        if (!laserGun) laserGun = GetComponentInParent<LaserGun>();
        if (!fillImage) fillImage = GetComponentInChildren<Image>();
    }

    void Update()
    {
        if (laserGun == null || fillImage == null) return;

        float pct = laserGun.HeatPct;      // 0 – 1
        fillImage.fillAmount = pct;

        if (animator)
        {
            animator.SetBool("overheated", pct >= 1f);
            animator.SetFloat("heat", pct);
        }
    }
} 