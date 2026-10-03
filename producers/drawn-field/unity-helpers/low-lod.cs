var r=System.Array.Find(UnityEngine.Object.FindObjectsByType<UnityEngine.MeshRenderer>(UnityEngine.FindObjectsSortMode.None),r=>r.name=="LowLODMESH");
var parent=r.transform.parent;
var result="parent="+(parent?parent.name:"none")+" components="+string.Join(",",System.Array.ConvertAll(r.GetComponents<UnityEngine.Component>(),c=>c.GetType().FullName))+" source="+UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r.gameObject)+" enabled="+r.enabled;
r.enabled=false;
return result;
