Shader "Hidden/SpaceNoiseBake"
{
    Properties
    {
        _NoiseScale ("Noise Scale", Float) = 2.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off  ZWrite Off  ZTest Always         // full-screen blit

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
            };

            float _NoiseScale;

            /* === copy of the old noise helpers === */
            half2 hash2(half2 p)
            {
                p = half2(dot(p, half2(127.1, 311.7)),
                          dot(p, half2(269.5, 183.3)));
                return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
            }

            half noise(half2 p)
            {
                half2 i = floor(p);
                half2 f = frac(p);
                half2 u = f * f * (3.0 - 2.0 * f);

                half2 ga = hash2(i + half2(0.0, 0.0));
                half2 gb = hash2(i + half2(1.0, 0.0));
                half2 gc = hash2(i + half2(0.0, 1.0));
                half2 gd = hash2(i + half2(1.0, 1.0));

                half va = dot(ga, f - half2(0.0, 0.0));
                half vb = dot(gb, f - half2(1.0, 0.0));
                half vc = dot(gc, f - half2(0.0, 1.0));
                half vd = dot(gd, f - half2(1.0, 1.0));

                return lerp(lerp(va, vb, u.x), lerp(vc, vd, u.x), u.y);
            }

            half fbm(half2 p)
            {
                half value = 0.0;
                half amplitude = 0.5;
                half frequency = 1.0;

                for (int i = 0; i < 3; i++)
                {
                    value += amplitude * noise(p * frequency);
                    amplitude *= 0.5;
                    frequency *= 2.0;
                }
                return value;
            }
            /* ====================================== */

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half2 pos = i.uv * _NoiseScale * 10.0;  // 10 scales roughly like the old worldPos*xz
                half n    = fbm(pos);
                return fixed4(n, n, n, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}