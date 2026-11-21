using System;
using Substrate.Presentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ships.Keystones.Tells
{
    /// <summary>
    /// Plays a kit's baked flipbook sheets from the kit's activation. The drawing and the timing
    /// live in the sheets.
    /// Design: https://github.com/amindell11/astronomical-home/issues/1574
    /// </summary>
    public sealed class KeystoneFlipbook : MonoBehaviour, IPresentationPart
    {
        [Serializable]
        public struct Sheet
        {
            public Material material;
            public int columns;
            public int rows;
            public int frames;

            [Tooltip("Ship lengths across the ship.")]
            public float width;

            [Tooltip("Ship lengths along the ship.")]
            public float length;

            [Tooltip("How far the sheet's centre sits ahead of the ship's centre, in ship lengths. Negative is behind.")]
            public float centreAhead;

            [Tooltip("Local Z offset in world units.")]
            public float depth;

            [Tooltip("Left in the world where the kit fired, instead of following the ship.")]
            public bool staysAtLaunch;
        }

        private static readonly int FrameId = Shader.PropertyToID("_Frame");
        private static Mesh unitQuad;

        [SerializeField] private Keystone keystone;

        [Tooltip("World units from nose to tail. Sheet sizes are in ship lengths.")]
        [SerializeField] private float shipLength;

        [SerializeField] private float framesPerSecond = 60f;
        [SerializeField] private Sheet[] sheets;

        private Renderer[] renderers;
        private Transform[] sheetTransforms;
        private MaterialPropertyBlock block;
        private int seenActivations;
        private float startedAt;
        private bool playing;

        private void Awake()
        {
            if (!keystone)
                throw new InvalidOperationException($"KeystoneFlipbook on '{name}' needs its keystone.");
            if (shipLength <= 0f)
                throw new InvalidOperationException($"KeystoneFlipbook on '{name}' needs a positive shipLength.");
            if (sheets == null)
                throw new InvalidOperationException($"KeystoneFlipbook on '{name}' needs its sheets.");

            for (var i = 0; i < sheets.Length; i++)
            {
                var sheet = sheets[i];
                if (!sheet.material)
                    throw new InvalidOperationException($"KeystoneFlipbook on '{name}' sheet {i} needs a material.");
                if (sheet.columns < 1)
                    throw new InvalidOperationException($"KeystoneFlipbook on '{name}' sheet {i} needs columns of at least 1.");
                if (sheet.rows < 1)
                    throw new InvalidOperationException($"KeystoneFlipbook on '{name}' sheet {i} needs rows of at least 1.");
                var cells = sheet.columns * sheet.rows;
                if (sheet.frames < 1 || sheet.frames > cells)
                    throw new InvalidOperationException(
                        $"KeystoneFlipbook on '{name}' sheet {i} needs frames from 1 to {cells}.");
            }

            block = new MaterialPropertyBlock();
            BuildSheets();
            seenActivations = keystone.Activations;
        }

        public void ApplyPresentation(bool visible) => enabled = visible;

        private void OnEnable() => HideSheets();

        private void OnDisable()
        {
            playing = false;
            HideSheets();
        }

        private void OnDestroy()
        {
            if (sheetTransforms == null) return;
            for (var i = 0; i < sheetTransforms.Length; i++)
            {
                if (!sheets[i].staysAtLaunch || !sheetTransforms[i]) continue;
                Destroy(sheetTransforms[i].gameObject);
            }
        }

        private void LateUpdate()
        {
            var fired = keystone.Activations > seenActivations;
            seenActivations = keystone.Activations;
            if (fired)
            {
                startedAt = Time.time;
                playing = true;
                PlaceLaunchSheets();
            }

            if (!playing)
            {
                HideSheets();
                return;
            }

            var frame = Mathf.FloorToInt((Time.time - startedAt) * framesPerSecond);
            var any = false;
            for (var i = 0; i < sheets.Length; i++)
            {
                var show = frame < sheets[i].frames;
                renderers[i].enabled = show;
                if (!show) continue;
                any = true;
                block.SetFloat(FrameId, frame);
                renderers[i].SetPropertyBlock(block);
            }

            if (!any) playing = false;
        }

        private void BuildSheets()
        {
            if (!unitQuad) unitQuad = CreateQuad();
            renderers = new Renderer[sheets.Length];
            sheetTransforms = new Transform[sheets.Length];
            for (var i = 0; i < sheets.Length; i++)
            {
                var sheet = sheets[i];
                var quad = new GameObject("Flipbook sheet");
                quad.AddComponent<MeshFilter>().sharedMesh = unitQuad;
                var renderer = quad.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = sheet.material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false;

                var sheetTransform = quad.transform;
                if (!sheet.staysAtLaunch)
                {
                    sheetTransform.SetParent(transform, false);
                    sheetTransform.localPosition = new Vector3(0f, sheet.centreAhead * shipLength, sheet.depth);
                    sheetTransform.localRotation = Quaternion.identity;
                    sheetTransform.localScale = SheetScale(sheet);
                }

                renderers[i] = renderer;
                sheetTransforms[i] = sheetTransform;
            }
        }

        private void PlaceLaunchSheets()
        {
            for (var i = 0; i < sheets.Length; i++)
            {
                if (!sheets[i].staysAtLaunch) continue;
                var sheet = sheets[i];
                sheetTransforms[i].SetPositionAndRotation(
                    transform.TransformPoint(0f, sheet.centreAhead * shipLength, sheet.depth),
                    transform.rotation);
                sheetTransforms[i].localScale = SheetScale(sheet);
            }
        }

        private Vector3 SheetScale(Sheet sheet) =>
            new(sheet.width * shipLength, sheet.length * shipLength, 1f);

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
