Ships.Ship enemy = null, player = null;
foreach (var s in UnityEngine.Object.FindObjectsByType<Ships.Ship>(UnityEngine.FindObjectsSortMode.None))
{
    if (s.name == "EyeballEnemy") enemy = s;
    if (s.name.StartsWith("Ship_2")) player = s;
}
if (enemy == null || player == null) return "missing ships";
var rb = player.GetComponent<UnityEngine.Rigidbody>();
var target = enemy.transform.position + new UnityEngine.Vector3(8f, 0f, 0f);
if (rb != null) { rb.position = target; rb.linearVelocity = UnityEngine.Vector3.zero; }
player.transform.position = target;
UnityEditor.Selection.objects = new UnityEngine.Object[] { enemy.gameObject };
return "player moved beside enemy; enemy root selected";
