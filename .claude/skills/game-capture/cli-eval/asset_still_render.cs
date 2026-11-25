// args via file edit: OUTPUT_PATH is an absolute PNG path (session scratchpad). Run after asset_still_scene.cs.
// Lays MESH_TOUR's E* children on a 5-wide grid and renders them through a temporary ortho camera.
var OUTPUT_PATH = @"<absolute path>.png";
var root = UnityEngine.GameObject.Find("MESH_TOUR");
float cell = 9.5f; int n = 0;
foreach (UnityEngine.Transform t in root.transform)
{
    if (!t.name.StartsWith("E")) continue;
    t.position = new UnityEngine.Vector3((n % 5) * cell, -(n / 5) * cell, 0f);
    n++;
}
var lgo = new UnityEngine.GameObject("tour_light"); lgo.transform.SetParent(root.transform);
var l = lgo.AddComponent<UnityEngine.Light>(); l.type = UnityEngine.LightType.Directional; l.intensity = 1.6f;
lgo.transform.rotation = UnityEngine.Quaternion.Euler(35f, 25f, 0f);
var cgo = new UnityEngine.GameObject("tour_cam"); cgo.transform.SetParent(root.transform);
var cam = cgo.AddComponent<UnityEngine.Camera>();
cam.orthographic = true; cam.orthographicSize = cell; cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor;
cam.backgroundColor = new UnityEngine.Color(0.08f, 0.08f, 0.1f); cam.nearClipPlane = 0.1f; cam.farClipPlane = 200f;
cgo.transform.position = new UnityEngine.Vector3(2f * cell, -0.5f * cell, -50f);
int w = 2500, h = 1000;
var rt = new UnityEngine.RenderTexture(w, h, 24); cam.targetTexture = rt; cam.Render();
UnityEngine.RenderTexture.active = rt;
var tex = new UnityEngine.Texture2D(w, h, UnityEngine.TextureFormat.RGB24, false);
tex.ReadPixels(new UnityEngine.Rect(0, 0, w, h), 0, 0); tex.Apply();
UnityEngine.RenderTexture.active = null; cam.targetTexture = null;
System.IO.File.WriteAllBytes(OUTPUT_PATH, UnityEngine.ImageConversion.EncodeToPNG(tex));
UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex);
UnityEngine.Object.DestroyImmediate(cgo);
return "ok " + n;
