var stage = UnityEngine.Object.FindFirstObjectByType<UI.Hangar.HangarPreviewStage>();
var camera = (UnityEngine.Camera)typeof(UI.Hangar.HangarPreviewStage).GetField("stageCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(stage);
camera.enabled = false;
return "Preview camera disabled; image rectangle retained";
