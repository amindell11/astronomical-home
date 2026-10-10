using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Ships.Presentation;
using Ships.Visuals;
using Ships.Visuals.Breakup;
using Utils;

public static class BuildNightshadeBreakup
{
    const string Scratch = "C:/Users/amind/.codex/visualizations/2026/10/06/01a112c7-f840-7683-a496-b2b5e5eabf06/breakup-01/";
    const string Folder = "Assets/Visuals/Ships/Nightshade/Breakup/";
    const string Chassis = "Assets/Prefabs/Ships/Nightshade.prefab";
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play before authoring.");
        var settings = JObject.Parse(File.ReadAllText("../../art/ships/nightshade/breakup.json"));
        var sidecar = JObject.Parse(File.ReadAllText(Scratch + "export/NightshadeBreakup.export.json"));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var modelPath = Folder + "NightshadeBreakup.fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        importer.isReadable = true;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.None;
        importer.weldVertices = false;
        importer.importAnimation = false;
        importer.SaveAndReimport();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var meshes = model.GetComponentsInChildren<MeshFilter>().ToDictionary(m => m.sharedMesh.name, m => m.sharedMesh);
        var pieces = (JArray)settings["pieces"];
        if (!meshes.Keys.OrderBy(n => n).SequenceEqual(pieces.Select(p => (string)p["id"]).OrderBy(n => n)))
            throw new InvalidOperationException("Settings and source debris groups differ.");
        var materials = new Dictionary<string, Material>();
        foreach (var slot in ((JObject)sidecar["roles"]).Properties().SelectMany(p => p.Value["material_slots"].Values<string>()).Distinct())
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/Visuals/Ships/Nightshade/Materials/" + slot + ".mat");
            if (!source) throw new InvalidOperationException("Missing source material " + slot);
            var path = Folder + slot + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(source); AssetDatabase.CreateAsset(material, path); }
            else EditorUtility.CopySerialized(source, material);
            material.name = source.name;
            material.renderQueue = slot == "Contour" ? 3101 : 3100;
            if (slot != "Contour") material.SetFloat("_SootStrength", (float)settings["soot"]);
            materials.Add(slot, material);
            EditorUtility.SetDirty(material);
        }
        var clip = new AnimationClip { name = "Nightshade burst", legacy = true, frameRate = 60, wrapMode = WrapMode.ClampForever };
        var root = new GameObject("NightshadeBreakup");
        root.SetActive(false);
        var renderers = new List<Renderer>();
        var pivots = new Dictionary<string, Vector3>();
        foreach (var piece in pieces)
        {
            var id = (string)piece["id"];
            var mesh = meshes[id];
            var pivot = Quaternion.Euler(0, 180, 0) * (mesh.bounds.center * 100);
            pivots[id] = pivot;
            var section = new GameObject(id);
            section.transform.SetParent(root.transform, false);
            section.transform.localPosition = pivot;
            var geometry = new GameObject("Surface");
            geometry.transform.SetParent(section.transform, false);
            geometry.transform.localPosition = -pivot;
            geometry.transform.localRotation = Quaternion.Euler(0, 180, 0);
            geometry.transform.localScale = Vector3.one * 100;
            geometry.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = geometry.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = model.GetComponentsInChildren<MeshRenderer>().Single(r => r.GetComponent<MeshFilter>().sharedMesh == mesh).sharedMaterials.Select(m => materials[m.name]).ToArray();
            renderer.sortingOrder = 4;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderers.Add(renderer);
            var delay = (float)piece["delay"];
            var times = delay > 0 ? new[] {0f, delay, delay+(float)settings["burst_seconds"], (float)settings["drift_start"], (float)settings["lifetime"]}
                                  : new[] {0f, (float)settings["burst_seconds"], (float)settings["drift_start"], (float)settings["lifetime"]};
            var fractions = delay > 0 ? new[] {0f, 0f, (float)settings["burst_fraction"], (float)settings["drift_fraction"], 1f}
                                      : new[] {0f, (float)settings["burst_fraction"], (float)settings["drift_fraction"], 1f};
            var offset = Vector(piece["offset"]);
            var spin = Vector(piece["spin"]);
            for (var axis=0; axis<3; axis++) Curve(clip,id,"localPosition."+"xyz"[axis],times,fractions.Select(f => pivot[axis]+offset[axis]*f).ToArray());
            for (var axis=0; axis<4; axis++) Curve(clip,id,"localRotation."+"xyzw"[axis],times,fractions.Select(f => Quaternion.Euler(spin*f)[axis]).ToArray());
        }
        clip.EnsureQuaternionContinuity();
        var clipPath = Folder + "Nightshade burst.anim";
        var savedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (savedClip) { EditorUtility.CopySerialized(clip, savedClip); UnityEngine.Object.DestroyImmediate(clip); }
        else { AssetDatabase.CreateAsset(clip, clipPath); savedClip=clip; }
        var motion = root.AddComponent<Animation>();
        motion.clip = savedClip;
        motion.AddClip(savedClip, savedClip.name);
        motion.playAutomatically = false;
        var debris = root.AddComponent<ShipBreakupDebris>();
        var crimson = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Visuals/Ships/Crimson/Breakup/CrimsonBreakup.prefab").GetComponent<ShipBreakupDebris>();
        var data = new SerializedObject(debris);
        data.FindProperty("motion").objectReferenceValue=motion;
        data.FindProperty("explosionPrefab").objectReferenceValue=new SerializedObject(crimson).FindProperty("explosionPrefab").objectReferenceValue;
        data.FindProperty("lifetime").floatValue=(float)settings["lifetime"];
        data.FindProperty("fadeDuration").floatValue=(float)settings["fade_duration"];
        var array=data.FindProperty("pieces"); array.arraySize=pieces.Count;
        for(var i=0;i<pieces.Count;i++) { var p=array.GetArrayElementAtIndex(i); p.FindPropertyRelative("renderer").objectReferenceValue=renderers[i]; p.FindPropertyRelative("lifetime").floatValue=(float)pieces[i]["lifetime"]; }
        data.ApplyModifiedPropertiesWithoutUndo();
        root.SetActive(true);
        var saved = PrefabUtility.SaveAsPrefabAsset(root,Folder+"NightshadeBreakup.prefab");
        UnityEngine.Object.DestroyImmediate(root);
        var ship = PrefabUtility.LoadPrefabContents(Chassis);
        try
        {
            var rig=ship.GetComponentInChildren<ShipVisualRig>(true);
            var hull=rig.transform.Find("Hull");
            var breakup=rig.GetComponent<ShipBreakupVisual>();
            if(!breakup) breakup=rig.gameObject.AddComponent<ShipBreakupVisual>();
            var visual=new SerializedObject(breakup);
            visual.FindProperty("hull").objectReferenceValue=hull;
            visual.FindProperty("debrisPrefab").objectReferenceValue=saved.GetComponent<ShipBreakupDebris>();
            visual.ApplyModifiedPropertiesWithoutUndo();
            var feedback=new SerializedObject(rig.GetComponentInChildren<HullVisuals>(true));
            feedback.FindProperty("explosionPrefab").objectReferenceValue=null;
            feedback.FindProperty("collisionExplosionPrefab").objectReferenceValue=null;
            feedback.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(ship,Chassis);
        }
        finally { PrefabUtility.UnloadPrefabContents(ship); }
        AssetDatabase.SaveAssets();
        return new {pieces=pieces.Count,pivots=pivots.ToDictionary(p=>p.Key,p=>p.Value.ToString("F4")),prefab=Folder+"NightshadeBreakup.prefab"};
    }
    static Vector3 Vector(JToken t) => new Vector3((float)t[0],(float)t[1],(float)t[2]);
    static void Curve(AnimationClip clip,string path,string property,float[] times,float[] values)
    {
        var curve=new AnimationCurve(times.Select((t,i)=>new Keyframe(t,values[i])).ToArray());
        for(var i=0;i<times.Length;i++) { AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear); AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear); }
        clip.SetCurve(path,typeof(Transform),property,curve);
    }
}
