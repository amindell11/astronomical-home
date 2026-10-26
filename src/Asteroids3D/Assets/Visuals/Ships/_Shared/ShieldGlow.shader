Shader "UI/ShieldGlow"
{
    Properties
    {
        _Color ("Tint", Color) = (0.51,0.75,0.76,1)
        _Thickness ("Ring Width", Range(0.005,0.1)) = 0.02
        _Softness ("Edge Softness", Range(0.001,0.02)) = 0.004
        _GlowIntensity ("Tip Glow", Range(0,1)) = 0.12
        [HideInInspector] _ArcLength ("Arc Length", Float) = 6.283185
        [HideInInspector] _TipStrength ("Tip Strength", Float) = 0
        [HideInInspector] _RingRotation ("Ring Rotation", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f
            {
                float4 pos:SV_POSITION;
                float2 uv:TEXCOORD0;
                float4 color:COLOR;
            };

            fixed4 _Color;
            float _Thickness, _Softness, _GlowIntensity, _ArcLength, _TipStrength, _RingRotation;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * 2 - 1;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float r = length(i.uv);
                float angle = atan2(i.uv.y,i.uv.x)+_RingRotation;
                float along = fmod(atan2(i.uv.y,i.uv.x) + 4.712389,6.283185);
                float endDistance = min(along,abs(_ArcLength-along));
                float open = step(_ArcLength,6.282);
                float taper = lerp(1,0.25+0.75*smoothstep(0,0.12,endDistance),open);
                float weight = 0.86+0.13*cos(angle*3)+0.09*sin(angle*7);
                float center = 0.9+0.002*sin(angle*3)+0.0013*sin(angle*7);
                float distance = abs(r-center);
                float width = _Thickness*weight*taper;
                float aa = max(fwidth(r),_Softness);
                float core = 1-smoothstep(width-aa*0.5,width+aa*0.5,distance);
                float key = 1-smoothstep(width+0.008,width+0.008+aa,distance);
                float tip = (1-smoothstep(0.02,0.09,endDistance))*_TipStrength*open;
                float glow = (1-smoothstep(width,width+0.035,distance))*tip*_GlowIntensity;
                float light = 0.5+0.5*cos(angle-2.35);
                float3 tint = lerp(_Color.rgb*0.72,lerp(_Color.rgb,float3(0.82,0.88,0.83),0.6),light);
                tint = lerp(tint,float3(0.9,0.94,0.86),tip*0.8);
                float alpha = max(core*0.88,max(key*0.3,glow));
                float3 rgb = lerp(float3(0.04,0.12,0.17),tint,core);
                return float4(rgb,alpha*_Color.a)*i.color;
            }
            ENDCG
        }
    }
}
