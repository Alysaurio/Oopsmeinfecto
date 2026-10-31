Shader "Custom/URP_CRT_Retro"
{
    Properties
    {
        _Curvature ("Curvatura de Pantalla", Range(0.0, 0.4)) = 0.12
        _ScanlineIntensity ("Intensidad Scanlines", Range(0.0, 1.0)) = 0.25
        _ScanlineCount ("Densidad de Líneas", Float) = 480.0
        _ChromaticAberration ("Aberración Cromática", Range(0.0, 0.02)) = 0.004
        _VignetteStrength ("Viñeteado", Range(0.1, 2.0)) = 0.6
        _NoiseStrength ("Ruido Estático", Range(0.0, 0.08)) = 0.02
        _InfectionProgress ("Progreso de Infección", Range(0.0, 1.0)) = 0.0
        _StepFlicker ("Parpadeo de Paso", Range(0.0, 1.0)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZWrite Off
        Cull Off

        Pass
        {
            Name "CRTRenderPass"
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Curvature;
            float _ScanlineIntensity;
            float _ScanlineCount;
            float _ChromaticAberration;
            float _VignetteStrength;
            float _NoiseStrength;
            float _InfectionProgress;
            float _StepFlicker;

            float2 DistortUV(float2 uv)
            {
                uv = uv * 2.0 - 1.0;
                float2 offset = abs(uv.yx) * _Curvature;
                uv = uv + uv * offset * offset;
                uv = uv * 0.5 + 0.5;
                return uv;
            }

            float PseudoNoise(float2 uv, float t)
            {
                return frac(sin(dot(uv + t, float2(12.9898, 78.233))) * 43758.5453);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 crtUV = DistortUV(uv);

                // Desplazamiento horizontal aleatorio simulando fallo de sincronismo analógico
                float hSyncGlitch = (PseudoNoise(float2(floor(crtUV.y * 100.0), _Time.y * 45.0), 0.0) - 0.5) * 2.0;
                crtUV.x += hSyncGlitch * 0.035 * _StepFlicker;

                // Bordes oscuros fuera del área convexa del tubo
                if (crtUV.x < 0.0 || crtUV.x > 1.0 || crtUV.y < 0.0 || crtUV.y > 1.0)
                {
                    return half4(0.0, 0.0, 0.0, 1.0);
                }

                // Aberración cromática (desplazamiento radial de canales R y B)
                float2 dir = crtUV - 0.5;
                float dist = length(dir);
                float2 caOffset = dir * (_ChromaticAberration * dist * 2.0);

                half r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, crtUV + caOffset).r;
                half g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, crtUV).g;
                half b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, crtUV - caOffset).b;
                half3 color = half3(r, g, b);

                // Líneas de escaneo horizontales
                float scanline = sin(crtUV.y * _ScanlineCount * 3.14159265);
                scanline = (scanline + 1.0) * 0.5;
                color *= (1.0 - _ScanlineIntensity * (1.0 - scanline));

                // Ruido sutil analógico
                float noise = (PseudoNoise(crtUV, _Time.y) - 0.5) * _NoiseStrength;
                color += noise;

                // Viñeteado perimetral
                float vignette = crtUV.x * crtUV.y * (1.0 - crtUV.x) * (1.0 - crtUV.y);
                float currentVignetteStrength = lerp(_VignetteStrength, _VignetteStrength * 1.4, _InfectionProgress);
                vignette = clamp(pow(16.0 * vignette, currentVignetteStrength), 0.0, 1.0);
                color *= vignette;

                // Tinte verdoso/tóxico en los bordes oscurecidos simulando niebla de miasma zombi
                half3 toxicTint = half3(0.1, 0.4, 0.2);
                float edgeMask = 1.0 - vignette; // vignette es 0 en los bordes y 1 al centro
                color.rgb += (toxicTint * edgeMask * _InfectionProgress);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}