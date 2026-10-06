using UnityEngine;

namespace Combat.Weapons
{
    /// <summary>
    /// Per-frame trigger state for a <em>single</em> weapon slot, pushed to an
    /// <see cref="Ships.Command.IWeapons"/>. Carries raw input facts only — what the trigger is doing, not
    /// what it means. The weapon interprets its own firing semantics (full-auto fires while
    /// held, semi-auto on each press, charge weapons accumulate while held and fire on
    /// release; see <see cref="WeaponComponent.HandleTrigger"/>). Weapons are commanded individually
    /// (one command per slot) rather than bundled, so the piloting and firing channels — and
    /// the weapons among themselves — stay independent.
    /// </summary>
    public struct WeaponCommand
    {
        /// <summary>True while the trigger is down this step.</summary>
        public bool held;

        /// <summary>True only on the step <see cref="held"/> rises, from a human or an AI commander alike.</summary>
        public bool pressed;

        /// <summary>Game-plane world point a shot fired this step flies to; straight-bolt weapons ignore it.</summary>
        public Vector3 targetPoint;
    }
}
