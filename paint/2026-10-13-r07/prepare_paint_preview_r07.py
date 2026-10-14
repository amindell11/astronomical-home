from pathlib import Path
import json,numpy as np
scratch=Path(__file__).parent;root=Path('D:/amind/git/agent-3/results/bulwark/paint/2026-10-13-r07')
data=json.loads((root/'paint-mesh.json').read_text());parts=[]
for part in data['parts']:
 record={'name':part['name'],'triangles':part['triangles'],'regions':part['regions'],'uv':part['uv']}
 for source,target in [('worldVertices','vertices'),('normalsWorld','normals'),('contourNormalsWorld','contourNormals')]:
  values=np.array(part[source]).reshape(-1,3);values[:,2]*=-1;record[target]=values.ravel().tolist()
 parts.append(record)
(root/'Bulwark-preview-mesh.json').write_text(json.dumps({'sourceSha256':data['sourceSha256'],'parts':parts},separators=(',',':')))
code=(scratch/'BulwarkLineupPreview.cs').read_text()
code=code.replace('BulwarkLineupPreview','BulwarkPaintPreview')
code=code.replace('C:/Users/amind/.codex/visualizations/2026/10/13/01a136e0-bb47-72b3-b885-0bb6c384f97a/paint/2026-10-13-r01','D:/amind/git/agent-3/results/bulwark/paint/2026-10-13-r07')
code=code.replace('var draws = ReadPrefab(ship.gameObject);','if (!new[] { "Crimson", "Vanguard", "Nightshade", "Valis" }.Contains(ship.name)) continue;\n            var draws = ReadPrefab(ship.gameObject);')
code=code.replace('var temporaryMaterials = new List<Material>();','var temporaryMaterials = new List<Material>();\n        var temporaryTextures = new List<Texture2D>();')
code=code.replace('ReadBulwark(temporaryMaterials)','ReadBulwark(temporaryMaterials, temporaryTextures)')
code=code.replace('Render(bulwark, "Bulwark-concept", true);','Render(bulwark, "Bulwark-concept", true);\n            Render(bulwark, "Bulwark-painted-large", true, 1400);')
code=code.replace('foreach (var material in temporaryMaterials) UnityEngine.Object.DestroyImmediate(material);','foreach (var material in temporaryMaterials) UnityEngine.Object.DestroyImmediate(material);\n            foreach (var texture in temporaryTextures) UnityEngine.Object.DestroyImmediate(texture);')
code=code.replace('Exact r08 evaluated geometry with temporary base regions; no production masks or prefab.','Locked r08 geometry and approved r02 UVs with separate grayscale paint masks; temporary materials, no gameplay prefab.')
start=code.index('    private static List<Draw> ReadBulwark(');end=code.index('    private static void Render(',start)
method='''    private static List<Draw> ReadBulwark(List<Material> temporaryMaterials, List<Texture2D> temporaryTextures)
    {
        var data = JsonConvert.DeserializeObject<PreviewMesh>(File.ReadAllText(MeshPath));
        if (data.sourceSha256 != "2790e4dfce08191ccf35443e46b2bacfecf70b630b2babdf6ca7c44cbbfe9e54")
            throw new InvalidOperationException("Bulwark UV checkpoint mismatch.");
        var palette = new[] { "#66505C", "#66505C", "#302E34", "#718591", "#66505C", "#999999", "#302E34" };
        var template = AssetDatabase.LoadAssetAtPath<Material>("Assets/Visuals/Ships/Valis/Materials/jade-iris/Graphite.mat");
        var shadow = LoadMask("Shadow", temporaryTextures);
        var light = LoadMask("Light", temporaryTextures);
        var ink = LoadMask("Ink", temporaryTextures);
        var regionMask = LoadMask("BaseRegion", temporaryTextures);
        var materials = new Material[8];
        for (var i = 0; i < palette.Length; i++)
        {
            ColorUtility.TryParseHtmlString(palette[i], out var color);
            ColorUtility.TryParseHtmlString("#D8CDB4", out var alternate);
            ColorUtility.TryParseHtmlString("#29222F", out var inkColor);
            ColorUtility.TryParseHtmlString("#E3D8CB", out var lightColor);
            var material = new Material(template) { name = "Bulwark paint " + i, hideFlags = HideFlags.HideAndDontSave };
            material.EnableKeyword("_PAINT_LAYERS");
            material.SetFloat("_PaintLayers", 1);
            material.SetTexture("_PaintShadowMap", shadow);
            material.SetTexture("_PaintLightMap", light);
            material.SetTexture("_PaintInkMap", ink);
            material.SetTexture("_EmissionMap", regionMask);
            material.SetFloat("_PaintShadowStrength", .9f);
            material.SetFloat("_PaintLightStrength", .7f);
            material.SetFloat("_PaintInkStrength", 1.05f);
            material.SetFloat("_PaintSaturation", 1);
            material.SetColor("_PaintInkColor", inkColor);
            material.SetColor("_PaintLightColor", lightColor);
            material.SetFloat("_TextureStrength", 0);
            material.SetFloat("_LineStrength", 0);
            material.SetFloat("_WearStrength", 0);
            material.SetFloat("_PaintEmissionBlend", i == 1 || i == 4 || i == 6 ? 1 : 0);
            material.SetColor("_EmissionColor", alternate.linear);
            material.SetFloat("_EmissionStrength", 0);
            if (i == 5) material.SetFloat("_SpecularStrength", .2f);
            material.SetColor("_PaperColor", color);
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_ShadowColor", i == 3 ? new Color(.5f, .6f, .69f) : i == 5 ? new Color(.74f, .74f, .74f) : new Color(.78f, .74f, .70f));
            material.SetFloat("_ShadowThreshold", .22f);
            material.SetFloat("_ShadowSoftness", .16f);
            materials[i] = material;
            temporaryMaterials.Add(material);
        }
        materials[7] = AssetDatabase.LoadAssetAtPath<Material>("Assets/Visuals/Ships/_Shared/Illustrated/Ship contour.mat");
        var draws = new List<Draw>();
        foreach (var part in data.parts)
        {
            var vertices = Vectors(part.vertices);
            var mesh = new Mesh { name = part.name, hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices.Concat(vertices).ToArray();
            mesh.normals = Vectors(part.normals).Concat(Vectors(part.contourNormals)).ToArray();
            var uv = new Vector2[vertices.Length];
            for (var i = 0; i < uv.Length; i++) uv[i] = new Vector2(part.uv[i * 2], part.uv[i * 2 + 1]);
            mesh.uv = uv.Concat(uv).ToArray();
            mesh.subMeshCount = 8;
            for (var region = 0; region < 7; region++)
            {
                var indices = new List<int>();
                for (var t = 0; t < part.regions.Length; t++)
                    if (part.regions[t] == region)
                        indices.AddRange(new[] { part.triangles[t * 3], part.triangles[t * 3 + 1], part.triangles[t * 3 + 2] });
                mesh.SetTriangles(indices, region);
            }
            mesh.SetTriangles(part.triangles.Select(i => i + vertices.Length).ToArray(), 7);
            mesh.RecalculateBounds();
            draws.Add(new Draw { Mesh = mesh, Matrix = Matrix4x4.identity, Materials = materials, OwnsMesh = true });
        }
        return draws;
    }

    private static Texture2D LoadMask(string name, List<Texture2D> textures)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, true) { name = name, hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
        if (!texture.LoadImage(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(MeshPath), "layers", name + ".png"))))
            throw new InvalidOperationException("Cannot decode " + name);
        textures.Add(texture);
        return texture;
    }

'''
code=code[:start]+method+code[end:]
code=code.replace('2790e4dfce08191ccf35443e46b2bacfecf70b630b2babdf6ca7c44cbbfe9e54',data['sourceSha256'])
code=code.replace('bool quarter)','bool quarter, int size = 640)')
code=code.replace('new Rect(0, 0, 640, 640)','new Rect(0, 0, size, size)').replace('GetTemporary(640, 640,','GetTemporary(size, size,').replace('Texture2D(640, 640,','Texture2D(size, size,')
code=code.replace('public float[] contourNormals;','public float[] contourNormals;\n        public float[] uv;')
code=code.replace('int size = 640)','int size = 640, int conceptView = 0)')
code=code.replace('new Color(0.055f, 0.07f, 0.095f, 1f)','conceptView > 0 ? new Color(.34f, .38f, .43f, 1f) : new Color(0.055f, 0.07f, 0.095f, 1f)')
code=code.replace('camera.transform.rotation = Quaternion.LookRotation(-camera.transform.position, Vector3.up);',
'''if (conceptView == 1) camera.transform.position = new Vector3(0, 0, -25);
            if (conceptView == 2) camera.transform.position = new Vector3(10, 15, -8.5f);
            if (conceptView == 3) camera.transform.position = new Vector3(-25, 0, 0);
            camera.transform.rotation = Quaternion.LookRotation(-camera.transform.position,
                conceptView == 1 ? Vector3.right : conceptView > 0 ? Vector3.back : Vector3.up);''')
