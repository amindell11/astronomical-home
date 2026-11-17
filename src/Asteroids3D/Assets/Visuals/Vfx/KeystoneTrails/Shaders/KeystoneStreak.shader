Shader "Astronomical/Vfx/Keystone Streak"
{
    Properties
    {
        _MainTex ("Streak", 2D) = "white" {}
        [MainColor] [HDR] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Fill ("Fill", Range(0, 1)) = 1
        [IntRange] _Bands ("Bands", Range(0, 5)) = 0
        _Gradient ("Gradient", Range(0, 1)) = 0
        _HotCore ("Hot Core", Range(0, 1)) = 0
        _InkColor ("Ink Color", Color) = (0, 0, 0, 1)
        _InkOpacity ("Ink Opacity", Range(0, 1)) = 1
        _InkWidth ("Ink Width", Range(0, 1)) = 0
        _Hatch ("Hatch", Range(0, 1)) = 0
        _HatchGap ("Hatch Gap", Range(2, 12)) = 5
        _HatchAngle ("Hatch Angle", Float) = 45
        _Additive ("Additive", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            // Premultiplied. Body alpha shrinks as additive rises; ink stays a normal over.
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _BaseColor;
                float _Fill;
                float _Bands;
                float _Gradient;
                float _HotCore;
                half4 _InkColor;
                float _InkOpacity;
                float _InkWidth;
                float _Hatch;
                float _HatchGap;
                float _HatchAngle;
                float _Additive;
            CBUFFER_END
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varying { float4 position:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            Varying Vert(Input v)
            {
                Varying o;
                o.position = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            float Band(float g)
            {
                float steps = floor(_Bands) + 1;
                float banded = floor(min(g, 0.9999) * steps) / max(steps - 1, 1);
                return lerp(g, banded, step(0.5, _Bands));
            }
            half4 Frag(Varying i) : SV_Target
            {
                float2 uv = i.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                float coverage = saturate(tex.r);
                float alongRaw = saturate(tex.g);
                float inside = saturate(tex.b);
                float along = Band(alongRaw);
                float tone = lerp(1, along, _Gradient);

                half3 body = _BaseColor.rgb * i.color.rgb;
                body = lerp(body, half3(1, 1, 1), saturate(along * _HotCore));
                float ang = radians(_HatchAngle);
                float gap = max(_HatchGap, 2);
                float stripe = step(frac(dot(i.position.xy, float2(cos(ang), sin(ang))) / gap), 1.0 / gap);
                body += stripe * _Hatch * along;

                float bodyA = coverage * tone * _Fill * _BaseColor.a * i.color.a;
                half3 bodyPremul = body * bodyA;
                float bodyOutA = bodyA * (1 - _Additive);

                float inkW = _InkWidth * (0.3 + 0.9 * alongRaw);
                float inkOn = 1 - step(inkW, inside);
                float inkA = inkOn * coverage * _InkOpacity * _InkColor.a * _BaseColor.a * i.color.a;
                half3 inkPremul = _InkColor.rgb * inkA;
                return half4(inkPremul + bodyPremul * (1 - inkA), inkA + bodyOutA * (1 - inkA));
            }
            ENDHLSL
        }
    }
}
