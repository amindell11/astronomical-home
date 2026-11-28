using Combat.Targeting;
using UnityEngine;
using UnityEngine.UI;
using Substrate;

namespace UI.Weapons
{
    /// <summary>
    /// World-space UI element that displays lock-on progress over a target.
    /// It expects to live under a Canvas set to World Space and to have an Animator
    /// that exposes a float parameter named "lockProgress" (0–1) and a Trigger
    /// named "LockComplete" for the flash when locking finishes. HUD ownership keeps its
    /// visibility and appearance separate from the ship's world visuals.
    /// As the threat reticle (on the player's own ship) it bypasses the Animator, which writes the
    /// same scale and colour, and drives an enemy-red pulse that quickens with progress and a hard flash.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class LockOnIndicator : MonoBehaviour
    {
        public static readonly Color ThreatTint = new(1f, .25f, .2f);

        private static readonly int LockProgress = Animator.StringToHash("lockProgress");
        private static readonly int LockComplete = Animator.StringToHash("LockComplete");

        // The Animator's blend runs from the idle scale to the locked pose as the lock builds.
        private const float OpenScale = 1f;
        private const float LockedScale = .4f;
        private const float SlowPulseHz = 2f;
        private const float FastPulseHz = 9f;
        private const float PulseDepth = .18f;
        private const float RestAlpha = .55f;
        private const float FlashSeconds = .45f;
        private const float FlashScale = 2.2f;

        [Tooltip("Animator driving reticle scale / flash. Will default to first child Animator if left unassigned.")]
        [SerializeField] private Animator animator;
        [SerializeField] private float verticalOffset = -5f;

        private CanvasGroup canvasGroup;
        private Graphic graphic;
        private LockChannel lockChannel;
        private bool subscribed;
        private Transform pose;
        private bool threat;
        private float progress;
        private bool locked;
        private float lockedAt;
        private float pulsePhase;

        public bool Visible => canvasGroup.alpha > 0f && pose && pose.gameObject.activeInHierarchy;

        public bool IsThreat => threat;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            graphic = GetComponentInChildren<Graphic>();
            if (!animator) animator = GetComponentInChildren<Animator>();
            Hide();
        }

        public void Initialize(LockChannel channel, Transform pose, bool threat)
        {
            Unsubscribe();
            lockChannel = channel;
            this.pose = pose;
            this.threat = threat;
            if (animator) animator.enabled = !threat;
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
            this.progress = progress;
            if (!threat && animator) animator.SetFloat(LockProgress, progress);
        }

        private void OnLockComplete()
        {
            canvasGroup.alpha = 1f;
            locked = true;
            lockedAt = Time.time;
            if (!threat && animator) animator.SetTrigger(LockComplete);
        }

        private void Hide()
        {
            canvasGroup.alpha = 0f;
            progress = 0f;
            locked = false;
        }

        private void LateUpdate()
        {
            if (!pose || !pose.gameObject.activeInHierarchy) Hide();
            if (!Visible) return;
            transform.rotation = GamePlane.Rotation;
            transform.position = pose.position + GamePlane.Normal * verticalOffset;
            if (threat) PresentThreat();
        }

        private void PresentThreat()
        {
            var build = locked ? 1f : progress;
            pulsePhase += Time.deltaTime * Mathf.Lerp(SlowPulseHz, FastPulseHz, build) * 2f * Mathf.PI;
            var pulse = .5f + .5f * Mathf.Sin(pulsePhase);
            var scale = Mathf.Lerp(OpenScale, LockedScale, build) * (1f + PulseDepth * pulse);
            var tint = ThreatTint;
            tint.a = Mathf.Lerp(RestAlpha, 1f, pulse);
            var flash = locked ? 1f - (Time.time - lockedAt) / FlashSeconds : 0f;
            if (flash > 0f)
            {
                scale = Mathf.Lerp(LockedScale, LockedScale * FlashScale, flash);
                tint = Color.Lerp(ThreatTint, Color.white, flash);
            }
            transform.localScale = Vector3.one * scale;
            graphic.color = tint;
        }
    }
}
