Shader "Astronomical/Drawn/Canopy"
{
    Properties
    {
        _BaseColor ("Damage Tint", Color) = (1,1,1,1)
        _GlassColor ("Glass Color", Color) = (0.025,0.065,0.15,1)
        _DrawnReflectionStrength ("Drawn Reflection Strength", Range(0,1)) = 0.85
        _ReflectionBounds ("Projection Center / Inverse Size", Vector) = (0,0,1,1)
        _ReflectionColor ("Reflection Color", Color) = (0.42,0.54,0.64,1)
        _ReflectionEdgeColor ("Glint Color", Color) = (0.56,0.64,0.67,1)
        [Toggle(_AUTHORED_REFLECTION)] _AuthoredReflection ("Authored Reflection", Float) = 0
        _ReflectionMap ("Reflection Drawing (+Z / -Z)", 2D) = "black" {}
        [Enum(Color Drawing,0,Painted Glass,1)] _ReflectionArtwork ("Reflection Artwork", Float) = 0
        _ReflectionVerticalRange ("Stripe Sampling Height Range", Vector) = (0,1,0,0)
        _SecondaryGlintLeft ("Left Glint Center / Scale / Strength", Vector) = (0.5,0.5,1,0)
        _SecondaryGlintRight ("Right Glint Center / Scale / Strength", Vector) = (0.5,0.5,1,0)
        _ReflectionInkColor ("Reflection Ink Color", Color) = (0,0,0,1)
        _ReflectionLayerStrengths ("Shadow / Light / Ink Strength", Vector) = (1,1,1,0)
        _ReflectionSaturation ("Reflection Saturation", Float) = 1
        _ShadowColor ("Glass Shadow Color", Color) = (0.35,0.45,0.8,1)
        _ShadowThreshold ("Shadow Threshold", Float) = 0.55
        _ShadowSoftness ("Shadow Softness", Float) = 0.18
        _CastShadowStrength ("Cast Shadow Strength", Range(0,1)) = 0.96
        _AmbientStrength ("Ambient Strength", Float) = 0.35
        [HideInInspector] _DamageFlash ("Damage Flash", Float) = 0
        [HideInInspector] _DebrisVisibility ("Debris Visibility", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _GlassColor, _ReflectionColor, _ReflectionEdgeColor, _ReflectionInkColor, _ShadowColor;
            float4 _ReflectionBounds, _ReflectionMap_TexelSize, _ReflectionMap_ST, _ReflectionLayerStrengths;
            float4 _ReflectionVerticalRange, _SecondaryGlintLeft, _SecondaryGlintRight;
            half _DrawnReflectionStrength, _ReflectionArtwork, _ReflectionSaturation;
            half _ShadowThreshold, _ShadowSoftness, _CastShadowStrength, _AmbientStrength;
            half _DamageFlash, _DebrisVisibility;
        CBUFFER_END
        TEXTURE2D(_ReflectionMap);
        SAMPLER(sampler_ReflectionMap);
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
            half localFacing : TEXCOORD4;
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
            output.localFacing = input.normalOS.z;
            return output;
        }
        void ClipDebris(float4 positionCS)
        {
            clip(_DebrisVisibility - InterleavedGradientNoise(positionCS.xy, 0) - 0.0001);
        }
        half4 SampleReflection(float2 uv, half localFacing)
        {
            half inside = all(uv >= 0) && all(uv <= 1);
            float2 inset = _ReflectionMap_TexelSize.xy * float2(1, 0.5);
            uv = clamp(uv, inset, 1 - inset);
            uv.x = (uv.x + step(localFacing, 0)) * 0.5;
            half4 drawing = SAMPLE_TEXTURE2D(_ReflectionMap, sampler_ReflectionMap, uv);
            drawing.a *= inside;
            return drawing;
        }
        half3 ReflectionPalette(half3 layers, half3 glass)
        {
            half3 masks = layers * _ReflectionLayerStrengths.xyz;
            half3 color = glass * (1 - masks.r);
            color = lerp(color, _ReflectionEdgeColor.rgb, saturate(masks.g));
            color = lerp(color, _ReflectionInkColor.rgb, saturate(masks.b));
            if (_ReflectionSaturation != 1)
            {
                half3 hsv = RgbToHsv(color);
                hsv.y = saturate(hsv.y * _ReflectionSaturation);
                color = HsvToRgb(hsv);
            }
            return color;
        }
        half SampleGlints(float2 uv, half localFacing)
        {
            half4 drawing = SampleReflection(uv, localFacing);
            half glints = drawing.g * drawing.a;
            if (_SecondaryGlintLeft.w > 0)
            {
                drawing = SampleReflection((uv - _SecondaryGlintLeft.xy) / _SecondaryGlintLeft.z + 0.5, localFacing);
                glints = max(glints, drawing.g * drawing.a * _SecondaryGlintLeft.w);
            }
            if (_SecondaryGlintRight.w > 0)
            {
                drawing = SampleReflection((uv - _SecondaryGlintRight.xy) / _SecondaryGlintRight.z + 0.5, localFacing);
                glints = max(glints, drawing.g * drawing.a * _SecondaryGlintRight.w);
            }
            return glints;
        }
        ENDHLSL
        Pass
        {
            Name "DrawnCanopy"
            Tags { "LightMode"="UniversalForwardOnly" }
            Stencil { Ref 1 WriteMask 1 Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma vertex CanopyVertex
            #pragma fragment CanopyFragment
            #pragma shader_feature_local_fragment _AUTHORED_REFLECTION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            half4 CanopyFragment(Interpolated input) : SV_Target
            {
                ClipDebris(input.positionCS);
                half3 normal = NormalizeNormalPerPixel(input.normalWS);
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
                half3 illumination = light.color * light.distanceAttenuation + max(SampleSH(normal), 0) * _AmbientStrength;
                illumination /= max(1, max(illumination.r, max(illumination.g, illumination.b)));
                half3 glass = _GlassColor.rgb;
                half3 fixedLayers = 0;
                #if defined(_AUTHORED_REFLECTION)
                    if (_ReflectionArtwork > 0.5)
                    {
                        fixedLayers = SampleReflection(input.reflectionUV, input.localFacing).rgb;
                        glass = ReflectionPalette(half3(fixedLayers.r, 0, fixedLayers.b), glass);
                    }
                #endif
                half3 color = glass * diffuse * illumination;
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 halfway = SafeNormalize(light.direction + view);
                half ndv = saturate(dot(normal, view));
                half grazing = pow(1 - ndv, 3);
                color = lerp(color, _ReflectionColor.rgb * illumination, grazing * 0.16);
                float3 referenceView = TransformObjectToWorldDir(float3(0, 0, input.localFacing >= 0 ? 1 : -1));
                float3 reflected = TransformWorldToObjectDir(reflect(-view, normal));
                float3 referenceReflection = TransformWorldToObjectDir(reflect(-referenceView, normal));
                float3 lightOS = TransformWorldToObjectDir(light.direction);
                // Subtract the authored pose so curvature changes motion without replacing the drawing.
                float2 uv = input.reflectionUV;
                uv += (reflected.xy - referenceReflection.xy - lightOS.xy) * float2(0.31, 0.10);
                float t = saturate((uv.y - 0.16) / 0.66);
                half facing = smoothstep(0.03, 0.35, ndv);
                half lightFacing = saturate(dot(normal, halfway));
                half response = smoothstep(0.35, 0.88, lightFacing);
                response *= facing * _DrawnReflectionStrength * lerp(0.15, 1, light.shadowAttenuation);
                #if defined(_AUTHORED_REFLECTION)
                    uv = TRANSFORM_TEX(uv, _ReflectionMap);
                    uv.y = clamp(uv.y, _ReflectionVerticalRange.x, _ReflectionVerticalRange.y);
                    half3 reflection;
                    if (_ReflectionArtwork > 0.5)
                    {
                        half glints = SampleGlints(uv, input.localFacing);
                        reflection = ReflectionPalette(half3(fixedLayers.r, glints, fixedLayers.b), _GlassColor.rgb) - glass;
                    }
                    else
                    {
                        half4 drawing = SampleReflection(uv, input.localFacing);
                        reflection = max(drawing.rgb - glass, 0) * drawing.a;
                    }
                    color += reflection * diffuse * illumination * response;
                #else
                    float pressure = pow(saturate(sin(t * PI)), 0.7);
                    float center = 0.46 + 0.09 * t - 0.05 * t * t;
                    float edge = 0.003 * sin(uv.y * 63) + 0.0015 * sin(uv.y * 127);
                    float aa = max(fwidth(uv.x), 0.001);
                    float width = 0.13 * pressure;
                    float reflection = 1 - smoothstep(width - aa * 1.5, width + aa * 1.5,
                        abs(uv.x - center + edge));
                    reflection *= smoothstep(0.16, 0.19, uv.y) * (1 - smoothstep(0.79, 0.82, uv.y));
                    float rimCenter = center + width + 0.065;
                    float rim = 1 - smoothstep(0.008 - aa, 0.008 + aa, abs(uv.x - rimCenter + edge));
                    rim *= smoothstep(0.30, 0.34, uv.y) * (1 - smoothstep(0.71, 0.74, uv.y));
                    rim *= 1 - smoothstep(0.53, 0.54, uv.y) * (1 - smoothstep(0.59, 0.60, uv.y));
                    color = lerp(color, _ReflectionColor.rgb * light.color, reflection * response);
                    color = lerp(color, _ReflectionEdgeColor.rgb * light.color, rim * response);
                #endif
                color = lerp(color * _BaseColor.rgb, _BaseColor.rgb * diffuse * illumination, _DamageFlash);
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex CanopyShadowVertex
            #pragma fragment CanopyShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection, _LightPosition;
            float4 CanopyShadowVertex(Input input) : SV_POSITION
            {
                float3 position = TransformObjectToWorld(input.positionOS.xyz);
                float3 normal = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction = normalize(_LightPosition - position);
                #else
                    float3 direction = _LightDirection;
                #endif
                float4 clipPosition = TransformWorldToHClip(ApplyShadowBias(position, normal, direction));
                return ApplyShadowClamping(clipPosition);
            }
            half4 CanopyShadowFragment(float4 positionCS : SV_POSITION) : SV_Target
            {
                ClipDebris(positionCS);
                return 0;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma vertex CanopyVertex
            #pragma fragment CanopyNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 CanopyNormalsFragment(Interpolated input) : SV_Target
            {
                ClipDebris(input.positionCS);
                half3 normal = NormalizeNormalPerPixel(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(normal);
                    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
                #else
                    return half4(normal, 0);
                #endif
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex CanopyVertex
            #pragma fragment CanopyDepthFragment
            half CanopyDepthFragment(Interpolated input) : SV_Target
            {
                ClipDebris(input.positionCS);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
}
