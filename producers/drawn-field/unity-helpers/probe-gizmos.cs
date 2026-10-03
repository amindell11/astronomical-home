var t = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");
var view = UnityEngine.Resources.FindObjectsOfTypeAll(t)[0];
var p = t.GetProperty("drawGizmos", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
var before = (bool)p.GetValue(view);
p.SetValue(view, false);
return new { before, after = (bool)p.GetValue(view), time = UnityEngine.Time.time };
