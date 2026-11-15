// Places one station model (FBX or model prefab) at the player ship, freezes the flight camera there at a given size.
var NAME = "Repair Station";
var ZOOM = 15f;
var USE_PREFAB = false;
var rig = UnityEngine.Object.FindFirstObjectByType<Game.PlayerRig>();
if (!rig || !rig.Player) return "no player yet";
var ship = rig.Player.transform;
foreach (var t in UnityEngine.Object.FindObjectsByType<UnityEngine.Transform>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
    if (t && t.name == "STATION_PROOF") UnityEngine.Object.DestroyImmediate(t.gameObject);
var body = ship.GetComponent<UnityEngine.Rigidbody>();
if (body) { body.linearVelocity = UnityEngine.Vector3.zero; body.angularVelocity = UnityEngine.Vector3.zero; }
var path = "Assets/Visuals/Environment/Stations/" + NAME + "/" + NAME + (USE_PREFAB ? " Model.prefab" : ".fbx");
var model = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
var go = (UnityEngine.GameObject)UnityEngine.Object.Instantiate(model, ship.position, UnityEngine.Quaternion.Euler(31.5f, 20.17f, 29.38f));
go.name = "STATION_PROOF";
var cams = UnityEngine.Object.FindObjectsByType<Cameras.ObserverCam>(UnityEngine.FindObjectsSortMode.None);
var report = "ship " + ship.position.ToString("F1") + "; cams " + cams.Length;
foreach (var oc in cams)
{
    if (!oc.Cam || !oc.Cam.isActiveAndEnabled) continue;
    oc.enabled = false;
    var t = oc.transform;
    var depth = UnityEngine.Vector3.Dot(t.position - ship.position, t.forward);
    t.position = ship.position + t.forward * depth;
    oc.Cam.orthographicSize = ZOOM;
    report += "; cam " + oc.Cam.name + " ortho " + oc.Cam.orthographic + " size " + oc.Cam.orthographicSize + " fwd " + t.forward.ToString("F2");
}
return report;
