Shader "Hidden/Astronomical/FarFieldBake"
{
    Properties
    {
        _BaseMap ("Painted Surface", 2D) = "white" {}
        [Normal] _BumpMap ("Relief Normal", 2D) = "bump" {}
        _BumpScale ("Relief Strength", Float) = 1
    }
    SubShader
    {
        ZWrite On
        ZTest LEqual
        // Off rather than Back: the render-texture projection flips Y (and so winding) on some APIs;
        // the depth test alone resolves a closed mesh.
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        float4x4 _BakeMVP;
        float3 _BakeRight, _BakeUp, _BakeToCamera;
        float4 _BaseMap_ST;
        float _BumpScale;
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

        struct BakeInput
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
        };

        struct BakeOutput
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 normalOS : TEXCOORD1;
            float4 tangentOS : TEXCOORD2;
        };

        BakeOutput BakeVertex(BakeInput input)
        {
            BakeOutput output;
            output.positionCS = mul(_BakeMVP, float4(input.positionOS.xyz, 1));
            output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
            output.normalOS = input.normalOS;
            output.tangentOS = input.tangentOS;
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "Albedo"
            HLSLPROGRAM
            #pragma vertex BakeVertex
            #pragma fragment AlbedoFragment
            float4 AlbedoFragment(BakeOutput input) : SV_Target
            {
                return float4(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Normal"
            HLSLPROGRAM
            #pragma vertex BakeVertex
            #pragma fragment NormalFragment
            float4 NormalFragment(BakeOutput input) : SV_Target
            {
                float3 normal = normalize(input.normalOS);
                float3 tangent = normalize(input.tangentOS.xyz);
                float3 bitangent = input.tangentOS.w * cross(normal, tangent);
                float3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                float3 n = normalize(mul(normalTS, float3x3(tangent, bitangent, normal)));
                float3 view = float3(dot(n, _BakeRight), dot(n, _BakeUp), dot(n, _BakeToCamera));
                return float4(view * 0.5 + 0.5, 1);
            }
            ENDHLSL
        }
    }
}
