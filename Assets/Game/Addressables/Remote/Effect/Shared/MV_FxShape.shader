// MV/FxShape — 월드 이펙트·우다다 화면 공용(기획서 06 §6.2.1 해석표, §6.11). 텍스처 없이 uv.z 모양 번호로 그린다.
// 0 단색, 1 빛(c → 알파 0.6@0.45 → 0), 2 사각형 + 1px OLC 외곽선, 3 타원 선(uv.w = 굵기/반지름), 4 채운 원 + 외곽선(uv.w).
// 정점색은 sRGB 바이트(Color32)라 Linear 색 공간이면 여기서 선형으로 바꾼다. 블렌딩은 머티리얼 속성(일반/가산 2종).
Shader "MV/FxShape"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst", Float) = 10
        _Outline ("Outline (OLC)", Color) = (0.290, 0.204, 0.275, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        ZTest Always
        Cull Off
        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _Outline;
            float _SrcBlend;
            float _DstBlend;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float4 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float4 uv : TEXCOORD0; };

            half3 Lin(half3 c)
            {
            #if !defined(UNITY_COLORSPACE_GAMMA)
                return SRGBToLinear(c);
            #else
                return c;
            #endif
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = half4(Lin(v.color.rgb), v.color.a);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = i.color;
                int shape = (int)round(i.uv.z);
                float2 q = i.uv.xy * 2.0 - 1.0;
                if (shape == 1)
                {
                    float d = length(q);
                    float a = d < 0.45 ? lerp(1.0, 0.6, d / 0.45) : lerp(0.6, 0.0, saturate((d - 0.45) / 0.55));
                    c.a *= a;
                }
                else if (shape == 2)
                {
                    float2 px = fwidth(i.uv.xy);
                    float2 e = min(i.uv.xy, 1.0 - i.uv.xy) / max(px, 1e-5);
                    if (min(e.x, e.y) < 1.0) c.rgb = _Outline.rgb;
                }
                else if (shape == 3)
                {
                    float d = length(q);
                    float w = max(i.uv.w, 1e-3);
                    float aa = fwidth(d) * 1.0;
                    float inner = smoothstep(1.0 - w - aa, 1.0 - w, d);
                    float outer = 1.0 - smoothstep(1.0 - aa, 1.0, d);
                    c.a *= inner * outer;
                }
                else if (shape == 4)
                {
                    float d = length(q);
                    float aa = fwidth(d);
                    c.a *= 1.0 - smoothstep(1.0 - aa, 1.0, d);
                    if (i.uv.w > 0 && d > 1.0 - i.uv.w) c.rgb = _Outline.rgb;
                }
                return c;
            }
            ENDHLSL
        }
    }
}
