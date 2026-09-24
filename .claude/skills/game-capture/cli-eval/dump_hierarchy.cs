// Dumps every GameObject in every loaded scene plus the DontDestroyOnLoad scene and hidden
// (HideAndDontSave) objects, as an indented tree. Writes to OUT and returns a summary.
var OUT = System.IO.Path.Combine(UnityEngine.Application.dataPath, "../../../results/hierarchy/hierarchy.txt");
var sb = new System.Text.StringBuilder();
int total = 0;
System.Action<UnityEngine.Transform, int> walk = null;
walk = (t, depth) =>
{
    total++;
    var go = t.gameObject;
    var comps = new System.Collections.Generic.List<string>();
    foreach (var c in go.GetComponents<UnityEngine.Component>())
        if (c != null && !(c is UnityEngine.Transform)) comps.Add(c.GetType().Name);
    sb.Append(new string(' ', depth * 2)).Append(go.activeSelf ? "" : "(inactive) ").Append(go.name);
    if (go.hideFlags != UnityEngine.HideFlags.None) sb.Append(" {hideFlags=").Append(go.hideFlags).Append("}");
    if (comps.Count > 0) sb.Append("  [").Append(string.Join(", ", comps)).Append("]");
    sb.AppendLine();
    for (int i = 0; i < t.childCount; i++) walk(t.GetChild(i), depth + 1);
};
// Loaded scenes
for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
{
    var sc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
    sb.AppendLine("=== SCENE: " + sc.name + " (loaded=" + sc.isLoaded + ", path=" + sc.path + ") ===");
    foreach (var root in sc.GetRootGameObjects()) walk(root.transform, 1);
    sb.AppendLine();
}
// DDOL + hidden: every GameObject in memory that is a scene object and whose root is not in a loaded scene above
var seen = new System.Collections.Generic.HashSet<UnityEngine.GameObject>();
var ddol = new System.Collections.Generic.List<UnityEngine.GameObject>();
var hidden = new System.Collections.Generic.List<UnityEngine.GameObject>();
foreach (var go in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>())
{
    if (UnityEditor.EditorUtility.IsPersistent(go)) continue;           // prefab assets
    if (go.transform.parent != null) continue;                           // roots only
    var sc = go.scene;
    if (sc.IsValid() && sc.isLoaded && sc.name != "DontDestroyOnLoad") continue;
    if (sc.name == "DontDestroyOnLoad") ddol.Add(go); else hidden.Add(go);
}
sb.AppendLine("=== SCENE: DontDestroyOnLoad ===");
foreach (var go in ddol) walk(go.transform, 1);
sb.AppendLine();
sb.AppendLine("=== NOT IN ANY LOADED SCENE (hidden / HideAndDontSave / unloaded) ===");
foreach (var go in hidden) walk(go.transform, 1);
System.IO.File.WriteAllText(OUT, sb.ToString());
return "objects=" + total + " scenes=" + UnityEngine.SceneManagement.SceneManager.sceneCount + " ddolRoots=" + ddol.Count + " hiddenRoots=" + hidden.Count + " -> " + OUT;
