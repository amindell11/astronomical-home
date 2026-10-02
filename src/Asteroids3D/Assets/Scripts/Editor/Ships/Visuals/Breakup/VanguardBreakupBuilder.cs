#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Utils;
using Object = UnityEngine.Object;

namespace Ships.Visuals.Breakup
{
    public static class VanguardBreakupBuilder
    {
        private const string Source = "Assets/Visuals/Ships/Vanguard/DrawnStudy";
        private const string Output = "Assets/Visuals/Ships/Vanguard/Breakup";
        private const string Rig = "Assets/Prefabs/Ships/Ship_1_IllustratedRig.prefab";
        private const float Lifetime = 1.4f;
        private const float Fade = .25f;
        private const float Soot = .9f;
        private const float BurstDuration = .12f;
        private static readonly string[] PartNames =
        {
            "MVP Wing", "MVP wing_armature", "MVP wing_armor", "MVP Power Nacelle Housing",
            "MVP Sparrow Tail", "MVP Tail", "MVP wing_tail"
        };
        private static readonly string[] PieceNames = { "Wing", "Wing root", "Armor", "Nacelle", "Sparrow tail", "Tail fin", "Wing fin" };
        private static readonly string[] Folders = { "Wings", "Wings", "Armor", "Nacelles", "Tails", "Tails", "Tails" };
        private static readonly Vector3[] Travel =
        {
            new(.72f, -.12f, -.05f), new(.43f, -.2f, .07f), new(.65f, .16f, -.06f),
            new(.45f, -.32f, .045f), new(.31f, .55f, -.055f), new(.58f, .46f, .09f), new(.79f, .32f, -.075f)
        };
        private static readonly Vector3[] Spins =
        {
            new(30, 55, 38), new(-35, 42, -55), new(55, -32, 64), new(-40, 95, 35),
            new(25, -48, -65), new(115, 80, 125), new(-85, 115, -100)
        };
        private static readonly float[] Delays = { .015f, .025f, .03f, .04f, .025f, .02f, .035f };
        private static readonly float[] PieceLifetimes = { 1.4f, 1.25f, 1.4f, 1.25f, 1.4f, .95f, 1.05f };
        private static readonly string[] Painted = PartNames.Concat(new[] { "MVP Fuselage", "MVP Cockpit.001" }).ToArray();

        private sealed class Surface
        {
            public string name;
            public Vector3[] positions;
            public Vector3[] normals;
            public Vector2[] uvs;
            public int[] indices;
        }

