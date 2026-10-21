using Combat.Targeting;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Lock readout widget: a spinner shown while the weapon's lock is on cooldown. One is
    /// generated per lock-capable weapon by <see cref="WeaponReadoutBuilder"/>.
    /// </summary>
    public sealed class LockReadoutUI : MonoBehaviour
    {
        [Tooltip("Spinner shown (and rotated) while the lock is on cooldown.")]
        [SerializeField] private Image spinner;

        private ILockStateSource lockSource;
        private bool isOnCooldown;

        public void Initialize(ILockStateSource lockSource)
        {
            Unsubscribe();

            this.lockSource = lockSource;
            if (this.lockSource == null)
            {
                SetCooldownState(false);
                return;
            }

            this.lockSource.OnStateChanged += HandleStateChanged;
            SetCooldownState(this.lockSource.State == LockState.Cooldown);
        }

        private void Update()
        {
            if (!isOnCooldown || !spinner) return;
            spinner.transform.Rotate(0f, 0f, -360f * Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void HandleStateChanged(LockState _, LockState next)
        {
            SetCooldownState(next == LockState.Cooldown);
        }

        private void SetCooldownState(bool onCooldown)
        {
            isOnCooldown = onCooldown;
            if (spinner)
                spinner.enabled = onCooldown;
        }

        private void Unsubscribe()
        {
            if (lockSource == null) return;
            lockSource.OnStateChanged -= HandleStateChanged;
        }
    }
}
