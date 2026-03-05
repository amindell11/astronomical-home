Shader "Custom/InstancedStarURP"
{
    Properties
    {
        _MainTex ("Fallback", 2D) = "white" {}
        _ParallaxMultiplier ("Parallax Multiplier", Range(0,2)) = 1
        _TileSize ("Tile Size", Float) = 800
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Background+50" }
        Blend One One
        ZTest LEqual
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ForwardUnlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct StarData
            {
                float3 pos;
                float  size;
                float4 color;
                float  parallax;
            };
            StructuredBuffer<StarData> _Stars;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                uint   instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionHCS : SV_Position;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR0;
            };

            float _ParallaxMultiplier;
            float _TileSize;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                // Fetch per-star data
                StarData star = _Stars[IN.instanceID];

                // Camera right/up vectors from view matrix
                float3 right = UNITY_MATRIX_V._m00_m10_m20;
                float3 up    = UNITY_MATRIX_V._m01_m11_m21;

                // Billboard offset (quad is in [-0.5,0.5] unit plane)
                float3 worldOffset = (right * IN.positionOS.x + up * IN.positionOS.y) * star.size;

                // Camera position
                float3 camPos = GetCameraPositionWS();
                float2 camXZ = camPos.xz;
                float2 tileOffset = floor(camXZ / _TileSize) * _TileSize;
                float3 worldPos = star.pos;
                worldPos.xz += tileOffset + camXZ * star.parallax * _ParallaxMultiplier;
                worldPos += worldOffset;

                OUT.positionHCS = TransformWorldToHClip(worldPos);
                OUT.uv = IN.uv;
                OUT.color = star.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 centeredUV = IN.uv - 0.5;
                float dist = length(centeredUV);
                float alpha = saturate(1.0 - dist * 2.0);
                alpha *= alpha; // softer falloff
                return half4(IN.color.rgb, IN.color.a * alpha);
            }
            ENDHLSL
        }
    }
} 