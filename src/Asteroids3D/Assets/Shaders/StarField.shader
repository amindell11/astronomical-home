Shader "Custom/StarField"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _StarColor ("Star Color", Color) = (1,1,1,1)
        _StarDensity ("Star Density", Range(0, 1)) = 0.5
        _StarSize ("Star Size", Range(0, 0.1)) = 0.01
        _SizeVariation ("Size Variation", Range(0, 1)) = 0.5
        _TwinkleSpeed ("Twinkle Speed", Range(0, 10)) = 1
        _CullDistance ("Cull Distance", Float) = 50
        _GridDensity ("Grid Density", Range(0.01, 1)) = 0.2
        _ParallaxStrength ("Parallax Strength", Range(0, 1)) = 0.5
        _StartingOffset ("Starting Offset", Vector) = (0.2,0.2,0,0)
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
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
                float4 screenPos : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _StarColor;
            float _StarDensity;
            float _StarSize;
            float _SizeVariation;
            float _TwinkleSpeed;
            float _CullDistance;
            float _GridDensity;
            float _ParallaxStrength;
            float4 _StartingOffset;

            // Improved hash function for better distribution
            float hash(float2 p)
            {
                p = 50.0 * frac(p * 0.3183099 + float2(0.71, 0.113));
                return -1.0 + 2.0 * frac(p.x * p.y * (p.x + p.y));
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Early exit if pixel is outside screen
                float2 screenUV = i.screenPos.xz / i.screenPos.w;
                if (screenUV.x < 0 || screenUV.x > 1 || screenUV.y < 0 || screenUV.y > 1)
                    return fixed4(0,0,0,0);

                // Use world position for star placement
                float2 worldPos = i.worldPos.xz;
                
                // Early exit if too far from camera
                float distFromCamera = length(worldPos - _WorldSpaceCameraPos.xz);
                if (distFromCamera > _CullDistance)
                    return fixed4(0,0,0,0);
                
                // Create a more complex grid pattern to avoid visible repetition
                float2 grid = floor(worldPos * _GridDensity);
                float2 gridUV = frac(worldPos * _GridDensity);
                
                // Generate multiple random values for each grid cell
                float random1 = hash(grid);
                float random2 = hash(grid + 1.0);
                float random = lerp(random1, random2, smoothstep(0.0, 1.0, gridUV.x));
                
                // Generate size variation
                float sizeRandom = hash(grid + float2(0.5, 0.5)); // Different seed for size
                float sizeVariation = lerp(1.0 - _SizeVariation, 1.0 + _SizeVariation, sizeRandom);
                float finalStarSize = _StarSize * sizeVariation;
                
                // Calculate parallax offset based on star size
                float parallaxFactor = 1.0 - (finalStarSize / (_StarSize * (1.0 + _SizeVariation))); // Smaller stars move slower
                float2 cameraOffset = (_WorldSpaceCameraPos.xz + _StartingOffset.xy) * _ParallaxStrength * parallaxFactor;
                
                // Apply parallax and starting offset to grid position
                float2 finalPos = worldPos + cameraOffset;
                grid = floor(finalPos * _GridDensity);
                gridUV = frac(finalPos * _GridDensity);
                
                // Only create stars where random value is below density threshold
                float star = step(random, _StarDensity);
                
                // Create star shape with soft edges
                float2 center = float2(0.5, 0.5);
                float dist = length(gridUV - center);
                float starShape = smoothstep(finalStarSize, 0, dist);
                
                // Add twinkling effect
                float twinkle = sin(_Time.y * _TwinkleSpeed + random * 10) * 0.5 + 0.5;
                
                // Combine everything
                float finalStar = star * starShape * twinkle;
                
                return float4(_StarColor.rgb, finalStar * _StarColor.a);
            }
            ENDCG
        }
    }
} 