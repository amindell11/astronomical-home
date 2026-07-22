using Game.Services;
using Movement.MPC.Field;
using UnityEngine;

namespace Game.RLHarness
{
    /// <summary>Single-arena composition shared by the RL host scenes (PlayMode tests compose theirs via TestArena).</summary>
    public static class HarnessArena
    {
        public static (UnitService units, ArenaContext arena, ProjectileService projectiles) Compose(GameObject host, Vector2 offset)
        {
            var units = host.AddComponent<UnitService>();
            var navField = host.AddComponent<NavFieldService>();
            var arena = new ArenaContext(offset, units.ActiveRegistry, navField);
            units.SetArena(arena);
            var projectiles = new ProjectileService(host.transform);
            units.SetProjectiles(projectiles);
            return (units, arena, projectiles);
        }
    }
}
