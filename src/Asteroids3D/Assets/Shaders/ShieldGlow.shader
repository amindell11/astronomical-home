Shader "UI/ShieldGlow"
{
    Properties
    {
        [HDR]_Color      ("Tint",        Color) = (0,1,1,1)
        _Thickness  ("Ring Width",  Range(0,1)) = 0.15
        _Softness   ("Edge Soft",   Range(0,0.5)) = 0.05
        _GlowPower  ("Fresnel Pow", Range(1,8)) = 3
        _GlowIntensity ("Glow Intensity", Range(0,10)) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend One One           // additive
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f     { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

            fixed4 _Color;
            float  _Thickness, _Softness, _GlowPower, _GlowIntensity;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv * 2 - 1;          // map to -1..1
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float r = length(i.uv);        // radial distance
                float edge = 1 - smoothstep(_Thickness, _Thickness + _Softness, abs(r - 1 + _Thickness));
                // Fresnel-ish: fade by view-angle modulated radius
                float fres = pow(1 - saturate(r), _GlowPower);
                return _Color * edge * fres * _GlowIntensity;
            }
            ENDCG
        }
    }
}
