var host = UnityEngine.Object.FindFirstObjectByType<Game.GameHost>();
var units = host.GetComponent<Substrate.Services.Units.UnitService>();
var matches=UnityEditor.AssetDatabase.FindAssets("Valis t:Prefab",new[]{"Assets/Prefabs/Ships"});
var path=System.Array.Find(System.Array.ConvertAll(matches,UnityEditor.AssetDatabase.GUIDToAssetPath),p=>p.EndsWith("/Valis.prefab"));
var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<Ships.Ship>(path);
var ship=units.SpawnShip(prefab,null,1,new UnityEngine.Vector3(0,-54,0),UnityEngine.Quaternion.Euler(0,0,180),host.ActiveSector.ObstacleField);
ship.name="Capacity light enemy";
ship.Damage.Shield.Configure(50);
return new {path,ship.name,hull=ship.Damage.Health.MaxValue,shield=ship.Damage.Shield.MaxValue};
