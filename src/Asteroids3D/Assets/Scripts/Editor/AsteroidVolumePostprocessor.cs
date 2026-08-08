using Asteroids.Spawning;
using UnityEditor;
using UnityEngine;

namespace AsteroidTools
{
    /// <summary>
    /// Keeps every <see cref="AsteroidSpawnSettings.MeshInfo.cachedVolume"/> in step with
    /// its mesh. Volume feeds the broadphase scalar, mass, damage lethality, and field
    /// packing, so a stale value is wrong in four places at once; baking on import is what
    /// makes that state un-authorable.
    ///
    /// Rebakes every entry rather than mapping imported paths back to the entries using
    /// them: that reverse lookup fails *silently* into a stale bake (sub-asset fileIDs, LOD
    /// meshes, the colliderMesh alias), which is the state this exists to prevent.
    ///
    /// Writing only on change is load-bearing, not an optimisation — saving re-imports the
    /// asset and re-enters this callback; an unchanged second pass ends the cycle.
    ///
    /// Lobes stay manual (<c>Rebake Asteroid Lobes</c>): regenerating AI geometry behind a
    /// build is worse than refusing it, so the build gate only stale-checks them.
    /// </summary>
    public static class AsteroidVolumePostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths) => BakeAll();

        internal static void BakeAll()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AsteroidSpawnSettings"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var settings = AssetDatabase.LoadAssetAtPath<AsteroidSpawnSettings>(path);
                if (settings == null || settings.meshInfos == null) continue;

                var changed = false;
                for (int i = 0; i < settings.meshInfos.Length; i++)
                {
                    var info = settings.meshInfos[i];
                    if (info.mesh == null) continue;

                    if (!AsteroidMeshVolume.TryCompute(info.mesh, out var volume, out _))
                    {
                        // Leave the existing value rather than zeroing it: a degenerate read
                        // here is far more likely to be a mid-import mesh than a bad asset,
                        // and the build gate is what refuses to ship a wrong one.
                        Debug.LogWarning(
                            $"[AsteroidVolume] {path}: mesh '{info.mesh.name}' read as degenerate; " +
                            "volume left unchanged.");
                        continue;
                    }

                    if (Mathf.Approximately(info.cachedVolume, volume)) continue;

                    info.cachedVolume = volume;
                    settings.meshInfos[i] = info; // MeshInfo is a struct — write back.
                    changed = true;
                }

                if (!changed) continue;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssetIfDirty(settings);
            }
        }
    }
}
