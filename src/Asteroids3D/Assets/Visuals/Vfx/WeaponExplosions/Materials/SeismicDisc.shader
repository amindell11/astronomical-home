Shader "Astronomical/Vfx/Painted Seismic Disc"
{
    Properties
    {
        _BaseMap ("Painted disc", 2D) = "white" {}
        _FillOpacity ("Fill opacity", Range(0,1)) = 0.38
        _EdgeGain ("Edge brightness", Range(0,2)) = 1
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
                float _FillOpacity;
                float _EdgeGain;
            CBUFFER_END
            struct Input { float4 position:POSITION; float2 uv:TEXCOORD0; };
            struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };
            Varying Vert(Input v)
            {
                Varying o; o.position=TransformObjectToHClip(v.position.xyz); o.uv=v.uv; return o;
            }
            half4 Frag(Varying i):SV_Target
            {
                half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
                float edge=smoothstep(0.92,0.98,length(i.uv-0.5)/0.45);
                c.a*=lerp(_FillOpacity,saturate(_EdgeGain),edge);
                c.rgb*=c.a*lerp(1,max(1,_EdgeGain),edge);
                return c;
            }
            ENDHLSL
        }
    }
}
