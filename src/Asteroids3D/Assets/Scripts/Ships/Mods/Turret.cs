using System;
using Balance;
using Loot;
using Ships.Loadout;
using Substrate;
using UnityEngine;

namespace Ships.Mods
{
    /// <summary>The Turret passive on the ship that holds it: its weapon sockets swing to the aim point instead of firing along the nose, and the hull turns slower but backs off harder.</summary>
    [Serializable]
    public sealed class Turret
    {
        [Tooltip("The Turret passive key this ship reads. Required on a ship that binds run mods.")]
        [SerializeField] private PassiveMod key;

        [Tooltip("Multiplier on the hull's top turn rate and turning torque.")]
        [Stat, SerializeField, Range(0.05f, 1f)] private float turnScale = 0.5f;

        [Tooltip("Multiplier on the hull's reverse thrust.")]
        [Stat, SerializeField, Min(1f)] private float reverseThrustScale = 2.5f;

        public bool Held { get; private set; }

        public void Read(HeldBehaviours held) => Held = held.Holds(key);

        public void Scale(ResolvedShipStats stats)
        {
            if (!Held) return;
            stats.maxYawRate *= turnScale;
            stats.yawTorque *= turnScale;
            stats.reverseForce *= reverseThrustScale;
        }

        /// <summary>Puts <paramref name="socket"/> back at its <paramref name="authored"/> local rotation, then, while held, swings it to face <paramref name="targetPoint"/>.</summary>
        public void Aim(Transform socket, Quaternion authored, Vector3 targetPoint)
        {
            socket.localRotation = authored;
            if (!Held) return;
            var facing = GamePlane.WorldDirOnPlane(socket.up);
            var toTarget = GamePlane.WorldDirOnPlane(targetPoint - socket.position);
            var turn = Vector3.SignedAngle(facing, toTarget, GamePlane.Normal);
            socket.rotation = Quaternion.AngleAxis(turn, GamePlane.Normal) * socket.rotation;
        }

#if UNITY_EDITOR
        internal void Configure(PassiveMod key, float turnScale, float reverseThrustScale)
        {
            this.key = key;
            this.turnScale = turnScale;
            this.reverseThrustScale = reverseThrustScale;
        }
#endif
    }
}
