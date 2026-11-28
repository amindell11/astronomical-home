using UnityEngine;

namespace Ships.Visuals
{
    [CreateAssetMenu(fileName = "EngineFlameFeel", menuName = "Game/Ships/Engine Flame Feel")]
    public sealed class EngineFlameFeel : ScriptableObject
    {
        [Tooltip("The engine flame material every main flame draws with.")]
        [SerializeField] private Material material;

        [Tooltip("Drawings in the baked sheet.")]
        [SerializeField] private int frames = 8;

        [Tooltip("Width of one sheet cell over its length.")]
        [SerializeField] private float cellAspect = 0.4f;

        [Tooltip("How often each flame picks a new drawing.")]
        [SerializeField] private float framesPerSecond = 12f;

        [Tooltip("Seconds for the flame to grow from nothing to full.")]
        [SerializeField] private float igniteTime = 0.11f;

        [Tooltip("Seconds for the flame to shrink from full to nothing.")]
        [SerializeField] private float cutoffTime = 0.29f;

        [Tooltip("Length of the idle flame while not thrusting, as a fraction of full.")]
        [SerializeField, Range(0f, 1f)] private float pilotLength = 0.2f;

        public Material Material => material;
        public int Frames => frames;
        public float CellAspect => cellAspect;
        public float FramesPerSecond => framesPerSecond;
        public float IgniteTime => igniteTime;
        public float CutoffTime => cutoffTime;
        public float PilotLength => pilotLength;
    }
}
