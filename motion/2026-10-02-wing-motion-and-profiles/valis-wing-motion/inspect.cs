var root = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/Prefabs/Ships/Valis.prefab");
var mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>("Assets/Visuals/Ships/Valis/Meshes/Valis hull.asset");
return Newtonsoft.Json.JsonConvert.SerializeObject(new {renderers = root.GetComponentsInChildren<UnityEngine.Renderer>(true).Select(r=>new {r.name, type=r.GetType().Name, pos=r.transform.localPosition, rot=r.transform.localRotation}), mesh.vertexCount, mesh.bounds, vertices=mesh.vertices.Take(12).ToArray(), uv=mesh.uv.Take(12).ToArray(), mesh.subMeshCount},new Newtonsoft.Json.JsonSerializerSettings { ReferenceLoopHandling=Newtonsoft.Json.ReferenceLoopHandling.Ignore });

