using System;
using Combat.Targeting;
using Ships.Command;
using Ships.Damage;
using Ships.Movement;
using UnityEngine;

namespace Ships.Presentation
{
    /// <summary>
    /// Narrow, push-oriented view of a ship handed to its visual rig — the presentation parallel to
    /// <see cref="ShipControl"/>. Assembled by the game-tier presentation installer and injected into
    /// the rig; visual components read it instead of discovering the ship via GetComponentInParent.
    /// Carries only what visuals consume: the follow anchor, the damage push-surface, the live pilot
    /// command (engine and wings), the applied thrust (thruster jets), the lock channel (lock
    /// indicator), the body (engine audio), and whether the keystone kit is live (wing sweep). A
    /// binder with no ship leaves the body and the kit read null.
    /// </summary>
    public readonly struct ShipView
    {
        /// <summary>Anchor the rig parents under / follows.</summary>
        public readonly Transform Root;

        /// <summary>Damage push-surface (damage/death events, health/shield resources).</summary>
        public readonly IDamageEvents Damage;

        /// <summary>Live pilot command for this step (engine audio, wing thrust pose).</summary>
        public readonly Func<PilotCommand> Command;

        /// <summary>Lock-on channel for the lock indicator.</summary>
        public readonly LockChannel Lock;

        /// <summary>Whether this is the local player's ship (drives player-vs-enemy presentation, e.g. minimap layer).</summary>
        public readonly bool IsPlayer;

        public readonly Rigidbody Body;

        /// <summary>Whether the ship's keystone kit is live right now. Null reads as never live.</summary>
        public readonly Func<bool> KeystoneLive;

        /// <summary>Thrust the movement system applied on the last step (thruster jets).</summary>
        public readonly Func<AppliedThrust> Thrust;

        public ShipView(Transform root, IDamageEvents damage, Func<PilotCommand> command, LockChannel @lock, bool isPlayer,
            Rigidbody body = null, Func<bool> keystoneLive = null, Func<AppliedThrust> thrust = null)
        {
            Root = root;
            Damage = damage;
            Command = command;
            Lock = @lock;
            IsPlayer = isPlayer;
            Body = body;
            KeystoneLive = keystoneLive;
            Thrust = thrust;
        }
    }
}
