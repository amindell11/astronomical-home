// Records the flight camera at a locked 24 fps of game time: 6 s open, 2 s in use, 2 s dark. Writes done.txt when finished.
var DIR = @"__DIR__";
System.IO.Directory.CreateDirectory(DIR);
UnityEngine.Camera cam = null;
foreach (var oc in UnityEngine.Object.FindObjectsByType<Cameras.ObserverCam>(UnityEngine.FindObjectsSortMode.None))
    if (oc.Cam && oc.Cam.enabled) cam = oc.Cam;
var go = UnityEngine.GameObject.Find("STATION_PROOF");
if (!cam || !go) return "missing camera or station";
var motion = go.GetComponent<Substrate.Sectors.Elements.Stations.Visual.StationMotion>();
motion.Show(Substrate.Sectors.Elements.Stations.Visual.StationLook.Open);
const int Fps = 24, Total = Fps * 10, InUseAt = Fps * 6, DarkAt = Fps * 8;
UnityEngine.Time.captureFramerate = Fps;
int count = 0, lastFrame = UnityEngine.Time.frameCount;
var rt = new UnityEngine.RenderTexture(1280, 720, 24, UnityEngine.RenderTextureFormat.ARGB32);
var tex = new UnityEngine.Texture2D(1280, 720, UnityEngine.TextureFormat.RGB24, false);
var log = new System.Text.StringBuilder();
UnityEditor.EditorApplication.CallbackFunction cb = null;
cb = () =>
{
    if (!UnityEngine.Application.isPlaying || !go) { UnityEditor.EditorApplication.update -= cb; UnityEngine.Time.captureFramerate = 0; return; }
    if (UnityEngine.Time.frameCount == lastFrame) return;
    log.Append(UnityEngine.Time.frameCount - lastFrame).Append(' ');
    lastFrame = UnityEngine.Time.frameCount;
    if (count == InUseAt) motion.Show(Substrate.Sectors.Elements.Stations.Visual.StationLook.InUse);
    if (count == DarkAt) motion.Show(Substrate.Sectors.Elements.Stations.Visual.StationLook.Dark);
    cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
    UnityEngine.RenderTexture.active = rt;
    tex.ReadPixels(new UnityEngine.Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
    UnityEngine.RenderTexture.active = null;
    System.IO.File.WriteAllBytes(System.IO.Path.Combine(DIR, "f" + count.ToString("0000") + ".png"), UnityEngine.ImageConversion.EncodeToPNG(tex));
    count++;
    if (count < Total) return;
    UnityEditor.EditorApplication.update -= cb;
    UnityEngine.Time.captureFramerate = 0;
    UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex);
    motion.Show(Substrate.Sectors.Elements.Stations.Visual.StationLook.Open);
    System.IO.File.WriteAllText(System.IO.Path.Combine(DIR, "done.txt"), "frame steps: " + log);
};
UnityEditor.EditorApplication.update += cb;
return "recording " + Total + " frames";
