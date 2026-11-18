var factionName = "__FACTION__"; var minionRank = __MINION__; var outPath = "__OUT__";
var size = 768;
var faction = UnityEditor.AssetDatabase.LoadAssetAtPath<Substrate.Sectors.Factions.Faction>("Assets/Settings/Factions/" + factionName + ".asset");
var rank = minionRank ? faction.Minion : faction.Rival;
var root = new UnityEngine.GameObject("capture root");
var priorTarget = UnityEngine.RenderTexture.active;
var priorAmbient = UnityEngine.RenderSettings.ambientLight; var priorMode = UnityEngine.RenderSettings.ambientMode;
var target = new UnityEngine.RenderTexture(size, size, 24);
var image = new UnityEngine.Texture2D(size, size, UnityEngine.TextureFormat.RGB24, false);
var info = "";
try {
    root.SetActive(false);
    var subject = UnityEngine.Object.Instantiate(rank.ship.gameObject, root.transform);
    var rig = subject.GetComponentInChildren<Ships.Presentation.ShipVisualRig>(true);
    var slot = 0;
    foreach (var renderer in rig.Silhouette) {
        var mats = renderer.sharedMaterials;
        for (var i = 0; i < mats.Length; i++) mats[i] = rank.hull[slot++];
        renderer.sharedMaterials = mats;
    }
    info = rank.ship.name + " slots " + slot + "/" + rank.hull.Length + " scale " + rank.scale.Size;
    foreach (var behaviour in subject.GetComponentsInChildren<UnityEngine.MonoBehaviour>(true))
        if (!(behaviour.GetType().Namespace ?? "").StartsWith("UnityEngine", System.StringComparison.Ordinal)) UnityEngine.Object.DestroyImmediate(behaviour);
    foreach (var p in subject.GetComponentsInChildren<UnityEngine.ParticleSystemRenderer>(true)) p.enabled = false;
    subject.transform.SetLocalPositionAndRotation(UnityEngine.Vector3.zero, UnityEngine.Quaternion.identity);
    subject.transform.localScale *= rank.scale.Size;
    var camera = new UnityEngine.GameObject("Camera", typeof(UnityEngine.Camera)).GetComponent<UnityEngine.Camera>();
    camera.transform.SetParent(root.transform, false);
    camera.transform.position = new UnityEngine.Vector3(0, 0, -6);
    camera.transform.LookAt(UnityEngine.Vector3.zero, UnityEngine.Vector3.up);
    camera.orthographic = true; camera.orthographicSize = 2.4f;
    camera.cullingMask = 1 << Substrate.LayerIds.Ship;
    camera.allowHDR = false; camera.allowMSAA = false; camera.targetTexture = target;
    UnityEngine.RenderTexture.active = target;
    UnityEngine.GL.Clear(true, true, new UnityEngine.Color(.25f, .28f, .35f));
    camera.clearFlags = UnityEngine.CameraClearFlags.Depth;
    var light = new UnityEngine.GameObject("Key light", typeof(UnityEngine.Light)).GetComponent<UnityEngine.Light>();
    light.transform.SetParent(root.transform, false);
    light.type = UnityEngine.LightType.Directional; light.transform.rotation = UnityEngine.Quaternion.Euler(25, -35, 0);
    light.color = new UnityEngine.Color(1, .96f, .9f); light.intensity = 1.15f; light.cullingMask = 1 << Substrate.LayerIds.Ship;
    UnityEngine.RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
    UnityEngine.RenderSettings.ambientLight = new UnityEngine.Color(.22f, .25f, .32f);
    root.SetActive(true);
    camera.Render();
    UnityEngine.RenderTexture.active = target;
    image.ReadPixels(new UnityEngine.Rect(0, 0, size, size), 0, 0); image.Apply();
    System.IO.File.WriteAllBytes(outPath, UnityEngine.ImageConversion.EncodeToPNG(image));
} finally {
    UnityEngine.RenderTexture.active = priorTarget;
    UnityEngine.RenderSettings.ambientLight = priorAmbient; UnityEngine.RenderSettings.ambientMode = priorMode;
    UnityEngine.Object.DestroyImmediate(root); target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
}
return info;
