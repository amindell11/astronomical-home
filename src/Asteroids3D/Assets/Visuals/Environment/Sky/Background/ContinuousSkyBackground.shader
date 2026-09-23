Shader "Custom/ContinuousSkyBackground"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Panorama", 2D) = "grey" {}
        _Tint ("Tint", Color) = (0.5, 0.5, 0.5, 1)
        [Gamma] _Exposure ("Exposure", Float) = 1
        _Rotation ("Rotation", Float) = 0
        _Parallax ("Parallax", Float) = 0.002
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Background" "RenderType" = "Background" }
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "../SkyCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_HDR;
                float4 _Tint;
                float _Exposure;
                float _Rotation;
                float _Parallax;
            CBUFFER_END

            float3 SamplePanorama(float2 uv, float2 dx, float2 dy)
            {
                float4 encoded = SAMPLE_TEXTURE2D_GRAD(_MainTex, sampler_MainTex, uv, dx, dy);
                return DecodeHDREnvironment(encoded, _MainTex_HDR);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                SkyCoordinates sky = GetSkyCoordinates(input.projectedPosition, 7);
                float2 position = sky.planePosition + sky.cameraPosition * _Parallax;
                float2 uv = position / float2(40 * PI, 20 * PI) + float2(0.25 + _Rotation / 360, 0.35);
                // Derivatives precede wrapping so the join cannot select a coarser mip.
                float2 dx = ddx(uv);
                float2 dy = ddy(uv);
                float t = frac(uv.y / 0.56) * 0.56;
                float2 a = float2(frac(uv.x), 0.15 + t);
                float2 b = float2(a.x, min(a.y + 0.56, 0.85));
                float3 color = lerp(SamplePanorama(b, dx, dy), SamplePanorama(a, dx, dy),
                    smoothstep(0, 0.14, t));
                #if defined(UNITY_COLORSPACE_GAMMA)
                    color *= 2;
                #else
                    color *= 4.59479380;
                #endif
                return float4(color * _Tint.rgb * _Exposure, 1);
            }
            ENDHLSL
        }
    }
}
