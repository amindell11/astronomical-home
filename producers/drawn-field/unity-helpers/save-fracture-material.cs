var folder="Assets/Visuals/Environment/Asteroids/DrawnStudy/";
UnityEditor.AssetDatabase.Refresh();
var modelImporter=(UnityEditor.ModelImporter)UnityEditor.AssetImporter.GetAtPath(folder+"AsteroidFractureStudy.fbx");
modelImporter.materialImportMode=UnityEditor.ModelImporterMaterialImportMode.None;
modelImporter.SaveAndReimport();
var textureImporter=(UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(folder+"AsteroidFracturePaint.png");
textureImporter.maxTextureSize=2048;textureImporter.mipmapEnabled=true;textureImporter.wrapMode=UnityEngine.TextureWrapMode.Repeat;textureImporter.anisoLevel=4;textureImporter.SaveAndReimport();
var material=new UnityEngine.Material(UnityEngine.Shader.Find("Astronomical/Comparison/Drawn Surface"));
material.SetTexture("_BaseMap",UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(folder+"AsteroidFracturePaint.png"));
material.SetFloat("_TextureStrength",1);material.SetFloat("_LineStrength",.9f);material.SetFloat("_LineThreshold",.035f);material.SetFloat("_LineSoftness",.02f);material.SetFloat("_SpecularStrength",0);material.SetFloat("_EmissionStrength",0);material.SetFloat("_PaletteLighting",1);material.SetFloat("_AmbientStrength",.35f);material.SetColor("_ShadowColor",new UnityEngine.Color(.13f,.30f,.60f));material.SetFloat("_ShadowThreshold",.55f);material.SetFloat("_ShadowSoftness",.14f);material.SetColor("_BaseColor",new UnityEngine.Color(4.2f,2.7f,1.3f));
UnityEditor.AssetDatabase.CreateAsset(material,folder+"AsteroidFracturePaint.mat");UnityEditor.AssetDatabase.SaveAssets();
return folder;

