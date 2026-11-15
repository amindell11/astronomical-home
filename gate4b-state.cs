var sb = new System.Text.StringBuilder();
sb.Append("t=").Append(UnityEngine.Time.time.ToString("F1")).Append(" scale=").Append(UnityEngine.Time.timeScale);
for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++) sb.Append(" scene ").Append(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name);
foreach (var oc in UnityEngine.Object.FindObjectsByType<Cameras.ObserverCam>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None)) sb.Append(" cam ").Append(oc.name).Append(" en ").Append(oc.enabled).Append(" camEn ").Append(oc.Cam ? oc.Cam.enabled.ToString() : "null");
sb.Append(" consoles ").Append(UnityEngine.Object.FindObjectsByType<UI.Hangar.Hardware.HangarConsole>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None).Length);
sb.Append(" spawners ").Append(UnityEngine.Object.FindObjectsByType<Substrate.Sectors.Elements.SectorSpawner>(UnityEngine.FindObjectsSortMode.None).Length);
sb.Append(" rocks ").Append(UnityEngine.Object.FindObjectsByType<Asteroids.AsteroidController>(UnityEngine.FindObjectsSortMode.None).Length);
foreach (var t in UnityEngine.Object.FindObjectsByType<UnityEngine.Transform>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None)) if (t.name == "STATION_PROOF") sb.Append(" proof");
return sb.ToString();
