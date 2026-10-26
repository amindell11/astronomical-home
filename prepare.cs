UnityEngine.Time.timeScale = 0f;
var module = UnityEngine.ScriptableObject.CreateInstance<Capture.GameView.GameViewEpisodeCapture>();
module.PrepareHud1280x720();
var report = new System.Collections.Generic.List<object>();
foreach (var s in UnityEngine.Object.FindObjectsByType<Ships.Ship>(UnityEngine.FindObjectsInactive.Include,UnityEngine.FindObjectsSortMode.None)) { s.Damage.Health.Reset(); s.Damage.Shield.Reset(); report.Add(new {s.name, s.gameObject.activeInHierarchy, position=s.transform.position.ToString()}); }
return report;
