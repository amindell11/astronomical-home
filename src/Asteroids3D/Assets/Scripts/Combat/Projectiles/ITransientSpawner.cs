using System;
using UnityEngine;

namespace Combat.Projectile
{
    /// <summary>
    /// A transient that spawns further transients (a grenade's concussion wave) announces them
    /// here, paired with the child's return-to-pool action; whoever tracks the spawner subscribes
    /// and tracks announced children under the same rule, recursively — spawners never know the
    /// tracker exists.
    /// </summary>
    public interface ITransientSpawner
    {
        event Action<MonoBehaviour, Action> Spawned;
    }
}
