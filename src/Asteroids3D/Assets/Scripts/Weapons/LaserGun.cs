using UnityEngine;

/// <summary>
/// Concrete weapon that fires pooled <see cref="LaserProjectile"/> instances.
/// All common launcher logic lives in <see cref="LauncherBase{TProj}"/>.
/// </summary>
public class LaserGun : LauncherBase<LaserProjectile>
{
    // No extra behaviour (yet). Add charge-up, spread, etc. here.
} 