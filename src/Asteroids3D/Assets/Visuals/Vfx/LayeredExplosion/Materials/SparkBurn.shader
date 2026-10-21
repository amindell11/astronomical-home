Shader "Astronomical/Vfx/Traveling Ember"
{
    Properties { _BaseMap ("Paint", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            struct Input { float4 vertex:POSITION; float3 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varying { float4 position:SV_POSITION; float3 uv:TEXCOORD0; half4 color:COLOR; };
            Varying Vert(Input v) { Varying o; o.position=TransformObjectToHClip(v.vertex.xyz); o.uv=v.uv; o.color=v.color; return o; }
            half4 Frag(Varying i):SV_Target
            {
                clip(i.uv.x-saturate((i.uv.z-.57)/.43));
                return SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv.xy)*i.color;
            }
            ENDHLSL
        }
    }
}
