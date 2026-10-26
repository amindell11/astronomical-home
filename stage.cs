UnityEngine.Time.timeScale = 0f;
var host = UnityEngine.Object.FindFirstObjectByType<Game.GameHost>();
var units = host.GetComponent<Substrate.Services.Units.UnitService>();
var ships = UnityEngine.Object.FindObjectsByType<Ships.Ship>(UnityEngine.FindObjectsSortMode.None);
foreach (var s in ships) {
 if(s.teamNumber == 0) {s.name="Capacity player"; s.transform.position=new UnityEngine.Vector3(-14,-65,0); s.transform.rotation=UnityEngine.Quaternion.identity; s.Damage.Health.Reset();s.Damage.Shield.Reset();}
 else if(s.name.StartsWith("Valis")) {s.name="Capacity light enemy";s.transform.position=new UnityEngine.Vector3(0,-54,0);s.transform.rotation=UnityEngine.Quaternion.Euler(0,0,180);s.Damage.Health.Reset();s.Damage.Shield.Configure(50);}
 else units.DespawnShip(s);
}
var cargo = UnityEditor.AssetDatabase.LoadAssetAtPath<Ships.Ship>("Assets/Prefabs/Ships/Cargo Ships/Junker_1.prefab");
var heavy = units.SpawnShip(cargo,null,1,new UnityEngine.Vector3(15,-62,0),UnityEngine.Quaternion.Euler(0,0,150),host.ActiveSector.ObstacleField);
heavy.name="Capacity heavy enemy";
heavy.Damage.Shield.Configure(150);
var observer=UnityEngine.Object.FindFirstObjectByType<Cameras.ObserverCam>();
observer.enabled=false;
UnityEngine.Camera.main.transform.position=new UnityEngine.Vector3(0,-60,-50);
UnityEngine.Camera.main.orthographicSize=17;
return "staged player 65/32, light 80/50, heavy 400/150";
