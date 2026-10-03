var root = UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Visuals/Ships/Valis/Breakup/ValisBreakup.prefab"));
try {
var clip = root.GetComponent<UnityEngine.Animation>().clip;
var part = root.transform.Find("Fuselage and canopy");
var start = part.localPosition;
clip.SampleAnimation(root, .2f);
return new { before = start.ToString("F7"), after = part.localPosition.ToString("F7"), bindings = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Take(UnityEditor.AnimationUtility.GetCurveBindings(clip), 8)) };
} finally { UnityEngine.Object.DestroyImmediate(root); }
