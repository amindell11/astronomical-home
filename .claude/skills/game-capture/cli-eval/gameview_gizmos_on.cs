UnityEditor.PlayModeWindow.SetViewType(UnityEditor.PlayModeWindow.PlayModeViewTypes.GameView);
var asm = typeof(UnityEditor.EditorWindow).Assembly;
var pmv = asm.GetType("UnityEditor.PlayModeView");
var gvType = asm.GetType("UnityEditor.GameView");
var main = (UnityEditor.EditorWindow)pmv.GetMethod("GetMainPlayModeView",
    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
    .Invoke(null, null);
if (main == null || !gvType.IsInstanceOfType(main)) return "NO GAME VIEW";
gvType.GetProperty("drawGizmos",
    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
    .SetValue(main, true);
main.Repaint();
return "gizmos ON for " + main.GetType().Name;
