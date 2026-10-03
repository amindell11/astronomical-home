var valis = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>("Assets/Visuals/Ships/Valis/Breakup/Valis burst.anim");
var vanguard = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>("Assets/Visuals/Ships/Vanguard/Breakup/Vanguard burst.anim");
var valisJson = UnityEditor.EditorJsonUtility.ToJson(valis, true);
var vanguardJson = UnityEditor.EditorJsonUtility.ToJson(vanguard, true);
System.IO.File.WriteAllText("D:/amind/git/agent-1/scratch/valis-breakup/valis-clip.json", valisJson);
System.IO.File.WriteAllText("D:/amind/git/agent-1/scratch/valis-breakup/vanguard-clip.json", vanguardJson);
return "Saved clip settings for comparison.";
