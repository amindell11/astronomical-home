Utils.GameSettings.SetPresentationEnabled(false);
var screens = UnityEngine.Object.FindObjectsByType<UI.HangarScreen>(UnityEngine.FindObjectsSortMode.None);
if (screens.Length != 1) return "HangarScreen count=" + screens.Length;
var field = typeof(UI.HangarScreen).GetField("launchButton",
    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
var button = (UnityEngine.UI.Button)field.GetValue(screens[0]);
if (button == null) return "no launch button";
button.onClick.Invoke();
return "presentation OFF, launched";
