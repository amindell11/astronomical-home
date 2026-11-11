using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ships.Presentation
{
    /// <summary>
    /// Swaps each renderer slot for the counterpart of its shader, and copies that counterpart's
    /// per-renderer properties from the ship material into the slot's property block.
    /// <c>_BaseColor</c>, <c>_ContourColor</c> and <c>_DamageFlash</c> may appear on a counterpart
    /// only as those carried ship values.
    /// </summary>
    [Serializable]
    public sealed class MaterialRestyle
    {
        private static readonly string[] CarriedFromHull = { "_BaseColor", "_ContourColor", "_DamageFlash" };

        [Serializable]
        public struct Counterpart
        {
            [Tooltip("Shader a ship material is drawn with.")]
            public Shader drawn;

            [Tooltip("Material worn in that shader's place. Per-renderer properties are copied from the ship material.")]
            public Material counterpart;
        }

        [SerializeField] private Counterpart[] counterparts = Array.Empty<Counterpart>();

        public IReadOnlyList<Counterpart> Counterparts => counterparts;

        public Material CounterpartOf(Material material)
        {
            if (!material) return null;
            foreach (var row in counterparts)
                if (row.drawn == material.shader) return row.counterpart;
            return null;
        }

        public void Apply(Renderer renderer)
        {
            var materials = renderer.sharedMaterials;
            var block = new MaterialPropertyBlock();
            for (var i = 0; i < materials.Length; i++)
            {
                var ship = materials[i];
                var counterpart = CounterpartOf(ship);
                if (!counterpart) continue;
                renderer.GetPropertyBlock(block, i);
                Carry(ship, counterpart.shader, block);
                renderer.SetPropertyBlock(block, i);
                materials[i] = counterpart;
            }
            renderer.sharedMaterials = materials;
        }

        internal static void Carry(Material ship, Shader counterpart, MaterialPropertyBlock block)
        {
            for (var i = 0; i < counterpart.GetPropertyCount(); i++)
            {
                var flags = counterpart.GetPropertyFlags(i);
                var id = counterpart.GetPropertyNameId(i);
                if ((flags & ShaderPropertyFlags.PerRendererData) == 0 || !ship.HasProperty(id)) continue;
                switch (counterpart.GetPropertyType(i))
                {
                    case ShaderPropertyType.Color:
                        block.SetColor(id, ship.GetColor(id));
                        break;
                    case ShaderPropertyType.Vector:
                        block.SetVector(id, ship.GetVector(id));
                        break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        block.SetFloat(id, ship.GetFloat(id));
                        break;
                    case ShaderPropertyType.Int:
                        block.SetInteger(id, ship.GetInteger(id));
                        break;
                    case ShaderPropertyType.Texture:
                        var texture = ship.GetTexture(id);
                        if (!texture) break;
                        block.SetTexture(id, texture);
                        if ((flags & ShaderPropertyFlags.NoScaleOffset) != 0) break;
                        var scale = ship.GetTextureScale(id);
                        var offset = ship.GetTextureOffset(id);
                        block.SetVector(counterpart.GetPropertyName(i) + "_ST",
                            new Vector4(scale.x, scale.y, offset.x, offset.y));
                        break;
                }
            }
        }

        public void Validate(string owner)
        {
            var seen = new HashSet<Shader>();
            foreach (var row in counterparts)
            {
                if (!row.drawn || !row.counterpart)
                    throw new InvalidOperationException(
                        $"{owner} restyle counterpart needs a drawn shader and a counterpart material.");
                if (!seen.Add(row.drawn))
                    throw new InvalidOperationException($"{owner} restyle maps '{row.drawn.name}' twice.");
                foreach (var property in CarriedFromHull)
                    RequireCarried(owner, row, property);
            }
        }

        private static void RequireCarried(string owner, Counterpart row, string property)
        {
            var shader = row.counterpart.shader;
            var onCounterpart = shader.FindPropertyIndex(property);
            if (onCounterpart >= 0 &&
                (shader.GetPropertyFlags(onCounterpart) & ShaderPropertyFlags.PerRendererData) == 0)
                throw new InvalidOperationException(
                    $"{owner} restyle: '{shader.name}' declares {property} without per-renderer data.");
            if (row.drawn.FindPropertyIndex(property) >= 0 && onCounterpart < 0)
                throw new InvalidOperationException(
                    $"{owner} restyle: '{row.drawn.name}' declares {property} and '{shader.name}' does not.");
        }
    }
}
