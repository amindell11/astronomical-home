using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Ships.Visuals.Breakup;
using UnityEditor;
using UnityEngine;

public static class BuildValisBreakup
{
    private const string Work = "D:/amind/git/agent-1/scratch/valis-breakup/";
    private const string Folder = "Assets/Visuals/Ships/Valis/Breakup";
    private sealed class Fragment
    {
        public string name;
        public int bone;
        public float lifetime;
        public List<int>[] sections = Enumerable.Range(0, 8).Select(_ => new List<int>()).ToArray();
    }
    private static string Group(string name)
    {
        var id = int.Parse(name.Substring(0, 2));
        if (id == 13) return "Upper spar";
        if (id == 10 || id == 11 || id == 12 || id == 70 || id == 71) return "Upper wing";
        if (id == 22) return "Lower inner spar";
        if (id == 20 || id == 21) return "Lower blade";
        if (id == 40 || id == 41 || id == 42) return "Forward outrigger";
        if (id == 50 || id == 51 || id == 52 || id == 53) return "Aft prong";
        if (id >= 60 && id <= 64) return "Engine spine";
        if (id == 4 || id == 5 || id == 6 || id == 7) return "Dorsal armor";
        if (id == 8) return "Ventral armor";
        if (id == 1 || id == 2 || id == 3) return "Fuselage and canopy";
        throw new Exception("Unassigned approved part " + name);
    }
    private static float Lifetime(string group)
    {
        switch (group)
        {
            case "Upper wing": case "Fuselage and canopy": return 1.4f;
            case "Aft prong": return 1.35f;
            case "Lower blade": return 1.25f;
            case "Dorsal armor": return 1.18f;
            case "Upper spar": return 1.15f;
            case "Engine spine": return 1.1f;
            case "Ventral armor": return 1.05f;
            case "Lower inner spar": return .98f;
            case "Forward outrigger": return .94f;
            default: throw new Exception(group);
        }
    }
    private static void Curve(AnimationClip clip, string path, string property, float start, float delta, float delay)
    {
        var curve = new AnimationCurve(new Keyframe(0f, start), new Keyframe(delay, start),
            new Keyframe(delay + .12f, start + delta * .62f), new Keyframe(.34f, start + delta * .78f), new Keyframe(1.4f, start + delta));
        for (var i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
        }
        clip.SetCurve(path, typeof(Transform), property, curve);
    }
    public static string Main()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Visuals/Ships/Valis", "Breakup");
        if (!AssetDatabase.IsValidFolder(Folder + "/Pieces")) AssetDatabase.CreateFolder(Folder, "Pieces");
        const string shipPath = "Assets/Prefabs/Ships/Valis.prefab";
        var ship = PrefabUtility.LoadPrefabContents(shipPath);
        var debris = new GameObject("ValisBreakup");
        try
        {
            var source = ship.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "Valis");
            var mesh = source.sharedMesh;
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var uv = mesh.uv;
            var weights = mesh.boneWeights;
            var fragments = new Dictionary<string, Fragment>();
            var data = JObject.Parse(File.ReadAllText(Work + "approved-export.json"));
            var sourceSections = new int[mesh.vertexCount];
            for (var s = 0; s < mesh.subMeshCount; s++)
                foreach (var v in mesh.GetTriangles(s)) sourceSections[v] = s;
            var cursor = 0;
            foreach (var part in data["parts"])
            {
                var group = Group((string)part["name"]);
                var count = part["vertices"].Count();
                for (var layer = 0; layer < 2; layer++)
                    for (var t = 0; t < count; t += 3)
                    {
                        var index = cursor + layer * count + t;
                        var paired = group.StartsWith("Upper") || group.StartsWith("Lower") || group == "Forward outrigger" || group == "Aft prong";
                        var side = (vertices[index].x + vertices[index + 1].x + vertices[index + 2].x) >= 0f ? " right" : " left";
                        var key = group + (paired ? side : "");
                        Fragment fragment;
                        if (!fragments.TryGetValue(key, out fragment))
                        {
                            fragment = new Fragment { name = key, bone = weights[index].boneIndex0, lifetime = Lifetime(group) };
                            fragments.Add(key, fragment);
                        }
                        for (var k = 0; k < 3; k++)
                        {
                            if (weights[index + k].boneIndex0 != fragment.bone) throw new Exception("Mixed rigid bones in " + key);
                            fragment.sections[sourceSections[index + k]].Add(index + k);
                        }
                    }
                cursor += count * 2;
            }
            if (cursor != mesh.vertexCount || fragments.Count != 16) throw new Exception("Approved geometry partition differs from the live hull.");
            debris.layer = source.gameObject.layer;
            var poses = new Transform[4];
            for (var i = 0; i < poses.Length; i++)
            {
                poses[i] = new GameObject(source.bones[i + 1].name + " pose").transform;
                poses[i].SetParent(debris.transform, false);
                poses[i].localPosition = source.bones[i + 1].localPosition;
                poses[i].localRotation = source.bones[i + 1].localRotation;
            }
            var clip = new AnimationClip { name = "Valis burst", legacy = true, frameRate = 60f, wrapMode = WrapMode.ClampForever };
            var renderers = new List<Renderer>();
            var lifetimes = new List<float>();
            var proof = new List<object>();
            var order = 0;
            foreach (var fragment in fragments.Values)
            {
                var indices = fragment.sections.SelectMany(s => s).Distinct().OrderBy(i => i).ToArray();
                var remap = indices.Select((v, i) => new { v, i }).ToDictionary(x => x.v, x => x.i);
                var bind = mesh.bindposes[fragment.bone];
                var positions = indices.Select(i => bind.MultiplyPoint3x4(vertices[i])).ToArray();
                var center = positions.Aggregate(Vector3.zero, (a, b) => a + b) / positions.Length;
                var piece = new Mesh { name = fragment.name };
                piece.SetVertices(positions.Select(p => p - center).ToArray());
                piece.SetNormals(indices.Select(i => bind.MultiplyVector(normals[i]).normalized).ToArray());
                piece.SetUVs(0, indices.Select(i => uv[i]).ToArray());
                piece.subMeshCount = 8;
                for (var s = 0; s < 8; s++) piece.SetTriangles(fragment.sections[s].Select(i => remap[i]).ToArray(), s);
                piece.RecalculateBounds();
                piece.RecalculateTangents();
                var meshPath = Folder + "/Pieces/" + fragment.name + ".asset";
                var prior = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (prior) { EditorUtility.CopySerialized(piece, prior); UnityEngine.Object.DestroyImmediate(piece); piece = prior; }
                else AssetDatabase.CreateAsset(piece, meshPath);
                var part = new GameObject(fragment.name);
                part.layer = source.gameObject.layer;
                part.transform.SetParent(fragment.bone == 0 ? debris.transform : poses[fragment.bone - 1], false);
                part.transform.localPosition = center;
                part.AddComponent<MeshFilter>().sharedMesh = piece;
                var renderer = part.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = source.sharedMaterials;
                renderer.shadowCastingMode = source.shadowCastingMode;
                renderer.receiveShadows = source.receiveShadows;
                renderers.Add(renderer);
                lifetimes.Add(fragment.lifetime);
                var globalCenter = bind.inverse.MultiplyPoint3x4(center);
                var side = globalCenter.x >= 0f ? 1f : -1f;
                var outward = new Vector3(globalCenter.x * 1.6f, globalCenter.y * .8f, 0f);
                if (fragment.name == "Fuselage and canopy") outward = new Vector3(.12f, .32f, 0f);
                if (fragment.name == "Dorsal armor") outward = new Vector3(-.24f, .48f, -.18f);
                if (fragment.name == "Ventral armor") outward = new Vector3(.2f, -.36f, .16f);
                if (fragment.name == "Engine spine") outward = new Vector3(-.18f, -.62f, .08f);
                var distance = fragment.bone > 0 ? 1.05f : fragment.name.StartsWith("Aft") ? .72f : .55f;
                var delta = outward.normalized * distance;
                delta.z += (order % 3 - 1) * .09f;
                var spin = new Vector3((order % 2 == 0 ? 1f : -1f) * 26f, side * 38f, side * (42f + order * 3f));
                var path = AnimationUtility.CalculateTransformPath(part.transform, debris.transform);
                var delay = .015f + order % 5 * .008f;
                for (var axis = 0; axis < 3; axis++)
                {
                    var suffix = axis == 0 ? "x" : axis == 1 ? "y" : "z";
                    Curve(clip, path, "localPosition." + suffix, center[axis], delta[axis], delay);
                    Curve(clip, path, "localEulerAnglesRaw." + suffix, 0f, spin[axis], delay);
                }
                proof.Add(new { fragment.name, fragment.bone, vertices = piece.vertexCount, fragment.lifetime });
                order++;
            }
            var clipPath = Folder + "/Valis burst.anim";
            var previousClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (previousClip) { EditorUtility.CopySerialized(clip, previousClip); UnityEngine.Object.DestroyImmediate(clip); clip = previousClip; }
            else AssetDatabase.CreateAsset(clip, clipPath);
            var animation = debris.AddComponent<Animation>();
            animation.AddClip(clip, clip.name);
            animation.clip = clip;
            animation.playAutomatically = false;
            var component = debris.AddComponent<ShipBreakupDebris>();
            var serialized = new SerializedObject(component);
            var pieces = serialized.FindProperty("pieces");
            pieces.arraySize = renderers.Count;
            for (var i = 0; i < renderers.Count; i++)
            {
                var entry = pieces.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("renderer").objectReferenceValue = renderers[i];
                entry.FindPropertyRelative("lifetime").floatValue = lifetimes[i];
            }
            var poseRoots = serialized.FindProperty("poseRoots");
            poseRoots.arraySize = poses.Length;
            for (var i = 0; i < poses.Length; i++) poseRoots.GetArrayElementAtIndex(i).objectReferenceValue = poses[i];
            var vanguard = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Visuals/Ships/Vanguard/Breakup/VanguardBreakup.prefab").GetComponent<ShipBreakupDebris>();
            serialized.FindProperty("explosionPrefab").objectReferenceValue = new SerializedObject(vanguard).FindProperty("explosionPrefab").objectReferenceValue;
            serialized.FindProperty("motion").objectReferenceValue = animation;
            serialized.FindProperty("lifetime").floatValue = 1.4f;
            serialized.FindProperty("fadeDuration").floatValue = .25f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(debris, Folder + "/ValisBreakup.prefab");
            var old = source.GetComponent<ShipBreakupVisual>();
            if (old) UnityEngine.Object.DestroyImmediate(old);
            var rig = source.GetComponentInParent<Ships.Presentation.ShipVisualRig>();
            var breakup = rig.GetComponent<ShipBreakupVisual>();
            if (!breakup) breakup = rig.gameObject.AddComponent<ShipBreakupVisual>();
            var wiring = new SerializedObject(breakup);
            wiring.FindProperty("hull").objectReferenceValue = source.transform;
            wiring.FindProperty("debrisPrefab").objectReferenceValue = prefab.GetComponent<ShipBreakupDebris>();
            var sources = wiring.FindProperty("poseSources");
            sources.arraySize = 4;
            for (var i = 0; i < 4; i++) sources.GetArrayElementAtIndex(i).objectReferenceValue = source.bones[i + 1];
            wiring.ApplyModifiedPropertiesWithoutUndo();
            var hullVisuals = new SerializedObject(rig.GetComponent<Ships.Visuals.HullVisuals>());
            hullVisuals.FindProperty("explosionPrefab").objectReferenceValue = null;
            hullVisuals.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(ship, shipPath);
            AssetDatabase.SaveAssets();
            File.WriteAllText(Work + "build-proof.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { fragments = proof, totalVertices = renderers.Sum(r => r.GetComponent<MeshFilter>().sharedMesh.vertexCount), sourceVertices = mesh.vertexCount, poseParents = poses.Length }, Newtonsoft.Json.Formatting.Indented));
            return "Authored 16 Valis fragments, four pose parents, production explosion and matching 1.4-second burst.";
        }
        finally { PrefabUtility.UnloadPrefabContents(ship); UnityEngine.Object.DestroyImmediate(debris); }
    }
}


