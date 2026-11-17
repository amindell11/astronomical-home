using System;
using Substrate.Presentation;
using UnityEngine;

namespace Ships.Keystones
{
    /// <summary>
    /// Sheds hull streaks while its keystone is active, and fades held overlays with that state.
    /// Rides the kit prefab, so the ship's presentation policy reaches it with the rest of the ship.
    /// </summary>
    public sealed class KeystoneTrail : MonoBehaviour, IPresentationPart
    {
        private const float OverlayRiseSeconds = 0.06f;
        private const float OverlayFallSeconds = 0.1f;
        // The lunge shoves launch streaks hard off the hull, keeping less of the ship's speed.
        private const float BurstKick = 2.2f;
        private const float BurstInherit = 0.6f;
        private const float MaxSpeedStretch = 3f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Kit whose active state sheds the trail.")]
        [SerializeField] private Keystone keystone;

        [Tooltip("Hull silhouette in the ship's local plane, nose +Y. Baked from the hull meshes.")]
        [SerializeField] private Vector2[] outline = Array.Empty<Vector2>();

        [Tooltip("Emitting layers. A bow layer sits ahead of the body layer.")]
        [SerializeField] private HullShed[] sheds = Array.Empty<HullShed>();

        [Tooltip("How each particle is shaped and lit over its life.")]
        [SerializeField] private TrailPose pose;

        [Tooltip("Meshes shown while the keystone is active, such as a pressure arc. Empty when the trail has none.")]
        [SerializeField] private Renderer[] heldOverlays = Array.Empty<Renderer>();

        private int[][] regions = Array.Empty<int[]>();
        private float[] accumulators = Array.Empty<float>();
        private float noseMin;
        private float noseMax;
        private Vector3 lastPosition;
        private bool wasActive;
        private float overlayAlpha;
        private Color[] overlayColors = Array.Empty<Color>();
        private MaterialPropertyBlock block;
        private ParticleSystem.Particle[] particles = Array.Empty<ParticleSystem.Particle>();

        private void Awake()
        {
            Validate();
            block = new MaterialPropertyBlock();
            CacheOverlays();
            BuildRegions();
            accumulators = new float[sheds.Length];
            var capacity = 0;
            foreach (var shed in sheds) capacity = Mathf.Max(capacity, shed.system.main.maxParticles);
            particles = new ParticleSystem.Particle[capacity];
            HideOverlays();
        }

        private void OnValidate() => Validate();

        private void OnEnable()
        {
            // Disabled frames are not travel.
            lastPosition = transform.position;
            if (overlayAlpha <= 0f) SetOverlaysEnabled(false);
        }

