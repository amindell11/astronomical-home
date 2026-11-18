using Balance;
using Combat.Projectiles;
using Combat.Tethers;
using Combat.Weapons.Conditions;
using Combat.Weapons.Mods;
using Substrate.Services.Projectiles;
using UnityEngine;

namespace Combat.Weapons.Arsenal
{
    public class Harpoons : WeaponBase<Harpoon>
    {
        [Header("Tether")]
        [Tooltip("Force per unit of stretch while the tether is taut.")]
        [Stat, SerializeField, Min(0f)] private float stiffness = 20000f;
        [Tooltip("Force per unit of speed at which the ends separate.")]
        [Stat, SerializeField, Min(0f)] private float damping = 2000f;
        [Tooltip("Units per second the tether shortens while held.")]
        [Stat, SerializeField, Min(0f)] private float reelRate = 6f;
        [Tooltip("Shortest length the tether reels to.")]
        [Stat, SerializeField, Min(0f)] private float minLength = 4f;
        [Tooltip("Tension that snaps the tether.")]
        [Stat, SerializeField, Min(0f)] private float snapForce = 16000f;
        [Tooltip("Seconds a held tether lasts.")]
        [Stat, SerializeField, Min(0.01f)] private float maxSeconds = 3f;

        [Header("Conditions")]
        [Stat, SerializeField] private Rounds rounds;
        [Stat, SerializeField] private Cooldown cooldown;

        private Harpoon inFlight;
        private Tether tether;

        public Rounds Rounds => rounds;
        public Tether Tether => tether;
        public Harpoon InFlight => inFlight;
        protected override WeaponCondition[] AuthoredConditions => new WeaponCondition[] { rounds, cooldown };
        public override bool IsOrdnance => true;
        public override bool AutoFire => false;
        public override bool Supports(WeaponModField field) => field != WeaponModField.Damage && base.Supports(field);

        private TetherSpec Spec => new(stiffness, damping, reelRate, minLength, snapForce, maxSeconds);

        protected override void Awake()
        {
            base.Awake();
            if (!rounds) rounds = GetComponent<Rounds>();
            if (!cooldown) cooldown = GetComponent<Cooldown>();
        }

        public override void HandleTrigger(in WeaponCommand cmd, IProjectileService projectiles)
        {
            if (!cmd.held)
            {
                LetGo();
                return;
            }

            if (cmd.pressed && !inFlight && tether == null)
                Fire(cmd.targetPoint, projectiles);
        }

        public override ProjectileBase Fire(Vector3 targetPoint, IProjectileService projectiles)
        {
            var harpoon = (Harpoon)base.Fire(targetPoint, projectiles);
            if (!harpoon) return null;
            Track(harpoon);
            return harpoon;
        }

        public override void Reset()
        {
            base.Reset();
            LetGo();
        }

        private void FixedUpdate()
        {
            if (tether == null) return;
            if (tether.Step(Time.fixedDeltaTime, Spec) != TetherEnd.None)
                tether = null;
        }

        private void OnDisable() => LetGo();

        private void Track(Harpoon harpoon)
        {
            inFlight = harpoon;
            harpoon.Hooked += OnHooked;
            harpoon.ReturnedToPool += OnFlightEnded;
        }

        private void OnHooked(HookTarget target, Vector3 point)
        {
            tether = new Tether(shooter.Body, firePoint, target, point);
        }

        private void OnFlightEnded()
        {
            inFlight.Hooked -= OnHooked;
            inFlight.ReturnedToPool -= OnFlightEnded;
            inFlight = null;
        }

        private void LetGo()
        {
            if (inFlight) inFlight.ReturnToPoolImmediate();
            tether = null;
        }
    }
}
