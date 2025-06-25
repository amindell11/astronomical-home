using UnityEngine;

/// <summary>
/// Global static class for game-wide settings like graphics, audio, etc.
/// Loads settings from PlayerPrefs on startup.
/// </summary>
public static class GameSettings
{
    /// <summary>Global toggle for all visual effects (VFX).</summary>
    public static bool VfxEnabled { get; private set; }

    /// <summary>
    /// This method is called by Unity when the game engine starts,
    /// before any scene has loaded. It ensures our settings are loaded right away.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void OnSubsystemRegistration()
    {
        LoadSettings();
    }

    /// <summary>
    /// Loads settings from PlayerPrefs. Defaults to 'true' (enabled) if not found.
    /// </summary>
    public static void LoadSettings()
    {
        // Get the "VFX_ENABLED" value from storage, defaulting to 1 (true).
        VfxEnabled = PlayerPrefs.GetInt("VFX_ENABLED", 1) == 1;
    }

    /// <summary>
    /// Updates the VFX setting and saves it to PlayerPrefs for future sessions.
    /// </summary>
    /// <param name="enabled">The new value for the VFX setting.</param>
    public static void SetVfxEnabled(bool enabled)
    {
        if (VfxEnabled != enabled)
        {
            VfxEnabled = enabled;
            PlayerPrefs.SetInt("VFX_ENABLED", enabled ? 1 : 0);
            PlayerPrefs.Save(); // Immediately write to disk
        }
    }
} 