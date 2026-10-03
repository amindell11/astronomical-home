using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Ships.Presentation;
using Ships.Visuals;
using Object = UnityEngine.Object;

public static class IntegrateIllustrated
{
    const string Study = "Assets/Visuals/Studies/DrawnArt/";
    const string Crimson = "Assets/Visuals/Ships/Crimson/";
    const string Ships = "Assets/Prefabs/Ships/";
    const string Shared = "Assets/Visuals/Ships/Shared/Illustrated/";

    public static string Build()
    {
        Directory.CreateDirectory(Shared);
        AssetDatabase.Refresh();
        var importer = (ModelImporter)AssetImporter.GetAtPath(Crimson + "Crimson.fbx");
        importer.isReadable = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.SaveAndReimport();
        var texture = (TextureImporter)AssetImporter.GetAtPath(Crimson + "Crimson_BaseColor.png");
        texture.maxTextureSize = 4096;
        texture.SaveAndReimport();
        var outline = new Material(Load<Material>(Study + "Materials/Ship/Outline/Gameplay outline.mat"));
        outline.renderQueue = -1;
        outline = Save(outline, Shared + "Ship contour.mat");
        var paint = new Material(Load<Material>(Study + "Materials/Ship/Surface/Hull paint.mat"));
        paint.SetTexture("_BaseMap", Load<Texture2D>(Crimson + "Crimson_BaseColor.png"));
        paint.SetVector("_OrangeGain", Vector4.one);
        paint = Save(paint, Crimson + "Hull paint.mat");
        for (int i = 1; i <= 2; i++) BuildRig(i, outline, paint);
        for (int i = 1; i <= 3; i++) WireShip(i);
        importer.isReadable = false;
        importer.SaveAndReimport();
        var rock = Load<Material>("Assets/Visuals/Vfx/LayeredExplosion/Materials/AsteroidContour.mat");
        rock.renderQueue = -1;
        EditorUtility.SetDirty(rock);
        var settings = new SerializedObject(Load<Object>("Assets/Settings/Asteroids/SpawnSettings.asset"));
        settings.FindProperty("asteroidPrefab").objectReferenceValue = Load<GameObject>("Assets/Visuals/Vfx/LayeredExplosion/Prefabs/FragmentingDrawnAsteroid.prefab").GetComponent<Asteroids.AsteroidController>();
        settings.ApplyModifiedPropertiesWithoutUndo();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Environments/Environment_3.unity");
        var lights = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Light>()).Where(l=>l.type==LightType.Directional).ToArray();
        var key = lights.Length == 0 ? new GameObject("Illustrated key light").AddComponent<Light>() : lights[0];
        key.type = LightType.Directional;
        key.transform.rotation = Quaternion.Euler(25,-35,0);
        key.color = new Color(1,.96f,.90f);
        key.intensity = 1.15f;
        key.shadows = LightShadows.Soft;
        key.shadowBias = .02f;
        key.shadowNormalBias = .05f;
        RenderSettings.sun = key;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.22f,.25f,.32f);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return "Saved production rigs, collider meshes, asteroid settings, contour materials and environment lighting.";
    }

    static void BuildRig(int index, Material outline, Material paint)
    {
        var path = Ships + $"Ship_{index}_IllustratedRig.prefab";
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(path)) AssetDatabase.CopyAsset(Ships+$"Ship_{index}_VisualRig.prefab",path);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            root.transform.localScale = Vector3.one;
            var model = root.transform.Find("Model");
            foreach(var child in model.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            foreach(var component in model.GetComponents<Component>()) if (!(component is Transform)) Object.DestroyImmediate(component);
            model.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
            model.localScale = Vector3.one;
            var visual = Object.Instantiate(Load<GameObject>(index==1 ? Study+"Prefabs/VanguardPreview.prefab" : Crimson+"Crimson.fbx"));
            visual.name = index==1 ? "Vanguard" : "Crimson";
            visual.transform.rotation = index==1 ? Quaternion.Euler(0,0,180) : Quaternion.LookRotation(Vector3.down,Vector3.back);
            var surfaces = visual.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name!="Silhouette").ToArray();
            var bounds = surfaces[0].bounds;
            foreach(var r in surfaces) bounds.Encapsulate(r.bounds);
            var scale = 2f/bounds.size.y;
            visual.transform.localScale *= scale;
            visual.transform.position = -bounds.center*scale;
            visual.transform.SetParent(model,true);
            foreach(var t in visual.GetComponentsInChildren<Transform>()) t.gameObject.layer = 7;
            foreach(var r in surfaces)
            {
                if(index!=2) continue;
                r.sharedMaterial = paint;
                var mesh = Object.Instantiate(r.GetComponent<MeshFilter>().sharedMesh);
                var vertices=mesh.vertices; var normals=mesh.normals;
                var joined=new Dictionary<Vector3,Vector3>();
                for(int v=0;v<vertices.Length;v++) { joined.TryGetValue(vertices[v],out var n); joined[vertices[v]]=n+normals[v]; }
                for(int v=0;v<vertices.Length;v++) normals[v]=joined[vertices[v]].normalized;
                mesh.normals=normals;
                mesh=Save(mesh,Crimson+r.name+" outline.asset");
                var shell=new GameObject("Silhouette",typeof(MeshFilter),typeof(MeshRenderer));
                shell.layer=7;
                shell.transform.SetParent(r.transform,false);
                shell.GetComponent<MeshFilter>().sharedMesh=mesh;
                var ink=shell.GetComponent<MeshRenderer>();
                ink.sharedMaterial=outline; ink.shadowCastingMode=ShadowCastingMode.Off; ink.receiveShadows=false;
            }
            foreach(var r in visual.GetComponentsInChildren<MeshRenderer>()) if(r.name=="Silhouette") r.sharedMaterial=outline;
            var hull=root.GetComponentInChildren<HullVisuals>(true);
            hull.enabled=false;
            var serialized=new SerializedObject(hull);
            serialized.FindProperty("hull").objectReferenceValue=null;
            var smoke=(ParticleSystem)serialized.FindProperty("smoke").objectReferenceValue;
            smoke.gameObject.SetActive(false);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var proxy=BuildProxy(surfaces,root.transform);
            proxy=Save(proxy,Shared+$"Ship_{index} collider.asset");
            var marker=root.GetComponentInChildren<MinimapShipMarker>(true);
            marker.GetComponent<MeshFilter>().sharedMesh=proxy;
            marker.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
            marker.transform.localScale=Vector3.one*2;
            var engines=surfaces.Where(r=>index==1 ? r.name.Contains("Nacelle Core") : r.name=="Engine nozzle").OrderBy(r=>r.bounds.center.x).ToArray();
            if(engines.Length==0) throw new InvalidOperationException("Missing authored engines");
            foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("ThrustMain")||t.name.StartsWith("ThrustSmall")))
            {
                var engine=engines.Length==1?engines[0]:(t.name.Contains("(1)")?engines[0]:engines[engines.Length-1]);
                t.position=new Vector3(engine.bounds.center.x,engine.bounds.min.y,engine.bounds.center.z);
            }
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static Mesh BuildProxy(MeshRenderer[] surfaces,Transform root)
    {
        var points=new List<Vector2>(); float low=float.MaxValue,high=float.MinValue;
        foreach(var r in surfaces)
        {
            if(r.name.Contains("ink")||r.name.Contains("wear")||r.name.Contains("panels")) continue;
            foreach(var v in r.GetComponent<MeshFilter>().sharedMesh.vertices)
            {
                var p=root.InverseTransformPoint(r.transform.TransformPoint(v));
                points.Add(new Vector2(p.x,p.y)); low=Mathf.Min(low,p.z); high=Mathf.Max(high,p.z);
            }
        }
        points=points.Distinct().OrderBy(p=>p.x).ThenBy(p=>p.y).ToList();
        var hull=new List<Vector2>();
        foreach(var p in points) { while(hull.Count>=2&&Cross(hull[hull.Count-2],hull[hull.Count-1],p)<=0) hull.RemoveAt(hull.Count-1); hull.Add(p); }
        var lower=hull.Count;
        for(int i=points.Count-2;i>=0;i--) { var p=points[i]; while(hull.Count>lower&&Cross(hull[hull.Count-2],hull[hull.Count-1],p)<=0) hull.RemoveAt(hull.Count-1); hull.Add(p); }
        hull.RemoveAt(hull.Count-1);
        var n=hull.Count; var vertices=new Vector3[n*2]; var triangles=new List<int>();
        for(int i=0;i<n;i++) { vertices[i]=new Vector3(hull[i].x,hull[i].y,low); vertices[i+n]=new Vector3(hull[i].x,hull[i].y,high); }
        for(int i=1;i<n-1;i++) { triangles.AddRange(new[]{0,i+1,i,n,n+i,n+i+1}); }
        for(int i=0;i<n;i++) { int j=(i+1)%n; triangles.AddRange(new[]{i,j,j+n,i,j+n,i+n}); }
        var mesh=new Mesh { vertices=vertices,triangles=triangles.ToArray() }; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }
    static float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);

    static void WireShip(int index)
    {
        var path=Ships+$"Ship_{index}.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var rigs=root.GetComponentsInChildren<ShipVisualRig>(true);
            var rig=rigs.Single(r=>r.name.StartsWith($"Ship_{index}_"));
            foreach(var extra in rigs.Where(r=>r!=rig)) Object.DestroyImmediate(extra.gameObject);
            if(index==3) { PrefabUtility.SaveAsPrefabAsset(root,path); return; }
            PrefabUtility.ReplacePrefabAssetOfPrefabInstance(rig.gameObject,Load<GameObject>(Ships+$"Ship_{index}_IllustratedRig.prefab"),InteractionMode.AutomatedAction);
            var mesh=root.GetComponentInChildren<MeshCollider>();
            mesh.sharedMesh=Load<Mesh>(Shared+$"Ship_{index} collider.asset");
            mesh.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
            mesh.transform.localScale=Vector3.one;
            PrefabUtility.RecordPrefabInstancePropertyModifications(mesh);
            PrefabUtility.RecordPrefabInstancePropertyModifications(mesh.transform);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static T Load<T>(string path) where T:Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException(path);
    static T Save<T>(T value,string path) where T:Object
    {
        value.name=Path.GetFileNameWithoutExtension(path);
        var old=AssetDatabase.LoadAssetAtPath<T>(path);
        if(!old) { AssetDatabase.CreateAsset(value,path); return value; }
        EditorUtility.CopySerialized(value,old); Object.DestroyImmediate(value); EditorUtility.SetDirty(old); return old;
    }
}

