from pathlib import Path
s=Path('scratch/capture/look-probe.cs').read_text().replace('results/drawn-look-probe','results/drawn-normals-probe')
needle='foreach(var renderer in stage.GetComponentsInChildren<UnityEngine.MeshRenderer>()) apply.Invoke'
code='''if(treatment==3) foreach(var filter in stage.GetComponentsInChildren<UnityEngine.MeshFilter>()) {
var source=filter.sharedMesh; var copy=UnityEngine.Object.Instantiate(source); owned.Add(copy);
var vertices=copy.vertices;var triangles=copy.triangles;var sums=new System.Collections.Generic.Dictionary<UnityEngine.Vector3,UnityEngine.Vector3>();
for(int i=0;i<triangles.Length;i+=3){var a=triangles[i];var b=triangles[i+1];var c=triangles[i+2];var normal=UnityEngine.Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);foreach(var index in new[]{a,b,c}){sums.TryGetValue(vertices[index],out var sum);sums[vertices[index]]=sum+normal;}}
var normals=new UnityEngine.Vector3[vertices.Length];for(int i=0;i<vertices.Length;i++)normals[i]=sums[vertices[i]].normalized;copy.normals=normals;filter.sharedMesh=copy;
}
'''
s=s.replace(needle,code+needle);Path('scratch/capture/look-normals.cs').write_text(s)
