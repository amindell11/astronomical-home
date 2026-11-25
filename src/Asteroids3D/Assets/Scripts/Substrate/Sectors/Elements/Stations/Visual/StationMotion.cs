using System;
using UnityEngine;

namespace Substrate.Sectors.Elements.Stations.Visual
{
    /// <summary>
    /// Visual-only motion and lights of an outpost station model. It watches its station body and is dark
    /// while the body's lit parts are off. Each placed station sits at its own tilt and drifts about it.
    /// When dark the lights go out at once and the parts coast to a stop while the drift goes on.
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

            [Tooltip("Turns per family turn. 1 keeps the family's pace; negative turns the other way.")]
            public float rate;
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

        [Tooltip("The station family's motion and light knobs.")]
        [SerializeField] private StationMotionFeelProfile feel;

        [Tooltip("Parts that turn, each about its own local axis.")]
        [SerializeField] private Spin[] spins;

        [Tooltip("Windows and the prize. Each glows at the emission its renderer carries when the model wakes: a worn palette's window light, else its material's.")]
        [SerializeField] private Renderer[] steadyLights;

        [Tooltip("Service-light bands, outermost first. A brightness pulse steps through them toward the berth.")]
        [SerializeField] private ChaseBand[] chase;

        private MaterialPropertyBlock block;
        private CachedLight[] steady;
        private CachedLight[][] bands;
        private FindStationBody body;
        private float power;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private float driftSeed;

        private void Awake()
        {
            if (!feel)
                throw new InvalidOperationException($"StationMotion on '{name}' needs its feel profile.");
            body = GetComponentInParent<FindStationBody>(true);
            if (!body)
                throw new InvalidOperationException($"StationMotion on '{name}' needs a station body above it.");

            foreach (var spin in spins)
                if (!spin.part)
                    throw new InvalidOperationException($"StationMotion on '{name}' is missing a spin part.");

            block = new MaterialPropertyBlock();
            steady = Cache(steadyLights, "steady light");
            bands = new CachedLight[chase.Length][];
            for (var i = 0; i < chase.Length; i++)
                bands[i] = Cache(chase[i].lamps, "chase lamp");

            restPosition = transform.localPosition;
        }

        private void OnEnable()
        {
            // Seeded from where it spawned, not UnityEngine.Random, so gameplay's random stream is untouched.
            var p = body.transform.position;
            var u1 = PlacedHash.Unit(p, new Vector3(12.9898f, 78.233f, 37.719f));
            var u2 = PlacedHash.Unit(p, new Vector3(39.3468f, 11.135f, 83.155f));
            var u3 = PlacedHash.Unit(p, new Vector3(73.156f, 52.235f, 9.151f));
            driftSeed = u1 * 1000f;
            var a = Mathf.Sqrt(1f - u1);
            var b = Mathf.Sqrt(u1);
            var t2 = 2f * Mathf.PI * u2;
            var t3 = 2f * Mathf.PI * u3;
            restRotation = new Quaternion(a * Mathf.Sin(t2), a * Mathf.Cos(t2), b * Mathf.Sin(t3), b * Mathf.Cos(t3));
            transform.localPosition = restPosition;
            transform.localRotation = restRotation;
            var dark = !body.Lit.activeSelf;
            power = dark ? 0f : 1f;
            PaintLights(dark);
        }

        private void Update()
        {
            var t = driftSeed + Time.time / feel.driftSeconds;
            transform.localPosition = restPosition + feel.driftUnits * Wander(t, 0f);
            transform.localRotation = restRotation * Quaternion.Euler(feel.driftDegrees * Wander(t, 50f));

            var dark = !body.Lit.activeSelf;
            power = Mathf.MoveTowards(power, dark ? 0f : 1f, Time.deltaTime / feel.coastSeconds);
            foreach (var spin in spins)
                spin.part.Rotate(spin.localAxis, 360f / feel.secondsPerTurn * spin.rate * power * Time.deltaTime, Space.Self);
            PaintLights(dark);
        }

        private void PaintLights(bool dark)
        {
            for (var i = 0; i < steady.Length; i++)
                Paint(steady[i], dark, 1f);

            var p = Mathf.Repeat(Time.time / feel.chaseSecondsPerSweep, 1f) * (chase.Length + 1);
            for (var i = 0; i < bands.Length; i++)
            {
                var factor = 1f + feel.chaseBoost * Mathf.Max(0f, 1f - Mathf.Abs(i + .5f - p));
                var lamps = bands[i];
                for (var j = 0; j < lamps.Length; j++)
                    Paint(lamps[j], dark, factor);
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
                renderer.GetPropertyBlock(block);
                var emission = block.HasVector(EmissionColorId)
                    ? block.GetVector(EmissionColorId)
                    : (Vector4)material.GetColor(EmissionColorId).linear;
                cached[i] = new CachedLight
                {
                    renderer = renderer,
                    linearEmission = emission,
                    paper = material.GetColor(PaperColorId)
                };
            }

            return cached;
        }

        private void Paint(CachedLight light, bool dark, float emissionFactor)
        {
            light.renderer.GetPropertyBlock(block);
            block.SetVector(EmissionColorId, dark ? Vector4.zero : light.linearEmission * emissionFactor);
            block.SetColor(PaperColorId, dark ? feel.darkColour : light.paper);
            light.renderer.SetPropertyBlock(block);
        }
    }
}
