var consoles = UnityEngine.Object.FindObjectsByType<UI.Hangar.Hardware.HangarConsole>(
    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
if (consoles.Length == 0) return "no HangarConsole in scene";
consoles[0].Launch();
UnityEngine.QualitySettings.SetQualityLevel(System.Array.IndexOf(UnityEngine.QualitySettings.names, "High Fidelity"), true);
return "launched; quality " + UnityEngine.QualitySettings.names[UnityEngine.QualitySettings.GetQualityLevel()];
