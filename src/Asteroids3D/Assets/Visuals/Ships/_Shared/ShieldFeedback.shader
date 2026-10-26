Shader "Astronomical/Ships/ShieldFeedback"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Input
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };
            struct Varying
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
            };
            Varying Vertex(Input input)
            {
                Varying output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                return output;
            }
            half4 Fragment(Varying input) : SV_Target { return input.color; }
            ENDHLSL
        }
    }
}
