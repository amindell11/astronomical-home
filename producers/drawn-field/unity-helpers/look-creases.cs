var previousScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var locale = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(UnityEditor.AssetDatabase.GUIDToAssetPath("1e74bde8878130343920c6da8b10638f"), UnityEditor.SceneManagement.OpenSceneMode.Additive);
UnityEngine.SceneManagement.SceneManager.SetActiveScene(locale);
try
{
    var output = "D:/amind/git/agent-4/results/drawn-crease-probe";
    System.IO.Directory.CreateDirectory(output);
    var report = new System.Text.StringBuilder();
    foreach (var treatment in new[] { 2, 3 })
    {
        var type = System.Type.GetType("Tests.PlayMode.Scenarios.Drawn.Drawn" + (treatment == 2 ? "Contour" : "Exploration") + "Scenario, Tests.PlayMode");
        var scenario = System.Activator.CreateInstance(type);
        var baseType = type.BaseType;
        var apply = baseType.GetMethod("ApplyTreatment", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var owned = (System.Collections.Generic.List<UnityEngine.Object>)baseType.GetField("owned", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(scenario);
        var root = new UnityEngine.GameObject("Crease probe");
        try
        {
            var ship = UnityEditor.AssetDatabase.LoadAssetAtPath<Ships.Ship>("Assets/Prefabs/Ships/Ship_1.prefab");
            var stage = UI.HangarPreviewStage.Create(false, root.transform);
            stage.Show(new Ships.Loadout.ShipLoadout(ship, ship.Engine, ship.Shield, null, null));
            var rig = stage.GetComponentInChildren<Ships.Presentation.ShipVisualRig>();
            rig.transform.localScale = ship.GetComponentInChildren<Ships.Presentation.ShipVisualRig>(true).transform.lossyScale;
            var hull = rig.transform.Find("Model").GetComponent<UnityEngine.MeshRenderer>();
            var source = hull.GetComponent<UnityEngine.MeshFilter>().sharedMesh;
            UnityEngine.Texture2D crease = null;
            if (treatment == 3)
            {
                const int size = 1024;
                var vertices = source.vertices;
                var uv = source.uv;
                var triangles = source.triangles;
                var edges = new System.Collections.Generic.Dictionary<(UnityEngine.Vector3, UnityEngine.Vector3), System.Collections.Generic.List<(int a, int b, UnityEngine.Vector3 normal)>>();
                for (var i = 0; i < triangles.Length; i += 3)
                {
                    var normal = UnityEngine.Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]).normalized;
                    for (var side = 0; side < 3; side++)
                    {
                        var a = triangles[i + side];
                        var b = triangles[i + (side + 1) % 3];
                        var p = vertices[a];
                        var q = vertices[b];
                        var forward = p.x < q.x || (p.x == q.x && (p.y < q.y || (p.y == q.y && p.z < q.z)));
                        var key = forward ? (p, q) : (q, p);
                        if (!edges.TryGetValue(key, out var incident))
                            edges.Add(key, incident = new System.Collections.Generic.List<(int a, int b, UnityEngine.Vector3 normal)>());
                        incident.Add((a, b, normal));
                    }
                }
                var values = new UnityEngine.Color32[size * size];
                for (var i = 0; i < values.Length; i++) values[i] = new UnityEngine.Color32(0, 0, 0, 255);
                var selected = 0;
                var uvSegments = 0;
                var minLength = source.bounds.size.magnitude * .015f;
                var cosAngle = UnityEngine.Mathf.Cos(40 * UnityEngine.Mathf.Deg2Rad);
                foreach (var edge in edges)
                {
                    if ((edge.Key.Item2 - edge.Key.Item1).magnitude < minLength) continue;
                    var incident = edge.Value;
                    var hard = false;
                    for (var a = 0; a < incident.Count; a++)
                        for (var b = a + 1; b < incident.Count; b++)
                            hard |= UnityEngine.Vector3.Dot(incident[a].normal, incident[b].normal) < cosAngle;
                    if (!hard) continue;
                    selected++;
                    foreach (var face in incident)
                    {
                        var a = uv[face.a] * (size - 1);
                        var b = uv[face.b] * (size - 1);
                        var delta = b - a;
                        var x0 = UnityEngine.Mathf.Max(0, UnityEngine.Mathf.FloorToInt(UnityEngine.Mathf.Min(a.x, b.x) - 1.5f));
                        var x1 = UnityEngine.Mathf.Min(size - 1, UnityEngine.Mathf.CeilToInt(UnityEngine.Mathf.Max(a.x, b.x) + 1.5f));
                        var y0 = UnityEngine.Mathf.Max(0, UnityEngine.Mathf.FloorToInt(UnityEngine.Mathf.Min(a.y, b.y) - 1.5f));
                        var y1 = UnityEngine.Mathf.Min(size - 1, UnityEngine.Mathf.CeilToInt(UnityEngine.Mathf.Max(a.y, b.y) + 1.5f));
                        for (var y = y0; y <= y1; y++)
                            for (var x = x0; x <= x1; x++)
                            {
                                var point = new UnityEngine.Vector2(x, y);
                                var t = delta.sqrMagnitude == 0 ? 0 : UnityEngine.Mathf.Clamp01(UnityEngine.Vector2.Dot(point - a, delta) / delta.sqrMagnitude);
                                var coverage = UnityEngine.Mathf.Clamp01(1.5f - UnityEngine.Vector2.Distance(point, a + t * delta));
                                var value = (byte)UnityEngine.Mathf.RoundToInt(coverage * 255);
                                var index = y * size + x;
                                if (value > values[index].r) values[index] = new UnityEngine.Color32(value, value, value, 255);
                            }
                        uvSegments++;
                    }
                }
                crease = new UnityEngine.Texture2D(size, size, UnityEngine.TextureFormat.RGBA32, true, true);
                owned.Add(crease);
                crease.filterMode = UnityEngine.FilterMode.Bilinear;
                crease.wrapMode = UnityEngine.TextureWrapMode.Clamp;
                crease.SetPixels32(values);
                crease.Apply(true, false);
                System.IO.File.WriteAllBytes(output + "/crease-mask.png", crease.EncodeToPNG());
                report.AppendLine($"Mesh: {source.name}; vertices: {vertices.Length}; triangles: {triangles.Length / 3}; geometric edges: {edges.Count}; selected edges: {selected}; UV segments: {uvSegments}; threshold: 40 degrees; minimum edge length: {minLength}; line width: 2 texels plus antialiasing.");
            }
            stage.transform.Find("Anchor").rotation = UnityEngine.Quaternion.Euler(0, 240, 0);
            foreach (var renderer in stage.GetComponentsInChildren<UnityEngine.MeshRenderer>())
            {
                try { apply.Invoke(scenario, new object[] { renderer, false }); }
                catch (System.Reflection.TargetInvocationException error) { throw error.InnerException; }
            }
            if (treatment == 3)
            {
                hull.sharedMaterial.SetTexture("_CreaseMap", crease);
                hull.sharedMaterial.SetFloat("_CreaseStrength", 1);
            }
            var camera = stage.GetComponentInChildren<UnityEngine.Camera>();
            camera.backgroundColor = new UnityEngine.Color(.025f, .035f, .06f, 1);
            var pixels = new UnityEngine.Texture2D(768, 768, UnityEngine.TextureFormat.RGB24, false);
            owned.Add(pixels);
            var prior = UnityEngine.RenderTexture.active;
            try
            {
                camera.Render();
                UnityEngine.RenderTexture.active = (UnityEngine.RenderTexture)stage.Texture;
                pixels.ReadPixels(new UnityEngine.Rect(0, 0, 768, 768), 0, 0);
                pixels.Apply();
            }
            finally { UnityEngine.RenderTexture.active = prior; }
            System.IO.File.WriteAllBytes(output + "/ship-" + treatment + ".png", pixels.EncodeToPNG());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            foreach (var item in owned) if (item) UnityEngine.Object.DestroyImmediate(item);
        }
    }
    System.IO.File.WriteAllText(output + "/diagnostics.txt", report.ToString());
    return output + "\n" + report;
}
finally
{
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousScene);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(locale, true);
}
