Shader "Astronomical/Comparison/Drawn Canopy"
{
    Properties
    {
        _BaseColor ("Glass Color", Color) = (0.025,0.065,0.15,1)
        _DrawnReflectionStrength ("Drawn Reflection Strength", Range(0,1)) = 0.85
        _ReflectionBounds ("Projection Center / Inverse Size", Vector) = (0,0,1,1)
        _ReflectionColor ("Reflection Color", Color) = (0.42,0.54,0.64,1)
        _ReflectionEdgeColor ("Glint Color", Color) = (0.56,0.64,0.67,1)
        _ShadowColor ("Glass Shadow Color", Color) = (0.35,0.45,0.8,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "DrawnCanopy"
            Tags { "LightMode"="UniversalForwardOnly" }
            Stencil { Ref 1 WriteMask 1 Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma vertex CanopyVertex
            #pragma fragment CanopyFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _ReflectionColor, _ReflectionEdgeColor;
                float4 _ReflectionBounds;
                half _DrawnReflectionStrength;
                half4 _ShadowColor;
            CBUFFER_END
            struct Input
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            struct Interpolated
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 reflectionUV : TEXCOORD2;
                half fog : TEXCOORD3;
            };
            Interpolated CanopyVertex(Input input)
            {
                Interpolated output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.reflectionUV = (input.positionOS.xy - _ReflectionBounds.xy) * _ReflectionBounds.zw + 0.5;
                output.fog = ComputeFogFactor(position.positionCS.z);
                return output;
            }
            half4 CanopyFragment(Interpolated input) : SV_Target
            {
                half3 normal = NormalizeNormalPerPixel(input.normalWS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                Light light = GetMainLight(shadowCoord);
                half ndl = dot(normal, light.direction);
                half transition = max(0.18, fwidth(ndl));
                half plane = smoothstep(0.55 - transition, 0.55 + transition, ndl);
                half3 diffuse = lerp(_ShadowColor.rgb, 1, plane * light.shadowAttenuation);
                diffuse *= lerp(1, light.shadowAttenuation, 0.96);
                half3 illumination = light.color * light.distanceAttenuation + max(SampleSH(normal), 0) * 0.35;
                illumination /= max(1, max(illumination.r, max(illumination.g, illumination.b)));
                half3 color = _BaseColor.rgb * diffuse * illumination;
                float2 uv = input.reflectionUV;
                float t = saturate((uv.y - 0.16) / 0.66);
                float pressure = pow(saturate(sin(t * PI)), 0.7);
                float center = 0.46 + 0.09 * t - 0.05 * t * t;
                float edge = 0.003 * sin(uv.y * 63) + 0.0015 * sin(uv.y * 127);
                float aa = max(fwidth(uv.x), 0.001);
                float reflection = 1 - smoothstep(0.10 * pressure - aa, 0.10 * pressure + aa,
                    abs(uv.x - center + edge));
                reflection *= smoothstep(0.16, 0.19, uv.y) * (1 - smoothstep(0.79, 0.82, uv.y));
                float rimCenter = 0.69 + 0.035 * sin((uv.y - 0.3) * 5);
                float rim = 1 - smoothstep(0.008 - aa, 0.008 + aa, abs(uv.x - rimCenter + edge));
                rim *= smoothstep(0.30, 0.34, uv.y) * (1 - smoothstep(0.71, 0.74, uv.y));
                rim *= 1 - smoothstep(0.53, 0.54, uv.y) * (1 - smoothstep(0.59, 0.60, uv.y));
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half facing = smoothstep(0.05, 0.55, saturate(dot(normal, view)));
                half lightFacing = saturate(dot(normal, SafeNormalize(light.direction + view)));
                half response = lerp(0.45, 0.75, smoothstep(0.2, 0.5, lightFacing));
                response += 0.25 * smoothstep(0.72, 0.84, lightFacing);
                response *= facing * _DrawnReflectionStrength * lerp(0.65, 1, light.shadowAttenuation);
                color = lerp(color, _ReflectionColor.rgb * light.color, reflection * response);
                color = lerp(color, _ReflectionEdgeColor.rgb * light.color, rim * response);
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
        UsePass "Astronomical/Comparison/Drawn Surface/ShadowCaster"
        UsePass "Astronomical/Comparison/Drawn Surface/DepthNormals"
        UsePass "Astronomical/Comparison/Drawn Surface/DepthOnly"
    }
}
