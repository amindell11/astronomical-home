using System;

namespace Combat.Projectiles
{
    public interface IReleasable
    {
        event Action Released;
    }
}
