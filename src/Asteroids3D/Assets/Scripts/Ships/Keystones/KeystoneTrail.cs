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

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Kit whose active state sheds the trail.")]
        [SerializeField] private Keystone keystone;

        [Tooltip("Hull silhouette in the ship's local plane, nose +Y. Baked from the hull meshes.")]
        [SerializeField] private Vector2[] outline = Array.Empty<Vector2>();

        [Tooltip("Emitting layers. A bow layer sits ahead of the body layer.")]
        [SerializeField] private HullShed[] sheds = Array.Empty<HullShed>();

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

        private void Awake()
        {
            Validate();
            block = new MaterialPropertyBlock();
            CacheOverlays();
            BuildRegions();
            accumulators = new float[sheds.Length];
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
            FadeOverlays(active);
        }

        private void Begin(Vector3 position)
        {
            for (var i = 0; i < accumulators.Length; i++) accumulators[i] = 0f;
            // A respawn can teleport the ship; the gap is not travel.
            lastPosition = position;
            var length = ShipLength;
            for (var i = 0; i < sheds.Length; i++)
                EmitCount(i, sheds[i].launchBurst, length);
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
                EmitCount(i, whole, length);
            }
        }

        private void EmitCount(int shedIndex, int count, float shipLength)
        {
            if (count <= 0) return;
            var indices = regions[shedIndex];
            if (indices.Length == 0) return;

            var shed = sheds[shedIndex];
            var system = shed.system;
            if (!system.isPlaying) system.Play(false);
            var shipVelocity = keystone.Body.linearVelocity;
            for (var n = 0; n < count; n++)
                EmitOne(shed, system, indices, shipLength, shipVelocity);
        }

        private void EmitOne(HullShed shed, ParticleSystem system, int[] indices, float shipLength, Vector3 shipVelocity)
        {
            var index = indices[UnityEngine.Random.Range(0, indices.Length)];
            var point = outline[index];
            var normal = HullOutline.Normal(outline, index);
            var outward = transform.TransformDirection(new Vector3(normal.x, normal.y, 0f));
            var parameters = new ParticleSystem.EmitParams
            {
                position = transform.TransformPoint(new Vector3(point.x, point.y, 0f)),
                velocity = shipVelocity * shed.inherit + outward * (shed.kick * shipLength),
                startSize = shed.size * UnityEngine.Random.Range(1f - shed.sizeVariation, 1f) * shipLength,
                applyShapeToPosition = false
            };
            system.Emit(parameters, 1);
        }

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
