// Freezes the staged station and centres the flight camera on a lamp: of the renderers named PREFIX*, the first lamp farthest from the station's centre.
var PREFIX = "__PREFIX__";
var ZOOM = __Z__f;
var go = UnityEngine.GameObject.Find("STATION_PROOF");
if (!go) return "no station";
go.GetComponent<Substrate.Sectors.Elements.Stations.Visual.StationMotion>().enabled = false;
UnityEngine.MeshFilter hit = null;
var centre = UnityEngine.Vector3.zero;
var best = -1f;
foreach (var f in go.GetComponentsInChildren<UnityEngine.MeshFilter>(true))
{
    if (!f.name.StartsWith(PREFIX)) continue;
    var verts = f.sharedMesh.vertices;
    var c = UnityEngine.Vector3.zero;
    var n = System.Math.Min(8, verts.Length);
    for (int i = 0; i < n; i++) c += f.transform.TransformPoint(verts[i]);
    c /= n;
    var d = (c - go.transform.position).magnitude;
    if (d > best) { best = d; hit = f; centre = c; }
}
if (!hit) return "no renderer starting " + PREFIX;
foreach (var oc in UnityEngine.Object.FindObjectsByType<Cameras.ObserverCam>(UnityEngine.FindObjectsSortMode.None))
    if (oc.Cam && oc.Cam.enabled) { oc.enabled = false; var p = oc.transform.position; oc.transform.position = new UnityEngine.Vector3(centre.x, centre.y, p.z); oc.Cam.orthographicSize = ZOOM; }
return "lamp " + hit.name + " at " + centre.ToString("F2") + " size " + ZOOM;
