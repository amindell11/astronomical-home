var host=UnityEngine.Object.FindFirstObjectByType<Game.Preview.PreviewHost>();
host.Playtest.CanopyProjection.ForceMode=UI.Projection.CanopyForceMode.BigMoment;
host.CanopyFeel.personalityEnabled=false;
UnityEditor.EditorApplication.ExecuteMenuItem("Astronomical/UI/Import Canopy Tuner Settings…");
return new { workingCopy=!UnityEditor.AssetDatabase.Contains(host.CanopyFeel), expectedGlow=host.CanopyFeel.bigMoment.settings.glow };
