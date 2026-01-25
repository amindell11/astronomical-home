using Combat.Targeting;
using Game;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// World-space UI element that displays lock-on progress over a target.
    /// It expects to live under a Canvas set to World Space and to have an Animator
    /// that exposes a float parameter named "lockProgress" (0–1) and a Trigger
    /// named "LockComplete" for the flash when locking finishes.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class LockOnIndicator : MonoBehaviour
    {
        private static readonly int LockProgress = Animator.StringToHash("lockProgress");
        private static readonly int LockComplete = Animator.StringToHash("LockComplete");

        [Tooltip("Animator driving reticle scale / flash. Will default to first child Animator if left unassigned.")]
        [SerializeField] private Animator animator;
        [SerializeField] private float verticalOffset = -5f;

        private CanvasGroup canvasGroup;
        private ITargetable targetable;

        private void Awake()
        {
            targetable = GetComponentInParent<ITargetable>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (!animator) animator = GetComponentInChildren<Animator>();
        }

        private void Start()
        {
            Hide();
        }
        private void OnEnable()
        {
            if (targetable == null) return;
            targetable.Lock.Progress += HandleLockProgress;
            targetable.Lock.Acquired += HandleLockAcquired;
            targetable.Lock.Released += HandleLockReleased;
        }

        private void OnDisable()
        {
            if (targetable == null) return;
            targetable.Lock.Progress -= HandleLockProgress;
            targetable.Lock.Acquired -= HandleLockAcquired;
            targetable.Lock.Released -= HandleLockReleased;
            targetable = null;
            Hide();
        }

        private void HandleLockProgress(float progress)
        {
            UpdateProgress(progress);
        }

        private void HandleLockAcquired()
        {
            OnLockComplete();
        }

        private void HandleLockReleased()
        {
            Hide();
        }

        private void UpdateProgress(float progress)
        {
            if (!canvasGroup) return;
            canvasGroup.alpha = 1f;
            if (animator) animator.SetFloat(LockProgress, progress);
        }

        private void OnLockComplete()
        {
            if (!canvasGroup) return;
            canvasGroup.alpha = 1f;
            if (animator) animator.SetTrigger(LockComplete);
        }

        private void Hide()
        {
            if (canvasGroup) canvasGroup.alpha = 0f;
        }

        private void LateUpdate()
        {
            if (canvasGroup && canvasGroup.alpha <= 0f) return;
            transform.rotation = Quaternion.Euler(90, 0, 0);
            transform.position = transform.parent.position + GamePlane.Normal * verticalOffset;
        }
    }
} 