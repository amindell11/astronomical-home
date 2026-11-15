using System;
using UnityEngine;

namespace Substrate.Sectors.Elements.Stations.Visual
{
    public enum StationLook { Open, InUse, Dark }

    /// <summary>
    /// Visual-only motion and light looks of an outpost station. It drifts a little about where it
    /// was placed. When it goes dark, every light goes out at once, the parts coast to a stop, and
    /// the drift goes on.
    /// </summary>
    public sealed class StationMotion : MonoBehaviour
    {
        [Serializable]
        private struct Spin
        {
            [Tooltip("Part that turns.")]
            public Transform part;

            [Tooltip("Axis in the part's local space.")]
            public Vector3 localAxis;

            [Tooltip("Seconds for one full turn. Negative turns the other way.")]
            public float secondsPerTurn;
        }

        [Serializable]
        private struct ChaseBand
        {
            [Tooltip("Lamps in this band.")]
            public Renderer[] lamps;
        }

        private struct CachedLight
        {
            public Renderer renderer;
            public Vector4 linearEmission;
            public Color paper;
        }

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int PaperColorId = Shader.PropertyToID("_PaperColor");

        [Tooltip("Parts that turn, each about its own local axis.")]
        [SerializeField] private Spin[] spins;

        [Tooltip("Windows and the prize. They glow at their material colour.")]
        [SerializeField] private Renderer[] steadyLights;

        [Tooltip("Service-light bands, outermost first. A brightness pulse steps through them toward the berth.")]
        [SerializeField] private ChaseBand[] chase;

        [Tooltip("Seconds for the pulse to cross every band once, plus one band of rest.")]
        [SerializeField] private float chaseSecondsPerSweep = 2.4f;

        [Tooltip("Extra brightness at the pulse peak. 1.5 reaches 2.5 times.")]
        [SerializeField] private float chaseBoost = 1.5f;

        [Tooltip("Service-lamp glow multiplier while the station is in use.")]
        [SerializeField] private float inUseBoost = 2.5f;

        [Tooltip("Seconds the parts take to coast to a stop when the station goes dark, and to spin back up when it relights.")]
        [SerializeField] private float coastSeconds = 10f;

        [Tooltip("Farthest the station wanders from where it was placed, in units.")]
        [SerializeField] private float driftUnits = 0.4f;

        [Tooltip("Largest wobble about each axis, in degrees.")]
        [SerializeField] private float driftDegrees = 2f;

        [Tooltip("Seconds over which the drift changes course. Larger is slower.")]
        [SerializeField] private float driftSeconds = 20f;

        [Tooltip("Body colour of every light while the station is dark.")]
        [SerializeField] private Color darkColour = new Color32(0x1E, 0x20, 0x30, 0xFF);

        [Tooltip("Light look. Applied at start, and live in the inspector during play.")]
        [SerializeField] private StationLook look = StationLook.Open;

        private MaterialPropertyBlock block;
        private CachedLight[] steady;
        private CachedLight[][] bands;
        private float power;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private float driftSeed;
        private bool awake;

        private void Awake()
        {
            foreach (var spin in spins)
                if (!spin.part)
                    throw new InvalidOperationException($"StationMotion on '{name}' is missing a spin part.");

            block = new MaterialPropertyBlock();
            steady = Cache(steadyLights, "steady light");
            bands = new CachedLight[chase.Length][];
            for (var i = 0; i < chase.Length; i++)
                bands[i] = Cache(chase[i].lamps, "chase lamp");

            power = look == StationLook.Dark ? 0f : 1f;
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            // Seeded from where it spawned, not UnityEngine.Random, so gameplay's random stream is untouched.
            driftSeed = Mathf.Repeat(Mathf.Sin(Vector3.Dot(transform.position, new Vector3(12.9898f, 78.233f, 37.719f))) * 43758.5453f, 1f) * 1000f;
            awake = true;
        }

        private void Start() => Show(look);

        private void OnValidate()
        {
            if (Application.isPlaying && awake)
                Show(look);
        }

        private void Update()
        {
            var t = driftSeed + Time.time / driftSeconds;
            transform.localPosition = restPosition + driftUnits * Wander(t, 0f);
            transform.localRotation = restRotation * Quaternion.Euler(driftDegrees * Wander(t, 50f));

            var dark = look == StationLook.Dark;
            power = Mathf.MoveTowards(power, dark ? 0f : 1f, Time.deltaTime / coastSeconds);
            foreach (var spin in spins)
                spin.part.Rotate(spin.localAxis, 360f / spin.secondsPerTurn * power * Time.deltaTime, Space.Self);
            if (dark) return;

            var p = Mathf.Repeat(Time.time / chaseSecondsPerSweep, 1f) * (chase.Length + 1);
            var flare = look == StationLook.InUse ? inUseBoost : 1f;
            for (var i = 0; i < bands.Length; i++)
            {
                var factor = (1f + chaseBoost * Mathf.Max(0f, 1f - Mathf.Abs(i + .5f - p))) * flare;
                var lamps = bands[i];
                for (var j = 0; j < lamps.Length; j++)
                {
                    var lamp = lamps[j];
                    lamp.renderer.GetPropertyBlock(block);
                    block.SetVector(EmissionColorId, lamp.linearEmission * factor);
                    lamp.renderer.SetPropertyBlock(block);
                }
            }
        }

        public void Show(StationLook next)
        {
            look = next;
            var dark = next == StationLook.Dark;
            for (var i = 0; i < steady.Length; i++)
                Paint(steady[i], dark);
            for (var i = 0; i < bands.Length; i++)
            {
                var lamps = bands[i];
                for (var j = 0; j < lamps.Length; j++)
                    Paint(lamps[j], dark);
            }
        }

        private static Vector3 Wander(float t, float lane) => new(
            Mathf.PerlinNoise(t, lane) * 2f - 1f,
            Mathf.PerlinNoise(t, lane + 17f) * 2f - 1f,
            Mathf.PerlinNoise(t, lane + 31f) * 2f - 1f);

        private CachedLight[] Cache(Renderer[] renderers, string kind)
        {
            var cached = new CachedLight[renderers.Length];
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (!renderer)
                    throw new InvalidOperationException($"StationMotion on '{name}' is missing a {kind}.");
                var material = renderer.sharedMaterial;
                if (!material)
                    throw new InvalidOperationException(
                        $"StationMotion on '{name}' is missing a {kind}'s material.");
                cached[i] = new CachedLight
                {
                    renderer = renderer,
                    linearEmission = material.GetColor(EmissionColorId).linear,
                    paper = material.GetColor(PaperColorId)
                };
            }

            return cached;
        }

        private void Paint(CachedLight light, bool dark)
        {
            light.renderer.GetPropertyBlock(block);
            block.SetVector(EmissionColorId, dark ? Vector4.zero : light.linearEmission);
            block.SetColor(PaperColorId, dark ? darkColour : light.paper);
            light.renderer.SetPropertyBlock(block);
        }
    }
}