        private void OnDisable()
        {
            StopEmitting();
            HideOverlays();
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void LateUpdate()
        {
            var active = keystone.Active;
            var position = transform.position;
            if (active && !wasActive) Begin(position);
            wasActive = active;
            if (active) Shed(position);
            PoseAll();
            FadeOverlays(active);
        }

        private void Begin(Vector3 position)
        {
            for (var i = 0; i < accumulators.Length; i++) accumulators[i] = 0f;
            // A respawn can teleport the ship; the gap is not travel.
            lastPosition = position;
            var length = ShipLength;
            for (var i = 0; i < sheds.Length; i++)
                EmitCount(i, sheds[i].launchBurst, length, true);
        }

        private void Shed(Vector3 position)
        {
            var distance = Vector3.Distance(position, lastPosition);
            lastPosition = position;
            var length = ShipLength;
            if (!(length > 0f)) return;

            for (var i = 0; i < sheds.Length; i++)
            {
                accumulators[i] += distance / length * sheds[i].perShipLength;
                var whole = (int)accumulators[i];
                if (whole <= 0) continue;
                accumulators[i] -= whole;
                EmitCount(i, whole, length, false);
            }
        }

        private void EmitCount(int shedIndex, int count, float shipLength, bool burst)
        {
            if (count <= 0) return;
            var indices = regions[shedIndex];
            if (indices.Length == 0) return;

            var shed = sheds[shedIndex];
            var system = shed.system;
            if (!system.isPlaying) system.Play(false);
            var shipVelocity = keystone.Body.linearVelocity;
            for (var n = 0; n < count; n++)
                EmitOne(shed, system, indices, shipLength, shipVelocity, burst);
        }

        private void EmitOne(HullShed shed, ParticleSystem system, int[] indices, float shipLength, Vector3 shipVelocity, bool burst)
        {
            var index = indices[UnityEngine.Random.Range(0, indices.Length)];
            var point = outline[index];
            var normal = HullOutline.Normal(outline, index);
            var outward = transform.TransformDirection(new Vector3(normal.x, normal.y, 0f));
            var parameters = new ParticleSystem.EmitParams
            {
                position = transform.TransformPoint(new Vector3(point.x, point.y, 0f)),
                velocity = shipVelocity * (shed.inherit * (burst ? BurstInherit : 1f))
                           + outward * (shed.kick * (burst ? BurstKick : 1f) * UnityEngine.Random.Range(0.5f, 1.3f) * shipLength),
                applyShapeToPosition = false
            };
            system.Emit(parameters, 1);
        }

        private void PoseAll()
        {
            var length = ShipLength;
            if (!(length > 0f)) return;
            var shipVelocity = keystone.Body.linearVelocity;
            foreach (var shed in sheds)
            {
                var system = shed.system;
                var count = system.GetParticles(particles);
                if (count == 0) continue;
                for (var i = 0; i < count; i++) Pose(ref particles[i], shed, shipVelocity, length);
                system.SetParticles(particles, count);
            }
        }

        private void Pose(ref ParticleSystem.Particle p, HullShed shed, Vector3 shipVelocity, float shipLength)
        {
            var seed = p.randomSeed;
            var age = p.startLifetime - p.remainingLifetime;
            var u = Mathf.Clamp01(age / p.startLifetime);
            var scale = shed.size * shipLength * (1f - shed.sizeVariation * Hash(seed, 1));
            var slide = (Vector2)(p.velocity - shipVelocity);
            var stretch = 1f + pose.speedStretch * Mathf.Min(MaxSpeedStretch, slide.magnitude / shipLength / pose.cruise);
            var bright = 1f + pose.colorVariation * (Hash(seed, 2) * 1.2f - 0.6f);
            float width, height, alpha, flashU = 1f;
            switch (shed.kind)
            {
                case ShedKind.Streak:
                {
                    var snap = Mathf.Min(1f, age / pose.snapSeconds);
                    var pop = Back(snap, pose.overshoot);
                    var grow = 1f + (pose.sizeEnd - 1f) * u;
                    var collapsing = pose.collapse > 0f ? Mathf.Max(0f, (u - (1f - pose.collapse)) / pose.collapse) : 0f;
                    var lengthJitter = 1f + pose.lengthVariation * (Hash(seed, 3) * 1.1f - 0.5f);
                    width = grow * pop * (1f - 0.9f * EaseIn(collapsing));
                    height = grow * pop * lengthJitter * stretch * (1f + (pose.stretch - 1f) * (1f - EaseOut(snap))) * (1f + 0.3f * collapsing);
                    alpha = 1f - EaseIn(u) * 0.5f;
                    flashU = age / (pose.snapSeconds * 1.5f);
                    bright *= 1f + pose.hotCore * Mathf.Max(0f, 1f - u / 0.4f) * 0.8f;
                    break;
                }
                case ShedKind.Line:
                    width = 1f;
                    height = stretch;
                    alpha = (1f - u) * 0.9f;
                    bright = 1f + pose.colorVariation * Hash(seed, 2) * 0.6f;
                    break;
                case ShedKind.Mote:
                    width = height = 1f;
                    alpha = Mathf.Min(1f, (1f - EaseIn(u)) * (0.55f + 0.45f * Mathf.Sin(age * 26f + Hash(seed, 4) * 6.28f)) * 1.6f);
                    bright += 0.3f;
                    break;
                default:
                    width = height = Mathf.Sin(Mathf.PI * u) * 1.15f;
                    alpha = 1f;
                    flashU = 0f;
                    bright = 1.5f;
                    break;
            }

            p.startSize3D = new Vector3(shed.cell.x * width * scale, shed.cell.y * height * scale, 1f);
            if (shed.kind is ShedKind.Streak or ShedKind.Line)
            {
                if (slide.sqrMagnitude > 0.0025f * shipLength * shipLength)
                    p.rotation3D = new Vector3(0f, 0f, Mathf.Atan2(slide.x, -slide.y) * Mathf.Rad2Deg);
            }
            else if (age <= Time.deltaTime)
                p.rotation3D = new Vector3(0f, 0f, (Hash(seed, 5) - 0.5f) * 46f);

            var flash = pose.flash * Mathf.Max(0f, 1f - flashU);
            var hot = Mathf.Max(0f, bright - 1f);
            var dim = Mathf.Min(1f, bright);
            p.startColor = new Color(
                Encode(pose.tint * dim * (1f - Mathf.Min(1f, flash * 0.6f + hot * 0.6f))),
                Encode(pose.tint * 0.35f * (1f - Mathf.Min(1f, flash + hot))),
                0f,
                Mathf.Min(1f, alpha * (1f + flash) * (0.6f + 0.4f * dim)));
        }

        private static float Hash(uint seed, int salt)
        {
            var h = seed ^ ((uint)salt * 0x9E3779B9u);
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }

        // Linear projects decode particle colour as sRGB; the shader reads R and G as plain amounts.
        private static float Encode(float amount) =>
            QualitySettings.activeColorSpace == ColorSpace.Linear ? Mathf.LinearToGammaSpace(amount) : amount;

        private static float EaseIn(float u) => u * u * u;

        private static float EaseOut(float u) => 1f - (1f - u) * (1f - u) * (1f - u);

        private static float Back(float u, float k) => 1f + (k + 1f) * Mathf.Pow(u - 1f, 3f) + k * Mathf.Pow(u - 1f, 2f);

        private float ShipLength => (noseMax - noseMin) * transform.lossyScale.y;

        private void BuildRegions()
        {
            var extent = HullOutline.Extent(outline);
            noseMin = extent.min;
            noseMax = extent.max;
            var localLength = noseMax - noseMin;
            var centre = (noseMin + noseMax) * 0.5f;
            regions = new int[sheds.Length][];
            for (var s = 0; s < sheds.Length; s++)
                regions[s] = Indices(sheds[s], centre, localLength);
        }

        private int[] Indices(HullShed shed, float centre, float localLength)
        {
            var matches = 0;
            for (var i = 0; i < outline.Length; i++)
                if (InRegion(shed, outline[i].y, centre, localLength)) matches++;

            var indices = new int[matches];
            var write = 0;
            for (var i = 0; i < outline.Length; i++)
                if (InRegion(shed, outline[i].y, centre, localLength)) indices[write++] = i;
            return indices;
        }

        private static bool InRegion(HullShed shed, float y, float centre, float localLength)
        {
            var along = (y - centre) / localLength;
            return shed.ahead ? along >= shed.regionY : along < shed.regionY;
        }

        private void FadeOverlays(bool active)
        {
            var seconds = active ? OverlayRiseSeconds : OverlayFallSeconds;
            overlayAlpha = Mathf.MoveTowards(overlayAlpha, active ? 1f : 0f, Time.deltaTime / seconds);
            PaintOverlays();
        }

        private void HideOverlays()
        {
            overlayAlpha = 0f;
            SetOverlaysEnabled(false);
        }

        private void PaintOverlays()
        {
            if (heldOverlays == null || block == null) return;
            var shown = overlayAlpha > 0f;
            for (var i = 0; i < heldOverlays.Length; i++)
            {
                var renderer = heldOverlays[i];
                if (!renderer) continue;
                renderer.enabled = shown;
                if (!shown) continue;
                var color = overlayColors[i];
                color.a = overlayAlpha;
                renderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                renderer.SetPropertyBlock(block);
            }
        }

        private void SetOverlaysEnabled(bool shown)
        {
            if (heldOverlays == null) return;
            for (var i = 0; i < heldOverlays.Length; i++)
                if (heldOverlays[i]) heldOverlays[i].enabled = shown;
        }

        private void CacheOverlays()
        {
            if (heldOverlays == null)
            {
                overlayColors = Array.Empty<Color>();
                return;
            }

            overlayColors = new Color[heldOverlays.Length];
            for (var i = 0; i < heldOverlays.Length; i++)
            {
                var renderer = heldOverlays[i];
                if (!renderer || !renderer.sharedMaterial) continue;
                var material = renderer.sharedMaterial;
                overlayColors[i] = material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : material.color;
            }
        }

        private void StopEmitting()
        {
            if (sheds == null) return;
            for (var i = 0; i < sheds.Length; i++)
            {
                var shed = sheds[i];
                if (shed == null || !shed.system) continue;
                shed.system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void Validate()
        {
            if (!keystone)
                throw new InvalidOperationException($"{nameof(KeystoneTrail)} on '{name}' needs its keystone.");
            if (outline == null || outline.Length < 3)
                throw new InvalidOperationException($"{nameof(KeystoneTrail)} on '{name}' needs an outline of at least 3 points.");
            var extent = HullOutline.Extent(outline);
            if (!(extent.max > extent.min))
                throw new InvalidOperationException($"{nameof(KeystoneTrail)} on '{name}' outline has no length along the nose.");
            if (!(pose.cruise > 0f) || !(pose.snapSeconds > 0f))
                throw new InvalidOperationException($"{nameof(KeystoneTrail)} on '{name}' pose needs a positive cruise and snap time.");
            if (sheds == null)
                throw new InvalidOperationException($"{nameof(KeystoneTrail)} on '{name}' needs its sheds.");
            for (var i = 0; i < sheds.Length; i++)
            {
                var shed = sheds[i];
                if (shed == null || !shed.system)
                    throw new InvalidOperationException($"{nameof(KeystoneTrail)} on '{name}' shed {i} needs a particle system.");
            }
        }
    }
}
