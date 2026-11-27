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
        [HideInInspector] _HitAngle ("Hit Angle", Float) = 0
        [HideInInspector] _HitAge ("Hit Age", Float) = 1
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
                float _HitAngle;
                float _HitAge;
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

                // A braced hit lights its plate and sends a ripple both ways along the arc.
                float hitPlate = floor((_HitAngle + _HalfAngle) / plateWidth);
                float hitLive = 1 - saturate(_HitAge / 0.3);
                float plateFlash = (plateIndex == hitPlate ? 1 : 0) * hitLive * hitLive;
                float ripple = (1 - smoothstep(0, 0.12, abs(abs(angle - _HitAngle) - _HitAge * 6))) * hitLive;
                // Three sparks glance off outward from the hit.
                float sparks = 0;
                float head = centre + 0.05 + _HitAge * 1.6;
                [unroll] for (int s = -1; s <= 1; s++)
                {
                    float d = abs(angle - (_HitAngle + s * 0.14)) * r;
                    sparks += (1 - smoothstep(0.004, 0.012 + aa, d))
                        * smoothstep(head - 0.3, head - 0.05, r) * (1 - smoothstep(head - 0.05, head, r));
                }
                sparks *= hitLive;

                float plate = face * 0.5 + rim * 0.9 + halo * 0.25 + sheen * face * 0.9;
                float body = plate * seam * inArc;
                float hotPlate = saturate((plateFlash + ripple * 0.8) * (face + rim)) * seam * inArc;
                float hotMask = saturate(hotPlate + sparks);

                float3 rgb = _Color.rgb * body + _HotColor.rgb * hotMask * 1.4;
                float alpha = saturate(body * 0.55 + hotMask * 0.4);
                float k = _Opacity * saturate(_Fade);
                return half4(rgb * k, alpha * k);
            }
            ENDHLSL
        }
    }
}
