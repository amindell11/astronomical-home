using System;
using Ships;
using UnityEngine;

namespace Substrate.Sectors.Elements
{
    /// <summary>
    /// A baked, serialized reference to a hand-placed ship child of a sector prefab, wired into
    /// the unit service in place at load (the ship you placed IS the runtime ship).
    /// Annotations are authored in the sector's inspector list, never on a per-child wrapper.
    /// Pose comes from the child's transform.
    /// </summary>
    [Serializable]
    public struct AdoptedShip
    {
        [Tooltip("Placed ship child to adopt.")]
        public Ship target;

        [Tooltip("Team number applied to the ship at adoption.")]
        public int team;

        [Tooltip("If false, the ship is deactivated immediately after adoption (e.g. a chaser revealed later).")]
        public bool startActive;

        [Tooltip("Optional respawn rule (origin None = no respawn).")]
        public RespawnPolicy respawn;
    }
}
