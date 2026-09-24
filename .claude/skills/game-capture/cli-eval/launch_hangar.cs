// Clicks the hangar's Launch button (internal field → reflection). Returns what it found.
var screens = UnityEngine.Object.FindObjectsByType<UI.HangarScreen>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
if (screens.Length == 0) return "no HangarScreen in scene";
var f = typeof(UI.HangarScreen).GetField("launchButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var btn = (UnityEngine.UI.Button)f.GetValue(screens[0]);
if (btn == null) return "launchButton null";
btn.onClick.Invoke();
return "launched via " + btn.name;
