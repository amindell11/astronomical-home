var host = UnityEngine.Object.FindFirstObjectByType<Game.Preview.PreviewHost>();
var observer = host.Playtest.GetComponentInChildren<Cameras.ObserverCam>();
observer.enabled = false;
observer.transform.position = new UnityEngine.Vector3(-4, -1, -50);
observer.Cam.orthographicSize = 18;
var root = UnityEngine.GameObject.Find("Extraction comparison");
if (root) UnityEngine.Object.DestroyImmediate(root);
host.Playtest.GetComponentInChildren<Game.PlayerRig>().Overlay.ObjectiveMarker.enabled = true;
return "Fixed the same actual-game camera pose for every layout comparison.";
