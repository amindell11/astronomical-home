using System.Collections.Generic;
using Combat.Conditions;
using Combat.Weapons;
using Ships.Weapons;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Generates the weapon HUD from the equipped loadout: walks each mounted weapon's
    /// conditions and instantiates the matching readout widget per condition (Heat → heat
    /// gauge, Rounds → ammo display), in slot order. The authored widgets under the HUD
    /// panel act as templates — deactivated at Awake and cloned into the panel's layout
    /// group per matching condition, so the HUD's shape follows the loadout (two Rounds
    /// weapons produce two ammo displays).
    /// </summary>
    public sealed class WeaponReadoutBuilder : MonoBehaviour
    {
        [Tooltip("Template cloned for each weapon that carries a Heat condition.")]
        [SerializeField] internal LaserHeatUI heatTemplate;

        [Tooltip("Template cloned for each weapon that carries a Rounds condition.")]
        [SerializeField] internal MissileAmmoUI ammoTemplate;

        internal readonly struct BoundReadout
        {
            public readonly WeaponComponent Weapon;
            public readonly WeaponCondition Condition;
            public readonly MonoBehaviour Widget;

            public BoundReadout(WeaponComponent weapon, WeaponCondition condition, MonoBehaviour widget)
            {
                Weapon = weapon;
                Condition = condition;
                Widget = widget;
            }
        }

        private readonly List<BoundReadout> built = new();

        internal IReadOnlyList<BoundReadout> Built => built;

        private void Awake()
        {
            if (heatTemplate) heatTemplate.gameObject.SetActive(false);
            if (ammoTemplate) ammoTemplate.gameObject.SetActive(false);
        }

        /// <summary>Rebuilds the readouts for the given loadout; null or unarmed clears the HUD.</summary>
        public void Build(WeaponsController weapons)
        {
            Clear();
            if (!weapons) return;

            BuildForWeapon(weapons.Primary);
            BuildForWeapon(weapons.Secondary);
        }

        /// <summary>The first built condition of the given type, in slot order, or null.</summary>
        public T FirstCondition<T>() where T : WeaponCondition
        {
            foreach (var readout in built)
                if (readout.Condition is T typed)
                    return typed;
            return null;
        }

        private void BuildForWeapon(WeaponComponent weapon)
        {
            if (!weapon) return;

            foreach (var condition in weapon.Conditions)
            {
                switch (condition)
                {
                    case Heat heat when heatTemplate:
                        var gauge = Clone(heatTemplate);
                        gauge.Initialize(heat);
                        built.Add(new BoundReadout(weapon, heat, gauge));
                        break;

                    case Rounds rounds when ammoTemplate:
                        var display = Clone(ammoTemplate);
                        display.Initialize(rounds, weapon.LockSource);
                        built.Add(new BoundReadout(weapon, rounds, display));
                        break;
                }
            }
        }

        private static T Clone<T>(T template) where T : MonoBehaviour
        {
            var widget = Instantiate(template, template.transform.parent);
            widget.gameObject.SetActive(true);
            return widget;
        }

        private void Clear()
        {
            foreach (var readout in built)
                if (readout.Widget)
                    Destroy(readout.Widget.gameObject);
            built.Clear();
        }
    }
}
