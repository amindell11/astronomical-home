using System;
using UnityEngine;
using UnityEngine.Events;

namespace Ships.Loadout
{
    /// <summary>
    /// Base for the composable ship-settings ScriptableObjects (frame + modules). Carries the
    /// <see cref="onChanged"/> event that fires from <see cref="OnValidate"/> so live inspector
    /// tuning can be picked up by runtime consumers, and owns the player-facing
    /// <see cref="DisplayName"/> the hangar shows, which falls back to the asset name.
    /// </summary>
    public abstract class TunableModule : ScriptableObject
    {
        [NonSerialized]
        public readonly UnityEvent onChanged = new UnityEvent();

        [Tooltip("Name shown in the hangar. Empty = the asset name.")]
        [SerializeField] private string displayName;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

        protected virtual void OnValidate()
        {
            onChanged?.Invoke();
        }
    }
}
