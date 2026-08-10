using UnityEngine;

namespace Asteroids
{
    /// <summary>
    /// The one definition of "how big is this rock" as a single scalar: the radius of a
    /// sphere with the mesh's baked volume.
    ///
    /// It answers a bulk/extent question — cheap-collider sizing, field packing, the
    /// obstacle-scan reach cull — not a shape question. Shape belongs to the baked lobes,
    /// which the MPC consumes directly; nothing here is meant to approximate a silhouette.
    /// </summary>
    public static class AsteroidGeometry
    {
        private const float InvFourThirdsPi = 3f / (4f * Mathf.PI);

        /// <summary>Radius of the sphere enclosing <paramref name="volume"/>; 0 for a
        /// non-positive volume, which only a failed bake can produce.</summary>
        public static float RadiusFromVolume(float volume) =>
            volume > 0f ? Mathf.Pow(volume * InvFourThirdsPi, 1f / 3f) : 0f;
    }
}
