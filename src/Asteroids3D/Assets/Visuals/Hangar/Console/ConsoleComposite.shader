Shader "Hangar/ConsoleComposite"
{
    Properties { [PerRendererData] _MainTex("Console camera", 2D) = "black" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 Frag(v2f_img input) : SV_Target { return tex2D(_MainTex, input.uv); }
            ENDCG
        }
    }
}
