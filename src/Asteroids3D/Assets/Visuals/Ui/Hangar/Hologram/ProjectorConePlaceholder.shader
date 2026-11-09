Shader "Astronomical/Hangar/Projector Cone Placeholder"
{
    Properties
    {
        _BeamColor ("Beam Color", Color) = (0.4,0.9,1,0.15)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "ProjectorCone"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex ConeVertex
            #pragma fragment ConeFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BeamColor;
            CBUFFER_END
            float4 ConeVertex(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }
            half4 ConeFragment(float4 positionCS : SV_POSITION) : SV_Target
            {
                return _BeamColor;
            }
            ENDHLSL
        }
    }
}
