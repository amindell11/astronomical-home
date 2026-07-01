namespace Game.Bootstrap
{
    /// <summary>
    /// Session policy for what happens when the persistent player ship dies.
    /// <see cref="None"/> = nothing, <see cref="RespawnInPlace"/> = revive via the rig's RespawnPolicy,
    /// <see cref="RestartSector"/> = tear down and reload the active sector (rig persists).
    /// </summary>
    public enum PlayerDeathBehavior { None, RespawnInPlace, RestartSector }
}
