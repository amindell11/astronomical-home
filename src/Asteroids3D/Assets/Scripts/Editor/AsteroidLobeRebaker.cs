using System.Text;
using Asteroids.Spawning;
using UnityEditor;
using UnityEngine;
using LobeSphere = Asteroids.Spawning.AsteroidSpawnSettings.MeshInfo.LobeSphere;

namespace AsteroidTools
{
    /// <summary>
    /// Force-rebakes the covering-sphere lobes for every AsteroidSpawnSettings asset
    /// and saves them. The reliable/headless way to bake (OnValidate only fires in the
    /// editor when the asset is touched); logs a per-mesh summary for eyeballing.
    /// </summary>
    public static class AsteroidLobeRebaker
    {
        [MenuItem("Tools/Asteroids/Rebake Asteroid Lobes")]
        public static void RebakeAll()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Asteroid lobe rebake ===");

            foreach (var guid in AssetDatabase.FindAssets("t:AsteroidSpawnSettings"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var settings = AssetDatabase.LoadAssetAtPath<AsteroidSpawnSettings>(path);
                if (settings == null || settings.meshInfos == null) continue;

                sb.AppendLine($"-- {path}");
                for (int i = 0; i < settings.meshInfos.Length; i++)
                {
                    var info = settings.meshInfos[i];
                    if (info.mesh == null) { sb.AppendLine("   (null mesh)"); continue; }

                    info.cachedLobes = AsteroidLobeBaker.Bake(info.mesh, out float aspect);
                    info.cachedLobeAspect = aspect;
                    settings.meshInfos[i] = info; // MeshInfo is a struct — write back.

                    sb.AppendLine(Summarize(info.mesh.name, aspect, info.cachedLobes));
                }
                EditorUtility.SetDirty(settings);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(sb.ToString());
        }

        private static string Summarize(string meshName, float aspect, LobeSphere[] lobes)
        {
            var sb = new StringBuilder();
            int k = lobes != null ? lobes.Length : 0;
            sb.Append($"   {meshName}: aspect={aspect:F3} K={k} lobes=[");
            if (lobes != null)
            {
                for (int i = 0; i < lobes.Length; i++)
                {
                    if (i > 0) sb.Append(", ");
                    var l = lobes[i];
                    sb.Append($"(({l.center.x:F3},{l.center.y:F3},{l.center.z:F3}), r={l.radius:F3})");
                }
            }
            sb.Append("]");
            return sb.ToString();
        }
    }
}