        [MenuItem("Tools/Ships/Build Vanguard Breakup")]
        public static void Build()
        {
            EnsureFolder(Output + "/Materials");
            foreach (var folder in Folders.Distinct()) EnsureFolder(Output + "/Pieces/" + folder);
            var source = Load<GameObject>(Source + "/VanguardStructure.fbx");
            var filters = source.GetComponentsInChildren<MeshFilter>(true).ToDictionary(filter => filter.name);
            var rig = PrefabUtility.LoadPrefabContents(Rig);
            var root = new GameObject("Vanguard breakup");
            try
            {
                var hull = rig.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Vanguard");
                var authored = hull.Find("Authored mesh");
                var sourceToHull = hull.worldToLocalMatrix * authored.localToWorldMatrix;
                var surfaces = Painted.ToDictionary(name => name, name => ReadSurface(source.transform, filters[name], sourceToHull));
                var ink = ReadSurface(source.transform, filters["Vanguard structural ink"], sourceToHull);
                var core = ReadSurface(source.transform, filters["MVP Power Nacelle Core"], sourceToHull);
                var inkOwners = InkOwners(ink, sourceToHull.inverse);
                var paint = DebrisMaterial(Load<Material>(Source + "/Vanguard vivid paint.mat"), "Debris paint", 3100, true);
                var contour = DebrisMaterial(Load<Material>("Assets/Visuals/Ships/Shared/Illustrated/Ship contour.mat"), "Debris contour", 3101, false);
                var inkPaint = DebrisMaterial(rig.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name == "Vanguard structural ink").sharedMaterial, "Debris ink", 3100, false);
                var corePaint = DebrisMaterial(rig.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.name == "MVP Power Nacelle Core").sharedMaterial, "Debris blue core", 3100, true);
                corePaint.SetFloat("_EmissionStrength", 0);
                corePaint.SetColor("_EmissionColor", Color.black);
                var debris = root.AddComponent<ShipBreakupDebris>();
                var motion = root.AddComponent<Animation>();
                motion.playAutomatically = false;
                motion.cullingType = AnimationCullingType.AlwaysAnimate;
                var clip = new AnimationClip { name = "Vanguard burst", legacy = true, wrapMode = WrapMode.ClampForever };
                var data = new SerializedObject(debris);
                var pieces = data.FindProperty("pieces");
                pieces.arraySize = PartNames.Length * 2;
                data.FindProperty("motion").objectReferenceValue = motion;
                data.FindProperty("explosionPrefab").objectReferenceValue = Load<GameObject>("Assets/Visuals/Vfx/LayeredExplosion/Prefabs/LayeredAsteroidExplosion.prefab").GetComponent<PooledVFX>();
                data.FindProperty("lifetime").floatValue = Lifetime;
                data.FindProperty("fadeDuration").floatValue = Fade;
                var width = surfaces.Values.SelectMany(s => s.positions).Max(p => p.x) - surfaces.Values.SelectMany(s => s.positions).Min(p => p.x);
                var pieceIndex = 0;
                for (var part = 0; part < PartNames.Length; part++)
                foreach (var side in new[] { -1, 1 })
                {
                    var name = PieceNames[part] + (side < 0 ? " left" : " right");
                    var mesh = BuildMesh(surfaces[PartNames[part]], part == 3 ? core : null, ink, inkOwners, side, name, out var pivot);
                    mesh = Save(mesh, Output + "/Pieces/" + Folders[part] + "/" + name + ".asset");
                    var piece = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                    piece.layer = hull.gameObject.layer;
                    piece.transform.SetParent(root.transform, false);
                    piece.transform.localPosition = pivot;
                    piece.GetComponent<MeshFilter>().sharedMesh = mesh;
                    piece.GetComponent<MeshRenderer>().sharedMaterials = new[] { paint, inkPaint, corePaint, contour };
                    piece.GetComponent<MeshRenderer>().sortingOrder = 4;
                    var entry = pieces.GetArrayElementAtIndex(pieceIndex++);
                    entry.FindPropertyRelative("renderer").objectReferenceValue = piece.GetComponent<MeshRenderer>();
                    entry.FindPropertyRelative("lifetime").floatValue = PieceLifetimes[part];
                    var travel = Travel[part] * width;
                    var spin = Spins[part];
                    travel.x *= side;
                    spin.y *= side;
                    spin.z *= side;
                    if (side > 0) { travel *= .92f; spin *= 1.13f; }
                    for (var axis = 0; axis < 3; axis++)
                    {
                        var suffix = new[] { ".x", ".y", ".z" }[axis];
                        Curve(clip, name, "m_LocalPosition" + suffix, pivot[axis], travel[axis], Delays[part] + (side > 0 ? .012f : 0));
                        Curve(clip, name, "localEulerAnglesRaw" + suffix, 0, spin[axis], Delays[part] + (side > 0 ? .012f : 0));
                    }
                }
                clip = Save(clip, Output + "/Vanguard burst.anim");
                motion.AddClip(clip, clip.name);
                motion.clip = clip;
                data.ApplyModifiedPropertiesWithoutUndo();
                var saved = PrefabUtility.SaveAsPrefabAsset(root, Output + "/VanguardBreakup.prefab");
                var visual = rig.GetComponent<ShipBreakupVisual>();
                if (!visual) visual = rig.AddComponent<ShipBreakupVisual>();
                var visualData = new SerializedObject(visual);
                visualData.FindProperty("hull").objectReferenceValue = hull;
                visualData.FindProperty("debrisPrefab").objectReferenceValue = saved.GetComponent<ShipBreakupDebris>();
                visualData.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(rig, Rig);
                AssetDatabase.SaveAssets();
                Debug.Log("Vanguard breakup built: 14 independently animated pieces, selected Vivid paint, one layered explosion.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                PrefabUtility.UnloadPrefabContents(rig);
            }
        }

