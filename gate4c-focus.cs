var X = __X__f; var Y = __Y__f; var ZOOM = __Z__f;
foreach (var oc in UnityEngine.Object.FindObjectsByType<Cameras.ObserverCam>(UnityEngine.FindObjectsSortMode.None))
    if (oc.Cam && oc.Cam.enabled) { oc.enabled = false; var p = oc.transform.position; oc.transform.position = new UnityEngine.Vector3(X, Y, p.z); oc.Cam.orthographicSize = ZOOM; }
return "focus " + X + "," + Y + " size " + ZOOM;
