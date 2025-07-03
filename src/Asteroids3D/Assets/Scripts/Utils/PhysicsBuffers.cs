using UnityEngine;

public static class PhysicsBuffers
{
    // Shared collider buffers – allocated lazily on first use.
    private static Collider[] _buf32;
    private static Collider[] _buf64;
    private static Collider[] _buf128;

    /// <summary>
    /// Returns a reusable <see cref="Collider"/> array that is at least <paramref name="minSize"/> elements long.
    /// Buffers are created once and reused for the lifetime of the application to avoid repeated allocations.
    /// </summary>
    /// <param name="minSize">Minimum required length of the buffer.</param>
    /// <returns>A shared collider buffer with length >= <paramref name="minSize"/>.</returns>
    public static Collider[] GetColliderBuffer(int minSize = 32)
    {
        if (minSize <= 32)
            return _buf32 ??= new Collider[32];
        if (minSize <= 64)
            return _buf64 ??= new Collider[64];
        if (minSize <= 128)
            return _buf128 ??= new Collider[128];

        // Fallback – rarely hit. Allocate a dedicated buffer big enough for the request.
        return new Collider[minSize];
    }
} 