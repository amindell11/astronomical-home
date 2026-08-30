var t = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AnnotationUtility");
var f = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
var annotations = (System.Array)t.GetMethod("GetAnnotations", f, null, System.Type.EmptyTypes, null).Invoke(null, null);
var at = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Annotation");
var scriptClass = at.GetField("scriptClass", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
var gizmoEnabled = at.GetField("gizmoEnabled", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
var wanted = new string[] { "Ship", "MovementController", "DamageController", "Lasers", "Missiles", "Missile",
    "LockOnSensor", "AICommander", "Navigator", "Gunner", "Scout", "ProjectileBase", "ObserverCam", "PlayerCommander" };
var sb = new System.Text.StringBuilder();
foreach (var a in annotations)
{
    var cls = (string)scriptClass.GetValue(a);
    foreach (var w in wanted)
        if (cls == w) sb.Append(cls).Append("=").Append((int)gizmoEnabled.GetValue(a)).Append("  ;;  ");
}
return sb.Length > 0 ? sb.ToString() : "no matching annotations";
