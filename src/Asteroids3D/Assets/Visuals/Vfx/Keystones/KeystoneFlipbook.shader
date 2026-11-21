Shader "Astronomical/Vfx/Keystone Flipbook"
{
    Properties
    {
        _BaseMap ("Sheet", 2D) = "white" {}
        _Columns ("Columns", Float) = 1
        _Rows ("Rows", Float) = 1
        [HideInInspector] _Frame ("Frame", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _Columns;
                float _Rows;
                float _Frame;
            CBUFFER_END
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };
            Varying Vert(Input v) { Varying o; o.position=TransformObjectToHClip(v.vertex.xyz); o.uv=v.uv; return o; }
            half4 Frag(Varying i):SV_Target
            {
                float col=fmod(_Frame,_Columns);
                float row=floor(_Frame/_Columns);
                float2 uv=float2((col+i.uv.x)/_Columns, 1-(row+1-i.uv.y)/_Rows);
                return SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv);
            }
            ENDHLSL
        }
    }
}
