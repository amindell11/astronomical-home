using System.Collections.Generic;
using Combat;
using Combat.Weapons;
using Movement;
using UnityEngine;

namespace Ships.Command
{
    // ── Commands ──

    /// <summary>
    /// Per-frame piloting command issued by a <see cref="Commander"/> and pushed to an
    /// <see cref="IPilot"/>. All values are in the canonical ship reference frame (XY plane).
    /// Holds only movement — firing lives in <see cref="WeaponCommand"/>.
    /// </summary>
    public struct PilotCommand
    {
        /// <summary>Normalized forward (+) / reverse (–) thrust input in the range [-1, 1].</summary>
        public float thrust;

        /// <summary>Normalized strafe input in the range [-1, 1]. Positive is ship-right.</summary>
        public float strafe;

        /// <summary>Triggers a boost impulse when positive. Normalized boost magnitude [0, 1]. Default 0.</summary>
        public float boost;

        /// <summary>Desired yaw rate input in the range [-1, 1].</summary>
        public float yawTorque;
    }

    /// <summary>Identifies a single weapon mount on a ship.</summary>
    public enum WeaponSlot
    {
        Primary,
        Secondary,
    }

    /// <summary>
    /// Per-frame trigger state for a <em>single</em> weapon slot, pushed to an
    /// <see cref="IWeapons"/>. Carries raw input facts only — what the trigger is doing, not
    /// what it means. The weapon interprets its own firing semantics (full-auto fires while
    /// held, semi-auto on each press, charge weapons accumulate while held and fire on
    /// release; see <c>WeaponComponent.HandleTrigger</c>). Weapons are commanded individually
    /// (one command per slot) rather than bundled, so the piloting and firing channels — and
    /// the weapons among themselves — stay independent.
    /// </summary>
    public struct WeaponCommand
    {
        /// <summary>True while the trigger is down this step.</summary>
        public bool held;

        /// <summary>
        /// True on a step the trigger was pulled. A human press is a rising edge of
        /// <see cref="held"/>; an AI commander re-decides every step and truthfully reports a
        /// press on <em>each</em> step it wants fire ("mashing"), which is what keeps semi-auto
        /// weapons pacing by their own cooldown under sustained AI intent.
        /// </summary>
        public bool pressed;
    }

    // ── Actuators (push targets) ──

    /// <summary>
    /// The piloting actuator: accepts a <see cref="PilotCommand"/> and turns it into ship motion.
    /// Implemented by the movement system; injected into a <see cref="Commander"/> so it can drive
    /// the ship without holding a reference to the whole ship.
    /// </summary>
    public interface IPilot
    {
        /// <summary>Sets the piloting command to apply on the next physics step.</summary>
        void Drive(in PilotCommand cmd);
    }

    /// <summary>
    /// The weapons actuator: feeds trigger state to individual weapon slots. Implemented by the
    /// weapons system; injected into armed <see cref="Commander"/>s. The commander pushes a
    /// separate <see cref="WeaponCommand"/> per slot every step (including trigger-up steps —
    /// charge weapons fire on release).
    /// </summary>
    public interface IWeapons
    {
        /// <summary>Applies one step of trigger state to a single weapon slot.</summary>
        void Fire(WeaponSlot slot, in WeaponCommand cmd);
    }

    // ── Read contexts ──

    /// <summary>
    /// The read-only status of a ship that a <see cref="Commander"/> is given to learn its own
    /// situation. This is the entire surface a commander sees of the ship it drives — it replaces
    /// handing over the whole ship so commanders cannot reach into unrelated systems.
    /// </summary>
    public interface IShipStatus
    {
        ShipId Id { get; }
        Transform Transform { get; }
        Kinematics Kinematics { get; }
        Dynamics Dynamics { get; }
        float HealthPct { get; }
        float ShieldPct { get; }
        bool BoostAvailable { get; }
        float BoostCooldownRemaining { get; }
        float MaxSpeed { get; }
        float MaxYawRate { get; }
    }

    /// <summary>
    /// The read-only view of a ship's weapons that an armed <see cref="Commander"/> is given.
    /// Slot-keyed so the commander can reason about each weapon independently. Only supplied for
    /// ships that actually carry weapons (see <see cref="ShipControl.IsArmed"/>).
    /// </summary>
    public interface IWeaponContext
    {
        /// <summary>The weapon slots this ship has equipped.</summary>
        IReadOnlyList<WeaponSlot> Slots { get; }

        /// <summary>Whether the weapon in the given slot can currently fire (cooldown, heat, ammo…).</summary>
        bool IsReady(WeaponSlot slot);

        /// <summary>Muzzle speed of the slot's projectile (for AI intercept lead). 0 = hitscan/no lead.</summary>
        float ProjectileSpeed(WeaponSlot slot);

        /// <summary>The firing solution helper for the given slot, or null if the slot is empty.</summary>
        Gunsight Sight(WeaponSlot slot);
    }

    /// <summary>
    /// The read-only view of a ship's weapons that the HUD is given — the UI-facing sibling of
    /// <see cref="IWeaponContext"/> (same backing object, separate surface, so commanders never
    /// see display concerns and the UI never sees firing solutions). Slot-keyed; each slot's
    /// displayable state is its list of <see cref="IWeaponReadout"/> contracts.
    /// </summary>
    public interface IWeaponReadouts
    {
        /// <summary>The weapon slots this ship has equipped.</summary>
        IReadOnlyList<WeaponSlot> Slots { get; }

        /// <summary>Name shown on the slot's readout panel.</summary>
        string DisplayName(WeaponSlot slot);

        /// <summary>The slot's displayable state (heat, ammo, lock…), in display order.</summary>
        IReadOnlyList<IWeaponReadout> Readouts(WeaponSlot slot);
    }

    // ── Control bundle ──

    /// <summary>
    /// The bundle of narrow interfaces handed to a <see cref="Commander"/> at initialization:
    /// the read context plus the actuators it may drive. The weapon members are null for unarmed
    /// ships, so a peaceful commander never sees a weapons surface.
    /// </summary>
    public readonly struct ShipControl
    {
        /// <summary>Read-only status of the ship the commander drives.</summary>
        public readonly IShipStatus Ship;

        /// <summary>Piloting actuator.</summary>
        public readonly IPilot Pilot;

        /// <summary>Read-only view of the ship's weapons, or null if unarmed.</summary>
        public readonly IWeaponContext Weapons;

        /// <summary>Weapons actuator, or null if unarmed.</summary>
        public readonly IWeapons WeaponActuator;

        /// <summary>
        /// Root seed scope for this agent's decision RNGs — construction metadata assigned at spawn,
        /// reproducible across reconstructed episodes (unlike the ship's runtime <see cref="ShipId"/>).
        /// A commander splits it into per-consumer streams; a non-AI commander ignores it.
        /// </summary>
        public readonly SeedScope DecisionSeed;

        public ShipControl(IShipStatus ship, IPilot pilot, SeedScope decisionSeed,
            IWeaponContext weapons = null, IWeapons weaponActuator = null)
        {
            Ship = ship;
            Pilot = pilot;
            DecisionSeed = decisionSeed;
            Weapons = weapons;
            WeaponActuator = weaponActuator;
        }

        /// <summary>True when both the weapon context and actuator are present.</summary>
        public bool IsArmed => Weapons != null && WeaponActuator != null;
    }
}
