using UnityEngine;

namespace Substrate.Sectors.Elements.Stations.Visual
{
    [CreateAssetMenu(fileName = "StationMotionFeel", menuName = "Sectors/Station Motion Feel Profile")]
    public sealed class StationMotionFeelProfile : ScriptableObject
    {
        [Tooltip("Seconds for one full turn of a part at rate 1. Each part's rate scales it.")]
        [Min(1f)] public float secondsPerTurn = 60f;

        [Tooltip("Seconds the parts take to coast to a stop when a station goes dark, and to spin back up when it relights.")]
        [Min(.01f)] public float coastSeconds = 10f;

        [Tooltip("Seconds for the service-light pulse to cross every band once, plus one band of rest.")]
        [Min(.01f)] public float chaseSecondsPerSweep = 2.4f;

        [Tooltip("Extra brightness at the pulse peak. 1.5 reaches 2.5 times.")]
        [Min(0f)] public float chaseBoost = 1.5f;

        [Tooltip("Farthest a station wanders from where it was placed, in units.")]
        [Min(0f)] public float driftUnits = 0.4f;

        [Tooltip("Largest wobble about each axis, in degrees.")]
        [Min(0f)] public float driftDegrees = 2f;

        [Tooltip("Seconds over which the drift changes course. Larger is slower.")]
        [Min(.01f)] public float driftSeconds = 20f;

        [Tooltip("Body colour of every light while a station is dark.")]
        public Color darkColour = new Color32(0x1E, 0x20, 0x30, 0xFF);
    }
}
