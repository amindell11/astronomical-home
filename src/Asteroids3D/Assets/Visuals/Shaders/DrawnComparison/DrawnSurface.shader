Shader "Astronomical/Comparison/Drawn Surface"
{
    Properties
    {
        _BaseMap ("Original Painted Surface", 2D) = "white" {}
        [Toggle(_NORMALMAP)] _UseRelief ("Sculpted Relief", Float) = 0
        [Normal] _BumpMap ("Sculpted Relief Normal", 2D) = "bump" {}
        _BumpScale ("Relief Strength", Range(0,2)) = 1
        _BaseColor ("Hull Color / Hit Flash", Color) = (1,1,1,1)
        _PaperColor ("Surface Palette", Color) = (0.65,0.7,0.75,1)
        _TextureStrength ("Painted Surface Strength", Range(0,1)) = 0.65
        _PigmentPreservation ("Preserve Saturated Paint", Range(0,1)) = 0
        _PaletteLighting ("Bound Palette Lighting", Range(0,1)) = 0
        _AmbientStrength ("Ambient Strength", Range(0,1)) = 1
        _LineStrength ("Painted Dark Mark Strength", Range(0,1)) = 0.35
        _LineThreshold ("Painted Dark Mark Threshold", Range(0,1)) = 0.12
        _LineSoftness ("Painted Dark Mark Transition", Range(0.001,0.2)) = 0.08
        _WearMap ("Static Wear Mask (R)", 2D) = "black" {}
        _WearStrength ("Static Wear Strength", Range(0,1)) = 0.08
        _DetailAlbedoMap ("Combat Damage Detail", 2D) = "gray" {}
        _DetailMask ("Combat Damage Mask (A)", 2D) = "white" {}
        _DetailAlbedoMapScale ("Combat Damage Amount", Range(0,2)) = 0
        _ShadowColor ("Shadow Plane Tint", Color) = (0.38,0.46,0.62,1)
        _ShadowThreshold ("Shadow Plane Threshold", Range(-1,1)) = 0.15
        _ShadowSoftness ("Shadow Transition", Range(0.01,0.5)) = 0.08
        _CastShadowStrength ("Cast Shadow Darkness", Range(0,1)) = 0
        _SpecularStrength ("Highlight Strength", Range(0,1)) = 0.12
        _EmissionMap ("Localized Emission", 2D) = "black" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        _EmissionStrength ("Emission Strength", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _DetailAlbedoMap_ST;
            half4 _BaseColor, _PaperColor, _ShadowColor, _EmissionColor;
            half _TextureStrength, _PigmentPreservation, _LineStrength, _LineThreshold, _LineSoftness, _WearStrength;
            half _PaletteLighting, _AmbientStrength;
            half _DetailAlbedoMapScale, _ShadowThreshold, _ShadowSoftness;
            half _SpecularStrength, _EmissionStrength;
            half _CastShadowStrength, _BumpScale;
        CBUFFER_END
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        half3 DrawnWorldNormal(half3 normalWS, half4 tangentWS, float2 uv)
        {
            #if defined(_NORMALMAP)
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                half3 bitangent = tangentWS.w * cross(normalWS, tangentWS.xyz);
                normalWS = TransformTangentToWorld(normalTS, half3x3(tangentWS.xyz, bitangent, normalWS));
            #endif
            return NormalizeNormalPerPixel(normalWS);
        }
        ENDHLSL
        Pass
        {
            Name "DrawnSurface"
            Stencil { Ref 1 WriteMask 1 Comp Always Pass Replace }
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex SurfaceVertex
            #pragma fragment SurfaceFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_WearMap); SAMPLER(sampler_WearMap);
            TEXTURE2D(_DetailAlbedoMap); SAMPLER(sampler_DetailAlbedoMap);
            TEXTURE2D(_DetailMask); SAMPLER(sampler_DetailMask);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            struct SurfaceInput
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };
            struct SurfaceOutput
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fog : TEXCOORD3;
                half4 tangentWS : TEXCOORD4;
            };
            SurfaceOutput SurfaceVertex(SurfaceInput input)
            {
                SurfaceOutput output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = half4(TransformObjectToWorldDir(input.tangentOS.xyz),
                    input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fog = ComputeFogFactor(position.positionCS.z);
                return output;
            }
            half4 SurfaceFragment(SurfaceOutput input) : SV_Target
            {
                half3 painted = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half luminance = dot(painted, half3(0.2126,0.7152,0.0722));
                half marks = 1 - smoothstep(_LineThreshold, _LineThreshold + _LineSoftness, luminance);
                half brightest = max(painted.r, max(painted.g, painted.b));
                half darkest = min(painted.r, min(painted.g, painted.b));
                half saturation = (brightest - darkest) / max(brightest, 0.001);
                half pigment = smoothstep(0.25, 0.60, saturation) * _PigmentPreservation;
                half3 albedo = lerp(_PaperColor.rgb, painted, max(_TextureStrength, pigment));
                albedo *= 1 - marks * _LineStrength;
                albedo *= 1 - SAMPLE_TEXTURE2D(_WearMap, sampler_WearMap, input.uv).r * _WearStrength;
                float2 detailUV = input.uv * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
                half3 detail = SAMPLE_TEXTURE2D(_DetailAlbedoMap, sampler_DetailAlbedoMap, detailUV).rgb;
                half mask = SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, input.uv).a;
                albedo *= lerp(1, 2 * detail * _DetailAlbedoMapScale - _DetailAlbedoMapScale + 1, mask);
                albedo *= _BaseColor.rgb;
                half3 normal = DrawnWorldNormal(input.normalWS, input.tangentWS, input.uv);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                Light light = GetMainLight(shadowCoord);
                half ndl = dot(normal, light.direction);
                half transition = max(_ShadowSoftness, fwidth(ndl));
                half plane = smoothstep(_ShadowThreshold - transition, _ShadowThreshold + transition, ndl);
                half3 diffuse = lerp(_ShadowColor.rgb, 1, plane * light.shadowAttenuation);
                diffuse *= lerp(1, light.shadowAttenuation, _CastShadowStrength);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half highlight = pow(saturate(dot(normal, SafeNormalize(light.direction + view))), 48);
                half3 color = albedo * (SampleSH(normal) + light.color * diffuse * light.distanceAttenuation);
                half3 illumination = light.color * light.distanceAttenuation + max(SampleSH(normal), 0) * _AmbientStrength;
                half peak = max(illumination.r, max(illumination.g, illumination.b));
                illumination /= max(1, peak);
                color = lerp(color, albedo * diffuse * illumination, _PaletteLighting);
                color += light.color * highlight * _SpecularStrength * light.shadowAttenuation;
                color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb * _EmissionStrength;
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex DrawnDepthVertex
            #pragma fragment DrawnDepthFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            struct DepthInput
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };
            struct DepthOutput
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                half4 tangentWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };
            DepthOutput DrawnDepthVertex(DepthInput input)
            {
                DepthOutput output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = half4(TransformObjectToWorldDir(input.tangentOS.xyz),
                    input.tangentOS.w * GetOddNegativeScale());
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            half4 DrawnDepthFragment(DepthOutput input) : SV_Target
            {
                half3 normalWS = DrawnWorldNormal(input.normalWS, input.tangentWS, input.uv);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = saturate(PackNormalOctQuadEncode(normalWS) * 0.5 + 0.5);
                    return half4(PackFloat2To888(oct), 0);
                #else
                    return half4(normalWS, 0);
                #endif
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            float4 DepthVertex(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }
            half DepthFragment(float4 positionCS : SV_POSITION) : SV_Target
            {
                return positionCS.z;
            }
            ENDHLSL
        }
    }
}
