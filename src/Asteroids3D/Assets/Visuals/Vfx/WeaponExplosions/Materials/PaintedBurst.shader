Shader "Astronomical/Vfx/Painted Weapon Burst"
{
    Properties
    {
        _BaseMap ("Painted frames", 2D) = "white" {}
        _CoreGlow ("Core brightness", Range(0, 2)) = 0.3
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
                float _CoreGlow;
            CBUFFER_END
            struct Input { float4 position:POSITION; half4 color:COLOR; float4 uv:TEXCOORD0; float blend:TEXCOORD1; };
            struct Varying { float4 position:SV_POSITION; half4 color:COLOR; float4 uv:TEXCOORD0; float blend:TEXCOORD1; };
            Varying Vert(Input v)
            {
                Varying o;
                o.position=TransformObjectToHClip(v.position.xyz);
                o.color=v.color; o.uv=v.uv; o.blend=v.blend;
                return o;
            }
            half4 Paint(float2 uv)
            {
                half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv);
                half core=smoothstep(0.6,0.95,min(c.r,min(c.g,c.b)));
                c.rgb*=c.a*(1+core*_CoreGlow);
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
