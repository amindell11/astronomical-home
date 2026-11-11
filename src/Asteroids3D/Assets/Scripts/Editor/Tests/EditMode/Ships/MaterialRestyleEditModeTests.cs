using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Ships.Presentation;
using UI.Hangar.Hologram;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Ships
{
    [Category("Ships")]
    public sealed class MaterialRestyleEditModeTests
    {
        private const string ShipProperties =
            "_Color (\"\", Color) = (1,1,1,1)\n" +
            "_Vector (\"\", Vector) = (0,0,0,0)\n" +
            "_Float (\"\", Float) = 0\n" +
            "_Range (\"\", Range(0,1)) = 0\n" +
            "_Int (\"\", Integer) = 0\n" +
            "_Tex (\"\", 2D) = \"white\" {}\n" +
            "_TexLocked (\"\", 2D) = \"white\" {}\n" +
            "_TexUnset (\"\", 2D) = \"black\" {}\n" +
            "_Ignored (\"\", Float) = 0\n";

        private const string CounterpartProperties =
            "[PerRendererData] _Color (\"\", Color) = (1,1,1,1)\n" +
            "[PerRendererData] _Vector (\"\", Vector) = (0,0,0,0)\n" +
            "[PerRendererData] _Float (\"\", Float) = 0\n" +
            "[PerRendererData] _Range (\"\", Range(0,1)) = 0\n" +
            "[PerRendererData] _Int (\"\", Integer) = 0\n" +
            "[PerRendererData] _Tex (\"\", 2D) = \"white\" {}\n" +
            "[PerRendererData][NoScaleOffset] _TexLocked (\"\", 2D) = \"white\" {}\n" +
            "[PerRendererData] _TexUnset (\"\", 2D) = \"black\" {}\n" +
            "_Ignored (\"\", Float) = 0\n";

        private readonly List<Object> owned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in owned)
                if (item) Object.DestroyImmediate(item);
            owned.Clear();
        }

        [Test]
        public void Carry_CopiesEachPropertyType_AndATexturesScaleOffset()
        {
            var ship = Own(new Material(Compile("RestyleShip", ShipProperties)));
            var counterpart = Compile("RestyleCounterpart", CounterpartProperties);
            var texture = Own(new Texture2D(2, 2));
            var locked = Own(new Texture2D(2, 2));
            var kept = Own(new Texture2D(2, 2));
            ship.SetColor("_Color", new Color(.2f, .4f, .6f, .8f));
            ship.SetVector("_Vector", new Vector4(1.5f, -2f, .25f, 4f));
            ship.SetFloat("_Float", .35f);
            ship.SetFloat("_Range", .7f);
            ship.SetInteger("_Int", 6);
            ship.SetTexture("_Tex", texture);
            ship.SetTextureScale("_Tex", new Vector2(2f, 3f));
            ship.SetTextureOffset("_Tex", new Vector2(.25f, .5f));
            ship.SetTexture("_TexLocked", locked);
            ship.SetTextureScale("_TexLocked", new Vector2(8f, 8f));
            ship.SetFloat("_Ignored", 9f);

            var block = new MaterialPropertyBlock();
            block.SetTexture("_TexUnset", kept);
            MaterialRestyle.Carry(ship, counterpart, block);

            // A block's color round-trips through linear space.
            Assert.That(Vector4.Distance(block.GetColor("_Color"), new Color(.2f, .4f, .6f, .8f)), Is.LessThan(1e-4f));
            Assert.That(block.GetVector("_Vector"), Is.EqualTo(new Vector4(1.5f, -2f, .25f, 4f)));
            Assert.That(block.GetFloat("_Float"), Is.EqualTo(.35f));
            Assert.That(block.GetFloat("_Range"), Is.EqualTo(.7f));
            Assert.That(block.GetInteger("_Int"), Is.EqualTo(6));
            Assert.That(block.GetTexture("_Tex"), Is.SameAs(texture));
            Assert.That(block.GetVector("_Tex_ST"), Is.EqualTo(new Vector4(2f, 3f, .25f, .5f)));
            Assert.That(block.GetTexture("_TexLocked"), Is.SameAs(locked));
            Assert.IsFalse(block.HasVector("_TexLocked_ST"), "a NoScaleOffset texture carries no _ST");
            Assert.That(block.GetTexture("_TexUnset"), Is.SameAs(kept), "an unset ship texture leaves the block's");
            Assert.IsFalse(block.HasFloat("_Ignored"), "only per-renderer properties carry");
        }

        [Test]
        public void Validate_Throws_ForACounterpartDrawnWithTheHullShader()
        {
            var hull = Shader.Find("Astronomical/Drawn/Surface");
            var restyle = With(new MaterialRestyle.Counterpart { drawn = hull, counterpart = Own(new Material(hull)) });
            var error = Assert.Throws<InvalidOperationException>(() => restyle.Validate("Probe"));
            Assert.That(error.Message, Does.Contain("Probe").And.Contain("_BaseColor"));
        }

        [Test]
        public void Validate_Passes_ForTheShippedProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<HangarHologramProfile>("Assets/Settings/UI/HangarHologram.asset");
            Assert.That(profile.Restyle.Counterparts, Is.Not.Empty);
            Assert.DoesNotThrow(() => profile.Restyle.Validate(profile.name));
        }

        private Shader Compile(string name, string properties) => Own(ShaderUtil.CreateShaderAsset(
            $"Shader \"Hidden/Tests/{name}\" {{ Properties {{\n{properties}}}\n" +
            "SubShader { Pass { HLSLPROGRAM\n#pragma vertex Vertex\n#pragma fragment Fragment\n" +
            "float4 Vertex(float4 position : POSITION) : SV_POSITION { return position; }\n" +
            "half4 Fragment() : SV_Target { return 0; }\nENDHLSL } } }", false));

        private T Own<T>(T item) where T : Object
        {
            owned.Add(item);
            return item;
        }

        private static MaterialRestyle With(MaterialRestyle.Counterpart row)
        {
            var restyle = new MaterialRestyle();
            typeof(MaterialRestyle).GetField("counterparts", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(restyle, new[] { row });
            return restyle;
        }
    }
}
