Shader "Astronomical/Drawn/Surface"
{
    Properties
    {
        _BaseMap ("Original Painted Surface", 2D) = "white" {}
        [Toggle(_PAINT_LAYERS)] _PaintLayers ("Separate Paint Layers", Float) = 0
        [Toggle(_LIGHT_DRIVEN_PAINT)] _LightDrivenPaint ("Light-Driven Hull Paint", Float) = 0
        _PaintLightingBias ("Authored Shadow Influence", Range(0,2)) = 1.2
        _PaintBrushMap ("Brushstroke Texture", 2D) = "gray" {}
        _PaintBrushStrength ("Brushstroke Contrast", Range(0,4)) = 0
        _PaintShadowMap ("Painted Shadow Mask", 2D) = "black" {}
        _PaintLightMap ("Painted Light Mask", 2D) = "black" {}
        _PaintInkMap ("Panel Ink Mask", 2D) = "black" {}
        _PaintShadowStrength ("Shadow Depth", Range(0,2)) = 1
        _PaintLightStrength ("Painted Light", Range(0,2)) = 1
        _PaintInkStrength ("Panel Ink", Range(0,2)) = 1
        _PaintSaturation ("Paint Saturation", Range(0,2)) = 1
        _PaintEmissionBlend ("Painted Indicator Blend", Range(0,1)) = 0
        _PaintLightColor ("Painted Light Color", Color) = (0.82,0.93,0.88,1)
        _PaintInkColor ("Panel Ink Color", Color) = (0.006,0.012,0.018,1)
        [HideInInspector] _PaintRegionCount ("Authored Palette Regions", Float) = 0
        [HideInInspector] _PaintRegionLightLift ("Region Highlight Lift", Float) = 0.25
        [HideInInspector] _PaintRegionMask0 ("Region Coverage 0", 2D) = "black" {}
        [HideInInspector] _PaintRegionMask1 ("Region Coverage 1", 2D) = "black" {}
        [HideInInspector] _PaintRegionMask2 ("Region Coverage 2", 2D) = "black" {}
        [HideInInspector] _PaintRegionMask3 ("Region Coverage 3", 2D) = "black" {}
        [HideInInspector] _PaintRegionColor0 ("Region 0", Color) = (0,0,0,1)
        [HideInInspector] _PaintRegionColor1 ("Region 1", Color) = (0,0,0,1)
        [HideInInspector] _PaintRegionColor2 ("Region 2", Color) = (0,0,0,1)
        [HideInInspector] _PaintRegionColor3 ("Region 3", Color) = (0,0,0,1)
        [HideInInspector] _PaintRegionColor4 ("Region 4", Color) = (0,0,0,1)
        [HideInInspector] _PaintRegionColor5 ("Region 5", Color) = (0,0,0,1)
        [HideInInspector] _PaintRegionColor6 ("Region 6", Color) = (0,0,0,1)
        [HideInInspector] _PaintRegionColor7 ("Region 7", Color) = (0,0,0,1)
        [HideInInspector] _PaintRegionColor8 ("Region 8", Color) = (0,0,0,1)
        [HideInInspector] _PaintRegionColor9 ("Region 9", Color) = (0,0,0,1)
        [HideInInspector] _PaintRegionColor10 ("Region 10", Color) = (0,0,0,1)
        [HideInInspector] _PaintRegionColor11 ("Region 11", Color) = (0,0,0,1)
        [HideInInspector] _PaintRegionColor12 ("Region 12", Color) = (0,0,0,1)
        [HideInInspector] _PaintRegionColor13 ("Region 13", Color) = (0,0,0,1)
        [HideInInspector] _PaintRegionColor14 ("Region 14", Color) = (0,0,0,1)
        [HideInInspector] _PaintRegionColor15 ("Region 15", Color) = (0,0,0,1)
        _DamageFlash ("Damage Flash", Range(0,1)) = 0
        _DebrisVisibility ("Debris Visibility", Range(0,1)) = 1
        _SootStrength ("Destruction Soot", Range(0,1)) = 0
        [Toggle(_NORMALMAP)] _UseRelief ("Sculpted Relief", Float) = 0
        [Normal] _BumpMap ("Sculpted Relief Normal", 2D) = "bump" {}
        _BumpScale ("Relief Strength", Range(0,2)) = 1
        [MainColor] _BaseColor ("Hull Color / Hit Flash", Color) = (1,1,1,1)
        _OrangeGain ("Orange Paint RGB Gain", Vector) = (1,1,1,0)
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
            float4 _BaseMap_ST, _DetailAlbedoMap_ST, _PaintBrushMap_ST;
            half4 _BaseColor, _PaperColor, _ShadowColor, _EmissionColor;
            half4 _OrangeGain, _PaintLightColor, _PaintInkColor;
            half _PaintShadowStrength, _PaintLightStrength, _PaintInkStrength;
            half _PaintSaturation, _PaintEmissionBlend;
            half _PaintLightingBias, _PaintBrushStrength;
            half _PaintRegionCount, _PaintRegionLightLift;
            half4 _PaintRegionColor0, _PaintRegionColor1, _PaintRegionColor2, _PaintRegionColor3;
            half4 _PaintRegionColor4, _PaintRegionColor5, _PaintRegionColor6, _PaintRegionColor7;
            half4 _PaintRegionColor8, _PaintRegionColor9, _PaintRegionColor10, _PaintRegionColor11;
            half4 _PaintRegionColor12, _PaintRegionColor13, _PaintRegionColor14, _PaintRegionColor15;
            half _TextureStrength, _PigmentPreservation, _LineStrength, _LineThreshold, _LineSoftness, _WearStrength;
            half _PaletteLighting, _AmbientStrength;
            half _DetailAlbedoMapScale, _ShadowThreshold, _ShadowSoftness;
            half _SpecularStrength, _EmissionStrength;
            half _CastShadowStrength, _BumpScale;
            half _DebrisVisibility, _SootStrength, _DamageFlash;
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
            #pragma shader_feature_local _PAINT_LAYERS
            #pragma shader_feature_local _LIGHT_DRIVEN_PAINT
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_PaintShadowMap); SAMPLER(sampler_PaintShadowMap);
            TEXTURE2D(_PaintLightMap); SAMPLER(sampler_PaintLightMap);
            TEXTURE2D(_PaintInkMap); SAMPLER(sampler_PaintInkMap);
            TEXTURE2D(_PaintBrushMap); SAMPLER(sampler_PaintBrushMap);
            TEXTURE2D(_PaintRegionMask0); SAMPLER(sampler_PaintRegionMask0);
            TEXTURE2D(_PaintRegionMask1);
            TEXTURE2D(_PaintRegionMask2);
            TEXTURE2D(_PaintRegionMask3);
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
                half4 color : COLOR;
            };
            struct SurfaceOutput
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fog : TEXCOORD3;
                half4 tangentWS : TEXCOORD4;
                half soot : TEXCOORD5;
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
                output.soot = input.color.r;
                return output;
            }
            half4 SurfaceFragment(SurfaceOutput input) : SV_Target
            {
                clip(_DebrisVisibility - InterleavedGradientNoise(input.positionCS.xy, 0));
                half3 normal = DrawnWorldNormal(input.normalWS, input.tangentWS, input.uv);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                Light light = GetMainLight(shadowCoord);
                half ndl = dot(normal, light.direction);
                half paintLighting = 0;
                half3 painted = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                half orange = smoothstep(0.15, 0.3, painted.r - painted.g) * smoothstep(0.06, 0.15, painted.g - painted.b);
                painted *= lerp(1, _OrangeGain.rgb, orange);
                half luminance = dot(painted, half3(0.2126,0.7152,0.0722));
                half marks = 1 - smoothstep(_LineThreshold, _LineThreshold + _LineSoftness, luminance);
                half brightest = max(painted.r, max(painted.g, painted.b));
                half darkest = min(painted.r, min(painted.g, painted.b));
                half saturation = (brightest - darkest) / max(brightest, 0.001);
                half pigment = smoothstep(0.25, 0.60, saturation) * _PigmentPreservation;
                half3 albedo = lerp(_PaperColor.rgb, painted, max(_TextureStrength, pigment));
                #if defined(_PAINT_LAYERS)
                    half shadow = SAMPLE_TEXTURE2D(_PaintShadowMap, sampler_PaintShadowMap, input.uv).r * _PaintShadowStrength;
                    half paintedLight = SAMPLE_TEXTURE2D(_PaintLightMap, sampler_PaintLightMap, input.uv).r * _PaintLightStrength;
                    half ink = SAMPLE_TEXTURE2D(_PaintInkMap, sampler_PaintInkMap, input.uv).r * _PaintInkStrength;
                    half3 basePalette = _PaperColor.rgb;
                    half3 lightPalette = _PaintLightColor.rgb;
                    if (_PaintRegionCount > 0)
                    {
                        half4 w0 = SAMPLE_TEXTURE2D(_PaintRegionMask0, sampler_PaintRegionMask0, input.uv);
                        half4 w1 = 0, w2 = 0, w3 = 0;
                        if (_PaintRegionCount > 4)
                            w1 = SAMPLE_TEXTURE2D(_PaintRegionMask1, sampler_PaintRegionMask0, input.uv);
                        if (_PaintRegionCount > 8)
                            w2 = SAMPLE_TEXTURE2D(_PaintRegionMask2, sampler_PaintRegionMask0, input.uv);
                        if (_PaintRegionCount > 12)
                            w3 = SAMPLE_TEXTURE2D(_PaintRegionMask3, sampler_PaintRegionMask0, input.uv);
                        basePalette = w0.x * _PaintRegionColor0.rgb + w0.y * _PaintRegionColor1.rgb
                                    + w0.z * _PaintRegionColor2.rgb + w0.w * _PaintRegionColor3.rgb
                                    + w1.x * _PaintRegionColor4.rgb + w1.y * _PaintRegionColor5.rgb
                                    + w1.z * _PaintRegionColor6.rgb + w1.w * _PaintRegionColor7.rgb
                                    + w2.x * _PaintRegionColor8.rgb + w2.y * _PaintRegionColor9.rgb
                                    + w2.z * _PaintRegionColor10.rgb + w2.w * _PaintRegionColor11.rgb
                                    + w3.x * _PaintRegionColor12.rgb + w3.y * _PaintRegionColor13.rgb
                                    + w3.z * _PaintRegionColor14.rgb + w3.w * _PaintRegionColor15.rgb;
                        basePalette /= max(dot(w0 + w1 + w2 + w3, half4(1, 1, 1, 1)), 0.0001h);
                        lightPalette = lerp(basePalette, 1, _PaintRegionLightLift);
                    }
                    #if defined(_LIGHT_DRIVEN_PAINT)
                        float3 brushPosition = TransformWorldToObject(input.positionWS);
                        half3 brushWeights = abs(TransformWorldToObjectNormal(input.normalWS));
                        brushWeights *= brushWeights;
                        brushWeights *= brushWeights;
                        brushWeights /= dot(brushWeights, half3(1, 1, 1));
                        half brush = SAMPLE_TEXTURE2D(_PaintBrushMap, sampler_PaintBrushMap,
                            brushPosition.zy * _PaintBrushMap_ST.xy + _PaintBrushMap_ST.zw).r * brushWeights.x;
                        brush += SAMPLE_TEXTURE2D(_PaintBrushMap, sampler_PaintBrushMap,
                            brushPosition.xz * _PaintBrushMap_ST.xy + _PaintBrushMap_ST.zw).r * brushWeights.y;
                        brush += SAMPLE_TEXTURE2D(_PaintBrushMap, sampler_PaintBrushMap,
                            brushPosition.xy * _PaintBrushMap_ST.xy + _PaintBrushMap_ST.zw).r * brushWeights.z;
                        // The house brush tile treats 0.625 as neutral pigment.
                        basePalette *= max(0, 1 + (brush - 0.625h) * _PaintBrushStrength);
                        half response = ndl + (saturate(paintedLight) - saturate(shadow))
                            * _PaintLightingBias * saturate(1 - abs(ndl));
                        half edge = max(_ShadowSoftness, fwidth(response));
                        half lit = smoothstep(_ShadowThreshold - edge, _ShadowThreshold + edge, response);
                        half3 relit = basePalette * lerp(_ShadowColor.rgb, 1, lit);
                        relit = lerp(relit, lightPalette,
                            smoothstep(0.7h, 1.0h, response) * saturate(_PaintLightStrength));
                        paintLighting = 1;
                        albedo = relit;
                    #else
                        albedo = basePalette * (1 - shadow);
                        albedo = lerp(albedo, lightPalette, saturate(paintedLight));
                    #endif
                    albedo = lerp(albedo, _PaintInkColor.rgb, saturate(ink));
                    half indicator = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).r;
                    albedo = lerp(albedo, _EmissionColor.rgb, saturate(indicator * _PaintEmissionBlend));
                    if (_PaintSaturation != 1)
                    {
                        half3 paintHSV = RgbToHsv(albedo);
                        paintHSV.y = saturate(paintHSV.y * _PaintSaturation);
                        albedo = HsvToRgb(paintHSV);
                    }
                #endif
                albedo *= 1 - marks * _LineStrength;
                albedo *= 1 - SAMPLE_TEXTURE2D(_WearMap, sampler_WearMap, input.uv).r * _WearStrength;
                float2 detailUV = input.uv * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
                half3 detail = SAMPLE_TEXTURE2D(_DetailAlbedoMap, sampler_DetailAlbedoMap, detailUV).rgb;
                half mask = SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, input.uv).a;
                albedo *= lerp(1, 2 * detail * _DetailAlbedoMapScale - _DetailAlbedoMapScale + 1, mask);
                albedo *= _BaseColor.rgb;
                albedo = lerp(albedo, _BaseColor.rgb, _DamageFlash);
                half soot = smoothstep(0.22, 0.72, input.soot) * _SootStrength;
                albedo = lerp(albedo * (1 - _SootStrength * 0.18), half3(0.065, 0.055, 0.05), soot * 0.88);

                half transition = max(_ShadowSoftness, fwidth(ndl));
                half plane = smoothstep(_ShadowThreshold - transition, _ShadowThreshold + transition, ndl);
                half3 diffuse = lerp(_ShadowColor.rgb, 1, plane * light.shadowAttenuation);
                diffuse = lerp(diffuse, lerp(_ShadowColor.rgb, 1, light.shadowAttenuation), paintLighting);
                diffuse *= lerp(1, light.shadowAttenuation, _CastShadowStrength);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half highlight = pow(saturate(dot(normal, SafeNormalize(light.direction + view))), 48);
                half3 color = albedo * (SampleSH(normal) + light.color * diffuse * light.distanceAttenuation);
                half3 illumination = light.color * light.distanceAttenuation + max(SampleSH(normal), 0) * _AmbientStrength;
                half peak = max(illumination.r, max(illumination.g, illumination.b));
                illumination /= max(1, peak);
                color = lerp(color, albedo * diffuse * illumination, _PaletteLighting);
                color += light.color * highlight * _SpecularStrength * light.shadowAttenuation * (1 - soot);
                color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb * _EmissionStrength * (1 - _SootStrength) * (1 - _PaintEmissionBlend);
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
            #pragma fragment DebrisShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            half4 DebrisShadowFragment(Varyings input) : SV_Target
            {
                clip(_DebrisVisibility - InterleavedGradientNoise(input.positionCS.xy, 0));
                return ShadowPassFragment(input);
            }
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
                clip(_DebrisVisibility - InterleavedGradientNoise(input.positionCS.xy, 0));
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
                clip(_DebrisVisibility - InterleavedGradientNoise(positionCS.xy, 0));
                return positionCS.z;
            }
            ENDHLSL
        }
    }
}
