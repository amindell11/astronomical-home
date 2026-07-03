using System;

namespace Asteroids.Fields.Core
{
    /// <summary>
    /// Stable identity of a field asteroid, independent of load order.
    /// Baseline asteroids are keyed by (cell, index-in-cell) so the same spot
    /// always produces the same IDs; fragments get fresh authored IDs from a
    /// session counter (they have no LUT home and live in the overlay).
    /// </summary>
    [Serializable]
    public readonly struct AsteroidId : IEquatable<AsteroidId>
    {
        public readonly int CellX;
        public readonly int CellY;
        public readonly int Index;
        public readonly bool Authored;

        private AsteroidId(int cellX, int cellY, int index, bool authored)
        {
            CellX = cellX;
            CellY = cellY;
            Index = index;
            Authored = authored;
        }

        public static AsteroidId Baseline(int cellX, int cellY, int index) =>
            new(cellX, cellY, index, false);

        public static AsteroidId CreateAuthored(int sessionIndex) =>
            new(0, 0, sessionIndex, true);

        public bool Equals(AsteroidId other) =>
            CellX == other.CellX && CellY == other.CellY &&
            Index == other.Index && Authored == other.Authored;

        public override bool Equals(object obj) => obj is AsteroidId other && Equals(other);

        public override int GetHashCode() =>
            (int)DeterministicRandom.Hash(CellX, CellY, Index, Authored ? 1 : 0);

        public override string ToString() =>
            Authored ? $"authored:{Index}" : $"baseline:({CellX},{CellY})#{Index}";
    }
}
