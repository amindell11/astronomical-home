// Establish presentation-off through the session profile BEFORE compose, so both the ship rig (live
// GameSettings static, read at Awake) and the asteroid field (compose-time Services snapshot) suppress
// their renderers. Poking GameSettings after compose leaves the field lit — the "magenta" asteroid leak.
var hosts = UnityEngine.Object.FindObjectsByType<Game.Play.GameSessionHost>(UnityEngine.FindObjectsSortMode.None);
if (hosts.Length != 1) return "GameSessionHost count=" + hosts.Length;
var pf = typeof(Game.Play.GameSessionHost).GetField("sessionProfile",
    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
var profile = (Game.Sessions.SessionProfile)pf.GetValue(hosts[0]);
profile.presentation = false;

var screens = UnityEngine.Object.FindObjectsByType<UI.HangarScreen>(UnityEngine.FindObjectsSortMode.None);
if (screens.Length != 1) return "profile flipped; HangarScreen count=" + screens.Length;
var field = typeof(UI.HangarScreen).GetField("launchButton",
    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
var button = (UnityEngine.UI.Button)field.GetValue(screens[0]);
if (button == null) return "profile flipped; no launch button";
button.onClick.Invoke();
return "profile.presentation=false before compose, launched";
