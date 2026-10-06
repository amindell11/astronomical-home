using System;
using UnityEngine;

namespace Combat.Projectiles
{
    /// <summary>A concussion charge in flight, as the HUD sees it: where it will stop and how far its blast reaches.</summary>
    public interface IChargeFlight
    {
        Vector3 TargetPoint { get; }
        float BlastRadius { get; }

        /// <summary>Raised once when the charge leaves play: detonated, popped or flushed.</summary>
        event Action Ended;
    }
}
