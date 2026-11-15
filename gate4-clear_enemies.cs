// Stops wave spawning and removes every ship that is not the player, so captures show the station alone.
int spawners = 0, removed = 0;
foreach (var s in UnityEngine.Object.FindObjectsByType<Substrate.Sectors.Elements.SectorSpawner>(UnityEngine.FindObjectsSortMode.None))
{
    if (!s.enabled) continue;
    s.enabled = false; spawners++;
}
var rig = UnityEngine.Object.FindFirstObjectByType<Game.PlayerRig>();
foreach (var ship in UnityEngine.Object.FindObjectsByType<Ships.Ship>(UnityEngine.FindObjectsSortMode.None))
{
    if (rig && ship == rig.Player) continue;
    ship.gameObject.SetActive(false); removed++;
}
return "spawners off " + spawners + ", ships hidden " + removed;
