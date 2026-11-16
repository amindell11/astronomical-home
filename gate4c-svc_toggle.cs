var SHOW = __SHOW__;
var go = UnityEngine.GameObject.Find("STATION_PROOF");
int n = 0;
foreach (var r in go.GetComponentsInChildren<UnityEngine.Renderer>(true))
    if (r.name.Contains("/ svc")) { r.enabled = SHOW; n++; }
var sb = new System.Text.StringBuilder("svc renderers " + (SHOW ? "on " : "off ") + n);
foreach (var r in go.GetComponentsInChildren<UnityEngine.Renderer>(true))
    if (r.name.StartsWith("Berth light A / svc")) { var m = r.sharedMaterial; sb.Append("; mat ").Append(m.name).Append(" shader ").Append(m.shader.name).Append(" kw ").Append(string.Join(",", m.shaderKeywords)); foreach (var p in new[]{"_EmissionColor","_BaseColor","_PaperColor"}) if (m.HasProperty(p)) sb.Append(" ").Append(p).Append(" ").Append(m.GetColor(p)); }
return sb.ToString();
