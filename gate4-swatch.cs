// Calibration swatches: four glowing quads through the flight camera; INPUTS are linear emission colours.
var INPUTS = new UnityEngine.Vector4[] { __INPUTS__ };
var OUT = @"__OUT__";
var station = UnityEngine.GameObject.Find("STATION_PROOF");
if (station) station.SetActive(false);
var old = UnityEngine.GameObject.Find("SWATCHES");
if (old) UnityEngine.Object.DestroyImmediate(old);
var root = new UnityEngine.GameObject("SWATCHES");
var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Visuals/Environment/Stations/Outpost/Materials/Outpost Window.mat");
UnityEngine.Camera cam = null;
foreach (var oc in UnityEngine.Object.FindObjectsByType<Cameras.ObserverCam>(UnityEngine.FindObjectsSortMode.None))
    if (oc.Cam && oc.Cam.enabled) cam = oc.Cam;
var centre = cam.transform.position; centre.z = 0f;
var block = new UnityEngine.MaterialPropertyBlock();
for (int i = 0; i < INPUTS.Length; i++)
{
    var q = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Quad);
    UnityEngine.Object.DestroyImmediate(q.GetComponent<UnityEngine.Collider>());
    q.transform.SetParent(root.transform);
    q.transform.position = centre + new UnityEngine.Vector3(-15f + i * 10f, 0f, 0f);
    q.transform.localScale = new UnityEngine.Vector3(1.2f, 0.8f, 1f);
    var r = q.GetComponent<UnityEngine.MeshRenderer>();
    r.sharedMaterial = mat;
    block.SetVector("_EmissionColor", INPUTS[i]);
    r.SetPropertyBlock(block);
}
var rt = UnityEngine.RenderTexture.GetTemporary(1280, 720, 24, UnityEngine.RenderTextureFormat.ARGB32);
cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
UnityEngine.RenderTexture.active = rt;
var tex = new UnityEngine.Texture2D(1280, 720, UnityEngine.TextureFormat.RGB24, false);
tex.ReadPixels(new UnityEngine.Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
UnityEngine.RenderTexture.active = null; UnityEngine.RenderTexture.ReleaseTemporary(rt);
System.IO.File.WriteAllBytes(OUT, UnityEngine.ImageConversion.EncodeToPNG(tex));
UnityEngine.Object.DestroyImmediate(tex);
var sb = new System.Text.StringBuilder();
for (int i = 0; i < INPUTS.Length; i++)
{
    var vp = cam.WorldToViewportPoint(centre + new UnityEngine.Vector3(-15f + i * 10f, 0f, 0f));
    sb.Append(((int)(vp.x * 1280)) + "," + (720 - (int)(vp.y * 720)) + ";");
}
return sb.ToString();
