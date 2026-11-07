Shader "Hidden/UI/CanopyProjection"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        TEXTURE2D(_HudInk);
        TEXTURE2D(_PreviousInk);
        TEXTURE2D_X(_SceneImage);
        float2 _Resolution;
        float _Decay, _Clock, _PersonalityEnabled, _AppearAge, _TransitionAge, _HitAge;
        float4 _Beam, _Phosphor, _Glass;

        float2 ImageUv(float2 p)
        {
            #if UNITY_UV_STARTS_AT_TOP
            p.y = 1.0 - p.y;
            #endif
            return p;
        }

        float Hash(float2 p)
        {
            return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
        }

        float CanopyRadius(float2 p)
        {
            float2 q = (p - float2(.5, .46)) / .49;
            float exponent = 4.0 - 2.0 * smoothstep(-.25, .35, q.y);
            return pow(pow(abs(q.x), exponent) + pow(abs(q.y), exponent), 1.0 / exponent);
        }

        float2 CanopyOffset(float2 p, float strength)
        {
            float radius = CanopyRadius(p);
            if (radius < .26) return 0.0;
            float2 gradient = float2(
                CanopyRadius(p + float2(.0001, 0.0)) - CanopyRadius(p - float2(.0001, 0.0)),
                CanopyRadius(p + float2(0.0, .0001)) - CanopyRadius(p - float2(0.0, .0001)));
            float rim = smoothstep(.26, 1.12, radius);
            float pixels = strength * 30.0 * (_Resolution.x / 1600.0) * rim * rim;
            return normalize(gradient / _Resolution) * pixels / _Resolution;
        }

        float3 Ink(float2 p)
        {
            if (any(p < 0.0) || any(p > 1.0)) return 0.0;
            return SAMPLE_TEXTURE2D(_PreviousInk, sampler_LinearClamp, ImageUv(p)).rgb;
        }

        float3 Bloom(float2 p)
        {
            if (any(p < 0.0) || any(p > 1.0)) return 0.0;
            float4 ink = SAMPLE_TEXTURE2D(_HudInk, sampler_LinearClamp, ImageUv(p));
            return LinearToSRGB(ink.rgb) * ink.a;
        }

        float4 History(Varyings input) : SV_Target
        {
            float3 now = LinearToSRGB(SAMPLE_TEXTURE2D(_HudInk, sampler_LinearClamp, input.texcoord).rgb);
            float3 old = 0.0;
            if (_Decay > 0.0)
                old = SAMPLE_TEXTURE2D(_PreviousInk, sampler_LinearClamp, input.texcoord).rgb * _Decay;
            return float4(max(now, old), 1.0);
        }

        float4 Composite(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float3 sceneLinear = SAMPLE_TEXTURE2D_X(_SceneImage, sampler_LinearClamp, input.texcoord).rgb;
            float3 scene = LinearToSRGB(sceneLinear);
            float2 p = ImageUv(input.texcoord);
            float2 center = p - .5;
            float edge = smoothstep(.025, .45, dot(center, center));
            float pixelScale = _Resolution.x / 1600.0;
            float glow = _Beam.x;
            float aberration = _Beam.y * pixelScale;
            float beamJitter = _Beam.z * pixelScale;
            float scanlines = _Phosphor.x;
            float flicker = _Phosphor.y;
            float warmth = _Phosphor.z;
            float wear = _Phosphor.w;
            float glassDistortion = _Glass.x;
            float damageShudder = _Glass.y * pixelScale;
            float transitionGlitch = _Glass.z;
            if (_PersonalityEnabled > .5)
            {
                p += CanopyOffset(p, glassDistortion);
                float band = floor(p.y * _Resolution.y / 3.0);
                p.x += (Hash(float2(band, floor(_Clock * 11.0))) - .5) * beamJitter / _Resolution.x;
                float kick = _HitAge >= 0.0 && _HitAge < .7 ? damageShudder * exp(-_HitAge * 7.0) : 0.0;
                float2 shudder = float2(sin(_HitAge * 71.0), sin(_HitAge * 53.0) * .65) * kick;
                p -= shudder / _Resolution;
                if (_TransitionAge >= 0.0 && _TransitionAge < .35)
                {
                    float tear = step(.75, Hash(float2(floor(p.y * 18.0), floor(_TransitionAge * 9.0))));
                    p.x += tear * sin(_TransitionAge * 39.0) * transitionGlitch * .025 * (1.0 - _TransitionAge / .35);
                }
            }
            float3 light = Ink(p);
            if (_PersonalityEnabled > .5)
            {
                float2 separation = normalize(center + float2(.0001, .0001)) * aberration * edge / _Resolution;
                float3 split = float3(Ink(p + separation).r, light.g, Ink(p - separation).b);
                light = lerp(light, split, .88);
                float grain = Hash(floor(p * _Resolution * .8));
                light *= 1.0 - wear * .62 * (1.0 - grain);
                light *= 1.0 - scanlines * .48 * (.5 + .5 * cos(p.y * _Resolution.y * 3.14159265));
                float radius = (1.5 + glow * 4.0) * pixelScale;
                float2 d = radius / _Resolution;
                float3 halo = Bloom(p) * .16;
                halo += (Bloom(p + float2(d.x, 0.0)) + Bloom(p - float2(d.x, 0.0))
                    + Bloom(p + float2(0.0, d.y)) + Bloom(p - float2(0.0, d.y))) * .12;
                halo += (Bloom(p + d) + Bloom(p - d) + Bloom(p + float2(d.x, -d.y))
                    + Bloom(p + float2(-d.x, d.y))) * .09;
                light += halo * glow * .65;
                float chroma = max(light.r, max(light.g, light.b)) - min(light.r, min(light.g, light.b));
                light = lerp(light, light * float3(1.08, 1.0, .67), warmth * (1.0 - smoothstep(.06, .3, chroma)));
                if (_AppearAge >= 0.0 && _AppearAge < .8)
                {
                    float dip = (step(.09, _AppearAge) - step(.19, _AppearAge))
                        + (step(.39, _AppearAge) - step(.49, _AppearAge));
                    light *= 1.0 - dip * flicker * .17;
                }
            }
            // Subtract identical conversions so empty HUD light preserves the scene exactly.
            float3 lightDelta = SRGBToLinear(min(scene + max(light, 0.0), 1.0)) - SRGBToLinear(scene);
            return float4(sceneLinear + lightDelta, 1.0);
        }
        ENDHLSL

        Pass
        {
            Name "Phosphor history"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment History
            ENDHLSL
        }
        Pass
        {
            Name "Canopy composite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Composite
            ENDHLSL
        }
    }
}
