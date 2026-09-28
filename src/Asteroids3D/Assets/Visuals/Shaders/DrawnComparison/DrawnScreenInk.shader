Shader "Hidden/Astronomical/Comparison/Screen Ink"
{
    Properties
    {
        _InkColor ("Ink", Color) = (0.003,0.004,0.009,1)
        _InkPixels ("Ink Radius (Pixels)", Range(0.5,4)) = 1.8
        _DepthThreshold ("Relative Depth Break", Range(0.0001,0.02)) = 0.006
        _NormalThreshold ("Normal Break", Range(0.1,1)) = 0.5
        _InkStrength ("Ink Strength", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment InkFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _InkColor;
                float _InkPixels, _DepthThreshold, _NormalThreshold, _InkStrength;
            CBUFFER_END
            float EyeDepth(float2 uv)
            {
                float raw = SampleSceneDepth(uv);
                return IsPerspectiveProjection() ? LinearEyeDepth(raw, _ZBufferParams) : LinearDepthToEyeDepth(raw);
            }
            half4 InkFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float2 offset = _InkPixels / _ScaledScreenParams.xy;
                float depth = EyeDepth(uv);
                float left = EyeDepth(uv - float2(offset.x, 0));
                float right = EyeDepth(uv + float2(offset.x, 0));
                float up = EyeDepth(uv + float2(0, offset.y));
                float down = EyeDepth(uv - float2(0, offset.y));
                float depthBreak = (abs(left + right - 2 * depth) + abs(up + down - 2 * depth)) / max(depth, 0.001);
                float3 normal = SampleSceneNormals(uv);
                float normalBreak = 0;
                normalBreak = max(normalBreak, 1 - dot(normal, SampleSceneNormals(uv - float2(offset.x, 0))));
                normalBreak = max(normalBreak, 1 - dot(normal, SampleSceneNormals(uv + float2(offset.x, 0))));
                normalBreak = max(normalBreak, 1 - dot(normal, SampleSceneNormals(uv - float2(0, offset.y))));
                normalBreak = max(normalBreak, 1 - dot(normal, SampleSceneNormals(uv + float2(0, offset.y))));
                float depthInk = smoothstep(_DepthThreshold, _DepthThreshold * 1.5, depthBreak);
                float normalInk = smoothstep(_NormalThreshold, _NormalThreshold + 0.15, normalBreak);
                float occupied = step(0.1, dot(normal, normal));
                color.rgb = lerp(color.rgb, _InkColor.rgb, max(depthInk, normalInk) * occupied * _InkStrength);
                return color;
            }
            ENDHLSL
        }
    }
}
