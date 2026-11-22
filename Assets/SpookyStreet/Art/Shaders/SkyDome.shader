// Handcrafted night sky dome (URP, unlit, opaque, no fog). The sky paint is a baked texture; the stars come from
// a mask texture (R = brightness, G = twinkle phase) and pulse on a clock the game drives, so a paused game holds
// them still. A broad per-vertex glow brightens the sky around the moon. Output is clamped under the bloom
// threshold (art bible §3: the attic window stays the brightest point).
// One pass, no keywords, every material property in UnityPerMaterial: SRP Batcher compatible.
Shader "SpookyStreet/SkyDome"
{
    Properties
    {
        [MainTexture] _BaseMap("Sky paint", 2D) = "black" {}
        [NoScaleOffset] _StarMap("Stars (R brightness, G twinkle phase)", 2D) = "black" {}
        _StarColor("Star colour", Color) = (1, 0.95, 0.78, 1)
        _TwinkleDepth("Twinkle depth", Range(0, 1)) = 0.6
        _SkyTime("Sky clock (seconds)", Float) = 0
        _GlowColor("Moon glow colour", Color) = (0.2, 0.2, 0.4, 1)
        _GlowPower("Moon glow tightness", Float) = 6
        _MoonPosition("Moon position (world)", Vector) = (0, 20, 40, 0)
        _MaxValue("Brightest value", Range(0, 1)) = 0.95
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Sky"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_StarMap);
            SAMPLER(sampler_StarMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _StarColor;
                float4 _GlowColor;
                float4 _MoonPosition;
                float _TwinkleDepth;
                float _SkyTime;
                float _GlowPower;
                float _MaxValue;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float glow : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                float3 toSky = normalize(positionWS - _WorldSpaceCameraPos);
                float3 toMoon = normalize(_MoonPosition.xyz - _WorldSpaceCameraPos);
                output.glow = pow(saturate(dot(toSky, toMoon)), _GlowPower);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 sky = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;
                float2 star = SAMPLE_TEXTURE2D(_StarMap, sampler_StarMap, input.uv).rg;
                float rate = 1.0 + 2.0 * star.g;
                float pulse = 0.5 + 0.5 * sin(_SkyTime * rate + star.g * 6.2831853);
                float twinkle = 1.0 - _TwinkleDepth * pulse;
                float3 colour = sky + _GlowColor.rgb * input.glow + _StarColor.rgb * (star.r * twinkle);
                colour = min(colour, _MaxValue);
                return half4(colour, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
