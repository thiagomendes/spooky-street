// Cut-paper sky pieces (URP, unlit, no fog): the moon, its halo, the paper clouds, bats and the shooting star.
// Colour = texture × vertex colour × tint. Vertex normals carry the cut edge's outward direction (zero inside the
// sheet): edges that face the moon, and every edge of a piece that passes in front of it, pick up a pale rim.
// Blend, depth write and culling are plain render states set per material (opaque for the cut meshes, alpha
// blended for the halo and the streak). Output is clamped under the bloom threshold (art bible §3).
// One pass, no keywords, every material property in UnityPerMaterial: SRP Batcher compatible.
Shader "SpookyStreet/SkyPaper"
{
    Properties
    {
        [MainTexture] _BaseMap("Paper", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1, 1, 1, 1)
        _RimColor("Moonlit edge colour (alpha = strength)", Color) = (0.8, 0.8, 0.9, 0)
        _MoonPosition("Moon position (world)", Vector) = (0, 20, 40, 0)
        _MaxValue("Brightest value", Range(0, 1)) = 0.95
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite("Depth write", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Paper"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _RimColor;
                float4 _MoonPosition;
                float _MaxValue;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float rim : TEXCOORD1;
            };

            // Vertex colours are authored in sRGB; the frame is linear unless the project runs in gamma space.
            float3 PaperToLinear(float3 c)
            {
            #ifdef UNITY_COLORSPACE_GAMMA
                return c;
            #else
                return c * (c * (c * 0.305306011 + 0.682171111) + 0.012522878);
            #endif
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                output.color = float4(PaperToLinear(input.color.rgb), input.color.a) * _BaseColor;

                // Edge direction in world space; its length is how much rim this vertex may show (0 inside).
                float3 edgeWS = mul((float3x3)UNITY_MATRIX_M, input.normalOS);
                float edge = saturate(length(edgeWS));
                float3 toMoon = normalize(_MoonPosition.xyz - positionWS);
                float facing = saturate(dot(edgeWS, toMoon));
                float3 view = normalize(positionWS - _WorldSpaceCameraPos);
                float3 moonView = normalize(_MoonPosition.xyz - _WorldSpaceCameraPos);
                float toward = saturate(dot(view, moonView));
                float lining = toward * toward;
                lining = lining * lining;
                lining = lining * lining;
                lining = lining * lining;
                output.rim = saturate(facing * 0.55 + edge * lining * lining) * _RimColor.a;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float4 paper = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float3 colour = paper.rgb * input.color.rgb;
                colour = lerp(colour, _RimColor.rgb, input.rim);
                colour = min(colour, _MaxValue);
                return half4(colour, paper.a * input.color.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
