using Combat.Targeting;
using Ships.Presentation;
using UnityEngine;
using Substrate;

namespace UI
{
    /// <summary>
    /// The lock reticle: a world-space mark over a ship, bound through <see cref="Bind"/> to that ship's
    /// <see cref="LockChannel"/> (see <see cref="IShipVisual"/>), so it answers whoever locks the ship.
    /// While a missile lock builds or holds, the lock events drive it: Animator float "lockProgress"
    /// (0–1) and trigger "LockComplete". Otherwise it holds the locked pose while any missile tracks
    /// the ship and hides when none does. A lost track first plays trigger "TrackLost" (state
    /// "ReticleBreak"); the reticle settles once that state ends.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class LockOnIndicator : MonoBehaviour, IShipVisual
    {
        private static readonly int LockProgress = Animator.StringToHash("lockProgress");
        private static readonly int LockComplete = Animator.StringToHash("LockComplete");
        private static readonly int TrackLost = Animator.StringToHash("TrackLost");
        private static readonly int BreakState = Animator.StringToHash("ReticleBreak");

        [Tooltip("Animator driving reticle scale / flash. Will default to first child Animator if left unassigned.")]
        [SerializeField] private Animator animator;
        [SerializeField] private float verticalOffset = -5f;

        private CanvasGroup canvasGroup;
        private LockChannel lockChannel;
        private bool subscribed;
        private bool lockBuildingOrHeld;
        private bool breaking;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (!animator) animator = GetComponentInChildren<Animator>();
        }

        private void Start()
        {
            Hide();
        }

        public void Bind(in ShipView view)
        {
            lockChannel = view.Lock;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable()
        {
            if (lockChannel != null) Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            lockBuildingOrHeld = false;
            breaking = false;
            Hide();
        }

        private void Subscribe()
        {
            if (subscribed || lockChannel == null) return;
            lockChannel.Progress += HandleLockProgress;
            lockChannel.Acquired += HandleLockAcquired;
            lockChannel.Released += HandleLockReleased;
            lockChannel.TrackingChanged += HandleTrackingChanged;
            subscribed = true;
            Settle();
        }

        private void Unsubscribe()
        {
            if (!subscribed || lockChannel == null) return;
            lockChannel.Progress -= HandleLockProgress;
            lockChannel.Acquired -= HandleLockAcquired;
            lockChannel.Released -= HandleLockReleased;
            lockChannel.TrackingChanged -= HandleTrackingChanged;
            subscribed = false;
        }

        private void HandleLockProgress(float progress)
        {
            lockBuildingOrHeld = true;
            UpdateProgress(progress);
        }

        private void HandleLockAcquired()
        {
            lockBuildingOrHeld = true;
            OnLockComplete();
        }

        private void HandleLockReleased()
        {
            lockBuildingOrHeld = false;
            Settle();
        }

        private void HandleTrackingChanged(TrackChange change)
        {
            if (change == TrackChange.Lost) PlayBreak();
            else Settle();
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

        private void PlayBreak()
        {
            breaking = true;
            canvasGroup.alpha = 1f;
            if (animator) animator.SetTrigger(TrackLost);
        }

        private void Settle()
        {
            if (breaking || lockBuildingOrHeld) return;
            if (lockChannel.TrackingCount > 0) ShowHeld();
            else Hide();
        }

        private void ShowHeld()
        {
            canvasGroup.alpha = 1f;
            if (animator) animator.SetFloat(LockProgress, 1f);
        }

        private void Hide()
        {
            if (canvasGroup) canvasGroup.alpha = 0f;
        }

        // A trigger set this frame stays pending until the Animator's next update takes it.
        private bool BreakPlaying() =>
            animator && (animator.GetBool(TrackLost) || animator.GetCurrentAnimatorStateInfo(0).shortNameHash == BreakState);

        private void LateUpdate()
        {
            if (breaking && !BreakPlaying())
            {
                breaking = false;
                Settle();
            }

            if (canvasGroup && canvasGroup.alpha <= 0f) return;
            transform.rotation = GamePlane.Rotation;
            transform.position = transform.parent.position + GamePlane.Normal * verticalOffset;
        }
    }
}
