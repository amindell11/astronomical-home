using System.Collections.Generic;
using Combat.Conditions;
using Combat.Targeting;
using Combat.Weapons;
using Ships.Command;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Generates the weapon HUD from the equipped loadout: one <see cref="WeaponReadoutPanel"/>
    /// per slot, filled with one widget per readout the weapon exposes (heat gauge, ammo
    /// counter, lock spinner), in display order. Consumes only the <see cref="IWeaponReadouts"/>
    /// read surface — the HUD's shape follows the loadout and never touches sim objects.
    /// </summary>
    public sealed class WeaponReadoutBuilder : MonoBehaviour
    {
        [Header("Prefabs")]
        [Tooltip("Panel instantiated per equipped weapon slot.")]
        [SerializeField] internal WeaponReadoutPanel panelPrefab;

        [Tooltip("Widget instantiated per heat readout.")]
        [SerializeField] internal HeatGaugeUI heatWidgetPrefab;

        [Tooltip("Widget instantiated per ammo readout.")]
        [SerializeField] internal AmmoCounterUI ammoWidgetPrefab;

        [Tooltip("Widget instantiated per lock readout.")]
        [SerializeField] internal LockReadoutUI lockWidgetPrefab;

        internal readonly struct BoundReadout
        {
            public readonly WeaponSlot Slot;
            public readonly IWeaponReadout Readout;
            public readonly MonoBehaviour Widget;

            public BoundReadout(WeaponSlot slot, IWeaponReadout readout, MonoBehaviour widget)
            {
                Slot = slot;
                Readout = readout;
                Widget = widget;
            }
        }

        private readonly List<WeaponReadoutPanel> panels = new();
        private readonly List<BoundReadout> built = new();

        internal IReadOnlyList<WeaponReadoutPanel> Panels => panels;
        internal IReadOnlyList<BoundReadout> Built => built;

        /// <summary>Rebuilds the readout panels for the given loadout; null or unarmed clears the HUD.</summary>
        public void Build(IWeaponReadouts weapons)
        {
            Clear();
            if (weapons == null) return;

            var slots = weapons.Slots;
            for (var i = 0; i < slots.Count; i++)
                BuildPanel(weapons, slots[i]);
        }

        /// <summary>The first built readout of the given type, in slot order, or null.</summary>
        public T FirstReadout<T>() where T : class
        {
            foreach (var readout in built)
                if (readout.Readout is T typed)
                    return typed;
            return null;
        }

        private void BuildPanel(IWeaponReadouts weapons, WeaponSlot slot)
        {
            if (!panelPrefab) return;

            var panel = Instantiate(panelPrefab, transform);
            panel.Initialize(weapons.DisplayName(slot));
            panels.Add(panel);

            var readouts = weapons.Readouts(slot);
            for (var i = 0; i < readouts.Count; i++)
            {
                var widget = CreateWidget(readouts[i], panel.Container);
                if (widget)
                    built.Add(new BoundReadout(slot, readouts[i], widget));
            }
        }

        private MonoBehaviour CreateWidget(IWeaponReadout readout, Transform container)
        {
            switch (readout)
            {
                case IHeatReadout heat when heatWidgetPrefab:
                    var gauge = Instantiate(heatWidgetPrefab, container);
                    gauge.Initialize(heat);
                    return gauge;

                case IAmmoReadout ammo when ammoWidgetPrefab:
                    var counter = Instantiate(ammoWidgetPrefab, container);
                    counter.Initialize(ammo);
                    return counter;

                case ILockStateSource lockSource when lockWidgetPrefab:
                    var spinner = Instantiate(lockWidgetPrefab, container);
                    spinner.Initialize(lockSource);
                    return spinner;

                default:
                    return null;
            }
        }

        private void Clear()
        {
            foreach (var panel in panels)
                if (panel)
                    Destroy(panel.gameObject);
            panels.Clear();
            built.Clear();
        }
    }
}
