// Renders the flight camera to a 1280x720 PNG. OUT is set by sed before each run.
var OUT = @"__OUT__";
UnityEngine.Camera cam = null;
foreach (var oc in UnityEngine.Object.FindObjectsByType<Cameras.ObserverCam>(UnityEngine.FindObjectsSortMode.None))
    if (oc.Cam && oc.Cam.enabled) cam = oc.Cam;
if (!cam) return "no flight camera";
System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(OUT));
var rt = UnityEngine.RenderTexture.GetTemporary(1280, 720, 24, UnityEngine.RenderTextureFormat.ARGB32, UnityEngine.RenderTextureReadWrite.Default, __AA__);
var prior = cam.targetTexture;
cam.targetTexture = rt; cam.Render(); cam.targetTexture = prior;
UnityEngine.RenderTexture.active = rt;
var tex = new UnityEngine.Texture2D(1280, 720, UnityEngine.TextureFormat.RGB24, false);
tex.ReadPixels(new UnityEngine.Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
UnityEngine.RenderTexture.active = null;
UnityEngine.RenderTexture.ReleaseTemporary(rt);
System.IO.File.WriteAllBytes(OUT, UnityEngine.ImageConversion.EncodeToPNG(tex));
UnityEngine.Object.DestroyImmediate(tex);
return "ok " + cam.name + " size " + cam.orthographicSize;
