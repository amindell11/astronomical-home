using System;
using Balance;
using Combat.Projectiles;
using Damage;
using Movement;
using Ships.Mods;
using Substrate;
using UnityEngine;
using Utils;

namespace Ships.Keystones.Kits
{
    /// <summary>
    /// Vanguard's kit: a dash whose window drops every hit on the ship. With Riposte, each shot the
    /// guard drops fires back down the line it came in on, as the ship's own shot carrying the
    /// dropped damage. With Double dash, a fresh press soon after the dash fires a second one, and the
    /// cooldown starts from the second, or from when its chance runs out.
    /// </summary>
    public sealed class GuardDash : Dash
    {
        [Tooltip("Seconds after activation during which every hit on the ship is dropped.")]
        [Stat, SerializeField] private float window = 0.4f;

        [Tooltip("The Riposte keystone mod, whose holding fires blocked shots back at their shooter.")]
        [SerializeField] private KeystoneMod riposte;

        [Tooltip("With Riposte, the shot a blocked hit fires back; it carries the blocked hit's damage.")]
        [SerializeField] private Laser riposteShot;

        [Tooltip("The Double dash keystone mod, whose holding lets the dash fire twice before its cooldown starts.")]
        [SerializeField] private KeystoneMod doubleDash;

        [Tooltip("With Double dash, seconds after the first dash during which a fresh press fires the second.")]
        [Stat, SerializeField] private float doubleDashWindow = 1f;

        // The first dash fired with Double dash: pending until the press is released, then armed.
        private bool secondPending;
        private bool secondArmed;

        public override string DisplayName => "Dash";
        public float Window => window;
        public bool Guarding => SinceActivation < window;
        public int Ripostes { get; private set; }

        public override bool Active => Guarding;
        public override bool FollowUpOpen => secondPending || secondArmed;

        public override bool Blocks(in DamageInfo hit) => Guarding;

        public override void NotifyBlocked(DamageInfo hit)
        {
            if (!Guarding) return;
            CreditActivation();
            if (Behaviours.Holds(riposte)) Riposte(in hit);
        }

        protected override KeystoneEffect Activate(in Kinematics kinematics, Vector2? target)
        {
            secondPending = !secondArmed && Behaviours.Holds(doubleDash);
            secondArmed = false;
            return base.Activate(in kinematics, target);
        }

        // The press that dashed can stay held past its step, so only a press after a release fires the second.
        protected override KeystoneEffect Idle(in Kinematics kinematics, float input)
        {
            if (!secondPending || input != 0f) return default;
            secondPending = false;
            secondArmed = true;
            ShortenCooldown(0f);
            return default;
        }

        // Step tests the press before Sustain, so the chance closes a step early, on the window's last step.
        protected override KeystoneEffect Sustain(in Kinematics kinematics, float dt, float baseMaxSpeed)
        {
            if ((!secondPending && !secondArmed) || SinceActivation + dt < doubleDashWindow) return default;
            secondPending = false;
            secondArmed = false;
            RestartCooldown();
            return default;
        }

        protected override void OnReset()
        {
            Ripostes = 0;
            secondPending = false;
            secondArmed = false;
        }

        // A blast's or a collision's velocity points away from its source, not back along a shot.
        private void Riposte(in DamageInfo hit)
        {
            if (hit.Kind is DamageKind.Collision or DamageKind.ConcussionWave || !hit.AttackerId.IsValid) return;
            var back = -GamePlane.WorldDirOnPlane(hit.HitVelocity);
            if (back.sqrMagnitude < 1e-6f) return;
            if (Host.Shooter == null || Host.Projectiles == null)
                throw new InvalidOperationException($"{name} holds Riposte on a host that fires nothing.");

            back.Normalize();
            var from = GamePlane.WorldPointOnPlane(hit.HitPoint);
            var shot = SimplePool<Laser>.Get(riposteShot, from, GamePlane.PlanePose(GamePlane.Normal, back));
            shot.Initialize(Host.Shooter);
            shot.SetDamageScale(hit.Amount / shot.Damage);
            Host.Projectiles.Register(shot, shot.ReturnToPoolImmediate);
            shot.Launch(back, from + back);
            Ripostes++;
        }
    }
}
