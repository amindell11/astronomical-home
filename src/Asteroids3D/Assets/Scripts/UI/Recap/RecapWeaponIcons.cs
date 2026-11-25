using System;
using System.Collections.Generic;
using UnityEngine;

namespace UI.Recap
{
    /// <summary>
    /// Run records keep weapon asset names. This maps them to HUD sprites without loading weapon prefabs.
    /// </summary>
    public sealed class RecapWeaponIcons : ScriptableObject
    {
        [Serializable]
        private struct Entry
        {
            public string weapon;
            public Sprite icon;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public static RecapWeaponIcons Load()
        {
            var catalog = Resources.Load<RecapWeaponIcons>("RunRecap/WeaponIcons");
            return catalog ? catalog : throw new InvalidOperationException("The recap requires RunRecap/WeaponIcons.");
        }

        public Sprite Find(string recordedName)
        {
            foreach (var entry in entries)
                if (entry.weapon == recordedName) return entry.icon;
            return null;
        }

        private void OnEnable() => RequireEntries();
        private void OnValidate() => RequireEntries();

        private void RequireEntries()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
                if (string.IsNullOrEmpty(entry.weapon) || !entry.icon || !names.Add(entry.weapon))
                    throw new InvalidOperationException($"Recap weapon icon '{entry.weapon}' needs a unique name and sprite.");
        }
    }
}
