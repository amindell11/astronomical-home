// Renders Crimson's debris three ways (top-down ortho): shipped, approved look, unremapped. Unsaved objects only.
var S = @"C:\Users\amind\AppData\Local\Temp\claude\D--amind-git-astronomical-home\edefa2ab-c4b6-408c-9041-2a45dd491575\scratchpad\still";
var dir = "Assets/Visuals/Ships/Crimson/Breakup/";
var names = new[] { "Armor left", "Armor right", "Engine left", "Engine right", "Wing left", "Wing right",
  "Upper Forward fin left", "Upper Forward fin right", "Lower Forward fin left", "Lower Forward fin right",
  "Upper Outer swept fin left", "Upper Outer swept fin right", "Lower Outer swept fin left", "Lower Outer swept fin right",
  "Upper Aft swept fin left", "Upper Aft swept fin right", "Lower Aft swept fin left", "Lower Aft swept fin right" };
var old = UnityEngine.GameObject.Find("DEBRIS_STILL"); if (old != null) UnityEngine.Object.DestroyImmediate(old);
var root = new UnityEngine.GameObject("DEBRIS_STILL");
var hullMat = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Visuals/Ships/Crimson/Hull paint.mat");
var newTex = hullMat.GetTexture("_BaseMap");
var oldTex = new UnityEngine.Texture2D(2, 2, UnityEngine.TextureFormat.RGBA32, true) { hideFlags = UnityEngine.HideFlags.DontSave };
UnityEngine.ImageConversion.LoadImage(oldTex, System.IO.File.ReadAllBytes(System.IO.Path.Combine(S, "..", "remap", "atlas_old.png")));
var labels = new[] { "shipped", "approved", "unremapped" };
var results = new System.Collections.Generic.List<string>();
for (int v = 0; v < 3; v++)
{
  var mat = new UnityEngine.Material(hullMat) { hideFlags = UnityEngine.HideFlags.DontSave };
  mat.SetTexture("_BaseMap", v == 1 ? (UnityEngine.Texture)oldTex : newTex);
  var block = new UnityEngine.GameObject(labels[v]); block.transform.SetParent(root.transform);
  block.transform.position = new UnityEngine.Vector3(0f, 0f, v * 100f);
  int tile = 300, cols = 6, rows = 3;
  var sheet = new UnityEngine.Texture2D(tile * cols, tile * rows, UnityEngine.TextureFormat.RGB24, false);
  var lgo = new UnityEngine.GameObject("light"); lgo.transform.SetParent(block.transform, false);
  var l = lgo.AddComponent<UnityEngine.Light>(); l.type = UnityEngine.LightType.Directional; l.intensity = 1.4f;
  var cgo = new UnityEngine.GameObject("cam"); cgo.transform.SetParent(block.transform, false);
  var cam = cgo.AddComponent<UnityEngine.Camera>();
  cam.orthographic = true; cam.clearFlags = UnityEngine.CameraClearFlags.SolidColor;
  cam.backgroundColor = new UnityEngine.Color(0.08f, 0.08f, 0.1f); cam.nearClipPlane = 0.01f; cam.farClipPlane = 100f;
  var rt = new UnityEngine.RenderTexture(tile, tile, 24); cam.targetTexture = rt;
  for (int i = 0; i < names.Length; i++)
  {
    var mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(dir + names[i] + ".asset");
    if (v > 0)
    {
      mesh = UnityEngine.Object.Instantiate(mesh); mesh.hideFlags = UnityEngine.HideFlags.DontSave;
      var bytes = System.IO.File.ReadAllBytes(System.IO.Path.Combine(S, "olduv", names[i] + ".bin"));
      var uv = new UnityEngine.Vector2[bytes.Length / 8];
      for (int k = 0; k < uv.Length; k++) uv[k] = new UnityEngine.Vector2(System.BitConverter.ToSingle(bytes, k * 8), System.BitConverter.ToSingle(bytes, k * 8 + 4));
      mesh.uv = uv;
    }
    var go = new UnityEngine.GameObject(names[i]); go.transform.SetParent(block.transform, false);
    go.AddComponent<UnityEngine.MeshFilter>().sharedMesh = mesh;
    go.AddComponent<UnityEngine.MeshRenderer>().sharedMaterial = mat;
    var b = mesh.bounds; var e = b.extents;
    var c = block.transform.TransformPoint(b.center);
    UnityEngine.Vector3 axis = e.x <= e.y && e.x <= e.z ? UnityEngine.Vector3.right : (e.y <= e.z ? UnityEngine.Vector3.up : UnityEngine.Vector3.forward);
    var view = (axis + 0.35f * UnityEngine.Vector3.one).normalized;
    cgo.transform.position = c + view * 20f; cgo.transform.LookAt(c, axis == UnityEngine.Vector3.up ? UnityEngine.Vector3.forward : UnityEngine.Vector3.up);
    lgo.transform.rotation = UnityEngine.Quaternion.LookRotation(-view + 0.4f * UnityEngine.Vector3.down);
    cam.orthographicSize = e.magnitude * 1.05f;
    cam.Render();
    UnityEngine.RenderTexture.active = rt;
    sheet.ReadPixels(new UnityEngine.Rect(0, 0, tile, tile), (i % cols) * tile, (rows - 1 - i / cols) * tile);
    UnityEngine.RenderTexture.active = null;
    go.SetActive(false);
  }
  sheet.Apply(); cam.targetTexture = null;
  var outp = System.IO.Path.Combine(S, "debris-" + labels[v] + ".png");
  System.IO.File.WriteAllBytes(outp, UnityEngine.ImageConversion.EncodeToPNG(sheet));
  UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(sheet);
  block.SetActive(false);
  results.Add(outp);
}
UnityEngine.Object.DestroyImmediate(root);
return string.Join(";", results);
