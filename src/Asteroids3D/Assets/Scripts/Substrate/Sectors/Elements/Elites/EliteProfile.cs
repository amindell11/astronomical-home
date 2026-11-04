using System;
using Combat.Weapons;
using Combat.Weapons.Mods;
using Loot;
using Ships.Loadout;
using UnityEngine;

namespace Substrate.Sectors.Elements.Elites
{
    [CreateAssetMenu(fileName = "EliteProfile", menuName = "Sectors/Elite Profile")]
    public sealed class EliteProfile : ScriptableObject
    {
        [Tooltip("The name the elite's health bar and arrival banner show.")]
        [SerializeField] private string displayName;
        [Tooltip("Size of the elite's hull and the stat retune that size needs.")]
        [SerializeField] private ShipScale scale = new();
        [Tooltip("Heavy primary weapon the elite carries in place of its chassis' authored one. Required.")]
        [SerializeField] private WeaponComponent primary;
        [Tooltip("Heavy secondary weapon; regular enemies never carry one. Empty leaves the mount unarmed.")]
        [SerializeField] private WeaponComponent secondary;
        [Tooltip("Run mods stacked on the elite when it spawns; ship mods retune its hull, engine and shield.")]
        [SerializeField] private LullMod[] mods = Array.Empty<LullMod>();
        [Tooltip("Steps fired as the elite's hull falls, in descending hull order. Empty = no escalation.")]
        [SerializeField] private EscalationStep[] escalation = Array.Empty<EscalationStep>();

        public string DisplayName => displayName;
        public ShipScale Scale => scale;
        public WeaponComponent Primary => primary;
        public WeaponComponent Secondary => secondary;
        public LullMod[] Mods => mods;
        public EscalationStep[] Escalation => escalation;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new InvalidOperationException($"Elite profile '{name}' needs a display name.");
            if (!primary)
                throw new InvalidOperationException($"Elite profile '{name}' needs a primary weapon.");
            if (Array.Exists(mods, mod => !mod))
                throw new InvalidOperationException($"Elite profile '{name}' has an empty mod slot.");
            for (var i = 0; i < escalation.Length; i++)
            {
                var step = escalation[i];
                if (i > 0 && step.hull >= escalation[i - 1].hull)
                    throw new InvalidOperationException(
                        $"Elite profile '{name}': escalation step {i} must sit below step {i - 1}'s hull.");
                if (step.escorts < 0)
                    throw new InvalidOperationException(
                        $"Elite profile '{name}': escalation step {i} has a negative escort count.");
                if (step.fireMods != null && Array.Exists(step.fireMods, mod => !mod))
                    throw new InvalidOperationException(
                        $"Elite profile '{name}': escalation step {i} has an empty fire mod slot.");
            }
        }

#if UNITY_EDITOR
        internal void Configure(string displayName, ShipScale scale, WeaponComponent primary,
            WeaponComponent secondary = null, LullMod[] mods = null, EscalationStep[] escalation = null)
        {
            this.displayName = displayName;
            this.scale = scale;
            this.primary = primary;
            this.secondary = secondary;
            this.mods = mods ?? Array.Empty<LullMod>();
            this.escalation = escalation ?? Array.Empty<EscalationStep>();
        }
#endif
    }

    [Serializable]
    public struct EscalationStep
    {
        [Tooltip("Fires when the elite's hull fraction falls to or below this.")]
        [Range(0f, 1f)] public float hull;
        [Tooltip("Escorts the elite calls in when the step fires.")]
        public int escorts;
        [Tooltip("Weapon mods added to the elite's stack when the step fires. Weapon mods only, so the AI's model of its handling stays true.")]
        public WeaponMod[] fireMods;
    }
}
