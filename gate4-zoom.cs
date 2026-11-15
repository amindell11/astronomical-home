var ZOOM = __ZOOM__f;
foreach (var oc in UnityEngine.Object.FindObjectsByType<Cameras.ObserverCam>(UnityEngine.FindObjectsSortMode.None))
    if (oc.Cam && oc.Cam.enabled) { oc.enabled = false; oc.Cam.orthographicSize = ZOOM; }
return "zoom " + ZOOM;
