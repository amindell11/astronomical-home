Shader "Astronomical/Vfx/Bulwark Ram Brace"
{
    Properties
    {
        _Color ("Plate", Color) = (1, 0.6, 0.1, 1)
        _HotColor ("Hit", Color) = (1, 0.95, 0.8, 1)
        _Plates ("Plates", Float) = 5
        _Thickness ("Band Width", Range(0.02, 0.4)) = 0.22
        _Opacity ("Opacity", Range(0, 1)) = 0.85
        [HideInInspector] _HalfAngle ("Half Angle", Float) = 1.0472
        [HideInInspector] _Extent ("Extent", Float) = 1
        [HideInInspector] _Fade ("Fade", Float) = 1
        [HideInInspector] _Progress ("Hold Progress", Float) = 0
        [HideInInspector] _Elapsed ("Elapsed", Float) = 0
        _HitFade ("Hit Fade Seconds", Range(0.05, 1)) = 0.35
        [HideInInspector] _HitAngles ("Hit Angles", Vector) = (0, 0, 0, 0)
        [HideInInspector] _HitAges ("Hit Ages", Vector) = (9, 9, 9, 9)
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
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _HotColor;
                float _Plates;
                float _Thickness;
                float _Opacity;
                float _HalfAngle;
                float _Extent;
                float _Fade;
                float _Progress;
                float _Elapsed;
                float _HitFade;
                float4 _HitAngles;
                float4 _HitAges;
            CBUFFER_END
            struct Input { float4 vertex:POSITION; };
            struct Varying { float4 position:SV_POSITION; float2 local:TEXCOORD0; };

            Varying Vert(Input v)
            {
                Varying o;
                o.position = TransformObjectToHClip(v.vertex.xyz);
                o.local = v.vertex.xy;
                return o;
            }

            half4 Frag(Varying i) : SV_Target
            {
                // Object space is in arc radii, the nose along +y.
                float r = length(i.local);
                float angle = atan2(i.local.x, i.local.y);
                float aa = max(fwidth(r), 0.004);
                float aaAngle = max(fwidth(angle), 0.002);

                // Plates unfold from the nose out to the brace's edge.
                float shown = _HalfAngle * saturate(_Extent);
                float inArc = 1 - smoothstep(shown - aaAngle, shown + aaAngle, abs(angle));

                // Segmented into plates with thin seams; a plate's face bows forward at its middle.
                float plateWidth = 2 * _HalfAngle / _Plates;
                float across = (angle + _HalfAngle) / plateWidth;
                float plateIndex = floor(across);
                float inPlate = frac(across);
                float seam = smoothstep(0, 0.06, inPlate) * smoothstep(0, 0.06, 1 - inPlate);
                float bow = 0.035 * sin(inPlate * PI);
                float centre = 1 + bow;

                float inner = centre - _Thickness;
                float face = smoothstep(inner - aa, inner + _Thickness * 0.6, r) * (1 - smoothstep(centre - aa, centre + aa, r));
                float rim = 1 - smoothstep(0, _Thickness * 0.22 + aa, abs(r - centre));
                float halo = (1 - smoothstep(centre, centre + 0.12, r)) * step(centre, r);

                // A sheen sweeps across the plates, quickening as the launch nears.
                float sweepRate = lerp(1.2, 4.0, _Progress);
                float sheen = pow(saturate(1 - abs(frac(_Elapsed * sweepRate) * 2.4 - 1.2 - angle / (2 * _HalfAngle))), 6);

                // Each recent braced hit flares around where it struck, ripples along the arc and
                // glances sparks off the rim, fading out on its own clock.
                float flash = 0;
                float ripple = 0;
                float sparks = 0;
                [unroll] for (int h = 0; h < 4; h++)
                {
                    float hitAngle = _HitAngles[h];
                    float age = _HitAges[h];
                    float t = saturate(age / _HitFade);
                    float live = (1 - t) * (1 - t);
                    float off = abs(angle - hitAngle);
                    float samePlate = floor((hitAngle + _HalfAngle) / plateWidth) == plateIndex ? 1 : 0;
                    flash += live * max(samePlate * 0.45, 1 - smoothstep(0.05, 0.4, off));
                    ripple += live * (1 - smoothstep(0, 0.1, abs(off - age * 2.5)));
                    float head = centre + 0.08 + age * 1.2;
                    [unroll] for (int s = -1; s <= 1; s++)
                    {
                        float d = abs(angle - (hitAngle + s * 0.16)) * r;
                        sparks += live * (1 - smoothstep(0.008, 0.016 + aa, d))
                            * smoothstep(centre - 0.02, centre + 0.02, r) * smoothstep(head - 0.35, head - 0.1, r)
                            * (1 - smoothstep(head - 0.04, head, r));
                    }
                }

                float plate = face * 0.5 + rim * 0.9 + halo * 0.25 + sheen * face * 0.9;
                float body = plate * seam * inArc;
                float hotPlate = saturate((flash + ripple * 0.6) * (face + rim)) * seam * inArc;
                float hot = saturate(hotPlate + sparks);

                // The plates fade with the brace; a hit already landed finishes its own fade.
                float k = _Opacity * saturate(_Fade);
                float3 rgb = _Color.rgb * body * k + _HotColor.rgb * hot * 1.4 * _Opacity;
                float alpha = saturate(body * 0.55 * k + hot * 0.4 * _Opacity);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
