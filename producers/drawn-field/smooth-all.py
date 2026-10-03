from pathlib import Path
p=Path('src/Asteroids3D/Assets/Scripts/Editor/Tests/PlayMode/Scenarios/Drawn/DrawnComparisonScenario.cs');s=p.read_text();start=s.index('            var faces = new Dictionary<Vector3, List<Vector3>>();');end=s.index('            mesh.normals = normals;',start);s=s[:start]+'''            var sums = new Dictionary<Vector3, Vector3>();
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = triangles[i];
                var b = triangles[i + 1];
                var c = triangles[i + 2];
                var normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                foreach (var index in new[] { a, b, c })
                {
                    sums.TryGetValue(vertices[index], out var sum);
                    sums[vertices[index]] = sum + normal;
                }
            }
            for (var i = 0; i < normals.Length; i++)
                normals[i] = sums[vertices[i]].normalized;
''' +s[end:];p.write_text(s)
