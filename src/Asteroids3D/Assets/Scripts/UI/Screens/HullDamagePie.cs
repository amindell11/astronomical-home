using System;
using System.Collections.Generic;
using System.Linq;
using Damage;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Screens
{
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class HullDamagePie : MaskableGraphic
    {
        private IReadOnlyList<(DamageKind Kind, float Amount)> slices = Array.Empty<(DamageKind, float)>();
        private float total;

        public void Initialize(IReadOnlyList<(DamageKind Kind, float Amount)> values)
        {
            slices = values;
            total = values.Sum(slice => slice.Amount);
            raycastTarget = false;
            SetVerticesDirty();
        }

        public static Color ColorFor(DamageKind kind) => kind switch
        {
            DamageKind.Laser => new Color(0.47f, 0.68f, 0.77f),
            DamageKind.Railgun => new Color(0.68f, 0.59f, 0.8f),
            DamageKind.Missile => new Color(0.88f, 0.61f, 0.4f),
            DamageKind.ConcussionWave => new Color(0.67f, 0.75f, 0.53f),
            DamageKind.Collision => new Color(0.65f, 0.67f, 0.69f),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        public static string LabelFor(DamageKind kind) => kind switch
        {
            DamageKind.Laser => "Lasers",
            DamageKind.Railgun => "Railguns",
            DamageKind.Missile => "Missiles",
            DamageKind.ConcussionWave => "Concussion",
            DamageKind.Collision => "Collisions",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            var center = rect.center;
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            var angle = Mathf.PI * 0.5f;
            foreach (var slice in slices)
            {
                var sweep = slice.Amount / total * Mathf.PI * 2f;
                var segments = Mathf.Max(1, Mathf.CeilToInt(sweep * 24f));
                var tint = ColorFor(slice.Kind);
                for (var i = 0; i < segments; i++)
                {
                    var a = angle - sweep * i / segments;
                    var b = angle - sweep * (i + 1) / segments;
                    var start = mesh.currentVertCount;
                    mesh.AddVert(center, tint, Vector2.zero);
                    mesh.AddVert(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, tint, Vector2.zero);
                    mesh.AddVert(center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, tint, Vector2.zero);
                    mesh.AddTriangle(start, start + 1, start + 2);
                }
                angle -= sweep;
            }
        }
    }
}
