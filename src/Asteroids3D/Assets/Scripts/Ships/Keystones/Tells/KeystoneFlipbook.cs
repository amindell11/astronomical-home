using System;
using Substrate;
using Substrate.Presentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ships.Keystones.Tells
{
    /// <summary>
    /// Plays a kit's baked flipbook sheets, each layer from its own start: the kit's press, its
    /// going live, or its hit. The drawing and the baked timing live in the sheets; the playback
    /// knobs live in the feel profile.
    /// Design: https://github.com/amindell11/astronomical-home/issues/1574
    /// </summary>
    public sealed class KeystoneFlipbook : MonoBehaviour, IPresentationPart
    {
        private static readonly int FrameId = Shader.PropertyToID("_Frame");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static Mesh unitQuad;

        [SerializeField] private Keystone keystone;

        [Tooltip("World units from nose to tail. Sheet sizes are in ship lengths.")]
        [SerializeField] private float shipLength;

        [SerializeField] private KeystoneFlipbookFeel feel;

        private Renderer[] renderers;
        private Transform[] sheetTransforms;
        private MaterialPropertyBlock block;
        private int seenActivations;
        private bool seenActive;
        private int seenHits;
        private LayerPlay[] play;

        private struct LayerPlay
        {
            public bool playing;
            public float startedAt;
            public bool liveEnded;
            public float liveEndedAt;
            public Vector3 position;
            public Quaternion rotation;
        }

        private void Awake()
        {
            if (!keystone)
                throw new InvalidOperationException($"KeystoneFlipbook on '{name}' needs its keystone.");
            if (shipLength <= 0f)
                throw new InvalidOperationException($"KeystoneFlipbook on '{name}' needs a positive shipLength.");
            if (!feel)
                throw new InvalidOperationException($"KeystoneFlipbook on '{name}' needs its feel profile.");
            var layers = feel.Layers;
            if (layers == null)
                throw new InvalidOperationException($"KeystoneFlipbook on '{name}' needs its layers.");

            for (var i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (!layer.material)
                    throw new InvalidOperationException($"KeystoneFlipbook on '{name}' layer {i} needs a material.");
                if (layer.columns < 1)
                    throw new InvalidOperationException(
                        $"KeystoneFlipbook on '{name}' layer {i} needs columns of at least 1.");
                if (layer.rows < 1)
                    throw new InvalidOperationException(
                        $"KeystoneFlipbook on '{name}' layer {i} needs rows of at least 1.");
                var cells = layer.columns * layer.rows;
                if (layer.frames < 1 || layer.frames > cells)
                    throw new InvalidOperationException(
                        $"KeystoneFlipbook on '{name}' layer {i} needs frames from 1 to {cells}.");
            }

            block = new MaterialPropertyBlock();
            BuildSheets();
            play = new LayerPlay[layers.Length];
            seenActivations = keystone.Activations;
            seenActive = keystone.Active;
            seenHits = keystone.Hits;
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void OnEnable() => HideSheets();

        private void OnDisable()
        {
            if (play != null)
            {
                for (var i = 0; i < play.Length; i++)
                    play[i].playing = false;
            }

            HideSheets();
        }

        private void OnDestroy()
        {
            if (sheetTransforms == null) return;
            // Launch sheets are left unparented, so the part destroys them.
            for (var i = 0; i < sheetTransforms.Length; i++)
            {
                var sheetTransform = sheetTransforms[i];
                if (!sheetTransform || sheetTransform.parent) continue;
                Destroy(sheetTransform.gameObject);
            }
        }

        private void LateUpdate()
        {
            var press = keystone.Activations > seenActivations;
            seenActivations = keystone.Activations;
            var live = keystone.Active && !seenActive;
            seenActive = keystone.Active;
            var hit = keystone.Hits > seenHits;
            seenHits = keystone.Hits;

            var layers = feel.Layers;
            var pose = FlatPose();
            var anyPlaying = false;
            for (var i = 0; i < layers.Length; i++)
            {
                var startsOn = layers[i].startsOn;
                var started = false;
                if (startsOn == KeystoneFlipbookStart.Press) started = press;
                else if (startsOn == KeystoneFlipbookStart.Live) started = live;
                else if (startsOn == KeystoneFlipbookStart.Hit) started = hit;
                if (started)
                {
                    play[i].playing = true;
                    play[i].startedAt = Time.time;
                    play[i].liveEnded = false;
                    play[i].position = startsOn == KeystoneFlipbookStart.Hit
                        ? keystone.LastHitPoint
                        : transform.position;
                    play[i].rotation = pose;
                }

                if (play[i].playing) anyPlaying = true;
            }

            if (!anyPlaying)
            {
                HideSheets();
                return;
            }

            var edgeOn = 1f - Mathf.Abs(Vector3.Dot(transform.forward, GamePlane.Normal));
            var banked = Mathf.Max(0.1f, 1f - edgeOn * feel.BankNarrowing);
            for (var i = 0; i < layers.Length; i++)
            {
                if (!play[i].playing)
                {
                    renderers[i].enabled = false;
                    continue;
                }

                var layer = layers[i];
                var elapsed = (Time.time - play[i].startedAt) * feel.Speed;
                if (layer.endsWithLive && !keystone.Active && !play[i].liveEnded)
                {
                    play[i].liveEnded = true;
                    play[i].liveEndedAt = Time.time;
                }

                var liveFade = 1f;
                if (play[i].liveEnded)
                    liveFade = layer.fadeOut > 0f
                        ? Mathf.Clamp01(1f - (Time.time - play[i].liveEndedAt) * feel.Speed / layer.fadeOut)
                        : 0f;

                var frame = Mathf.FloorToInt(elapsed * layer.speed * feel.FramesPerSecond);
                if (frame >= layer.frames || liveFade <= 0f)
                    play[i].playing = false;

                var visible = layer.show && frame < layer.frames && liveFade > 0f
                    && (layer.endsAt <= 0f || elapsed < layer.endsAt);
                renderers[i].enabled = visible;

                var k = feel.Scale * layer.size;
                var offset = (layer.anchorAhead + (layer.centreAhead - layer.anchorAhead) * k) * shipLength;
                var sheetTransform = sheetTransforms[i];
                if (layer.staysAtLaunch)
                    sheetTransform.SetPositionAndRotation(
                        play[i].position + play[i].rotation * new Vector3(0f, offset, layer.depth),
                        play[i].rotation);
                else
                    sheetTransform.SetPositionAndRotation(
                        transform.position + pose * new Vector3(0f, offset, layer.depth), pose);
                var across = layer.staysAtLaunch ? 1f : banked;
                sheetTransform.localScale = new Vector3(
                    layer.width * shipLength * k * across, layer.length * shipLength * k, 1f);

                if (!visible) continue;
                var opacity = layer.opacity;
                if (layer.endsAt > 0f && layer.fadeOut > 0f)
                    opacity *= Mathf.Clamp01((layer.endsAt - elapsed) / layer.fadeOut);
                if (play[i].liveEnded)
                    opacity *= liveFade;
                block.SetFloat(FrameId, frame);
                block.SetFloat(OpacityId, opacity);
                renderers[i].SetPropertyBlock(block);
            }
        }

        // The ship banks by rolling; the sheets stay flat on the game plane along its heading.
        private Quaternion FlatPose() =>
            GamePlane.PlanePose(GamePlane.Normal, Vector3.ProjectOnPlane(transform.up, GamePlane.Normal));

        private void BuildSheets()
        {
            if (!unitQuad) unitQuad = CreateQuad();
            var layers = feel.Layers;
            renderers = new Renderer[layers.Length];
            sheetTransforms = new Transform[layers.Length];
            for (var i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                var quad = new GameObject("Flipbook sheet");
                quad.AddComponent<MeshFilter>().sharedMesh = unitQuad;
                var renderer = quad.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = layer.material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false;

                var sheetTransform = quad.transform;
                if (!layer.staysAtLaunch)
                {
                    sheetTransform.SetParent(transform, false);
                    sheetTransform.localRotation = Quaternion.identity;
                }

                renderers[i] = renderer;
                sheetTransforms[i] = sheetTransform;
            }
        }

        // Presentation enables child renderers; a sheet draws only while its frame plays.
        private void HideSheets()
        {
            if (renderers == null) return;
            for (var i = 0; i < renderers.Length; i++)
                if (renderers[i]) renderers[i].enabled = false;
        }

        private static Mesh CreateQuad()
        {
            var mesh = new Mesh { name = "Keystone flipbook quad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            mesh.hideFlags = HideFlags.HideAndDontSave;
            return mesh;
        }
    }
}
