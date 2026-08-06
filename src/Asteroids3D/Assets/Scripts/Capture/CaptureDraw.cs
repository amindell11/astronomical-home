using System;
using System.Collections.Generic;
using Game.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Capture
{
    /// <summary>Immediate-mode diagnostic drawing for captured frames, in GamePlane plane-space. Primitives exist only for the frame they are drawn in — anything not redrawn next capture is invisible. Rendered with LineRenderer/TextMesh because editor Gizmos/Handles never appear in offscreen camera renders.</summary>
    public sealed class CaptureDraw : IDiagnosticCanvas
    {
        private const float LiftAbovePlane = 3f;
        private const int RingSegments = 48;
        private const int LabelFontSize = 64;

        private readonly Transform root;
        private readonly Camera billboardCamera;
        private readonly Material lineMaterial;
        private readonly Font labelFont;
        private readonly List<LineRenderer> lines = new();
        private readonly List<TextMesh> labels = new();
        private readonly List<MeshRenderer> labelRenderers = new();
        private int lineCursor;
        private int labelCursor;

        public float LineWidth { get; set; } = 0.28f;

        internal CaptureDraw(Transform parent, Camera billboardCamera)
        {
            this.billboardCamera = billboardCamera;
            root = new GameObject("[Capture] Draw").transform;
            root.SetParent(parent, false);
            lineMaterial = CreateLineMaterial();
            labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!labelFont)
                throw new InvalidOperationException("[Capture] built-in LegacyRuntime.ttf font not found — labels cannot render");
        }

        public void Line(Vector2 a, Vector2 b, Color color)
        {
            var line = NextLine(2, color);
            line.SetPosition(0, Lift(a));
            line.SetPosition(1, Lift(b));
        }

        /// <summary>Arrow from origin along v, arrowhead at origin + v.</summary>
        public void Vector(Vector2 origin, Vector2 v, Color color)
        {
            if (v.sqrMagnitude < 1e-8f) return;
            var tip = origin + v;
            Line(origin, tip, color);

            var back = -v.normalized;
            var side = new Vector2(-back.y, back.x);
            var headLength = Mathf.Clamp(0.25f * v.magnitude, 0.5f, 2f);
            var head = NextLine(3, color);
            head.SetPosition(0, Lift(tip + headLength * (back + 0.6f * side)));
            head.SetPosition(1, Lift(tip));
            head.SetPosition(2, Lift(tip + headLength * (back - 0.6f * side)));
        }

        public void Ring(Vector2 center, float radius, Color color)
        {
            var ring = NextLine(RingSegments, color, loop: true);
            for (var i = 0; i < RingSegments; i++)
            {
                var angle = i * 2f * Mathf.PI / RingSegments;
                ring.SetPosition(i, Lift(center + radius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))));
            }
        }

        public void Rect(Vector2 center, Vector2 size, Color color)
        {
            var half = size * 0.5f;
            var rect = NextLine(4, color, loop: true);
            rect.SetPosition(0, Lift(center + new Vector2(-half.x, -half.y)));
            rect.SetPosition(1, Lift(center + new Vector2(half.x, -half.y)));
            rect.SetPosition(2, Lift(center + new Vector2(half.x, half.y)));
            rect.SetPosition(3, Lift(center + new Vector2(-half.x, half.y)));
        }

        public void Arc(Vector2 center, float radius, Vector2 fromDir, float sweepRad, Color color)
        {
            if (fromDir.sqrMagnitude < 1e-8f) return;
            var segments = Mathf.Max(2, Mathf.CeilToInt(RingSegments * Mathf.Abs(sweepRad) / (2f * Mathf.PI)));
            var startAngle = Mathf.Atan2(fromDir.y, fromDir.x);
            var arc = NextLine(segments + 1, color);
            for (var i = 0; i <= segments; i++)
            {
                var angle = startAngle + sweepRad * i / segments;
                arc.SetPosition(i, Lift(center + radius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle))));
            }
        }

        /// <summary>Short streak of the given length trailing behind head, opposite dir (normalized internally).</summary>
        public void Trail(Vector2 head, Vector2 dir, float length, Color color)
        {
            if (dir.sqrMagnitude < 1e-8f) return;
            Line(head - dir.normalized * length, head, color);
        }

        public void Label(Vector2 pos, string text, Color color, float size = 4f)
        {
            if (labelCursor == labels.Count) CreateLabel();
            var label = labels[labelCursor];
            labelRenderers[labelCursor].enabled = true;
            labelCursor++;
            label.text = text;
            label.color = color;
            label.characterSize = size * 10f / LabelFontSize;
            label.transform.position = Lift(pos);
            label.transform.rotation = billboardCamera.transform.rotation;
        }

        internal void BeginFrame()
        {
            lineCursor = 0;
            labelCursor = 0;
        }

        internal void DisableAll()
        {
            BeginFrame();
            DisableUnused();
        }

        internal void DisableUnused()
        {
            for (var i = lineCursor; i < lines.Count; i++) lines[i].enabled = false;
            for (var i = labelCursor; i < labelRenderers.Count; i++) labelRenderers[i].enabled = false;
        }

        /// <summary>Overlay renderers stay force-hidden except inside the capture render submit, so diagnostics never leak into the main camera or scene view.</summary>
        internal void SetVisible(bool visible)
        {
            foreach (var line in lines) line.forceRenderingOff = !visible;
            foreach (var labelRenderer in labelRenderers) labelRenderer.forceRenderingOff = !visible;
        }

        internal void Dispose()
        {
            if (lineMaterial) UnityEngine.Object.DestroyImmediate(lineMaterial);
        }

        private static Vector3 Lift(Vector2 plane) =>
            GamePlane.PlanePointToWorld(plane) + GamePlane.Rotation * Vector3.forward * LiftAbovePlane;

        private LineRenderer NextLine(int positions, Color color, bool loop = false)
        {
            var line = lineCursor < lines.Count ? lines[lineCursor] : CreateLine();
            lineCursor++;
            line.enabled = true;
            line.loop = loop;
            line.positionCount = positions;
            line.widthMultiplier = LineWidth;
            line.startColor = line.endColor = color;
            return line;
        }

        private LineRenderer CreateLine()
        {
            var go = new GameObject($"line{lines.Count}");
            go.transform.SetParent(root, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = lineMaterial;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.forceRenderingOff = true;
            lines.Add(line);
            return line;
        }

        private void CreateLabel()
        {
            var go = new GameObject($"label{labels.Count}");
            go.transform.SetParent(root, false);
            var label = go.AddComponent<TextMesh>();
            label.font = labelFont;
            label.fontSize = LabelFontSize;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = labelFont.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.forceRenderingOff = true;
            labels.Add(label);
            labelRenderers.Add(renderer);
        }

        // Diagnostics must never be occluded by scene geometry: depth test always passes, no depth writes.
        private static Material CreateLineMaterial()
        {
            var shader = Shader.Find("Hidden/Internal-Colored");
            if (!shader) shader = Shader.Find("Sprites/Default");
            if (!shader) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader)
                throw new InvalidOperationException("[Capture] no overlay line shader found");
            var material = new Material(shader) { hideFlags = HideFlags.DontSave };
            material.SetInt("_ZTest", (int)CompareFunction.Always);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = (int)RenderQueue.Overlay;
            return material;
        }
    }
}
