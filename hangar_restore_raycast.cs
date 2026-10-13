var screen = UnityEngine.Object.FindFirstObjectByType<UI.Hangar.HangarScreen>();
var field = typeof(UI.Hangar.HangarScreen).GetField("previewImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
((UnityEngine.UI.RawImage)field.GetValue(screen)).raycastTarget = true;
return "preview raycastTarget=true";
