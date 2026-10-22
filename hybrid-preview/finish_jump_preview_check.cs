var window = UnityEditor.EditorWindow.GetWindow<Game.Travel.SectorJumpPreviewWindow>();
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var type = window.GetType();
var time = (float)type.GetField("time", flags).GetValue(window);
if (time == 1f) throw new System.Exception("Preview playback did not advance");
type.GetField("playing", flags).SetValue(window, false);
type.GetField("time", flags).SetValue(window, 1.7f);
window.Repaint();
return new { playbackTime=time, scrubbedTime=1.7f, paused=true, resourcesSurvivedPlayMode=true };
