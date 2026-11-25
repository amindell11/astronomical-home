Shader "Astronomical/Vfx/Keystone Ghost"
{
    Properties
    {
        _Color ("Colour", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END
            struct Input { float4 vertex:POSITION; };
            struct Varying { float4 position:SV_POSITION; };
            Varying Vert(Input v) { Varying o; o.position=TransformObjectToHClip(v.vertex.xyz); return o; }
            half4 Frag(Varying i):SV_Target { return _Color; }
            ENDHLSL
        }
    }
}
