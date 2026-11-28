Shader "Astronomical/Drawn/FarFieldRock"
{
    Properties
    {
        _AlbedoAtlas ("Albedo Atlas (premultiplied, A = coverage)", 2D) = "black" {}
        _NormalAtlas ("View Normal Atlas (premultiplied, A = coverage)", 2D) = "black" {}
        _AtlasColumns ("Atlas Columns (views)", Float) = 12
        _AtlasRows ("Atlas Rows (meshes)", Float) = 10
        _MaxAtlasLod ("Max Atlas Mip", Float) = 4
        _BaseColor ("Hull Color", Color) = (1,1,1,1)
        _ShadowColor ("Shadow Plane Tint", Color) = (0.38,0.46,0.62,1)
        _ShadowThreshold ("Shadow Plane Threshold", Range(-1,1)) = 0.15
        _ShadowSoftness ("Shadow Transition", Range(0.01,0.5)) = 0.08
        _PaletteLighting ("Bound Palette Lighting", Range(0,1)) = 0
        _AmbientStrength ("Ambient Strength", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            Name "FarFieldRock"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex FarVertex
            #pragma fragment FarFragment
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _ShadowColor;
                half _ShadowThreshold, _ShadowSoftness, _PaletteLighting, _AmbientStrength;
                float _AtlasColumns, _AtlasRows, _MaxAtlasLod;
            CBUFFER_END

            struct FarRock
            {
                float3 position;
                float halfSize;
                float spin;
                float cell;
                float alpha;
            };

            StructuredBuffer<FarRock> _FarRocks;
            float3 _FarRight, _FarUp, _FarToCamera;
            TEXTURE2D(_AlbedoAtlas); SAMPLER(sampler_AlbedoAtlas);
            TEXTURE2D(_NormalAtlas);

            struct FarOutput
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 spin : TEXCOORD1;
                float alpha : TEXCOORD2;
                half fog : TEXCOORD3;
            };

            static const float2 Corners[6] =
            {
                float2(-1, -1), float2(-1, 1), float2(1, 1),
                float2(-1, -1), float2(1, 1), float2(1, -1)
            };

            FarOutput FarVertex(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                FarRock rock = _FarRocks[instanceID];
                float2 c = Corners[vertexID];
                float s, k;
                sincos(rock.spin, s, k);
                float2 r = float2(c.x * k - c.y * s, c.x * s + c.y * k);
                float3 positionWS = rock.position + (r.x * _FarRight + r.y * _FarUp) * rock.halfSize;

                float col = fmod(rock.cell, _AtlasColumns);
                float row = floor(rock.cell / _AtlasColumns);
                FarOutput output;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = (float2(col, row) + c * 0.5 + 0.5) / float2(_AtlasColumns, _AtlasRows);
                output.spin = float2(s, k);
                output.alpha = rock.alpha;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 FarFragment(FarOutput input) : SV_Target
            {
                float lod = min(CALCULATE_TEXTURE2D_LOD(_AlbedoAtlas, sampler_AlbedoAtlas, input.uv), _MaxAtlasLod);
                half4 albedoSample = SAMPLE_TEXTURE2D_LOD(_AlbedoAtlas, sampler_AlbedoAtlas, input.uv, lod);
                half4 normalSample = SAMPLE_TEXTURE2D_LOD(_NormalAtlas, sampler_AlbedoAtlas, input.uv, lod);
                half coverage = albedoSample.a;
                clip(coverage - 0.5);
                clip(input.alpha - InterleavedGradientNoise(input.positionCS.xy, 0));

                half3 albedo = albedoSample.rgb / coverage;
                half3 n = normalize(normalSample.rgb / normalSample.a * 2 - 1);
                float s = input.spin.x, k = input.spin.y;
                float2 nxy = float2(n.x * k - n.y * s, n.x * s + n.y * k);
                half3 normal = NormalizeNormalPerPixel(nxy.x * _FarRight + nxy.y * _FarUp + n.z * _FarToCamera);

                albedo *= _BaseColor.rgb;
                Light light = GetMainLight();
                half ndl = dot(normal, light.direction);
                half transition = max(_ShadowSoftness, fwidth(ndl));
                half plane = smoothstep(_ShadowThreshold - transition, _ShadowThreshold + transition, ndl);
                half3 diffuse = lerp(_ShadowColor.rgb, 1, plane);
                half3 ambient = SampleSH(normal);
                half3 color = albedo * (ambient + light.color * diffuse * light.distanceAttenuation);
                half3 illumination = light.color * light.distanceAttenuation + max(ambient, 0) * _AmbientStrength;
                half peak = max(illumination.r, max(illumination.g, illumination.b));
                illumination /= max(1, peak);
                color = lerp(color, albedo * diffuse * illumination, _PaletteLighting);
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
    }
}
