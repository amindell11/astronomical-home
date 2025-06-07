Shader "Custom/SpaceBackground"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color1 ("Color 1", Color) = (0.2, 0.1, 0.4, 1)
        _Color2 ("Color 2", Color) = (0.1, 0.3, 0.5, 1)
        _Color3 ("Color 3", Color) = (0.3, 0.1, 0.5, 1)
        _NoiseScale ("Noise Scale", Range(0.1, 10)) = 2.5
        _NoiseStrength ("Noise Strength", Range(0, 5)) = 0.8
        _ScrollSpeed ("Scroll Speed", Range(0, 5)) = 0.15
        _Distortion ("Distortion", Range(0, 5)) = 0.3
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color1;
            float4 _Color2;
            float4 _Color3;
            float _NoiseScale;
            float _NoiseStrength;
            float _ScrollSpeed;
            float _Distortion;

            // Improved hash function for better distribution
            float2 hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                          dot(p, float2(269.5, 183.3)));
                return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
            }

            // Improved noise function with better gradients
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                
                // Cubic interpolation
                float2 u = f * f * (3.0 - 2.0 * f);
                
                // Sample corners with improved gradients
                float2 ga = hash2(i + float2(0.0, 0.0));
                float2 gb = hash2(i + float2(1.0, 0.0));
                float2 gc = hash2(i + float2(0.0, 1.0));
                float2 gd = hash2(i + float2(1.0, 1.0));
                
                float va = dot(ga, f - float2(0.0, 0.0));
                float vb = dot(gb, f - float2(1.0, 0.0));
                float vc = dot(gc, f - float2(0.0, 1.0));
                float vd = dot(gd, f - float2(1.0, 1.0));
                
                return lerp(lerp(va, vb, u.x), lerp(vc, vd, u.x), u.y);
            }

            // Enhanced fbm with multiple octaves and improved persistence
            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                float persistence = 0.5;
                
                // Add more octaves for detail
                for(int i = 0; i < 6; i++)
                {
                    value += amplitude * noise(p * frequency);
                    amplitude *= persistence;
                    frequency *= 2.0;
                }
                
                return value;
            }

            // New function for creating swirling patterns
            float2 swirl(float2 p, float time)
            {
                float angle = length(p) * 0.5 + time * 0.2;
                float2 offset = float2(cos(angle), sin(angle)) * 0.1;
                return p + offset;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Calculate base position with scrolling
                float2 basePos = i.worldPos.xy * _NoiseScale;
                float time = _Time.y * _ScrollSpeed;
                
                // Create swirling effect
                float2 swirledPos = swirl(basePos, time);
                
                // Generate multiple layers of noise with different scales
                float noise1 = fbm(swirledPos);
                float noise2 = fbm(swirledPos * 1.5 + time * 0.5);
                float noise3 = fbm(swirledPos * 0.5 - time * 0.3);
                
                // Combine noise layers with different weights
                float combinedNoise = noise1 * 0.5 + noise2 * 0.3 + noise3 * 0.2;
                
                // Apply noise strength
                combinedNoise = combinedNoise * _NoiseStrength;
                
                // Create color mixing based on noise
                float3 color = lerp(_Color1.rgb, _Color2.rgb, combinedNoise);
                color = lerp(color, _Color3.rgb, noise2 * _Distortion);
                
                // Add subtle pulsing effect
                float pulse = sin(time * 0.5) * 0.1 + 0.9;
                color *= pulse;
                
                // Add subtle color variation based on position
                float2 colorVar = sin(basePos * 0.1 + time * 0.2) * 0.1;
                color += float3(colorVar.x, colorVar.y, colorVar.x * colorVar.y) * 0.1;
                
                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
} 