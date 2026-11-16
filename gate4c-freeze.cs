var Q = "__Q__";
var go = UnityEngine.GameObject.Find("STATION_PROOF");
if (!go) return "no station";
go.GetComponent<Substrate.Sectors.Elements.Stations.Visual.StationMotion>().enabled = false;
UnityEngine.QualitySettings.SetQualityLevel(System.Array.IndexOf(UnityEngine.QualitySettings.names, Q), true);
var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
return "frozen; quality " + UnityEngine.QualitySettings.names[UnityEngine.QualitySettings.GetQualityLevel()] + "; names " + string.Join("/", UnityEngine.QualitySettings.names)
  + (urp ? "; shadowRes " + urp.mainLightShadowmapResolution + " dist " + urp.shadowDistance + " cascades " + urp.shadowCascadeCount + " soft " + urp.supportsSoftShadows + " depthBias " + urp.shadowDepthBias + " normalBias " + urp.shadowNormalBias : "");
