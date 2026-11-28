using System;

namespace Combat.Projectiles
{
    public interface IBlastFront
    {
        float Radius { get; }
        float MaxRadius { get; }
        event Action Begun;
    }
}
