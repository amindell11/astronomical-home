var destination = "D:/amind/git/agent-3/scratch/keystone-review/review3-mesh-quads.csv";
var rows = new System.Text.StringBuilder("ship,simTime,quad,areaA,areaB,travelWidthCos,widthChangeRatio,ax,ay,bx,by,cx,cy,dx,dy,u0,v0,u1,v1\n");
var saved = new System.Collections.Generic.HashSet<int>();
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
var type = typeof(Ships.Keystones.KeystoneSlipstream);
var deadline = UnityEditor.EditorApplication.timeSinceStartup + 90;
UnityEditor.EditorApplication.CallbackFunction inspect = null;
inspect = () =>
{
    if (UnityEditor.EditorApplication.timeSinceStartup > deadline)
    {
        UnityEditor.EditorApplication.update -= inspect;
        System.IO.File.WriteAllText(destination, rows.ToString());
        return;
    }
    foreach (var flow in UnityEngine.Object.FindObjectsByType<Ships.Keystones.KeystoneSlipstream>(UnityEngine.FindObjectsSortMode.None))
    {
        if (saved.Contains(flow.GetInstanceID())) continue;
        var count = (int)type.GetField("count", flags).GetValue(flow);
        var keystone = (Ships.Keystones.Keystone)type.GetField("keystone", flags).GetValue(flow);
        if (count < 13 || !keystone.Active) continue;
        var mesh = (UnityEngine.Mesh)type.GetField("mesh", flags).GetValue(flow);
        var points = mesh.vertices;
        var indices = mesh.triangles;
        var uv = mesh.uv;
        var ship = flow.GetComponentInParent<Ships.Ship>().name;
        var matrix = flow.transform.localToWorldMatrix;
        for (var i = 0; i < indices.Length; i += 6)
        {
            var ia = indices[i];
            var ib = indices[i + 2];
            var ic = indices[i + 1];
            var id = indices[i + 5];
            var a = matrix.MultiplyPoint3x4(points[ia]);
            var b = matrix.MultiplyPoint3x4(points[ib]);
            var c = matrix.MultiplyPoint3x4(points[ic]);
            var d = matrix.MultiplyPoint3x4(points[id]);
            var width = b - a;
            var nextWidth = d - c;
            var travel = (c + d - a - b) * .5f;
            var areaA = UnityEngine.Vector3.Cross(c - a, b - a).z;
            var areaB = UnityEngine.Vector3.Cross(c - b, d - b).z;
            var parallel = UnityEngine.Vector3.Dot(travel.normalized, width.normalized);
            var change = (nextWidth - width).magnitude / travel.magnitude;
            rows.Append(ship).Append(',').Append(UnityEngine.Time.time.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(i / 6);
            foreach (var value in new[] { areaA, areaB, parallel, change, a.x, a.y, b.x, b.y, c.x, c.y, d.x, d.y, uv[ia].x, uv[ia].y, uv[ib].x, uv[ib].y })
                rows.Append(',').Append(value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            rows.AppendLine();
        }
        saved.Add(flow.GetInstanceID());
    }
    if (saved.Count < 2) return;
    UnityEditor.EditorApplication.update -= inspect;
    System.IO.File.WriteAllText(destination, rows.ToString());
};
UnityEditor.EditorApplication.update += inspect;
return "Mesh-only observer armed for two active slipstreams; output: " + destination;
