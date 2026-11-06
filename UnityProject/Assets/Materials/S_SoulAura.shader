// Whole-body glow on a legal soul target: drawn from the body's own mesh, so it follows any silhouette
// and pose, standing or fallen, skinned or not. Rim lights the body's edge; Shell is a slightly larger inside-out copy
// that fades to nothing at its outer edge, the usual soft-atmosphere trick. Both passes run on the transparent queue,
// so SoulGlowPass draws them again after post-processing and they stay in colour in soul vision.
Shader "BorrowedSouls/SoulAura"
{
    Properties
    {
        _AuraColor ("Colour", Color) = (0.45, 0.95, 1, 1)
        _AuraStrength ("Strength (VesselView sets this per body)", Range(0, 2)) = 0.35
        _AuraWidth ("Shell width (metres)", Range(0, 0.6)) = 0.18
        _RimPower ("Rim tightness", Range(0.5, 8)) = 2.5
        _ShellPower ("Shell falloff", Range(0.5, 8)) = 2
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha One
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _AuraColor;
            float _AuraStrength;
            float _AuraWidth;
            float _RimPower;
            float _ShellPower;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 normalWS : TEXCOORD0;
            float3 viewWS : TEXCOORD1;
        };

        Varyings Build(Attributes input, float width)
        {
            Varyings output;
            float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz) + normalWS * width;
            output.positionCS = TransformWorldToHClip(positionWS);
            output.normalWS = normalWS;
            output.viewWS = GetWorldSpaceViewDir(positionWS);
            return output;
        }

        // dot(normal, view): positive on faces towards the camera, negative on the inside-out shell.
        float Facing(Varyings input)
        {
            return dot(normalize(input.normalWS), normalize(input.viewWS));
        }
        ENDHLSL

        Pass
        {
            Name "Rim"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            Varyings Vert(Attributes input) { return Build(input, 0.0); }

            half4 Frag(Varyings input) : SV_Target
            {
                float rim = pow(1.0 - saturate(Facing(input)), _RimPower);
                return half4(_AuraColor.rgb, rim * _AuraStrength);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Shell"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            Varyings Vert(Attributes input) { return Build(input, _AuraWidth); }

            half4 Frag(Varyings input) : SV_Target
            {
                float shell = pow(saturate(-Facing(input)), _ShellPower);
                return half4(_AuraColor.rgb, shell * _AuraStrength);
            }
            ENDHLSL
        }
    }
}
