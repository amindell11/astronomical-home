var host=UnityEngine.Object.FindFirstObjectByType<Game.Preview.PreviewHost>();
UnityEngine.Time.timeScale=0f;
var p=host.Playtest.CanopyProjection;
return new { forced=p.ForceMode.ToString(), active=p.ActiveState.ToString(), p.Settings.glow, p.Settings.aberration, expected=host.CanopyFeel.bigMoment.settings.glow, effective=p.EffectsEnabled, assetDirty=UnityEditor.EditorUtility.IsDirty(UnityEditor.AssetDatabase.LoadAssetAtPath<UI.Projection.CanopyProjectionProfile>("Assets/Settings/UI/CanopyProjection.asset")) };
