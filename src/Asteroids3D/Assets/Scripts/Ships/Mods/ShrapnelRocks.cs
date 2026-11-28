using System;
using Asteroids.Fragnetics;
using Balance;
using Loot;
using UnityEngine;

namespace Ships.Mods
{
    /// <summary>The Shrapnel rocks passive on the ship that holds it: a rock it breaks throws its fragments away from it, harder, and those fragments hit other ships harder.</summary>
    [Serializable]
    public sealed class ShrapnelRocks
    {
        [Tooltip("The Shrapnel rocks passive key this ship reads. Required on a ship that binds run mods.")]
        [SerializeField] private PassiveMod key;

        [Tooltip("Multiplier on the fragments' separation speed relative to the broken rock.")]
        [Stat, SerializeField, Min(0f)] private float spreadScale = 2f;

        [Tooltip("Speed (m/s) added to every fragment, straight away from the ship that broke the rock.")]
        [Stat, SerializeField, Min(0f)] private float throwSpeed = 12f;

        [Tooltip("Multiplier on a thrown fragment's collision damage to any ship but the thrower.")]
        [Stat, SerializeField, Min(0f)] private float enemyDamageScale = 3f;

        public bool Held { get; private set; }

        public float EnemyDamageScale => enemyDamageScale;

        public void Read(HeldBehaviours held) => Held = held.Holds(key);

        public void Throw(Frag[] frags, Vector3 rockVelocity, Vector3 away)
        {
            for (var i = 0; i < frags.Length; i++)
                frags[i].Velocity = rockVelocity + (frags[i].Velocity - rockVelocity) * spreadScale + away * throwSpeed;
        }

#if UNITY_EDITOR
        internal void Configure(PassiveMod key, float spreadScale, float throwSpeed, float enemyDamageScale)
        {
            this.key = key;
            this.spreadScale = spreadScale;
            this.throwSpeed = throwSpeed;
            this.enemyDamageScale = enemyDamageScale;
        }
#endif
    }
}
