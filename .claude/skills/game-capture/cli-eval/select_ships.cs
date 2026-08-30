// args via file edit: NAMES holds ship-name prefixes; SUB optionally targets a child component's object.
var NAMES = new string[] { "Ship_2", "EyeballEnemy" };
string SUB = null;
var picked = new System.Collections.Generic.List<UnityEngine.Object>();
foreach (var s in UnityEngine.Object.FindObjectsByType<Ships.Ship>(UnityEngine.FindObjectsSortMode.None))
{
    foreach (var n in NAMES)
    {
        if (!s.name.StartsWith(n)) continue;
        if (SUB == null) { picked.Add(s.gameObject); break; }
        var t = System.Type.GetType(SUB);
        if (t == null) return "type not found: " + SUB;
        var c = s.GetComponentInChildren(t, true);
        if (c != null) picked.Add(c.gameObject);
        break;
    }
}
UnityEditor.Selection.objects = picked.ToArray();
var sb = new System.Text.StringBuilder("selected: ");
foreach (var o in picked) sb.Append(o.name).Append(" ");
return sb.ToString();
