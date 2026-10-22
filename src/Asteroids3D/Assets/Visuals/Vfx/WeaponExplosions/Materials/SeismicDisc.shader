Shader "Astronomical/Vfx/Painted Seismic Disc"
{
    Properties
    {
        _BaseMap ("Painted disc", 2D) = "white" {}
        _WaveProgress ("Wave progress", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float _WaveProgress;
            CBUFFER_END
            struct Input { float4 position:POSITION; float2 uv:TEXCOORD0; };
            struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };
            Varying Vert(Input v)
            {
                Varying o; o.position=TransformObjectToHClip(v.position.xyz); o.uv=v.uv; return o;
            }
            half4 Frame(float2 uv, float frame)
            {
                float2 cell=float2(fmod(frame,4),7-floor(frame/4));
                half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,(cell+uv)/float2(4,8));
                c.rgb*=c.a;
                return c;
            }
            half4 Frag(Varying i):SV_Target
            {
                float frame=saturate(_WaveProgress)*31;
                return lerp(Frame(i.uv,floor(frame)),Frame(i.uv,min(floor(frame)+1,31)),frac(frame));
            }
            ENDHLSL
        }
    }
}
