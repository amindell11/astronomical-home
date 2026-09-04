using System;
using AI;
using Combat.Projectiles;
using Combat.Targeting;
using Combat.Weapons;
using Movement.MPC;
using Ships;
using Ships.Damage;
using Ships.Movement;

namespace Game.Capture
{
    /// <summary>
    /// Single home for gizmo-capture-profile knowledge: which component types a
    /// profile selects for drawing (<see cref="Resolve"/>) and whether footage
    /// under it carries presentation (<see cref="PresentationFor"/>). Presentation
    /// must be decided pre-spawn — visual/audio rigs self-gate at Awake — so both
    /// capture lanes (SessionSpec.Presentation, the scenario runner's
    /// SessionProfile) consult this rule before composing a session.
    /// </summary>
    public static class GizmoCaptureProfiles
    {
        private static readonly Type[] Steering =
        {
            typeof(AICommander),
            typeof(Navigator),
            typeof(Scout),
            typeof(MovementController),
        };

        private static readonly Type[] Combat =
        {
            typeof(Ship),
            typeof(ProjectileBase),
            typeof(Gunner),
            typeof(LockOnSensor),
            typeof(Missile),
            typeof(Missiles),
            typeof(Lasers),
            typeof(DamageController),
        };

        private static readonly Type[] Everything = Combine(Steering, Combat);

        /// <summary>None films plain gameplay; any gizmo profile films presentation-off — silhouettes plus gizmos are the footage.</summary>
        public static bool PresentationFor(GizmoCaptureProfile profile) => profile == GizmoCaptureProfile.None;

        /// <summary>None selects no types — the Game View then films the game alone, which is how plain gameplay footage is captured.</summary>
        public static Type[] Resolve(GizmoCaptureProfile profile) => profile switch
        {
            GizmoCaptureProfile.None => Array.Empty<Type>(),
            GizmoCaptureProfile.Steering => Steering,
            GizmoCaptureProfile.Combat => Combat,
            GizmoCaptureProfile.Everything => Everything,
            _ => throw new ArgumentException($"Gizmo capture profile {profile} does not select any component types."),
        };

        private static Type[] Combine(Type[] a, Type[] b)
        {
            var combined = new Type[a.Length + b.Length];
            Array.Copy(a, combined, a.Length);
            Array.Copy(b, 0, combined, a.Length, b.Length);
            return combined;
        }
    }
}
