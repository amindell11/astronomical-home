using System.Collections.Generic;
using Combat.Conditions;
using Combat.Targeting;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{

    [RequireComponent(typeof(CanvasGroup))]
    public sealed class MissileAmmoUI : MonoBehaviour
    {
        [Header("Dynamic Icon Generation")]
        [Tooltip("Prefab used to instantiate each missile icon.")]
        [SerializeField] private Image iconPrefab;

        [Tooltip("Parent transform that will hold the instantiated icons. Defaults to this transform.")]
        [SerializeField] private Transform iconContainer;

        [Header("Colors")]
        [Tooltip("Color of a filled ammo slot.")]
        [SerializeField] private Color filledColor = Color.white;
        [Tooltip("Color of an empty ammo slot.")]
        [SerializeField] private Color emptyColor  = Color.gray;

        [Tooltip("Glow intensity when ammo slot is filled.")]
        [SerializeField, Range(0f, 5f)] private float filledGlowIntensity = 1.2f;
        [Tooltip("Glow intensity when ammo slot is empty.")]
        [SerializeField, Range(0f, 5f)] private float emptyGlowIntensity  = 0f;

        // Runtime collection of missile icons (existing or instantiated)
        private readonly List<Image> icons = new();
        // Parallel list of GlowingUIController references
        private readonly List<GlowingUIController> glowControllers = new();

        // Tag used to identify missile icons already present in the hierarchy.
        private const string IconTag = "MissileAmmoIcon";

        [Tooltip("Optional spinner that is enabled while the launcher is on cooldown.")]
        [SerializeField] private Image cooldownSpinner;

        private CanvasGroup canvasGroup;

        // Cache delegate to avoid allocations
        private System.Action<int> ammoChangedHandler;
        private Rounds rounds;
        private TargetingComputer targeting;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (!iconContainer) iconContainer = transform;
            ammoChangedHandler = OnAmmoChanged;
        }

        public void Initialize(Rounds rounds, TargetingComputer targeting)
        {
            if (this.rounds && ammoChangedHandler != null)
                this.rounds.OnAmmoCountChanged -= ammoChangedHandler;

            this.rounds = rounds;
            this.targeting = targeting;
            if (!this.rounds) return;

            RebuildIcons();
            this.rounds.OnAmmoCountChanged += ammoChangedHandler;
            UpdateAmmoIcons(this.rounds.AmmoCount);
        }

        public void RegisterIcon(Image image, GlowingUIController glow)
        {
            if (!image) return;

            var index = icons.IndexOf(image);
            if (index >= 0)
            {
                if (index < glowControllers.Count)
                    glowControllers[index] = glow;
                else
                    glowControllers.Add(glow);
                return;
            }

            icons.Add(image);
            glowControllers.Add(glow);
        }

        private void Update()
        {
            if (!targeting || !cooldownSpinner) return;

            var onCooldown = targeting.State == LockState.Cooldown;
            cooldownSpinner.enabled = onCooldown;
            if (onCooldown)
            {
                cooldownSpinner.transform.Rotate(0f, 0f, -360f * Time.unscaledDeltaTime);
            }
        }

        private void OnDestroy()
        {
            if (rounds && ammoChangedHandler != null)
                rounds.OnAmmoCountChanged -= ammoChangedHandler;
        }

        // ───────────────────────── Event Callbacks ─────────────────────────

        private void OnAmmoChanged(int newAmmo)
        {
            UpdateAmmoIcons(newAmmo);
        }

        // ───────────────────────── Helpers ─────────────────────────

        private void UpdateAmmoIcons(int ammo)
        {
            if (icons == null || icons.Count == 0) return;

            var max = rounds ? rounds.MaxAmmo : 0;

            for (var i = 0; i < icons.Count; i++)
            {
                var img = icons[i];
                if (!img) continue;

                var slotActive = i < max;
                img.enabled = slotActive;
                if (!slotActive) continue;

                var hasAmmo = i < ammo;

                var glow = (i < glowControllers.Count) ? glowControllers[i] : null;
                if (glow)
                {
                    glow.SetBaseColor(hasAmmo ? filledColor : emptyColor);
                    glow.SetEmissionIntensity(hasAmmo ? filledGlowIntensity : emptyGlowIntensity);
                }
                else
                {
                    img.color = hasAmmo ? filledColor : emptyColor;
                }
            }

        }

        private void RebuildIcons()
        {
            if (!iconPrefab || !rounds) return;

            var max = rounds.MaxAmmo;

            if (icons.Count > max)
            {
                for (var i = icons.Count - 1; i >= max; i--)
                {
                    if (icons[i])
                        Destroy(icons[i].gameObject);
                    icons.RemoveAt(i);
                    if (i < glowControllers.Count)
                        glowControllers.RemoveAt(i);
                }
            }

            for (var i = icons.Count; i < max; i++)
            {
                var newIcon = Instantiate(iconPrefab, iconContainer);
                newIcon.tag = IconTag;
                if (!icons.Contains(newIcon))
                {
                    icons.Add(newIcon);
                    glowControllers.Add(null);
                }
            }
        }
    }
} 
