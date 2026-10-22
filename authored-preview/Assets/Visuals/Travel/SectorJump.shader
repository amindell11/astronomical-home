Shader "Astronomical/Travel/SectorJump"
{
    Properties
    {
        _FromSky("Outgoing locale", 2D) = "black" {}
        _ToSky("Incoming locale", 2D) = "black" {}
        _ShipImage("Ship", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Opaque" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert_img
            #pragma fragment Frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            sampler2D _FromSky, _ToSky, _ShipImage;
            float4 _Direction, _Motion, _Streaks, _Nebula, _Ship, _Exhaust;
            float4 _StreakColor, _ExhaustColor;

            float Hash(float value) { return frac(sin(value * 127.1 + 31.7) * 43758.5453); }

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
                return tex2D(_ShipImage, saturate(local)) * inside;
            }

            float4 Frag(v2f_img input) : SV_Target
            {
                float2 p = (input.uv - 0.5) * float2(_Motion.w, 1);
                float2 direction = _Direction.xy;
                float along = dot(p, direction);
                float across = dot(p, float2(-direction.y, direction.x));
                float stretch = 1 - rcp(1 + _Nebula.x * _Motion.x);
                float2 skyP = p - direction * along * stretch;
                float crossTerm = abs(direction.x * direction.y * stretch);
                float2 extent = 0.5 * float2(
                    (1 - direction.x * direction.x * stretch) * _Motion.w + crossTerm,
                    1 - direction.y * direction.y * stretch + crossTerm * _Motion.w);
                extent += abs(direction) * (abs(_Nebula.z) + _Nebula.y * _Motion.x * 0.5);
                float fit = min(_Motion.w / (2 * extent.x), 1 / (2 * extent.y));
                float2 skyUV = 0.5 + (skyP + direction * _Nebula.z) * fit / float2(_Motion.w, 1);
                float3 sky = 0;
                [unroll] for (int sampleIndex = 0; sampleIndex < 24; sampleIndex++)
                {
                    float offset = (sampleIndex + 0.5) / 24.0 - 0.5;
                    sky += Sky(skyUV + direction / float2(_Motion.w, 1) * offset * _Nebula.y * _Motion.x * fit);
                }
                sky /= 24;
                float stars = 0;
                float streakRow = floor(across * _Streaks.x);
                float aa = max(fwidth(across), 0.0001);
                [unroll] for (int adjacent = -1; adjacent <= 1; adjacent++)
                {
                    float id = streakRow + adjacent;
                    float random = Hash(id);
                    float transverse = abs(across - (id + Hash(id + 91)) / _Streaks.x);
                    float period = lerp(0.35, 0.9, random);
                    float longitudinal = abs(frac((along + _Motion.y * lerp(0.65, 1.25, random)) / period + random) - 0.5) * period;
                    float length = _Streaks.y * _Motion.x * lerp(0.4, 1, Hash(id + 42));
                    float end = 1 - smoothstep(length * 0.25, length * 0.5 + aa, longitudinal);
                    float lineIntensity = 1 - smoothstep(_Streaks.z * 0.5, _Streaks.z * 0.5 + aa, transverse);
                    float glow = exp2(-transverse / max(_Streaks.z * 3, aa)) * 0.15;
                    stars += (lineIntensity + glow) * end * lerp(0.25, 1, random);
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
                float4 core = Ship(p);
                float coreWeight = lerp(1, _Ship.w, saturate(_Ship.y));
                float3 color = sky * (1 - saturate(trail.a)) + trail.rgb;
                color = color * (1 - core.a * coreWeight) + core.rgb * coreWeight;
                return float4(color, 1);
            }
            ENDHLSL
        }
    }
}
