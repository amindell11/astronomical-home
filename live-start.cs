var host=UnityEngine.Object.FindFirstObjectByType<Game.Preview.PreviewHost>();
var panel=typeof(Game.Preview.PreviewHostBootstrap).Assembly.GetType("Game.Preview.PreviewControlPanel");
var window=UnityEditor.EditorWindow.GetWindow(panel);
window.position=new UnityEngine.Rect(150,150,600,850); window.Focus(); window.Repaint();
var hangar=host.Playtest.GetComponentInChildren<UI.Hangar.HangarScreen>();
if(hangar) ((UnityEngine.UI.Button)typeof(UI.Hangar.HangarScreen).GetField("launchButton",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(hangar)).onClick.Invoke();
return new { host.IsReady, host.Mode, host.PlaytestSetup, hasCopy=!UnityEditor.AssetDatabase.Contains(host.CanopyFeel), same=host.CanopyFeel==host.Playtest.CanopyProjection.Profile, saved=host.CanopyFeel.personalityEnabled };
