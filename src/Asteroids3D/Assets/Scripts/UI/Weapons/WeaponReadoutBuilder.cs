using System.Collections.Generic;
using Combat.Targeting;
using Combat.Weapons;
using Ships.Command;
using UnityEngine;
using Combat.Weapons.Conditions;

namespace UI.Weapons
{
    /// <summary>
    /// Binds every weapon readout for HUD audio and builds corner panels for charge and lock.
    /// Heat and ammunition are drawn beside the aim point by <see cref="AimCrosshair"/>.
    /// </summary>
    public sealed class WeaponReadoutBuilder : MonoBehaviour
    {
        [Header("Prefabs")]
        [Tooltip("Panel instantiated per equipped weapon slot.")]
        [SerializeField] internal WeaponReadoutPanel panelPrefab;

        [Tooltip("Widget instantiated per charge readout.")]
        [SerializeField] internal ChargeGaugeUI chargeWidgetPrefab;

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
                if (readouts[i] is IHeatReadout or IAmmoReadout)
                {
                    built.Add(new BoundReadout(slot, readouts[i], null));
                    continue;
                }
                if (!panel)
                {
                    panel = Instantiate(panelPrefab, transform);
                    panel.Initialize(weapons.DisplayName(slot));
                    panels.Add(panel);
                }
                var widget = CreateWidget(readouts[i], panel.Container);
                if (widget)
                    built.Add(new BoundReadout(slot, readouts[i], widget));
            }
        }

        private MonoBehaviour CreateWidget(IWeaponReadout readout, Transform container)
        {
            switch (readout)
            {
                case IChargeReadout charge when chargeWidgetPrefab:
                    var chargeGauge = Instantiate(chargeWidgetPrefab, container);
                    chargeGauge.Initialize(charge);
                    return chargeGauge;

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
