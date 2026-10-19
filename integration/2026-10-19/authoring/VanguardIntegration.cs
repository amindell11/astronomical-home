using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ships
{
    public static class VanguardIntegration
    {
        private const string Visuals = "Assets/Visuals/Ships/Vanguard/";
        private const string Prefab = "Assets/Prefabs/Ships/Vanguard.prefab";
        private const string Output = "../../results/vanguard-redo/integration/";

        [Serializable]
        private sealed class Node
        {
            public string path;
            public Vector3 position;
            public Vector3 rotation;
            public Vector3 scale;
            public Vector3 worldPosition;
            public Vector3 worldRotation;
            public Vector3 worldScale;
            public string mesh;
            public Vector3 boundsCenter;
            public Vector3 boundsSize;
            public Vector3 worldBoundsCenter;
            public Vector3 worldBoundsSize;
            public int vertices;
            public int[] triangles;
            public string[] materials;
        }

        [Serializable]
        private sealed class Inspection
        {
            public Node[] current;
            public Node[] exported;
            public Node[] breakupBefore;
            public Node[] breakupAfter;
        }

        [Serializable] private sealed class Group { public string role; public string family; public string[] slots; }
        [Serializable] private sealed class Groups { public Group[] groups; }

        public static void Apply()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var debrisPath = Visuals + "VanguardBreakup.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(debrisPath);
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.weldVertices = false;
            importer.isReadable = true;
            importer.SaveAndReimport();

            var root = PrefabUtility.LoadPrefabContents(Prefab);
            var breakupPath = Visuals + "Breakup/VanguardBreakup.prefab";
            var breakup = PrefabUtility.LoadPrefabContents(breakupPath);
            var report = new Inspection { current = Read(root), breakupBefore = Read(breakup) };
            try
            {
                var hull = root.transform.Find("ShipBaseRig/Hull/Vanguard");
                var authored = hull.Find("Authored mesh");
                var authorToHull = hull.worldToLocalMatrix * authored.localToWorldMatrix;
                var paint = AssetDatabase.LoadAssetAtPath<Material>(Visuals + "Materials/vanguard/HullPrimary.mat");
                var debrisPaint = AssetDatabase.LoadAssetAtPath<Material>(Visuals + "Breakup/Materials/Debris paint.mat");
                debrisPaint.CopyPropertiesFromMaterial(paint);
                debrisPaint.renderQueue = 3100;
                debrisPaint.SetFloat("_SootStrength", .9f);
                debrisPaint.SetFloat("_DebrisVisibility", 1f);
                EditorUtility.SetDirty(debrisPaint);
                var debrisInk = AssetDatabase.LoadAssetAtPath<Material>(Visuals + "Breakup/Materials/Debris ink.mat");
                var debrisCore = AssetDatabase.LoadAssetAtPath<Material>(Visuals + "Breakup/Materials/Debris blue core.mat");
                var debrisContour = AssetDatabase.LoadAssetAtPath<Material>(Visuals + "Breakup/Materials/Debris contour.mat");
                var debrisModel = AssetDatabase.LoadAssetAtPath<GameObject>(debrisPath);
                var groups = JsonUtility.FromJson<Groups>(File.ReadAllText(Output + "groups.json")).groups;
                var assigned = new HashSet<Transform>();
                foreach (var group in groups)
                {
                    var source = debrisModel.transform.Find(group.role).GetComponent<MeshFilter>();
                    var sourceToHull = authorToHull * source.transform.localToWorldMatrix;
                    var center = sourceToHull.MultiplyPoint3x4(source.sharedMesh.bounds.center);
                    var piece = breakup.GetComponentsInChildren<MeshFilter>().Single(f =>
                        (f.name == group.family + " left" || f.name == group.family + " right") &&
                        Mathf.Sign((breakup.transform.worldToLocalMatrix * f.transform.localToWorldMatrix).MultiplyPoint3x4(f.sharedMesh.bounds.center).x) == Mathf.Sign(center.x));
                    if (!assigned.Add(piece.transform)) throw new InvalidOperationException("Duplicate piece: " + piece.name);
                    var pieceToHull = breakup.transform.worldToLocalMatrix * piece.transform.localToWorldMatrix;
                    var oldMesh = piece.sharedMesh;
                    var renderer = piece.GetComponent<MeshRenderer>();
                    var inkSlot = Array.IndexOf(renderer.sharedMaterials, debrisInk);
                    if (inkSlot != 1) throw new InvalidOperationException("Unexpected ink slot: " + piece.name);
                    var rebuilt = BuildPiece(source.sharedMesh, sourceToHull, pieceToHull.inverse, oldMesh, inkSlot);
                    rebuilt.name = oldMesh.name;
                    EditorUtility.CopySerialized(rebuilt, oldMesh);
                    EditorUtility.SetDirty(oldMesh);
                    UnityEngine.Object.DestroyImmediate(rebuilt);
                    renderer.sharedMaterials = group.slots.Select(s => s == "Contour" ? debrisContour : s == "CoreGlow" ? debrisCore : debrisPaint).Append(debrisInk).ToArray();
                }
                if (assigned.Count != 14) throw new InvalidOperationException("Expected all fourteen breakup pieces.");

                for (var i = authored.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(authored.GetChild(i).gameObject);
                var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Visuals + "Vanguard.fbx");
                var model = UnityEngine.Object.Instantiate(modelAsset, authored, false);
                model.name = "Vanguard roles";
                foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = hull.gameObject.layer;
                var contour = AssetDatabase.LoadAssetAtPath<Material>("Assets/Visuals/Ships/_Shared/Illustrated/Ship contour.mat");
                model.transform.Find("Hull").GetComponent<MeshRenderer>().sharedMaterials = new[]
                {
                    AssetDatabase.LoadAssetAtPath<Material>(Visuals + "Materials/vanguard/HullAccent.mat"), paint,
                    AssetDatabase.LoadAssetAtPath<Material>(Visuals + "Materials/vanguard/Structure.mat"), contour
                };
                model.transform.Find("Canopy").GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Visuals + "Materials/Canopy.mat");
                model.transform.Find("Cores").GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Visuals + "Materials/Engine glow.mat");
                model.transform.Find("Ink").GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Visuals + "Materials/Structural ink.mat");
                var rig = new SerializedObject(root.GetComponentInChildren<Ships.Presentation.ShipVisualRig>(true));
                var silhouette = rig.FindProperty("silhouette");
                var renderers = model.GetComponentsInChildren<Renderer>();
                silhouette.arraySize = renderers.Length;
                for (var i = 0; i < renderers.Length; i++) silhouette.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
                rig.ApplyModifiedPropertiesWithoutUndo();
                report.exported = Read(root);
                report.breakupAfter = Read(breakup);
                if (renderers.Any(r => r.sharedMaterials.Any(m => !m))) throw new InvalidOperationException("Unbound material.");
                PrefabUtility.SaveAsPrefabAsset(root, Prefab);
                PrefabUtility.SaveAsPrefabAsset(breakup, breakupPath);
                AssetDatabase.SaveAssets();
                File.WriteAllText(Output + "applied.json", JsonUtility.ToJson(report, true));
                Debug.Log("VANGUARD_SUBSTITUTION_PASS");
                var testType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Tests.EditMode.ShipAnatomyEditModeTests")).Single(t => t != null);
                try
                {
                    testType.GetMethod("Chassis_HullKeepsTheContourAsASeparateVertexRange").Invoke(Activator.CreateInstance(testType), new object[] { Prefab });
                    File.WriteAllText(Output + "contour-test-before.txt", "PASS");
                }
                catch (System.Reflection.TargetInvocationException failure)
                {
                    File.WriteAllText(Output + "contour-test-before.txt", failure.InnerException.ToString());
                    Debug.Log("VANGUARD_CONTOUR_TEST_REPRODUCED");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                PrefabUtility.UnloadPrefabContents(breakup);
            }
        }

        private static Mesh BuildPiece(Mesh source, Matrix4x4 toHull, Matrix4x4 hullToPiece, Mesh old, int inkSlot)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var colors = new List<Color>();
            var sourceVertices = source.vertices;
            var sourceNormals = source.normals;
            var sourceUv = source.uv;
            var normalToHull = toHull.inverse.transpose;
            var normalToPiece = hullToPiece.inverse.transpose;
            for (var i = 0; i < source.vertexCount; i++)
            {
                var p = toHull.MultiplyPoint3x4(sourceVertices[i]);
                var n = normalToHull.MultiplyVector(sourceNormals[i]).normalized;
                vertices.Add(hullToPiece.MultiplyPoint3x4(p));
                normals.Add(normalToPiece.MultiplyVector(n).normalized);
                uv.Add(sourceUv[i]);
                var brush = Mathf.Sin(p.x * 4 + p.y * 6) * .14f + Mathf.Sin(p.z * 8 - p.y * 7) * .09f;
                var inward = Mathf.Clamp01(Vector3.Dot(n, -p.normalized) * .5f + .5f);
                var soot = Mathf.Clamp01(.25f + inward * .5f + brush);
                colors.Add(new Color(soot, soot, soot, 1));
            }
            var inkIndices = old.GetTriangles(inkSlot);
            var inkMap = new Dictionary<int, int>();
            var oldVertices = old.vertices;
            var oldNormals = old.normals;
            var oldUv = old.uv;
            var oldColors = old.colors;
            for (var i = 0; i < inkIndices.Length; i++)
            {
                var original = inkIndices[i];
                if (!inkMap.TryGetValue(original, out var replacement))
                {
                    replacement = vertices.Count;
                    inkMap.Add(original, replacement);
                    vertices.Add(oldVertices[original]);
                    normals.Add(oldNormals[original]);
                    uv.Add(oldUv[original]);
                    colors.Add(oldColors[original]);
                }
                inkIndices[i] = replacement;
            }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetColors(colors);
            mesh.subMeshCount = source.subMeshCount + 1;
            for (var i = 0; i < source.subMeshCount; i++) mesh.SetTriangles(source.GetTriangles(i), i);
            mesh.SetTriangles(inkIndices, source.subMeshCount);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static void Inspect()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(Visuals + "Vanguard.fbx");
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importBlendShapes = false;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.weldVertices = false;
            importer.isReadable = true;
            importer.SaveAndReimport();
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Visuals + "Vanguard.fbx");
            var report = new Inspection { current = Read(old), exported = Read(model) };
            File.WriteAllText(Output + "inspection.json", JsonUtility.ToJson(report, true));
            Debug.Log("VANGUARD_INSPECTION_PASS");
        }

        private static Node[] Read(GameObject root)
        {
            return root.GetComponentsInChildren<Transform>(true).Select(t =>
            {
                var filter = t.GetComponent<MeshFilter>();
                var mesh = filter ? filter.sharedMesh : null;
                var renderer = t.GetComponent<Renderer>();
                return new Node
                {
                    path = AnimationUtility.CalculateTransformPath(t, root.transform),
                    position = t.localPosition, rotation = t.localEulerAngles, scale = t.localScale,
                    worldPosition = t.position, worldRotation = t.eulerAngles, worldScale = t.lossyScale,
                    mesh = mesh ? AssetDatabase.GetAssetPath(mesh) : "",
                    boundsCenter = mesh ? mesh.bounds.center : Vector3.zero,
                    boundsSize = mesh ? mesh.bounds.size : Vector3.zero,
                    worldBoundsCenter = renderer ? renderer.bounds.center : Vector3.zero,
                    worldBoundsSize = renderer ? renderer.bounds.size : Vector3.zero,
                    vertices = mesh ? mesh.vertexCount : 0,
                    triangles = mesh ? Enumerable.Range(0, mesh.subMeshCount).Select(i => (int)mesh.GetIndexCount(i) / 3).ToArray() : Array.Empty<int>(),
                    materials = renderer ? renderer.sharedMaterials.Select(m => m ? m.name : "null").ToArray() : Array.Empty<string>()
                };
            }).ToArray();
        }
    }
}
