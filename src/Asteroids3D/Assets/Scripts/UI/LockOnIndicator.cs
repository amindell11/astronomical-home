using UnityEngine;

/// <summary>
/// World-space UI element that displays lock-on progress over a target.
/// It expects to live under a Canvas set to World Space and to have an Animator
/// that exposes a float parameter named "lockProgress" (0–1) and a Trigger
/// named "LockComplete" for the flash when locking finishes.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public sealed class LockOnIndicator : MonoBehaviour
{
    [Tooltip("Animator driving reticle scale / flash. Will default to first child Animator if left unassigned.")]
    [SerializeField] private Animator animator;
    [SerializeField] private float verticalOffset = -5f;

    private CanvasGroup canvasGroup;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        Hide(); // start invisible
    }

    /// <summary>Animate progress while lock is building (0 → 1).</summary>
    public void UpdateProgress(float progress)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = 1f;
        if (animator != null)
        {
            animator.SetFloat("lockProgress", progress);
        }
    }

    /// <summary>Call once when the lock is fully acquired to play flash animation.</summary>
    public void OnLockComplete()
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = 1f;
        if (animator != null)
        {
            animator.SetTrigger("LockComplete");
        }
    }

    /// <summary>Immediately hides the indicator (no animation).</summary>
    public void Hide()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }
    void LateUpdate()
    {
        transform.rotation = Quaternion.Euler(90, 0, 0);
        transform.position = transform.parent.position + GamePlane.Normal * verticalOffset;
    }
} 