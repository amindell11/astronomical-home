Shader "Custom/TriplanarRock_Metallic"
{
    Properties
    {
        _Albedo      ("Albedo (RGB)", 2D) = "white" {}
        _Normal      ("Normal (RGB)", 2D) = "bump"  {}
        _MetalGloss  ("Metallic (R) / Smooth (A)", 2D) = "white" {}
        _Scale       ("Tile Scale", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 250

        // --- everything between CGPROGRAM and ENDCG is compiled ---
        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf Standard fullforwardshadows
        #include "UnityCG.cginc"

        sampler2D _Albedo, _Normal, _MetalGloss;
        half      _Scale;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
            INTERNAL_DATA          // needed for world-space data
        };

        inline half3 BlendWeights (half3 n)
        {
            n = abs(n);
            return n / (n.x + n.y + n.z);
        }

        void TriplanarSample(
            float3 wp, float3 w, out fixed4 albedo,
            out fixed3 normalTS, out half metallic, out half smoothness)
        {
            float2 uvX = wp.yz * _Scale;
            float2 uvY = wp.xz * _Scale;
            float2 uvZ = wp.xy * _Scale;

            fixed4 cX = tex2D(_Albedo, uvX);
            fixed4 cY = tex2D(_Albedo, uvY);
            fixed4 cZ = tex2D(_Albedo, uvZ);
            albedo = cX * w.x + cY * w.y + cZ * w.z;

            fixed3 nX = UnpackNormal(tex2D(_Normal, uvX));
            fixed3 nY = UnpackNormal(tex2D(_Normal, uvY));
            fixed3 nZ = UnpackNormal(tex2D(_Normal, uvZ));
            normalTS = normalize(nX * w.x + nY * w.y + nZ * w.z);

            fixed4 mX = tex2D(_MetalGloss, uvX);
            fixed4 mY = tex2D(_MetalGloss, uvY);
            fixed4 mZ = tex2D(_MetalGloss, uvZ);
            metallic   = mX.r * w.x + mY.r * w.y + mZ.r * w.z;
            smoothness = mX.a * w.x + mY.a * w.y + mZ.a * w.z;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            half3 w = BlendWeights(IN.worldNormal);

            fixed4 albedo;
            fixed3 normalTS;
            half   metallic, smooth;
            TriplanarSample(IN.worldPos, w, albedo, normalTS, metallic, smooth);

            o.Albedo     = albedo.rgb;
            o.Normal     = normalTS;
            o.Metallic   = metallic;
            o.Smoothness = smooth;
            o.Occlusion  = 1;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
