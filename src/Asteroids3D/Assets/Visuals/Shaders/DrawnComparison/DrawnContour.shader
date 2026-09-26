Shader "Astronomical/Comparison/Drawn Contour"
{
    Properties
    {
        _ContourColor ("Contour Color", Color) = (0.025,0.035,0.065,1)
        _ContourPixels ("Shadow Side Width (Pixels)", Range(0,8)) = 3.2
        _ContourMinimum ("Lit Side Width Fraction", Range(0,1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+1" }
        Pass
        {
            Name "DrawnContour"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Stencil { Ref 1 ReadMask 1 WriteMask 0 Comp NotEqual }
            Cull Front
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex ContourVertex
            #pragma fragment ContourFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _ContourColor;
                float _ContourPixels, _ContourMinimum;
            CBUFFER_END
            struct ContourInput
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            float4 ContourVertex(ContourInput input) : SV_POSITION
            {
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float4 normalCS = mul(UNITY_MATRIX_VP, float4(normalWS, 0));
                float2 direction = (normalCS.xy - positionCS.xy / positionCS.w * normalCS.w) * _ScaledScreenParams.xy;
                direction *= rsqrt(max(dot(direction, direction), 0.000001));
                Light light = GetMainLight();
                float shadowSide = 1 - smoothstep(-0.2, 0.6, dot(normalWS, light.direction));
                float width = _ContourPixels * lerp(_ContourMinimum, 1, shadowSide);
                float3 viewDirection = GetWorldSpaceNormalizeViewDir(TransformObjectToWorld(input.positionOS.xyz));
                float facing = dot(normalWS, viewDirection);
                width *= sqrt(saturate(1 - facing * facing));
                positionCS.xy += direction * (2 * width / _ScaledScreenParams.xy) * positionCS.w;
                return positionCS;
            }
            half4 ContourFragment() : SV_Target
            {
                return _ContourColor;
            }
            ENDHLSL
        }
    }
}
