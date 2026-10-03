var rock=UnityEngine.Object.FindFirstObjectByType<Asteroids.AsteroidController>();
return string.Join("\n",System.Array.ConvertAll(UnityEngine.Object.FindObjectsByType<UnityEngine.MeshRenderer>(UnityEngine.FindObjectsSortMode.None),r=>r.name+" "+r.transform.position+" "+string.Join(",",System.Array.ConvertAll(r.sharedMaterials,m=>m?m.name+" : "+m.shader.name:"null"))));
