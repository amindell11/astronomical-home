Shader "Astronomical/Travel/SectorJump"
{
    Properties
    {
        _FromSky("Outgoing locale", 2D) = "black" {}
        _ToSky("Incoming locale", 2D) = "black" {}
        _ShipImage("Ship", 2D) = "black" {}
        _ShipImageTo("Ship in destination lighting", 2D) = "black" {}
        _Opacity("Opacity", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Blend SrcAlpha OneMinusSrcAlpha, Zero One
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            sampler2D _FromSky, _ToSky, _ShipImage, _ShipImageTo;
            float4 _Direction, _Motion, _Streaks, _Nebula, _Ship, _Exhaust;
            float4 _StreakColor, _ExhaustColor, _StreakDistribution;
            float _RawBackgroundBlend, _Opacity;
            int _Seed;

            float StreakHash(uint value)
            {
                value ^= (uint)_Seed;
                value ^= value >> 16;
                value *= 0x7feb352du;
                value ^= value >> 15;
                value *= 0x846ca68bu;
                value ^= value >> 16;
                return (value & 0x00ffffffu) / 16777216.0;
            }

            float3 Sky(float2 uv)
            {
                return lerp(tex2D(_FromSky, saturate(uv)).rgb,
                    tex2D(_ToSky, saturate(uv)).rgb, _Motion.z);
            }

            float4 Ship(float2 p)
            {
                float2 local = float2(dot(p, float2(_Direction.z, _Direction.w)),
                    dot(p, float2(-_Direction.w, _Direction.z))) / _Ship.x + 0.5;
                float inside = step(0, local.x) * step(local.x, 1) * step(0, local.y) * step(local.y, 1);
                return lerp(tex2D(_ShipImage, saturate(local)),
                    tex2D(_ShipImageTo, saturate(local)), _Motion.z) * inside;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                // URP camera intermediates are stored y-flipped on D3D, so texcoord's origin is bottom-left.
                float2 p = (input.texcoord - 0.5) * float2(_Motion.w, 1);
                float2 direction = _Direction.xy;
                float along = dot(p, direction);
                float across = dot(p, float2(-direction.y, direction.x));
                float treatment = 1 - _RawBackgroundBlend;
                float stretch = 1 - rcp(1 + _Nebula.x * _Motion.x * treatment);
                float softness = _Nebula.y * _Motion.x * treatment;
                float drift = _Nebula.z * treatment;
                float2 skyP = p - direction * along * stretch;
                float crossTerm = abs(direction.x * direction.y * stretch);
                float2 extent = 0.5 * float2(
                    (1 - direction.x * direction.x * stretch) * _Motion.w + crossTerm,
                    1 - direction.y * direction.y * stretch + crossTerm * _Motion.w);
                extent += abs(direction) * (abs(drift) + softness * 0.5);
                float fit = min(_Motion.w / (2 * extent.x), 1 / (2 * extent.y));
                float2 skyUV = 0.5 + (skyP + direction * drift) * fit / float2(_Motion.w, 1);
                float3 sky = 0;
                [unroll] for (int sampleIndex = 0; sampleIndex < 64; sampleIndex++)
                {
                    float offset = (sampleIndex + 0.5) / 64.0 - 0.5;
                    sky += Sky(skyUV + direction / float2(_Motion.w, 1) * offset * softness * fit);
                }
                sky /= 64;
                float stars = 0;
                float aa = max(fwidth(across), 0.0001);
                float alongExtent = abs(direction.x) * _Motion.w + abs(direction.y);
                float acrossExtent = abs(direction.y) * _Motion.w + abs(direction.x);
                float period = alongExtent + 4 * _Streaks.y;
                [loop] for (int streakIndex = 0; streakIndex < (int)_Streaks.x; streakIndex++)
                {
                    uint id = (uint)streakIndex * 7u;
                    float variation = _StreakDistribution.x;
                    float depth = lerp(1, lerp(0.35, 1.4, StreakHash(id + 2u)), variation);
                    float starAcross = (StreakHash(id) - 0.5) * acrossExtent * _StreakDistribution.y;
                    float starAlong = (frac(StreakHash(id + 1u) - _Motion.y * depth / period) - 0.5) * period;
                    float fan = (StreakHash(id + 6u) * 2 - 1) * _StreakDistribution.z;
                    float2 delta = float2(along - starAlong, across - starAcross);
                    float transverse = abs(dot(delta, float2(-sin(fan), cos(fan))));
                    float longitudinal = abs(dot(delta, float2(cos(fan), sin(fan))));
                    float streakLength = _Streaks.y * _Motion.x * depth *
                        lerp(1, lerp(0.4, 1.5, StreakHash(id + 3u)), variation);
                    float width = _Streaks.z * depth *
                        lerp(1, lerp(0.5, 1.8, StreakHash(id + 4u)), variation);
                    float brightness = lerp(1, lerp(0.2, 1.3, StreakHash(id + 5u)), variation);
                    float end = 1 - smoothstep(streakLength * 0.25, streakLength * 0.5 + aa, longitudinal);
                    float lineIntensity = 1 - smoothstep(width * 0.5, width * 0.5 + aa, transverse);
                    float glow = exp2(-transverse / max(width * 3, aa)) * 0.15;
                    stars += (lineIntensity + glow) * end * brightness;
                }
                sky += stars * _StreakColor.rgb * _Streaks.w * _Motion.x;
                sky *= 1 - _Nebula.w * _Motion.x * smoothstep(0.2, 0.9, length(p));

                float2 local = float2(dot(p, float2(_Direction.z, _Direction.w)),
                    dot(p, float2(-_Direction.w, _Direction.z))) / _Ship.x;
                float exhaustDistance = -local.y - 0.21;
                float exhaustFade = saturate(1 - exhaustDistance / max(_Exhaust.x, 0.0001));
                float exhaustWidth = 0.018 * exhaustFade + 0.004;
                float nozzles = exp2(-pow((abs(local.x) - 0.105) / exhaustWidth, 2));
                sky += nozzles * exhaustFade * exhaustFade * step(0, exhaustDistance) * _Exhaust.y * _ExhaustColor.rgb;

                float4 trail = 0;
                float weightSum = 0;
                [unroll] for (int sampleIndex = 1; sampleIndex <= 32; sampleIndex++)
                {
                    float t = sampleIndex / 32.0;
                    float weight = pow(1 - t, _Exhaust.z);
                    trail += Ship(p + direction * t * _Ship.z * _Ship.x) * weight;
                    weightSum += weight;
                }
                trail *= _Ship.y / max(weightSum, 0.001);
                trail /= max(1, trail.a);
                float4 core = Ship(p);
                float coreWeight = lerp(1, _Ship.w, saturate(_Ship.y));
                float3 color = sky * (1 - trail.a) + trail.rgb;
                color = color * (1 - core.a * coreWeight) + core.rgb * coreWeight;
                return float4(saturate(color), _Opacity);
            }
            ENDHLSL
        }
    }
}
