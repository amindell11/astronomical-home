#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// One-shot utility to migrate legacy <see cref="AsteroidSpawnSettings"> assets that still use the
/// deprecated 'asteroidMeshes' array into the new 'meshInfos' structure.
/// Run via the Unity menu:  Tools ▸ Asteroid ▸ Migrate Spawn Settings.
/// </summary>
public static class AsteroidSpawnSettingsMigrator
{
    private const string MigrateMenu           = "Tools/Asteroid/Migrate Spawn Settings (Project)";
    private const string MigrateSelectedMenu   = "Tools/Asteroid/Migrate Spawn Settings (Selected)";
    private const string RecalcMenu            = "Tools/Asteroid/Re-calculate Cached Volumes (Project)";
    private const string RecalcSelectedMenu    = "Tools/Asteroid/Re-calculate Cached Volumes (Selected)";

    /*──────────────────────────────────────────────────────────────────────────*/
    /* Migration from legacy 'asteroidMeshes' array ---------------------------*/

    [MenuItem(MigrateMenu, priority = 2000)]
    private static void MigrateAllProjectSettings() => ProcessSettings(FindAllSettings(), true);

    [MenuItem(MigrateSelectedMenu, priority = 2001)]
    private static void MigrateSelectedSettings() => ProcessSettings(GetSelectedSettings(), true);

    [MenuItem(MigrateMenu, validate = true, priority = 2000)]
    [MenuItem(MigrateSelectedMenu, validate = true, priority = 2001)]
    private static bool ValidateMenu() => true; // Always enabled

    private static bool NeedsMigration(AsteroidSpawnSettings settings)
    {
        var so = new SerializedObject(settings);
        SerializedProperty meshInfosProp = so.FindProperty("meshInfos");
        SerializedProperty legacyMeshesProp = so.FindProperty("asteroidMeshes");
        return (meshInfosProp == null || meshInfosProp.arraySize == 0) &&
               legacyMeshesProp != null && legacyMeshesProp.arraySize > 0;
    }

    private static void PerformMigration(AsteroidSpawnSettings settings)
    {
        var so = new SerializedObject(settings);
        var meshInfosProp = so.FindProperty("meshInfos");
        var legacyMeshesProp = so.FindProperty("asteroidMeshes");

        int count = legacyMeshesProp != null ? legacyMeshesProp.arraySize : 0;
        if (count == 0) return;

        // Resize meshInfos array
        meshInfosProp.arraySize = count;
        for (int i = 0; i < count; i++)
        {
            var mesh = legacyMeshesProp.GetArrayElementAtIndex(i).objectReferenceValue as Mesh;
            var meshInfoElem = meshInfosProp.GetArrayElementAtIndex(i);
            meshInfoElem.FindPropertyRelative("mesh").objectReferenceValue = mesh;
            // Populate collider mesh – duplicate & optimise
            Mesh colliderMesh = GenerateOptimizedColliderMesh(mesh, settings);
            meshInfoElem.FindPropertyRelative("colliderMesh").objectReferenceValue = colliderMesh;

            float cachedVol = ComputeMeshVolume(mesh);
            meshInfoElem.FindPropertyRelative("cachedVolume").floatValue = cachedVol;
        }

        // Clear legacy array to avoid repeated migrations
        legacyMeshesProp.arraySize = 0;

        so.ApplyModifiedProperties();
    }

    /*──────────────────────────────────────────────────────────────────────────*/
    /* Recalculate volumes (and optionally fill collider meshes) -------------*/
    [MenuItem(RecalcMenu, priority = 2002)]
    private static void RecalculateVolumesProject() => ProcessSettings(FindAllSettings(), false);

    [MenuItem(RecalcSelectedMenu, priority = 2003)]
    private static void RecalculateVolumesSelected() => ProcessSettings(GetSelectedSettings(), false);

    /*──────────────────────────────────────────────────────────────────────────*/
    /* Core processing --------------------------------------------------------*/

    private static void ProcessSettings(IEnumerable<AsteroidSpawnSettings> settingsCollection, bool doMigration)
    {
        int updated = 0;

        foreach (var settings in settingsCollection)
        {
            if (settings == null) continue;

            bool changed = false;

            if (doMigration && NeedsMigration(settings))
            {
                PerformMigration(settings);
                changed = true;
            }

            if (settings.meshInfos != null)
            {
                for (int i = 0; i < settings.meshInfos.Length; i++)
                {
                    var info = settings.meshInfos[i];
                    if (info.mesh == null) continue;

                    float vol = ComputeMeshVolume(info.mesh);
                    if (!Mathf.Approximately(vol, info.cachedVolume))
                    {
                        info.cachedVolume = vol;
                        changed = true;
                    }

                    if (info.colliderMesh == null)
                    {
                        info.colliderMesh = GenerateOptimizedColliderMesh(info.mesh, settings);
                        changed = true;
                    }

                    settings.meshInfos[i] = info; // write back if struct changed
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(settings);
                updated++;
            }
        }

        if (updated > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"AsteroidSpawnSettingsMigrator: Updated {updated} asset(s).");
        }
        else
        {
            Debug.Log("AsteroidSpawnSettingsMigrator: No changes needed.");
        }
    }

    /*──────────────────────────────────────────────────────────────────────────*/
    /* Utility ----------------------------------------------------------------*/

    private static IEnumerable<AsteroidSpawnSettings> FindAllSettings()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AsteroidSpawnSettings"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var settings = AssetDatabase.LoadAssetAtPath<AsteroidSpawnSettings>(path);
            if (settings != null) yield return settings;
        }
    }

    private static IEnumerable<AsteroidSpawnSettings> GetSelectedSettings() =>
        Selection.GetFiltered<AsteroidSpawnSettings>(SelectionMode.Assets);

    private static Mesh GenerateOptimizedColliderMesh(Mesh sourceMesh, AsteroidSpawnSettings owner)
    {
        if (sourceMesh == null) return null;

        // If the source mesh is already marked readable/optimised we can reuse it.
        // Otherwise duplicate and optimise.

        Mesh collider = Object.Instantiate(sourceMesh);
        collider.name = sourceMesh.name + "_collider";

        // Basic optimisation pass (Unity will remove duplicate verts, etc.)
        MeshUtility.Optimize(collider);

        // Store as a sub-asset so it persists.
        AssetDatabase.AddObjectToAsset(collider, owner);

        return collider;
    }

    private static float ComputeMeshVolume(Mesh mesh)
    {
        if (mesh == null) return 1f;

        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;

        double volume = 0.0;
        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector3 v0 = verts[tris[i]];
            Vector3 v1 = verts[tris[i + 1]];
            Vector3 v2 = verts[tris[i + 2]];

            volume += Vector3.Dot(v0, Vector3.Cross(v1, v2));
        }

        volume /= 6.0;
        return Mathf.Abs((float)volume);
    }
}
#endif 