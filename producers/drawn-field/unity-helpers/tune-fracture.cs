var mat=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Visuals/Environment/Asteroids/DrawnStudy/AsteroidFracturePaint.mat");
mat.SetColor("_ShadowColor",new UnityEngine.Color(.25f,.46f,1.30f));
mat.SetFloat("_ShadowSoftness",.18f);mat.SetColor("_BaseColor",new UnityEngine.Color(3.0f,1.8f,.75f));
mat.SetFloat("_LineStrength",.3f);
UnityEditor.EditorUtility.SetDirty(mat);UnityEditor.AssetDatabase.SaveAssets();
return "Saved";

