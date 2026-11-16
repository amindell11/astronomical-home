// MODE: "marks+lights" turns shadows off on ink marks and service lights; "all" on every renderer; "on" restores all.
var MODE = "__MODE__";
var go = UnityEngine.GameObject.Find("STATION_PROOF");
if (!go) return "no station";
int n = 0;
foreach (var r in go.GetComponentsInChildren<UnityEngine.Renderer>(true))
{
    var m = r.sharedMaterial ? r.sharedMaterial.name : "";
    bool target = MODE == "all" || (MODE == "marks+lights" && (m.Contains("Ink Mark") || m.Contains("Service")));
    bool off = MODE != "on" && target;
    r.shadowCastingMode = off ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
    r.receiveShadows = !off;
    if (off) n++;
}
return MODE + ": shadows off on " + n;
