using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ships
{
    public static class VanguardFinish
    {
        public static void Apply()
        {
            const string prefabPath = "Assets/Prefabs/Ships/Vanguard.prefab";
            const string visuals = "Assets/Visuals/Ships/Vanguard/";
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var ink = root.transform.Find("ShipBaseRig/Hull/Vanguard/Authored mesh/Vanguard roles/Ink").GetComponent<MeshRenderer>();
                ink.shadowCastingMode = ShadowCastingMode.Off;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            var retired = new[]
            {
                visuals + "VanguardStructure.fbx", visuals + "VanguardBaseColor.png", visuals + "Vanguard vivid paint.mat",
                visuals + "Meshes/Vanguard painted hull.asset", visuals + "Meshes/Vanguard contour.asset"
            }.Concat(Directory.GetFiles(visuals + "Materials/mint_plum", "*.mat"))
             .Concat(Directory.GetFiles(visuals + "Materials/vanguard", "*.mat").Where(p => !new[] { "HullPrimary", "HullAccent", "Structure" }.Contains(Path.GetFileNameWithoutExtension(p))))
             .Select(p => p.Replace('\\', '/')).ToArray();
            var retained = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") &&
                new[] { ".prefab", ".mat", ".unity", ".asset" }.Contains(Path.GetExtension(p)) && !retired.Contains(p)).ToArray();
            var dependencies = AssetDatabase.GetDependencies(retained, true);
            foreach (var path in retired)
            {
                if (dependencies.Contains(path)) throw new InvalidOperationException("Still referenced: " + path);
                if (!AssetDatabase.DeleteAsset(path)) throw new InvalidOperationException("Cannot retire: " + path);
            }
            if (!AssetDatabase.DeleteAsset(visuals + "Materials/mint_plum")) throw new InvalidOperationException("Cannot retire empty palette folder.");
            AssetDatabase.SaveAssets();
            File.WriteAllText("../../results/vanguard-redo/integration/retired-assets.json", JsonUtility.ToJson(new Retired { paths = retired }, true));
            Debug.Log("VANGUARD_FINISH_PASS");
        }

        [Serializable] private sealed class Retired { public string[] paths; }
    }
}
