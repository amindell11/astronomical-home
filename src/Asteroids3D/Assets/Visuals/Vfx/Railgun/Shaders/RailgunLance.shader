Shader "Astronomical/Vfx/Railgun Lance"
{
    Properties
    {
        _BodyMap ("Body, four frames", 2D) = "white" {}
        _FringeMap ("Fringe, four frames", 2D) = "black" {}
        _MuzzleMap ("Muzzle", 2D) = "black" {}
        _Fps ("Crackle frames per second", Float) = 22
        _FringeSpacing ("Fringe repeat distance", Float) = 20
        [HideInInspector] _Shot ("Shot", Float) = 0
        [HideInInspector] _ShotAge ("Shot age", Float) = 0
        [HideInInspector] _BeamLength ("Beam length", Float) = 60
        [HideInInspector] _LineWidth ("Line width", Float) = 1
        [HideInInspector] _DashPhase ("Inward travel", Float) = 0
        [HideInInspector] _DashSpacing ("Dash repeat distance", Float) = 6
        [HideInInspector] _DashGap ("Dash gap fraction", Float) = 0.3
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BodyMap); SAMPLER(sampler_BodyMap);
            TEXTURE2D(_FringeMap); SAMPLER(sampler_FringeMap);
            TEXTURE2D(_MuzzleMap); SAMPLER(sampler_MuzzleMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BodyMap_TexelSize;
                float4 _FringeMap_TexelSize;
                float _Fps;
                float _FringeSpacing;
                float _Shot;
                float _ShotAge;
                float _BeamLength;
                float _LineWidth;
                float _DashPhase;
                float _DashSpacing;
                float _DashGap;
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
            float2 FrameUV(float frame, float2 uv, float4 texelSize)
            {
                float2 inset = texelSize.xy * float2(4, 1) * 0.5;
                uv = clamp(uv, inset, 1 - inset);
                return float2((fmod(frame, 4) + uv.x) * 0.25, uv.y);
            }
            half4 Premultiply(half4 color) { color.rgb *= color.a; return color; }
            half4 BodyFrame(float frame, float2 uv)
            {
                return Premultiply(SAMPLE_TEXTURE2D(_BodyMap, sampler_BodyMap, FrameUV(frame, uv, _BodyMap_TexelSize)));
            }
            half4 FringeFrame(float frame, float2 uv)
            {
                return Premultiply(SAMPLE_TEXTURE2D(_FringeMap, sampler_FringeMap, FrameUV(frame, uv, _FringeMap_TexelSize)));
            }
            half4 Charge(float2 uv, float distance)
            {
                float along = frac((distance + _DashPhase) / _DashSpacing);
                float dash = along / (1 - _DashGap);
                float halfWidth = dash < 0.21 ? dash / 0.21 : lerp(1, 0.38, saturate((dash - 0.21) / 0.79));
                float across = abs(uv.y * 2 - 1);
                float aa = max(fwidth(across), 0.001);
                float shape = (1 - smoothstep(halfWidth - aa, halfWidth, across)) * (1 - step(1, dash));
                float core = smoothstep(0.15, 0.21, dash) * (1 - smoothstep(0.6, 0.65, dash));
                core *= 1 - smoothstep(0.2, 0.2 + aa, across);
                return half4(lerp(half3(0.107, 0.597, 1), half3(0.694, 0.930, 1), core) * shape, shape);
            }
            half4 Frag(Varying i):SV_Target
            {
                float distance = i.uv.x * _BeamLength;
                if (_Shot < 0.5) return Charge(i.uv, distance) * i.color.a;

                float time = _ShotAge * _Fps;
                float frame = floor(time);
                float blend = smoothstep(0, 1, frac(time));
                float2 bodyUV = float2(lerp(40.0 / 2048, 2000.0 / 2048, i.uv.x), i.uv.y);
                half4 body = lerp(BodyFrame(frame, bodyUV), BodyFrame(frame + 1, bodyUV), blend);
                float2 fringeUV = float2(frac(distance / _FringeSpacing - _ShotAge * 0.7), i.uv.y);
                half4 fringe = lerp(FringeFrame(frame, fringeUV), FringeFrame(frame + 1, fringeUV), blend);
                fringe *= smoothstep(0, 0.4, distance) * smoothstep(0, 0.4, _BeamLength - distance);
                half4 color = fringe + body * (1 - fringe.a);

                float vertical = i.uv.y - 0.5;
                float glow = exp2(-110 * vertical * vertical) * 0.25;
                glow *= smoothstep(0, 0.7, distance) * smoothstep(0, 0.7, _BeamLength - distance);
                color.rgb += half3(0.05, 0.26, 1) * glow * (1 - color.a);

                float2 muzzleUV = float2(distance / _LineWidth + 0.25, i.uv.y);
                half4 muzzle = Premultiply(SAMPLE_TEXTURE2D(_MuzzleMap, sampler_MuzzleMap, saturate(muzzleUV)));
                muzzle *= (1 - step(1, muzzleUV.x)) * exp(-_ShotAge * 18);
                color = muzzle + color * (1 - muzzle.a);
                return color * i.color.a;
            }
            ENDHLSL
        }
    }
}
