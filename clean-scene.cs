var camera=UnityEngine.Camera.main;
camera.orthographicSize=34f;
foreach (var canvas in UnityEngine.Object.FindObjectsByType<UnityEngine.Canvas>(UnityEngine.FindObjectsSortMode.None)) canvas.enabled=false;
var rows=new System.Collections.Generic.List<object>();
foreach(var ship in UnityEngine.Object.FindObjectsByType<Ships.Ship>(UnityEngine.FindObjectsSortMode.None)) {
 if(!ship.name.StartsWith("Capacity ")) continue;
 var point=camera.WorldToScreenPoint(ship.transform.position);
 var top=point.y;
 if(ship.VisualRig && ship.VisualRig.Silhouette.Count>0) {
  foreach(var renderer in ship.VisualRig.Silhouette) top=UnityEngine.Mathf.Max(top,camera.WorldToScreenPoint(new UnityEngine.Vector3(renderer.bounds.center.x,renderer.bounds.max.y,renderer.bounds.center.z)).y);
 } else {
  foreach(var renderer in ship.GetComponentsInChildren<UnityEngine.MeshRenderer>()) top=UnityEngine.Mathf.Max(top,camera.WorldToScreenPoint(renderer.bounds.max).y);
 }
 rows.Add(new {name=ship.name,x=point.x,y=720-top,hullMax=ship.Damage.Health.MaxValue,shieldMax=ship.Damage.Shield.MaxValue,hull=ship.Damage.Health.CurrentValue,shield=ship.Damage.Shield.CurrentValue});
}
return new {width=1280,height=720,orthographicSize=34,ships=rows};
