// Hides asteroids within 60 u of the player and stops asteroid spawners, so captures show the station alone.
var rig = UnityEngine.Object.FindFirstObjectByType<Game.PlayerRig>();
if (!rig || !rig.Player) return "no player";
var at = rig.Player.transform.position;
int spawners = 0, hidden = 0;
foreach (var s in UnityEngine.Object.FindObjectsByType<Asteroids.Spawning.AsteroidSpawner>(UnityEngine.FindObjectsSortMode.None)) { if (s.enabled) { s.enabled = false; spawners++; } }
foreach (var a in UnityEngine.Object.FindObjectsByType<Asteroids.AsteroidController>(UnityEngine.FindObjectsSortMode.None))
    if ((a.transform.position - at).magnitude < 60f) { a.gameObject.SetActive(false); hidden++; }
return "asteroid spawners off " + spawners + ", asteroids hidden " + hidden;
