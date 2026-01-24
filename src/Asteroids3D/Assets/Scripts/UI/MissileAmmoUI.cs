using System.Collections.Generic;
using System.Linq;
using Combat.Conditions;
using Combat.Targeting;
using Combat.Weapons;
using UnityEngine;
using UnityEngine.UI;
using Utils;
using Weapons;

namespace UI
{

    [RequireComponent(typeof(CanvasGroup))]
    public sealed class MissileAmmoUI : MonoBehaviour
    {
        [Tooltip("Missile launcher whose ammo we want to display.")]
        [SerializeField] private WeaponMissiles launcher;

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

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (!iconContainer) iconContainer = transform;
        }

        private void Start()
        {
            if (!launcher)
            {
                launcher = GameObject.FindGameObjectWithTag(TagNames.Player).GetComponentInChildren<WeaponMissiles>();
            }

            if (!launcher) return;
            rounds = launcher.GetComponent<Rounds>();
            RebuildIcons();

            ammoChangedHandler = OnAmmoChanged;
            if (rounds) rounds.OnAmmoCountChanged += ammoChangedHandler;

            UpdateAmmoIcons(rounds ? rounds.AmmoCount : 0);
        }

        private void OnDisable()
        {
            if (rounds && ammoChangedHandler != null)
            {
                rounds.OnAmmoCountChanged -= ammoChangedHandler;
            }
        }

        private void Update()
        {
            if (!launcher || !cooldownSpinner) return;

            var onCooldown = launcher.Targeting.State == LockState.Cooldown;
            cooldownSpinner.enabled = onCooldown;
            if (onCooldown)
            {
                // Simple rotation animation
                cooldownSpinner.transform.Rotate(0f, 0f, -360f * Time.unscaledDeltaTime);
            }
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
            // Refresh the icon list by first collecting any existing images that have the
            // designated tag, then creating additional ones as needed.

            icons.Clear();
            glowControllers.Clear();

            // 1. Gather existing icons with the correct tag (might have been laid out in the editor)
            if (iconContainer)
            {
                var existing = iconContainer.GetComponentsInChildren<Image>(includeInactive: true)
                    .Where(img => img.CompareTag(IconTag));
                icons.AddRange(existing);
                foreach (var img in existing)
                {
                    if (!img) { glowControllers.Add(null); continue; }
                    var glow = img.GetComponent<GlowingUIController>();
                    glowControllers.Add(glow);
                }
            }

            // 2. Ensure we have exactly rounds.MaxAmmo icons by adding more if necessary
            if (!iconPrefab || !rounds) return;
            {
                var max = rounds.MaxAmmo;

                // Remove excess icons if there are too many (e.g., max ammo decreased)
                if (icons.Count > max)
                {
                    for (var i = icons.Count - 1; i >= max; i--)
                    {
                        if (icons[i])
                        {
                            Destroy(icons[i].gameObject);
                        }
                        icons.RemoveAt(i);
                    }
                }

                // Instantiate additional icons if needed
                for (var i = icons.Count; i < max; i++)
                {
                    var newIcon = Instantiate(iconPrefab, iconContainer);
                    newIcon.tag = IconTag; // Mark so we can find it next time
                    icons.Add(newIcon);

                    // Ensure each instantiated icon has a glow controller
                    var glow = newIcon.GetComponent<GlowingUIController>();
                    if (!glow)
                    {
                        glow = newIcon.gameObject.AddComponent<GlowingUIController>();
                        glow.ApplyMissileAmmoPreset(); // Sensible defaults
                    }
                    glowControllers.Add(glow);
                }
            }
        }
    }
} 