using System;
using UnityEngine;

namespace Combat.Projectiles
{
    public interface IDetonating
    {
        event Action<Vector3> OnDetonated;
    }
}
