using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ships.Visuals
{
    public sealed class EngineFlame : MonoBehaviour
    {
        private static readonly int FrameId = Shader.PropertyToID("_Frame");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static Mesh unitQuad;

        [SerializeField] private EngineFlameFeel feel;

        [Tooltip("Length of a full flame's sheet cell, in this nozzle's local units.")]
        [SerializeField] private float length;

        private MeshRenderer sheet;
        private Transform sheetTransform;
        private MaterialPropertyBlock block;
        private float current;
        private float frameTime;
        private int frame;

        public Color Colour { get; set; }
        public float CurrentLength => current;

        private void Awake()
        {
            if (!feel)
                throw new InvalidOperationException($"EngineFlame on '{name}' needs its feel profile.");
            if (!feel.Material)
                throw new InvalidOperationException($"EngineFlame on '{name}' needs its flame material.");
            if (feel.Frames < 1)
                throw new InvalidOperationException($"EngineFlame on '{name}' needs frames of at least 1.");
            if (feel.FramesPerSecond <= 0f)
                throw new InvalidOperationException($"EngineFlame on '{name}' needs a positive framesPerSecond.");
            if (feel.IgniteTime <= 0f)
                throw new InvalidOperationException($"EngineFlame on '{name}' needs a positive igniteTime.");
            if (feel.CutoffTime <= 0f)
                throw new InvalidOperationException($"EngineFlame on '{name}' needs a positive cutoffTime.");
            if (length <= 0f)
                throw new InvalidOperationException($"EngineFlame on '{name}' needs a positive length.");

            block = new MaterialPropertyBlock();
            if (!unitQuad) unitQuad = CreateQuad();

            var quad = new GameObject("Engine flame") { layer = gameObject.layer };
            quad.AddComponent<MeshFilter>().sharedMesh = unitQuad;
            sheet = quad.AddComponent<MeshRenderer>();
            sheet.sharedMaterial = feel.Material;
            sheet.shadowCastingMode = ShadowCastingMode.Off;
            sheet.receiveShadows = false;
            sheet.enabled = false;

            sheetTransform = quad.transform;
            sheetTransform.SetParent(transform, false);
            sheetTransform.localRotation = Quaternion.identity;

            current = feel.PilotLength;
            frame = UnityEngine.Random.Range(0, feel.Frames);
        }

        public void Tick(bool thrusting, float deltaTime)
        {
            var target = thrusting ? 1f : feel.PilotLength;
            if (current < target)
                current = Mathf.Min(target, current + deltaTime / feel.IgniteTime);
            else
                current = Mathf.Max(target, current - deltaTime / feel.CutoffTime);

            var step = 1f / feel.FramesPerSecond;
            frameTime += deltaTime;
            while (frameTime >= step)
            {
                frameTime -= step;
                frame = UnityEngine.Random.Range(0, feel.Frames);
            }

            sheet.enabled = true;
            var drawn = length * current;
            sheetTransform.localScale = new Vector3(length * feel.CellAspect * Mathf.Sqrt(current), drawn, 1f);
            // The nozzle sits on the sheet's top edge.
            sheetTransform.localPosition = new Vector3(0f, -drawn / 2f, 0f);
            block.SetFloat(FrameId, frame);
            block.SetColor(BaseColorId, Colour);
            sheet.SetPropertyBlock(block);
        }

        public static Color ColourOf(Material flame)
        {
            if (!flame)
                throw new ArgumentNullException(nameof(flame));

            var emission = flame.GetColor(EmissionColorId);
            var largest = Mathf.Max(emission.r, Mathf.Max(emission.g, emission.b));
            if (largest > 1f)
                return new Color(emission.r / largest, emission.g / largest, emission.b / largest, 1f);
            emission.a = 1f;
            return emission;
        }

        private static Mesh CreateQuad()
        {
            var mesh = new Mesh { name = "Engine flame quad" };
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
