var prefab = (UnityEngine.GameObject)UnityEditor.AssetDatabase.LoadMainAssetAtPath("Assets/Prefabs/Ships/Nightshade.prefab");
var go = UnityEngine.Object.Instantiate(prefab);
go.hideFlags = UnityEngine.HideFlags.HideAndDontSave;
var sb = new System.Text.StringBuilder();
try {
    UnityEngine.Physics.SyncTransforms();
    var rb = go.GetComponent<UnityEngine.Rigidbody>();
    sb.AppendLine("rootScale " + go.transform.localScale.ToString("F4"));
    sb.AppendLine("com " + rb.centerOfMass.ToString("F5") + " implicitCom " + rb.automaticCenterOfMass + " constraints " + rb.constraints);
    var mc = go.transform.Find("Mesh").GetComponent<UnityEngine.MeshCollider>();
    sb.AppendLine("collider mesh " + mc.sharedMesh.name + " verts " + mc.sharedMesh.vertexCount + " meshBounds c" + mc.sharedMesh.bounds.center.ToString("F5") + " e" + mc.sharedMesh.bounds.extents.ToString("F5"));
    var m = go.transform.worldToLocalMatrix * mc.transform.localToWorldMatrix;
    var min = new UnityEngine.Vector3(1e9f, 1e9f, 1e9f); var max = -min;
    foreach (var v in mc.sharedMesh.vertices) { var p = m.MultiplyPoint3x4(v); min = UnityEngine.Vector3.Min(min, p); max = UnityEngine.Vector3.Max(max, p); }
    sb.AppendLine("collider in ship space min " + min.ToString("F5") + " max " + max.ToString("F5"));
    sb.AppendLine("collider layer " + mc.gameObject.layer + " lossy " + mc.transform.lossyScale.ToString("F3"));
    foreach (var r in go.transform.Find("ShipBaseRig/Hull").GetComponentsInChildren<UnityEngine.Renderer>(true)) {
        var mesh = r.GetComponent<UnityEngine.MeshFilter>().sharedMesh;
        var mm = go.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
        var c = mm.MultiplyPoint3x4(mesh.bounds.center);
        sb.AppendLine("hull " + r.name + " mesh " + mesh.name + " subs " + mesh.subMeshCount + " verts " + mesh.vertexCount + " meshExt " + mesh.bounds.extents.ToString("F5") + " shipCentre " + c.ToString("F5") + " lossy " + r.transform.lossyScale.ToString("F3") + " mats " + r.sharedMaterials.Length);
    }
} finally { UnityEngine.Object.DestroyImmediate(go); }
return sb.ToString();
