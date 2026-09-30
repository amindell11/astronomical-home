Shader "Astronomical/Vfx/Crackle Flipbook"
{
    Properties
    {
        _BaseMap ("Flipbook (one row of frames, head up)", 2D) = "white" {}
        _GlowMap ("Glow", 2D) = "black" {}
        [MainColor] _BaseColor ("Tint and fade", Color) = (1,1,1,1)
        _Frames ("Frames", Float) = 6
        _Fps ("Frames per second", Float) = 14
        [ToggleUI] _Crossfade ("Crossfade between frames", Float) = 1
        [ToggleUI] _Ease ("Ease the crossfade", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            // Premultiplied output: the body blends over the scene, the glow adds under it.
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_GlowMap); SAMPLER(sampler_GlowMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _Frames;
                float _Fps;
                float _Crossfade;
                float _Ease;
            CBUFFER_END
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; };
            Varying Vert(Input v) { Varying o; o.position=TransformObjectToHClip(v.vertex.xyz); o.uv=v.uv; return o; }
            half4 Frame(float index, float2 uv)
            {
                half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,float2((fmod(index,_Frames)+uv.x)/_Frames,uv.y));
                c.rgb*=c.a;
                return c;
            }
            half4 Frag(Varying i):SV_Target
            {
                float t=_Time.y*_Fps;
                float k=floor(t);
                float f=t-k;
                f=lerp(f,smoothstep(0,1,f),_Ease)*_Crossfade;
                half4 body=lerp(Frame(k,i.uv),Frame(k+1,i.uv),f);
                half4 glow=SAMPLE_TEXTURE2D(_GlowMap,sampler_GlowMap,i.uv);
                half3 rgb=body.rgb+glow.rgb*glow.a*(1-body.a);
                return half4(rgb*_BaseColor.rgb,body.a)*_BaseColor.a;
            }
            ENDHLSL
        }
    }
}
