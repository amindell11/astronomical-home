using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class VanguardVariantBuilder
{
    private const string ModelPath = "Assets/Visuals/Ships/Vanguard/Vanguard.fbx";
    private const string MaterialFolder = "Assets/Visuals/Ships/Vanguard/Materials";
    private const string SourceRigPath = "Assets/Prefabs/Ships/Ship_1_VisualRig.prefab";
    private const string RigVariantPath = "Assets/Prefabs/Ships/Ship_1_Vanguard_VisualRig.prefab";
    private const string SourceShipPath = "Assets/Prefabs/Ships/Ship_1.prefab";
    private const string ShipVariantPath = "Assets/Prefabs/Ships/Ship_1_Vanguard.prefab";

    [MenuItem("Tools/Astronomical/Build Ship 1 Vanguard Variant")]
    public static void Build()
    {
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        EnsureFolder(MaterialFolder);

        var materials = CreateMaterials();
        var importedRoot = RequireAsset<GameObject>(ModelPath);
        var importedRenderer = importedRoot.GetComponentInChildren<MeshRenderer>(true);
        var importedFilter = importedRenderer != null ? importedRenderer.GetComponent<MeshFilter>() : null;
        if (importedFilter == null || importedFilter.sharedMesh == null)
            throw new InvalidOperationException($"{ModelPath} does not contain an imported mesh.");

        var importedMesh = importedFilter.sharedMesh;
        var orderedMaterials = ResolveMaterialOrder(importedRenderer, importedMesh.subMeshCount, materials);
        BuildVisualRigVariant(importedMesh, orderedMaterials);
        BuildShipVariant();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var rigType = PrefabUtility.GetPrefabAssetType(RequireAsset<GameObject>(RigVariantPath));
        var shipType = PrefabUtility.GetPrefabAssetType(RequireAsset<GameObject>(ShipVariantPath));
        if (rigType != PrefabAssetType.Variant || shipType != PrefabAssetType.Variant)
            throw new InvalidOperationException($"Expected prefab variants, got rig={rigType}, ship={shipType}.");

        Debug.Log($"Vanguard variants built. Mesh={importedMesh.name}, vertices={importedMesh.vertexCount}, " +
                  $"submeshes={importedMesh.subMeshCount}, rig={RigVariantPath}, ship={ShipVariantPath}");
    }

    private static void BuildVisualRigVariant(Mesh mesh, Material[] materials)
    {
        var source = RequireAsset<GameObject>(SourceRigPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            instance.name = "Ship_1_Vanguard_VisualRig";
            var model = instance.transform.Find("Model");
            if (model == null)
                throw new InvalidOperationException($"{SourceRigPath} has no Model child.");

            var filter = model.GetComponent<MeshFilter>();
            var renderer = model.GetComponent<MeshRenderer>();
            if (filter == null || renderer == null || filter.sharedMesh == null)
                throw new InvalidOperationException($"{SourceRigPath}/Model is missing its source mesh renderer.");

            var targetBounds = TransformBounds(filter.sharedMesh.bounds,
                Matrix4x4.TRS(model.localPosition, model.localRotation, model.localScale));

            var correction = LongAxisToPositiveY(mesh.bounds.size);
            var correctedBounds = TransformBounds(mesh.bounds, Matrix4x4.Rotate(correction));
            var scale = MaxComponent(targetBounds.size) / MaxComponent(correctedBounds.size);
            var scaledCenter = correction * mesh.bounds.center * scale;

            model.localRotation = correction;
            model.localScale = Vector3.one * scale;
            model.localPosition = targetBounds.center - scaledCenter;
            filter.sharedMesh = mesh;
            renderer.sharedMaterials = materials;

            PrefabUtility.SaveAsPrefabAsset(instance, RigVariantPath);
            Debug.Log($"Vanguard rig fit: sourceBounds={targetBounds.size}, importedBounds={correctedBounds.size}, scale={scale:F4}, rotation={correction.eulerAngles}");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static void BuildShipVariant()
    {
        var sourceShip = RequireAsset<GameObject>(SourceShipPath);
        var rigVariant = RequireAsset<GameObject>(RigVariantPath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(sourceShip);
        try
        {
            instance.name = "Ship_1_Vanguard";
            var oldRig = instance.transform.Find("Ship_1_VisualRig");
            if (oldRig == null)
                throw new InvalidOperationException($"{SourceShipPath} has no Ship_1_VisualRig child.");

            var siblingIndex = oldRig.GetSiblingIndex();
            UnityEngine.Object.DestroyImmediate(oldRig.gameObject);
            var newRig = (GameObject)PrefabUtility.InstantiatePrefab(rigVariant, instance.transform);
            newRig.name = "Ship_1_Vanguard_VisualRig";
            newRig.transform.SetSiblingIndex(siblingIndex);
            newRig.transform.localPosition = Vector3.zero;
            newRig.transform.localRotation = Quaternion.identity;
            newRig.transform.localScale = Vector3.one;

            PrefabUtility.SaveAsPrefabAsset(instance, ShipVariantPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static Dictionary<string, Material> CreateMaterials()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
            throw new InvalidOperationException("No supported lit shader is available.");

        var specs = new[]
        {
            new MaterialSpec("VNG_Hull_White", new Color(0.76f, 0.74f, 0.69f), 0.18f, 0.30f),
            new MaterialSpec("VNG_Panel_Gray", new Color(0.30f, 0.34f, 0.38f), 0.32f, 0.36f),
            new MaterialSpec("VNG_Mechanical_Charcoal", new Color(0.025f, 0.035f, 0.052f), 0.58f, 0.24f),
            new MaterialSpec("VNG_Accent_Orange", new Color(0.95f, 0.24f, 0.045f), 0.16f, 0.28f),
            new MaterialSpec("VNG_Canopy_Smoke", new Color(0.025f, 0.065f, 0.105f), 0.42f, 0.14f),
            new MaterialSpec("VNG_Engine_Blue", new Color(0.015f, 0.12f, 0.38f), 0.35f, 0.18f,
                new Color(0.09f, 1.44f, 4.5f))
        };

        var result = new Dictionary<string, Material>(StringComparer.Ordinal);
        foreach (var spec in specs)
        {
            var path = $"{MaterialFolder}/{spec.Name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = spec.Name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            SetColor(material, "_BaseColor", "_Color", spec.BaseColor);
            SetFloat(material, "_Metallic", spec.Metallic);
            SetFloat(material, "_Smoothness", 1f - spec.Roughness);
            if (spec.Emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                SetColor(material, "_EmissionColor", "_EmissionColor", spec.Emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(material);
            result.Add(spec.Name, material);
        }

        return result;
    }

    private static Material[] ResolveMaterialOrder(MeshRenderer importedRenderer, int subMeshCount,
        IReadOnlyDictionary<string, Material> materials)
    {
        var imported = importedRenderer.sharedMaterials;
        if (imported.Length != subMeshCount)
            throw new InvalidOperationException($"Imported material count {imported.Length} does not match submesh count {subMeshCount}.");

        return imported.Select(material =>
        {
            if (material == null || !materials.TryGetValue(material.name, out var replacement))
                throw new InvalidOperationException($"No Vanguard material mapping for imported slot '{material?.name ?? "<null>"}'.");
            return replacement;
        }).ToArray();
    }

    private static Quaternion LongAxisToPositiveY(Vector3 size)
    {
        if (size.y >= size.x && size.y >= size.z)
            return Quaternion.identity;
        if (size.z >= size.x)
            return Quaternion.Euler(-90f, 0f, 0f);
        return Quaternion.Euler(0f, 0f, 90f);
    }

    private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
    {
        var result = new Bounds(matrix.MultiplyPoint3x4(bounds.center), Vector3.zero);
        var extents = bounds.extents;
        for (var x = -1; x <= 1; x += 2)
        for (var y = -1; y <= 1; y += 2)
        for (var z = -1; z <= 1; z += 2)
            result.Encapsulate(matrix.MultiplyPoint3x4(bounds.center + Vector3.Scale(extents, new Vector3(x, y, z))));
        return result;
    }

    private static float MaxComponent(Vector3 value) => Mathf.Max(value.x, Mathf.Max(value.y, value.z));

    private static void SetColor(Material material, string preferred, string fallback, Color value)
    {
        var property = material.HasProperty(preferred) ? preferred : fallback;
        if (material.HasProperty(property))
            material.SetColor(property, value);
    }

    private static void SetFloat(Material material, string property, float value)
    {
        if (material.HasProperty(property))
            material.SetFloat(property, value);
    }

    private static T RequireAsset<T>(string path) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        return asset != null ? asset : throw new FileNotFoundException($"Missing asset: {path}");
    }

    private static void EnsureFolder(string path)
    {
        var current = "Assets";
        foreach (var part in path.Split('/').Skip(1))
        {
            var next = $"{current}/{part}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, part);
            current = next;
        }
    }

    private readonly struct MaterialSpec
    {
        public MaterialSpec(string name, Color baseColor, float metallic, float roughness, Color emission = default)
        {
            Name = name;
            BaseColor = baseColor;
            Metallic = metallic;
            Roughness = roughness;
            Emission = emission;
        }

        public string Name { get; }
        public Color BaseColor { get; }
        public float Metallic { get; }
        public float Roughness { get; }
        public Color Emission { get; }
    }
}
