var drivers = UnityEngine.Object.FindObjectsByType<Game.Session.GameDriver>(UnityEngine.FindObjectsSortMode.None);
if (drivers.Length != 1) return "GameDriver count=" + drivers.Length;
var pf = typeof(Game.Session.GameDriver).GetField("sessionProfile",
    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
var profile = (Game.Session.SessionProfile)pf.GetValue(drivers[0]);
profile.presentation = false;

Ships.Ship player = null;
foreach (var s in UnityEngine.Object.FindObjectsByType<Ships.Ship>(UnityEngine.FindObjectsSortMode.None))
    if (s.name.StartsWith("Ship_2")) player = s;
if (player == null) return "profile flipped; no player found";
var dc = player.GetComponentInChildren<Ships.Damage.DamageController>();
if (dc == null) return "profile flipped; player has no DamageController";
dc.TakeDamage(new Damage.DamageInfo(99999f, Damage.DamageKind.Collision, default,
    0f, UnityEngine.Vector3.zero, player.transform.position));
return "profile.presentation=false, player killed";