        private static Surface ReadSurface(Transform root, MeshFilter filter, Matrix4x4 sourceToHull)
        {
            var mesh = filter.sharedMesh;
            var matrix = sourceToHull * root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var normalMatrix = matrix.inverse.transpose;
            return new Surface
            {
                name = filter.name,
                positions = mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray(),
                normals = mesh.normals.Select(n => normalMatrix.MultiplyVector(n).normalized).ToArray(),
                uvs = mesh.uv.Length == mesh.vertexCount ? mesh.uv : new Vector2[mesh.vertexCount],
                indices = mesh.triangles
            };
        }

        private static Mesh BuildMesh(Surface hull, Surface core, Surface ink, string[] owners, int side, string name, out Vector3 pivot)
        {
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();
            var indices = new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            Add(hull, 0, true, null);
            if (core != null) Add(core, 2, true, null);
            Add(ink, 1, false, owners);
            if (indices[0].Count == 0) throw new InvalidOperationException(name + " has no painted triangles.");
            var bounds = new Bounds(positions[0], Vector3.zero);
            foreach (var position in positions) bounds.Encapsulate(position);
            pivot = bounds.center;
            for (var i = 0; i < positions.Count; i++) positions[i] -= pivot;
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32, subMeshCount = 4 };
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            for (var i = 0; i < indices.Length; i++) mesh.SetTriangles(indices[i], i);
            mesh.RecalculateBounds();
            return mesh;

