var host = UnityEngine.Object.FindFirstObjectByType<Game.Preview.PreviewHost>();
var objective = host.Playtest.GetComponent<Substrate.Services.Objectives.ObjectiveService>();
objective.SpineTarget.position = new UnityEngine.Vector3(14,7,0);
return "Staged the real extraction station in view for the near-destination sample.";
