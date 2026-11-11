using Substrate;
using UnityEngine;

namespace Combat.Projectiles.Visual
{
    /// <summary>
    /// How far below the game plane, along its normal and away from the camera, every shot draws.
    /// It is deeper than any drawn hull reaches, so a hull covers a shot wherever they overlap while the
    /// shot's collider stays on the plane. Projectile prefabs place their visuals at this depth and
    /// <c>ShotDepthEditModeTests</c> holds them and the hulls to it.
    /// </summary>
    public static class ShotDepth
    {
        public const float BelowPlane = 0.35f;

        public static Vector3 Offset => GamePlane.Normal * BelowPlane;
    }
}
