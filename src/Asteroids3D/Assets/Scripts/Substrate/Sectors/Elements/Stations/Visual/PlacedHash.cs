using UnityEngine;

namespace Substrate.Sectors.Elements.Stations.Visual
{
    /// <summary>A unit value hashed from a station's placed position, so placement-seeded looks leave UnityEngine.Random alone.</summary>
    internal static class PlacedHash
    {
        public static float Unit(Vector3 position, Vector3 key) =>
            Mathf.Repeat(Mathf.Sin(Vector3.Dot(position, key)) * 43758.5453f, 1f);
    }
}
