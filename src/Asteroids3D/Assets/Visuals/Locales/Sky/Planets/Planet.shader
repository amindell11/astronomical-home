Shader "Locales/Planet"
{
    Properties
    {
        _MainTex ("Planet", 2D) = "black" {}
        _Tint ("Tint (RGB)", Color) = (1, 1, 1, 1)
        _PlanetCenter ("Center (reference-view units)", Vector) = (15, 1.5, 0, 0)
        _PlanetSize ("Diameter (reference-view units)", Range(1, 60)) = 34
        _Parallax ("Parallax", Range(0, 0.01)) = 0.0015
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "RenderType"="Transparent" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "../SkyCommon.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float4 _PlanetCenter;
                float _PlanetSize;
                float _Parallax;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                SkyCoordinates coordinates = GetSkyCoordinates(input.projectedPosition, 7);
                float2 uv = (coordinates.planePosition + coordinates.cameraPosition * _Parallax -
                    _PlanetCenter.xy) / _PlanetSize + 0.5;
                clip(min(min(uv.x, uv.y), min(1 - uv.x, 1 - uv.y)));
                half4 planet = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                // Near-opaque painted interiors must fully hide stars while their feathered rims retain transparency.
                half alpha = saturate(planet.a / 0.98);
                return half4(planet.rgb * _Tint.rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }
}
