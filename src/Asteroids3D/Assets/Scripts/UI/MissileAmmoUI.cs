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

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();


        if (iconContainer == null) iconContainer = transform;

        // Initial rebuild will occur in Start once we have a launcher reference
    }
    void Start()
    {
        if (launcher == null)
        {
            // Fallback: grab first launcher in scene (useful when dropped into HUD prefab).
            launcher = GameObject.FindGameObjectWithTag(TagNames.Player).GetComponentInChildren<MissileLauncher>();
        }

        if (launcher != null)
        {
            // Build icons based on launcher's max ammo now that we have the reference
            RebuildIcons();

            // Prepare cached handler & subscribe
            ammoChangedHandler = OnAmmoChanged;
            launcher.AmmoCountChanged += ammoChangedHandler;

            // Initialize UI with the starting ammo value
            UpdateAmmoIcons(launcher.AmmoCount);
        }
    }

    void OnDisable()
    {
        if (launcher != null && ammoChangedHandler != null)
        {
            launcher.AmmoCountChanged -= ammoChangedHandler;
        }
    }

    void Update()
    {
        if (launcher == null || cooldownSpinner == null) return;

        bool onCooldown = launcher.State == MissileLauncher.LockState.Cooldown;
        cooldownSpinner.enabled = onCooldown;
        if (onCooldown)
        {
            // Simple rotation animation
            cooldownSpinner.transform.Rotate(0f, 0f, -360f * Time.unscaledDeltaTime);
        }
    }

    // ───────────────────────── Event Callbacks ─────────────────────────

    void OnAmmoChanged(int newAmmo)
    {
        UpdateAmmoIcons(newAmmo);
    }

    // ───────────────────────── Helpers ─────────────────────────

    void UpdateAmmoIcons(int ammo)
    {
        if (icons == null || icons.Count == 0) return;

        int max = launcher.MaxAmmo;

        for (int i = 0; i < icons.Count; i++)
        {
            var img = icons[i];
            if (!img) continue;

            bool slotActive = i < max;
            img.enabled = slotActive;
            if (!slotActive) continue;

            bool hasAmmo = i < ammo;

            // Use glow controller if present, otherwise fall back to Image.color
            GlowingUIController glow = (i < glowControllers.Count) ? glowControllers[i] : img.GetComponent<GlowingUIController>();
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

    void RebuildIcons()
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
            // populate glowControllers for existing icons
            foreach (var img in existing)
            {
                if (!img) { glowControllers.Add(null); continue; }
                var glow = img.GetComponent<GlowingUIController>();
                glowControllers.Add(glow);
            }
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

                // Ensure each instantiated icon has a glow controller
                GlowingUIController glow = newIcon.GetComponent<GlowingUIController>();
                if (glow == null)
                {
                    glow = newIcon.gameObject.AddComponent<GlowingUIController>();
                    glow.ApplyMissileAmmoPreset(); // Sensible defaults
                }
                glowControllers.Add(glow);
            }
        }
    }
} 