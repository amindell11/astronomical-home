from pathlib import Path
import shutil

slot = Path('D:/amind/git/agent-2')
source = slot / 'results/valis-crimson-retry/v02'
art = slot / 'art/ships/valis'
assets = slot / 'src/Asteroids3D/Assets'
(art / 'layers').mkdir(exist_ok=True)
(art / 'tools').mkdir(exist_ok=True)
(assets / 'Visuals/Ships/Valis/Paint').mkdir(exist_ok=True)
shutil.copy2(source / 'Valis-asymmetric-tuned.blend', art / 'Valis.blend')
shutil.copy2(source / 'selected-settings.json', art / 'paint-settings.json')
for name in ('Shadow', 'Light', 'Ink'):
    shutil.copy2(source / 'layers' / (name + '.png'), art / 'layers' / (name + '.png'))
    shutil.copy2(source / 'layers' / (name + '.png'), assets / 'Visuals/Ships/Valis/Paint' / (name + '.png'))
for name in ('Valis-Paint-Layers.ora', 'UV-guide.png'):
    shutil.copy2(source / name, art / name)

shader = assets / 'Visuals/Shaders/DrawnComparison/DrawnSurface.shader'
text = shader.read_text()
assert '_PaintLayers' not in text
text = text.replace('        _DebrisVisibility', '''        [Toggle(_PAINT_LAYERS)] _PaintLayers ("Separate Paint Layers", Float) = 0
        _PaintShadowMap ("Painted Shadow Mask", 2D) = "black" {}
        _PaintLightMap ("Painted Light Mask", 2D) = "black" {}
        _PaintInkMap ("Panel Ink Mask", 2D) = "black" {}
        _PaintShadowStrength ("Shadow Depth", Range(0,2)) = 1
        _PaintLightStrength ("Painted Light", Range(0,2)) = 1
        _PaintInkStrength ("Panel Ink", Range(0,2)) = 1
        _PaintLightColor ("Painted Light Color", Color) = (0.82,0.93,0.88,1)
        _PaintInkColor ("Panel Ink Color", Color) = (0.006,0.012,0.018,1)
        _DebrisVisibility''', 1)
text = text.replace('            half4 _OrangeGain;', '''            half4 _OrangeGain, _PaintLightColor, _PaintInkColor;
            half _PaintShadowStrength, _PaintLightStrength, _PaintInkStrength;''')
text = text.replace('            #pragma shader_feature_local _NORMALMAP', '            #pragma shader_feature_local _NORMALMAP\n            #pragma shader_feature_local _PAINT_LAYERS', 1)
text = text.replace('            TEXTURE2D(_BaseMap);', '''            TEXTURE2D(_PaintShadowMap); SAMPLER(sampler_PaintShadowMap);
            TEXTURE2D(_PaintLightMap); SAMPLER(sampler_PaintLightMap);
            TEXTURE2D(_PaintInkMap); SAMPLER(sampler_PaintInkMap);
            TEXTURE2D(_BaseMap);''', 1)
text = text.replace('                albedo *= 1 - marks', '''                #if defined(_PAINT_LAYERS)
                    half shadow = SAMPLE_TEXTURE2D(_PaintShadowMap, sampler_PaintShadowMap, input.uv).r * _PaintShadowStrength;
                    half paintedLight = SAMPLE_TEXTURE2D(_PaintLightMap, sampler_PaintLightMap, input.uv).r * _PaintLightStrength;
                    half ink = SAMPLE_TEXTURE2D(_PaintInkMap, sampler_PaintInkMap, input.uv).r * _PaintInkStrength;
                    albedo = _PaperColor.rgb * (1 - shadow);
                    albedo = lerp(albedo, _PaintLightColor.rgb, saturate(paintedLight));
                    albedo = lerp(albedo, _PaintInkColor.rgb, saturate(ink));
                #endif
                albedo *= 1 - marks''', 1)
shader.write_text(text)

exporter = '''import argparse, hashlib, json, sys
from pathlib import Path
import bpy

p = argparse.ArgumentParser(description='Export evaluated Valis surfaces and authored UVs for the Unity paint updater.')
p.add_argument('--source', required=True, type=Path)
p.add_argument('--output', required=True, type=Path)
a = p.parse_args(sys.argv[sys.argv.index('--') + 1:])
bpy.ops.wm.open_mainfile(filepath=str(a.source.resolve()))
objects = [o for o in bpy.context.scene.objects if o.type == 'MESH']
geometry = {o.name: dict(vertices=[list(o.matrix_world @ v.co) for v in o.data.vertices], faces=[list(f.vertices) for f in o.data.polygons], modifiers=[[m.name, m.type] for m in o.modifiers]) for o in objects}
fingerprint = hashlib.sha256(json.dumps(geometry, sort_keys=True, separators=(',', ':')).encode()).hexdigest()
expected = json.loads((a.source.parent / 'mesh-approval.json').read_text())
assert fingerprint == 'c713fb3e410c5c6b66c74ad0e5d5b4885984cfe709b026866cdc6e6ae8d73981'
parts = []
deps = bpy.context.evaluated_depsgraph_get()
for obj in objects:
    evaluated = obj.evaluated_get(deps)
    mesh = evaluated.to_mesh()
    mesh.calc_loop_triangles()
    uv_layer = mesh.uv_layers['ValisPaintUV'].data
    vertices, uv = [], []
    for triangle in mesh.loop_triangles:
        for vertex, loop in zip(triangle.vertices, triangle.loops):
            position = obj.matrix_world @ mesh.vertices[vertex].co
            vertices.append(dict(x=position.x, y=-position.y, z=-position.z))
            coord = uv_layer[loop].uv
            uv.append(dict(x=coord.x, y=coord.y))
    parts.append(dict(name=obj.name, material=obj.data.materials[0].name, vertices=vertices, uv=uv))
    evaluated.to_mesh_clear()
a.output.parent.mkdir(parents=True, exist_ok=True)
a.output.write_text(json.dumps(dict(parts=parts, geometry_sha256=fingerprint), separators=(',', ':')))
print('EXPORTED_VALIS_PAINT ' + str(a.output.resolve()))
'''
(art / 'tools/export_paint_uv.py').write_text(exporter)

tests = assets / 'Scripts/Editor/Tests/EditMode/Rendering/Illustrated/ValisPrefabEditModeTests.cs'
text = tests.read_text().replace('                Assert.That(material.GetFloat("_TextureStrength"), Is.Zero);', '''                Assert.That(material.IsKeywordEnabled("_PAINT_LAYERS"), Is.True);
                foreach (var layer in new[] { "Shadow", "Light", "Ink" })
                    Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_Paint" + layer + "Map")),
                        Is.EqualTo("Assets/Visuals/Ships/Valis/Paint/" + layer + ".png"));
                Assert.That(material.GetFloat("_PaintShadowStrength"), Is.EqualTo(1.02f));
                Assert.That(material.GetFloat("_PaintLightStrength"), Is.EqualTo(.2f));
                Assert.That(material.GetFloat("_PaintInkStrength"), Is.EqualTo(1.5f));
                Assert.That(material.GetColor("_EmissionColor").maxColorComponent,
                    material.name == "Lavender" ? Is.GreaterThan(1f) : Is.Zero);''')
tests.write_text(text)
print('Prepared Valis source, independent paint assets and illustrated shader variant')
