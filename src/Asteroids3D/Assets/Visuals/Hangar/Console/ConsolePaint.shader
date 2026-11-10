Shader "Hangar/ConsolePaint"
{
    Properties
    {
        _BaseMap("Baked paint and ink", 2D) = "white" {}
        _EmissionMap("Electronic emission", 2D) = "black" {}
        _Starlight("Faint starlight", Color) = (0.4614, 0.4845, 0.5271, 1)
        _LightResponse("Light response", Float) = 1
        _EmissionStrength("Emission strength", Float) = 1
        _EmissionTint("Emission tint", Color) = (1, 1, 1, 1)
        _ConstantEmission("Backlit lettering", Color) = (0, 0, 0, 1)
        _UseEmissionAlpha("Use display alpha", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Starlight;
                half _LightResponse;
                half _EmissionStrength;
                half4 _EmissionTint;
                half4 _ConstantEmission;
                half _UseEmissionAlpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half3 vertexLight : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.vertexLight = VertexLighting(output.positionWS, output.normalWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 illumination = _Starlight.rgb;
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    illumination += input.vertexLight * _LightResponse;
                #endif
                #if defined(_ADDITIONAL_LIGHTS)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    uint count = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(count)
                        Light light = GetAdditionalLight(lightIndex, input.positionWS);
                        illumination += light.color * light.distanceAttenuation *
                            saturate(dot(normal, light.direction)) * _LightResponse;
                    LIGHT_LOOP_END
                #endif
                half3 paint = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half4 sampledEmission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv);
                half3 emission = sampledEmission.rgb * lerp(1, sampledEmission.a, _UseEmissionAlpha);
                return half4(paint * illumination + emission * _EmissionStrength * _EmissionTint.rgb + _ConstantEmission.rgb, 1);
            }
            ENDHLSL
        }
    }
}
