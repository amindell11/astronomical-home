using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Displays the current ammo count and cooldown status for a <see cref="MissileLauncher"/>.
/// Attach this to a world- or screen-space canvas that contains a horizontal layout
/// of missile icons (Images). Optionally assign a spinner Image that becomes
/// visible during launcher cooldown.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public sealed class MissileAmmoUI : MonoBehaviour
{
    [Tooltip("Missile launcher whose ammo we want to display.")]
    [SerializeField] private MissileLauncher launcher;

    [Header("Dynamic Icon Generation")]
    [Tooltip("Prefab used to instantiate each missile icon.")]
    [SerializeField] private Image iconPrefab;

    [Tooltip("Parent transform that will hold the instantiated icons. Defaults to this transform.")]
    [SerializeField] private Transform iconContainer;

    // Runtime collection of missile icons (existing or instantiated)
    private readonly List<Image> icons = new();

    // Tag used to identify missile icons already present in the hierarchy.
    private const string IconTag = "MissileAmmoIcon";

    [Tooltip("Optional spinner that is enabled while the launcher is on cooldown.")]
    [SerializeField] private Image cooldownSpinner;

    private CanvasGroup canvasGroup;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (launcher == null)
        {
            // Fallback: grab first launcher in scene (useful when dropped into HUD prefab).
            launcher = FindObjectOfType<MissileLauncher>();
        }

        if (iconContainer == null) iconContainer = transform;

        RebuildIcons();
    }

    void Update()
    {
        if (launcher == null) return;

        int ammo = launcher.AmmoCount;
        int max  = launcher.MaxAmmo;

        // Rebuild icons if max ammo changed (e.g., via upgrades)
        if (icons.Count != launcher.MaxAmmo)
        {
            RebuildIcons();
        }

        // Update missile icons
        for (int i = 0; i < icons.Count; i++)
        {
            var img = icons[i];
            if (!img) continue;

            bool slotActive = i < max;
            img.enabled = slotActive;
            if (!slotActive) continue;

            img.color = (i < ammo) ? Color.white : Color.gray;
        }

        // Cooldown spinner
        if (cooldownSpinner)
        {
            bool onCooldown = launcher.State == MissileLauncher.LockState.Cooldown;
            cooldownSpinner.enabled = onCooldown;
            if (onCooldown)
            {
                // Simple rotation animation
                cooldownSpinner.transform.Rotate(0f, 0f, -360f * Time.unscaledDeltaTime);
            }
        }
    }

    void RebuildIcons()
    {
        // Refresh the icon list by first collecting any existing images that have the
        // designated tag, then creating additional ones as needed.

        icons.Clear();

        // 1. Gather existing icons with the correct tag (might have been laid out in the editor)
        if (iconContainer)
        {
            var existing = iconContainer.GetComponentsInChildren<Image>(includeInactive: true)
                                         .Where(img => img.CompareTag(IconTag));
            icons.AddRange(existing);
        }

        // 2. Ensure we have exactly launcher.MaxAmmo icons by adding more if necessary
        if (iconPrefab != null && launcher != null)
        {
            int max = launcher.MaxAmmo;

            // Remove excess icons if there are too many (e.g., max ammo decreased)
            if (icons.Count > max)
            {
                for (int i = icons.Count - 1; i >= max; i--)
                {
                    if (icons[i])
                    {
                        Destroy(icons[i].gameObject);
                    }
                    icons.RemoveAt(i);
                }
            }

            // Instantiate additional icons if needed
            for (int i = icons.Count; i < max; i++)
            {
                Image newIcon = Instantiate(iconPrefab, iconContainer);
                newIcon.tag = IconTag; // Mark so we can find it next time
                icons.Add(newIcon);
            }
        }
    }
} 