Shader "Custom/ShipShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        _EmissionPower ("Emission Power", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Enable GPU instancing & strip unneeded variants to reduce CPU overhead
        #pragma surface surf Standard fullforwardshadows noambient
        #pragma target 3.0
        #pragma multi_compile_instancing
        #pragma instancing_options assumeuniformscaling
        #pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2 DYNAMICLIGHTMAP_ON DIRLIGHTMAP_COMBINED DIRLIGHTMAP_SEPARATE LIGHTMAP_ON SHADOWS_SHADOWMASK

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        fixed4 _EmissionColor;
        half _EmissionPower;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Sample the texture
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            
            // Set the albedo color
            o.Albedo = c.rgb;
            
            // Set metallic and smoothness
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            
            // Set emission
            o.Emission = _EmissionColor.rgb * _EmissionPower;
            
            // Set alpha
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
} 