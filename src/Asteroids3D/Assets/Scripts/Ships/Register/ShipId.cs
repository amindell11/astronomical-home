using System;

namespace Ships
{
    public readonly struct ShipId : IEquatable<ShipId>
    {
        public readonly int Value;

        public ShipId(int value) => Value = value;

        public static ShipId Invalid => new(0);
        public bool IsValid => Value != 0;

        public bool Equals(ShipId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ShipId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => $"ShipId({Value})";

        public static bool operator ==(ShipId left, ShipId right) => left.Equals(right);
        public static bool operator !=(ShipId left, ShipId right) => !left.Equals(right);
    }
}
