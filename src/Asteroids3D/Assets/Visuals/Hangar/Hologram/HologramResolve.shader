Shader "Astronomical/Hangar/Hologram Resolve"
{
    Properties
    {
        _Tint ("Lift And Rim Tint", Color) = (0.91,0.933,0.973,1)
        _Saturation ("Paint Saturation", Range(0,1.5)) = 0.85
        _Lift ("Dark Paint Lift", Range(0,1)) = 0.08
        _FillLight ("Fill Light", Range(0,2)) = 0.62
        _FillBody ("Fill Body", Range(0,1)) = 0.6
        _PigmentKeep ("Pigment Keep", Range(0,1)) = 0.6
        _RimLight ("Rim Light", Range(0,2)) = 0.18
        _RimPower ("Rim Power", Range(0.5,6)) = 2.5
        _LineColor ("Line Color", Color) = (1,0.945,0.839,1)
        _LineLight ("Outline Light", Range(0,2)) = 1
        _LineBody ("Line Body", Range(0,1)) = 0.85
        _CreaseLight ("Crease Light", Range(0,2)) = 0.55
        _PaintEdgeLight ("Paint Edge Light", Range(0,2)) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            Name "HologramResolve"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Resolve
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint, _LineColor;
                half _Saturation, _Lift, _FillLight, _FillBody, _PigmentKeep, _RimLight, _RimPower;
                half _LineLight, _LineBody, _CreaseLight, _PaintEdgeLight;
            CBUFFER_END
            static const float2 Taps[4] = { float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1) };
            static const half3 Luma = half3(0.2126, 0.7152, 0.0722);

            bool HasHull(float rawDepth)
            {
                #if UNITY_REVERSED_Z
                    return rawDepth > 0.0;
                #else
                    return rawDepth < 1.0;
                #endif
            }

            half4 Resolve(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 stage = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                // Hull writes alpha 1 and the contour magenta at 0.5, so alpha splits fill from outline.
                half hull = saturate(2.0 * stage.a - 1.0);
                // Below half alpha, magenta is the contour's edge; anything else is a soft sprite and passes through.
                half loose = stage.a < 0.5
                    ? 1.0 - saturate((min(stage.r, stage.b) - stage.g) / max(stage.a, 0.001)) : 0.0;
                half outline = (stage.a < 0.5 ? 2.0 * stage.a : 2.0 - 2.0 * stage.a) * (1.0 - loose);
                half3 light = 0.0;
                half body = 0.0;
                if (hull > 0.0)
                {
                    half3 paint = saturate((stage.rgb - outline * half3(1.0, 0.0, 1.0)) / max(hull, 0.001));
                    half luminance = dot(paint, Luma);
                    half3 kept = lerp(luminance.xxx, paint, _Saturation);
                    kept += (1.0 - kept) * _Lift * _Tint.rgb;
                    half top = max(paint.r, max(paint.g, paint.b));
                    half low = min(paint.r, min(paint.g, paint.b));
                    half pigment = smoothstep(0.25, 0.6, (top - low) / max(top, 0.001)) * _PigmentKeep;
                    half rim = 0.0;
                    half crease = 0.0;
                    half paintEdge = 0.0;
                    float rawDepth = SampleSceneDepth(uv);
                    if (HasHull(rawDepth))
                    {
                        float3 normal = SampleSceneNormals(uv);
                        float3 position = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                        rim = pow(1.0 - saturate(dot(normal, normalize(GetCameraPositionWS() - position))), _RimPower);
                        float eye = LinearEyeDepth(rawDepth, _ZBufferParams);
                        half tapLuminance[4];
                        bool painted = true;
                        [unroll] for (int i = 0; i < 4; i++)
                        {
                            float2 tap = uv + Taps[i] * _ScreenSize.zw;
                            float tapDepth = SampleSceneDepth(tap);
                            if (HasHull(tapDepth))
                            {
                                half bend = 1.0 - dot(normal, SampleSceneNormals(tap));
                                half jump = abs(eye - LinearEyeDepth(tapDepth, _ZBufferParams)) / eye;
                                crease = max(crease, saturate(max((bend - 0.03) / 0.15, (jump - 0.005) / 0.0105)));
                            }
                            half4 neighbour = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, tap);
                            painted = painted && neighbour.a > 0.98;
                            tapLuminance[i] = dot(neighbour.rgb, Luma);
                        }
                        if (painted)
                            paintEdge = smoothstep(0.07, 0.22, length(float2(tapLuminance[0] - tapLuminance[1],
                                tapLuminance[2] - tapLuminance[3])));
                    }
                    half inner = min(crease * _CreaseLight + paintEdge * _PaintEdgeLight, 2.0);
                    light = kept * lerp(_FillLight, 1.0, pigment) + _Tint.rgb * rim * _RimLight + _LineColor.rgb * inner;
                    body = lerp(lerp(_FillBody, 1.0, pigment), 1.0, saturate(inner) * _LineBody);
                }
                half3 color = light * hull + _LineColor.rgb * _LineLight * outline + stage.rgb * loose * _FillLight;
                half alpha = body * hull + _LineBody * outline * saturate(_LineLight) + stage.a * loose * _FillBody;
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
