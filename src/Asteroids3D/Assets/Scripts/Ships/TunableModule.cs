using System;
using UnityEngine;
using UnityEngine.Events;

namespace Ships
{
    /// <summary>
    /// Base for the composable ship-settings ScriptableObjects (frame + modules). Carries the
    /// <see cref="onChanged"/> event that fires from <see cref="OnValidate"/> so live inspector
    /// tuning can be picked up by runtime consumers (preserves the old
    /// <c>ShipSettings.onSettingsChanged</c> behaviour after the split).
    /// </summary>
    public abstract class TunableModule : ScriptableObject
    {
        [NonSerialized]
        public readonly UnityEvent onChanged = new UnityEvent();

        protected virtual void OnValidate()
        {
            onChanged?.Invoke();
        }
    }
}
