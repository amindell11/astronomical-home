using System;
using AI;
using Combat.Targeting;
using Combat.Weapons;
using Combat.Weapons.Conditions;
using Damage;
using Ships;
using Ships.Command;
using Ships.Registry;
using Substrate;
using UnityEngine;

namespace RL.Probes
{
    /// <summary>What one weapon did over one episode. The lock counts mean something only under <see cref="hasLock"/>, the charge counts only under <see cref="hasCharge"/>.</summary>
    [Serializable]
    public struct MarksmanshipTally
    {
        public string weapon;
        public int steps;
        public int envelopeSteps;
        public float meanCrossingSpeed;
        public int fired;
        public int hits;
        public float damageDealt;
        public int selfHits;
        public bool kill;
        public bool hasLock;
        public int guidedLaunches;
        public bool hasCharge;
        public int fullChargeShots;
        public int earlyShots;
        public float meanEarlyCharge;
        public int droppedCharges;
    }

    /// <summary>Counts one episode of a shooter's primary weapon against one target: shots off <see cref="WeaponComponent.OnFire"/>, hits off the target's damage events, and per fixed step whether the weapon's own firing envelope held. A hit is damage applied to the target that the shooter caused by anything but a collision; the same event on the shooter is a self-hit. Lock and charge behaviour is read through whatever <see cref="WeaponComponent.LockSource"/> and charge readout the weapon exposes, never by weapon type. A pure observer: construct after the pair-reset, dispose to unhook.</summary>
    public sealed class MarksmanshipSampler : IDisposable
    {
        private readonly Ship shooter;
        private readonly Ship target;
        private readonly ShipId shooterId;
        private readonly WeaponComponent weapon;
        private readonly Gunsight sight;
        private readonly ILockStateSource lockSource;
        private readonly IChargeReadout charge;

        private MarksmanshipTally tally;
        private float crossingSpeedSum;
        private float earlyChargeSum;
        private float lastCharge;
        private float releasedCharge;

        public MarksmanshipSampler(Ship shooter, Ship target)
        {
            this.shooter = shooter;
            this.target = target;
            shooterId = shooter.Id;
            weapon = shooter.Weapons.Primary;
            sight = shooter.Weapons.Context.Sight(WeaponSlot.Primary);
            lockSource = weapon.LockSource;
            charge = ChargeReadout(weapon);
            tally = new MarksmanshipTally
            {
                weapon = shooter.Weapons.PrimaryMountPrefab.name,
                hasLock = lockSource != null,
                hasCharge = charge != null,
            };

            weapon.OnFire += OnFired;
            target.Damage.OnDamaged += OnTargetDamaged;
            target.Damage.OnDeath += OnTargetDeath;
            shooter.Damage.OnDamaged += OnShooterDamaged;
            if (lockSource != null) lockSource.OnStateChanged += OnLockStateChanged;
            if (charge != null)
            {
                lastCharge = charge.ChargePct;
                charge.OnChargeChanged += OnChargeChanged;
            }
        }

        public MarksmanshipTally Tally
        {
            get
            {
                var result = tally;
                result.meanCrossingSpeed = tally.steps > 0 ? crossingSpeedSum / tally.steps : 0f;
                result.meanEarlyCharge = tally.earlyShots > 0 ? earlyChargeSum / tally.earlyShots : 0f;
                return result;
            }
        }

        public void Sample()
        {
            if (!Alive(shooter) || !Alive(target)) return;
            tally.steps++;

            var shooterKin = shooter.Kinematics;
            var targetKin = target.Kinematics;
            // Evaluate at the intercept lead the gunner fires this weapon at, not the target's center.
            var aimWorld = GamePlane.PlanePointToWorld(Gunner.AimPoint(
                in shooterKin, targetKin.pos, targetKin.vel, weapon.ProjectileSpeed));
            if (sight.InEnvelope(aimWorld)) tally.envelopeSteps++;

            var sightLine = (targetKin.pos - shooterKin.pos).normalized;
            crossingSpeedSum += Mathf.Abs(sightLine.x * targetKin.vel.y - sightLine.y * targetKin.vel.x);
        }

        public void Dispose()
        {
            weapon.OnFire -= OnFired;
            target.Damage.OnDamaged -= OnTargetDamaged;
            target.Damage.OnDeath -= OnTargetDeath;
            shooter.Damage.OnDamaged -= OnShooterDamaged;
            if (lockSource != null) lockSource.OnStateChanged -= OnLockStateChanged;
            if (charge != null) charge.OnChargeChanged -= OnChargeChanged;
        }

        private void OnFired()
        {
            tally.fired++;
            if (charge == null) return;

            // Firing zeroes the charge before OnFire, so the release just counted as a drop was this shot.
            tally.droppedCharges--;
            if (releasedCharge >= 1f)
            {
                tally.fullChargeShots++;
                return;
            }
            tally.earlyShots++;
            earlyChargeSum += releasedCharge;
        }

        private void OnChargeChanged(float value)
        {
            // A dying ship's weapons reset; nobody released that trigger.
            if (value <= 0f && Alive(shooter))
            {
                releasedCharge = lastCharge;
                tally.droppedCharges++;
            }
            lastCharge = value;
        }

        // Only a launch consumes a held lock; expiry and loss drop it to Idle.
        private void OnLockStateChanged(LockState previous, LockState next)
        {
            if (previous == LockState.Locked && next == LockState.Cooldown) tally.guidedLaunches++;
        }

        private void OnTargetDamaged(DamageInfo hit)
        {
            if (!IsWeaponHit(in hit)) return;
            tally.hits++;
            tally.damageDealt += hit.Amount;
        }

        private void OnTargetDeath(ShipId victim, DamageInfo killingBlow) => tally.kill = IsWeaponHit(in killingBlow);

        private void OnShooterDamaged(DamageInfo hit)
        {
            if (IsWeaponHit(in hit)) tally.selfHits++;
        }

        // The shooter carries one weapon, so its every non-collision damage event is that weapon's.
        private bool IsWeaponHit(in DamageInfo hit) =>
            hit.AttackerId == shooterId && hit.Kind != DamageKind.Collision;

        private static bool Alive(Ship ship) => ship.Damage.Health.CurrentValue > 0f;

        private static IChargeReadout ChargeReadout(WeaponComponent weapon)
        {
            foreach (var readout in weapon.Readouts)
                if (readout is IChargeReadout chargeReadout)
                    return chargeReadout;
            return null;
        }
    }
}
