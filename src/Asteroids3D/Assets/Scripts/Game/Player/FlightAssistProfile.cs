using UnityEngine;

namespace Game.Player
{
    [CreateAssetMenu(fileName = "FlightAssist", menuName = "Game/Flight Assist Profile")]
    public sealed class FlightAssistProfile : ScriptableObject
    {
        [Tooltip("Share of the engines' authority the assist spends braking drift on a released axis.")]
        [Range(0f, 1f)] public float strength = 1f;

        [Tooltip("Drift speed along an axis, in units per second, at or above which the assist brakes with full strength. Below it the braking eases off linearly.")]
        [Min(0.01f)] public float fullBrakeSpeed = 8f;

        [Tooltip("Seconds after a key on an axis is released, or after a boost, before the assist brakes that axis.")]
        [Min(0f)] public float delay = 0.2f;
    }
}
