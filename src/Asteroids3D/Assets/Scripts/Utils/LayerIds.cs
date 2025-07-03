using UnityEngine;

public static class LayerIds
{
    public static readonly int Ship = LayerMask.NameToLayer(TagNames.Ship);
    public static readonly int Asteroid = LayerMask.NameToLayer(TagNames.Asteroid);
    public static readonly int Projectile = LayerMask.NameToLayer(TagNames.Projectile);
    public static readonly int Missile = LayerMask.NameToLayer(TagNames.Missile);
    public static readonly int AsteroidCullingBoundary = LayerMask.NameToLayer(TagNames.AsteroidCullingBoundary);

    /// <summary>
    /// Combine multiple layer indices into a LayerMask.
    /// </summary>
    public static int Mask(params int[] layers)
    {
        int mask = 0;
        foreach (var l in layers)
        {
            mask |= 1 << l;
        }
        return mask;
    }
} 