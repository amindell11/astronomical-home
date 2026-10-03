var list=UnityEngine.Object.FindObjectsByType<UnityEngine.Light>(UnityEngine.FindObjectsSortMode.None);
return System.String.Join("\n", System.Array.ConvertAll(list,l=>l.name+" intensity="+l.intensity+" rotation="+l.transform.eulerAngles+" sun="+(l==UnityEngine.RenderSettings.sun)));