            void Add(Surface surface, int slot, bool outlined, string[] triangleOwners)
            {
                var sums = new Dictionary<Vector3, Vector3>();
                if (outlined)
                for (var i = 0; i < surface.indices.Length; i += 3)
                {
                    var a = surface.positions[surface.indices[i]];
                    var b = surface.positions[surface.indices[i + 1]];
                    var c = surface.positions[surface.indices[i + 2]];
                    var normal = Vector3.Cross(b - a, c - a);
                    foreach (var point in new[] { a, b, c })
                    {
                        sums.TryGetValue(point, out var sum);
                        sums[point] = sum + normal;
                    }
                }
                var remap = new Dictionary<int, int>();
                var outline = new Dictionary<int, int>();
                for (var i = 0; i < surface.indices.Length; i += 3)
                {
                    if (triangleOwners != null && triangleOwners[i / 3] != hull.name) continue;
                    var centerX = (surface.positions[surface.indices[i]].x + surface.positions[surface.indices[i + 1]].x + surface.positions[surface.indices[i + 2]].x) / 3;
                    if (side < 0 && centerX >= 0 || side > 0 && centerX < 0) continue;
                    for (var corner = 0; corner < 3; corner++)
                    {
                        var sourceIndex = surface.indices[i + corner];
                        if (!remap.TryGetValue(sourceIndex, out var target))
                        {
                            target = positions.Count;
                            remap.Add(sourceIndex, target);
                            var position = surface.positions[sourceIndex];
                            var brush = Mathf.Sin(position.x * 4 + position.y * 6) * .14f + Mathf.Sin(position.z * 8 - position.y * 7) * .09f;
                            var inward = Mathf.Clamp01(Vector3.Dot(surface.normals[sourceIndex], -position.normalized) * .5f + .5f);
                            var soot = Mathf.Clamp01(.25f + inward * .5f + brush);
                            var color = new Color(soot, soot, soot, 1);
                            positions.Add(position); normals.Add(surface.normals[sourceIndex]); uvs.Add(surface.uvs[sourceIndex]); colors.Add(color);
                            if (outlined)
                            {
                                outline[sourceIndex] = positions.Count;
                                positions.Add(position); normals.Add(sums[position].normalized); uvs.Add(surface.uvs[sourceIndex]); colors.Add(color);
                            }
                        }
                        indices[slot].Add(target);
                        if (outlined) indices[3].Add(outline[sourceIndex]);
                    }
                }
            }
        }

        private static string[] InkOwners(Surface ink, Matrix4x4 toSource)
        {
            var lines = JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "../../../art/ships/vanguard/drawn-study/structural-lines.json")))["lines"].ToArray();
            var parents = Enumerable.Range(0, ink.indices.Length / 3).ToArray();
            var connections = new Dictionary<Vector3, int>();
            for (var triangle = 0; triangle < parents.Length; triangle++)
            for (var corner = 0; corner < 3; corner++)
            {
                var position = ink.positions[ink.indices[triangle * 3 + corner]];
                if (connections.TryGetValue(position, out var connected)) parents[Find(triangle)] = Find(connected);
                else connections.Add(position, triangle);
            }
            var groups = Enumerable.Range(0, parents.Length).GroupBy(Find).ToArray();
            var owners = new string[parents.Length];
            foreach (var group in groups)
            {
                var samples = group.Select(triangle =>
                {
                    var i = triangle * 3;
                    var p = toSource.MultiplyPoint3x4((ink.positions[ink.indices[i]] + ink.positions[ink.indices[i + 1]] + ink.positions[ink.indices[i + 2]]) / 3);
                    return new Vector2(-p.x, -p.z);
                }).ToArray();
                var owner = Enumerable.Range(0, lines.Length).OrderBy(line =>
                {
                    var points = lines[line]["points"].Select(p => new Vector2((float)p[0], (float)p[1])).ToArray();
                    return samples.Sum(sample => Enumerable.Range(0, points.Length - 1).Min(segment =>
                    {
                        var edge = points[segment + 1] - points[segment];
                        var progress = Mathf.Clamp01(Vector2.Dot(sample - points[segment], edge) / edge.sqrMagnitude);
                        return (sample - points[segment] - edge * progress).sqrMagnitude;
                    }));
                }).First();
                foreach (var triangle in group) owners[triangle] = (string)lines[owner]["object"];
            }
            return owners;

            int Find(int index)
            {
                while (parents[index] != index) index = parents[index];
                return index;
            }
        }
        private static void Curve(AnimationClip clip, string path, string property, float start, float distance, float delay)
        {
            var curve = new AnimationCurve(new Keyframe(0, start), new Keyframe(delay, start),
                new Keyframe(delay + BurstDuration, start + distance * .62f),
                new Keyframe(.34f, start + distance * .78f), new Keyframe(Lifetime, start + distance));
            for (var key = 0; key < curve.length; key++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, key, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, key, AnimationUtility.TangentMode.ClampedAuto);
            }
            clip.SetCurve(path, typeof(Transform), property, curve);
        }
        private static Material DebrisMaterial(Material source, string name, int queue, bool scorched)
        {
            var material = new Material(source) { name = name, renderQueue = queue };
            if (scorched) material.SetFloat("_SootStrength", Soot);
            return Save(material, Output + "/Materials/" + name + ".mat");
        }

        private static T Save<T>(T asset, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!existing) { AssetDatabase.CreateAsset(asset, path); return asset; }
            EditorUtility.CopySerialized(asset, existing);
            Object.DestroyImmediate(asset);
            return existing;
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset) throw new InvalidOperationException("Missing Vanguard breakup source: " + path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
    }
}
#endif






