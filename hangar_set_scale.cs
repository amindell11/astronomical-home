var screen = UnityEngine.Object.FindFirstObjectByType<UI.Hangar.HangarScreen>();
var field = typeof(UI.Hangar.HangarScreen).GetField("previewImage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var image = (UnityEngine.UI.RawImage)field.GetValue(screen);
image.rectTransform.localScale = UnityEngine.Vector3.one;
return "preview localScale=1";