code=code.replace('Render(bulwark, "Bulwark-painted-large", true, 1400);',
'''Render(bulwark, "Bulwark-painted-large", true, 1400);
            Render(bulwark, "Bulwark-concept-aligned", false, 1400, 1);
            Render(bulwark, "Bulwark-concept-aligned", true, 1400, 2);
            Render(bulwark, "Bulwark-side", true, 1400, 3);''')
code=code.replace('var bounds = new Bounds();','var height = conceptView == 3 ? Mathf.RoundToInt(size * .45f) : size;\n        var bounds = new Bounds();')
code=code.replace('camera.orthographicSize = quarter ? 4.6f : 3.7f;',
                  'camera.orthographicSize = conceptView == 2 ? 3.3f : conceptView == 3 ? 1.65f : quarter ? 4.6f : 3.7f;')
code=code.replace('new Rect(0, 0, size, size)','new Rect(0, 0, size, height)').replace('GetTemporary(size, size,','GetTemporary(size, height,').replace('Texture2D(size, size,','Texture2D(size, height,')
code=code.replace('sceneOrAssetSaved = false,',
                  'conceptComparisonViews = "Top nose-left; quarter camera (10,15,-8.5); side camera (-25,0,0); neutral gray background.", sceneOrAssetSaved = false,')
(scratch/'BulwarkPaintPreviewR07.cs').write_text(code.replace('BulwarkPaintPreview','BulwarkPaintPreviewR07'),encoding='utf-8')
print('Native paint preview prepared',len(parts),'parts')
