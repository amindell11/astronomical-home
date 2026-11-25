var asm = typeof(UnityEditor.Editor).Assembly;
var util = asm.GetType("UnityEditor.AnnotationUtility");
var f = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
var annotations = (System.Array)util.GetMethod("GetAnnotations", f, null, System.Type.EmptyTypes, null).Invoke(null, null);
var at = asm.GetType("UnityEditor.Annotation");
var inst = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;
var scriptClass = at.GetField("scriptClass", inst);
var classId = at.GetField("classID", inst);
var setGizmo = util.GetMethod("SetGizmoEnabled", f);
var wanted = new string[] { "MovementController", "Missile" };
var sb = new System.Text.StringBuilder();
foreach (var a in annotations)
{
    var cls = (string)scriptClass.GetValue(a);
    foreach (var w in wanted)
        if (cls == w)
        {
            setGizmo.Invoke(null, new object[] { (int)classId.GetValue(a), cls, 1, false });
            sb.Append(cls).Append(" enabled  ;;  ");
        }
}
UnityEditor.SceneView.RepaintAll();
return sb.Length > 0 ? sb.ToString() : "nothing matched";
