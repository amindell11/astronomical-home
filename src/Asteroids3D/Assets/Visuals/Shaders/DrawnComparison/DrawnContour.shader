Shader "Astronomical/Comparison/Drawn Contour"
{
    Properties
    {
        _ContourColor ("Contour Color", Color) = (0.025,0.035,0.065,1)
        _DebrisVisibility ("Debris Visibility", Range(0,1)) = 1
        _ContourPixels ("Contour Width (Pixels)", Range(0,12)) = 3.2
        _ContourMinimum ("Lit Side Width Fraction", Range(0,1)) = 0.25
        _UniformWidth ("Uniform Screen Width", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Transparent-5" }
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
                half _DebrisVisibility;
                float _ContourPixels, _ContourMinimum, _UniformWidth;
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
                width = lerp(width, _ContourPixels, _UniformWidth);
                positionCS.xy += direction * (2 * width / _ScaledScreenParams.xy) * positionCS.w;
                return positionCS;
            }
            half4 ContourFragment(float4 positionCS : SV_POSITION) : SV_Target
            {
                clip(_DebrisVisibility - InterleavedGradientNoise(positionCS.xy, 0));
                return _ContourColor;
            }
            ENDHLSL
        }
    }
}
