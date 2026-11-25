// Clicks the hangar's Launch. Returns what it found.
var consoles = UnityEngine.Object.FindObjectsByType<UI.Hangar.Hardware.HangarConsole>(
    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
if (consoles.Length == 0) return "no HangarConsole in scene";
consoles[0].Launch();
return "launched";
