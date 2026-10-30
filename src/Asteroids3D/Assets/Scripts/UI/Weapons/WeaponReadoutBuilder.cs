using System.Collections.Generic;
using Combat.Targeting;
using Combat.Weapons;
using Ships.Command;
using UnityEngine;
using Combat.Weapons.Conditions;

namespace UI.Weapons
{
    /// <summary>
    /// Binds weapon readouts for HUD audio and builds corner panels for lock feedback.
    /// Heat, ammunition and held charge surround Keystone in <see cref="WeaponResourceHud"/>.
    /// </summary>
    public sealed class WeaponReadoutBuilder : MonoBehaviour
    {
        [Header("Prefabs")]
        [Tooltip("Panel instantiated per equipped weapon slot.")]
        [SerializeField] internal WeaponReadoutPanel panelPrefab;

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

        public void Build(IWeaponReadouts weapons)
        {
            Clear();
            if (weapons == null) return;

            var slots = weapons.Slots;
            for (var i = 0; i < slots.Count; i++)
                BuildPanel(weapons, slots[i]);
        }

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

            WeaponReadoutPanel panel = null;
            var readouts = weapons.Readouts(slot);
            for (var i = 0; i < readouts.Count; i++)
            {
                if (readouts[i] is IHeatReadout or IAmmoReadout or IChargeReadout)
                {
                    built.Add(new BoundReadout(slot, readouts[i], null));
                    continue;
                }
                if (readouts[i] is not ILockStateSource lockSource || !lockWidgetPrefab) continue;
                if (!panel)
                {
                    panel = Instantiate(panelPrefab, transform);
                    panel.Initialize(weapons.DisplayName(slot));
                    panels.Add(panel);
                }
                var widget = Instantiate(lockWidgetPrefab, panel.Container);
                widget.Initialize(lockSource);
                built.Add(new BoundReadout(slot, readouts[i], widget));
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
