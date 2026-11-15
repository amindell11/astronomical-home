var SHOW = false;
var rig = UnityEngine.Object.FindFirstObjectByType<Game.PlayerRig>();
if (!rig || !rig.Overlay) return "no overlay";
var group = rig.Overlay.GetComponent<UnityEngine.CanvasGroup>();
if (!group) return "overlay has no CanvasGroup";
group.alpha = SHOW ? 1f : 0f;
return "hud alpha " + group.alpha;
