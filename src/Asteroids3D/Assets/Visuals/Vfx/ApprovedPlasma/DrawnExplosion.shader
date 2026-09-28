Shader "Astronomical/Studies/Jagged Explosion Flipbook"
{
    Properties
    {
        _BaseMap ("Eight drawn frames", 2D) = "white" {}
        _CoreGlow ("Bright core gain", Range(0,4)) = 1.2
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth test", Float) = 4
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float _CoreGlow;
            CBUFFER_END
            struct Input { float4 vertex:POSITION; float4 uv:TEXCOORD0; float blend:TEXCOORD1; half4 color:COLOR; };
            struct Varying { float4 position:SV_POSITION; float4 uv:TEXCOORD0; float blend:TEXCOORD1; half4 color:COLOR; };
            Varying Vert(Input v)
            {
                Varying o; o.position=TransformObjectToHClip(v.vertex.xyz); o.uv=v.uv; o.blend=v.blend; o.color=v.color; return o;
            }
            half4 Paint(float2 uv)
            {
                half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv);
                c.a=smoothstep(.025,.4,c.a);
                half core=smoothstep(.6,.95,min(c.r,min(c.g,c.b)));
                c.rgb*=1+core*_CoreGlow;
                c.rgb*=c.a;
                return c;
            }
            half4 Frag(Varying i):SV_Target
            {
                half4 c=lerp(Paint(i.uv.xy),Paint(i.uv.zw),i.blend);
                c.rgb*=i.color.rgb*i.color.a; c.a*=i.color.a;
                return c;
            }
            ENDHLSL
        }
    }
}
