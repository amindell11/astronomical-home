using UnityEngine;
using UnityEngine.UI;
using Combat.Weapons.Conditions;

namespace UI
{
    /// <summary>
    /// Ammo readout widget: a "current / max" counter plus a fill bar that runs while the
    /// magazine reloads. One is generated per ammo-carrying weapon by
    /// <see cref="WeaponReadoutBuilder"/>.
    /// </summary>
    public sealed class AmmoCounterUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Text showing the ammo count as 'current / max'.")]
        [SerializeField] private Text countText;

        [Tooltip("Fill image showing reload progress; hidden while not reloading.")]
        [SerializeField] private Image reloadFill;

        private IAmmoReadout ammo;

        public void Initialize(IAmmoReadout ammo)
        {
            Unsubscribe();

            this.ammo = ammo;
            if (this.ammo == null)
            {
                SetCount(0, 0);
                ShowReload(false);
                return;
            }

            this.ammo.OnAmmoCountChanged += OnAmmoChanged;
            this.ammo.OnReloadStarted += OnReloadStarted;
            this.ammo.OnReloadCompleted += OnReloadCompleted;
            SetCount(this.ammo.AmmoCount, this.ammo.MaxAmmo);
            ShowReload(this.ammo.IsReloading);
        }

        private void Update()
        {
            // Reload progress is a continuous value, so it is polled while visible;
            // everything else on this widget is event-driven.
            if (ammo != null && ammo.IsReloading && reloadFill)
                reloadFill.fillAmount = ammo.ReloadProgress;
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void OnAmmoChanged(int count)
        {
            SetCount(count, ammo?.MaxAmmo ?? 0);
        }

        private void OnReloadStarted()
        {
            ShowReload(true);
        }

        private void OnReloadCompleted()
        {
            ShowReload(false);
        }

        private void SetCount(int count, int max)
        {
            if (countText)
                countText.text = $"{count} / {max}";
        }

        private void ShowReload(bool reloading)
        {
            if (!reloadFill) return;
            reloadFill.enabled = reloading;
            if (reloading)
                reloadFill.fillAmount = ammo?.ReloadProgress ?? 0f;
        }

        private void Unsubscribe()
        {
            if (ammo == null) return;
            ammo.OnAmmoCountChanged -= OnAmmoChanged;
            ammo.OnReloadStarted -= OnReloadStarted;
            ammo.OnReloadCompleted -= OnReloadCompleted;
        }
    }
}
