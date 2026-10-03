var root = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Visuals/Ships/Valis/Breakup/ValisBreakup.prefab");
var animation = root.GetComponent<UnityEngine.Animation>();
return new { animation.cullingType, animation.enabled, animation.clip.legacy, animation.clip.length, curves = UnityEditor.AnimationUtility.GetCurveBindings(animation.clip).Length };
