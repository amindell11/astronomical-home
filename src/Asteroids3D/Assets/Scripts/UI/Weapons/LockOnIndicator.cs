using Combat.Targeting;
using UnityEngine;
using Substrate;

namespace UI.Weapons
{
    /// <summary>
    /// World-space UI element that displays lock-on progress over a target.
    /// It expects to live under a Canvas set to World Space and to have an Animator
    /// that exposes a float parameter named "lockProgress" (0–1) and a Trigger
    /// named "LockComplete" for the flash when locking finishes. HUD ownership keeps its
    /// visibility and appearance separate from the ship's world visuals.
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
        private LockChannel lockChannel;
        private bool subscribed;
        private Transform pose;

        public bool Visible => canvasGroup.alpha > 0f && pose && pose.gameObject.activeInHierarchy;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (!animator) animator = GetComponentInChildren<Animator>();
            Hide();
        }

        public void Initialize(LockChannel channel, Transform pose)
        {
            Unsubscribe();
            lockChannel = channel;
            this.pose = pose;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable()
        {
            if (lockChannel != null) Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            Hide();
        }

        private void Subscribe()
        {
            if (subscribed || lockChannel == null) return;
            lockChannel.Progress += HandleLockProgress;
            lockChannel.Acquired += HandleLockAcquired;
            lockChannel.Released += HandleLockReleased;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || lockChannel == null) return;
            lockChannel.Progress -= HandleLockProgress;
            lockChannel.Acquired -= HandleLockAcquired;
            lockChannel.Released -= HandleLockReleased;
            subscribed = false;
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
            canvasGroup.alpha = 1f;
            if (animator) animator.SetFloat(LockProgress, progress);
        }

        private void OnLockComplete()
        {
            canvasGroup.alpha = 1f;
            if (animator) animator.SetTrigger(LockComplete);
        }

        private void Hide()
        {
            canvasGroup.alpha = 0f;
        }

        private void LateUpdate()
        {
            if (!pose || !pose.gameObject.activeInHierarchy) Hide();
            if (!Visible) return;
            transform.rotation = GamePlane.Rotation;
            transform.position = pose.position + GamePlane.Normal * verticalOffset;
        }
    }
}
