var moves = new string[,] {
 { "Assets/Scripts/UI/LockOnIndicator.cs", "Assets/Scripts/UI/Weapons/LockOnIndicator.cs" },
 { "Assets/Scripts/Editor/Tests/PlayMode/UILifecyclePlayModeTests.cs", "Assets/Scripts/Editor/Tests/PlayMode/PlayerState/UILifecyclePlayModeTests.cs" },
 { "Assets/Scripts/Editor/Tests/PlayMode/ShipChildComponentStatePlayModeTests.cs", "Assets/Scripts/Editor/Tests/PlayMode/Weapons/ShipChildComponentStatePlayModeTests.cs" }
};
for (var i = 0; i < moves.GetLength(0); i++) {
 if (!System.IO.File.Exists(moves[i, 0])) continue;
 var error = UnityEditor.AssetDatabase.MoveAsset(moves[i, 0], moves[i, 1]);
 if (error.Length != 0) throw new System.Exception(error);
}
var rigPath = "Assets/Visuals/Ships/_Shared/ShipBaseRig.prefab";
var rig = UnityEditor.PrefabUtility.LoadPrefabContents(rigPath);
try {
 foreach (var reticle in rig.GetComponentsInChildren<UI.Weapons.LockOnIndicator>(true))
  UnityEngine.Object.DestroyImmediate(reticle.gameObject);
 UnityEditor.PrefabUtility.SaveAsPrefabAsset(rig, rigPath);
} finally { UnityEditor.PrefabUtility.UnloadPrefabContents(rig); }
var reticlePath = "Assets/Prefabs/UI/UILockOnIndicator.prefab";
var reticleAsset = UnityEditor.PrefabUtility.LoadPrefabContents(reticlePath);
try {
 ((UnityEngine.RectTransform)reticleAsset.transform).sizeDelta = UnityEngine.Vector2.one * 5f;
 UnityEditor.PrefabUtility.SaveAsPrefabAsset(reticleAsset, reticlePath);
} finally { UnityEditor.PrefabUtility.UnloadPrefabContents(reticleAsset); }
var overlayPath = "Assets/Prefabs/UI/UIOverlay.prefab";
var overlay = UnityEditor.PrefabUtility.LoadPrefabContents(overlayPath);
try {
 var serialized = new UnityEditor.SerializedObject(overlay.GetComponent<UI.Overlay>());
 serialized.FindProperty("lockReticle").objectReferenceValue = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(reticlePath).GetComponent<UI.Weapons.LockOnIndicator>();
 serialized.ApplyModifiedPropertiesWithoutUndo();
 UnityEditor.PrefabUtility.SaveAsPrefabAsset(overlay, overlayPath);
} finally { UnityEditor.PrefabUtility.UnloadPrefabContents(overlay); }
UnityEditor.AssetDatabase.SaveAssets();
return "Moved HUD reticle and lifecycle tests; preserved source reticle size, animation and color; wired Overlay.";
