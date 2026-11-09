// MV/WorldVertexColor — 절차 메쉬(지면 띠·옹벽·상자 빌보드·담장, 기획서 02 §2.4.3, §2.6.4, 03 §3.8.9).
// 색 = 정점색만(안개 혼합 mixC는 CPU가 이미 넣었다). 정점색은 sRGB 바이트(Color32)라 Linear 색 공간이면 여기서 선형으로 바꾼다(D-42 Linear 유지).
// 안개 알파(상자·담장)는 정점색 알파. 깊이 버퍼 안 씀.
Shader "MV/WorldVertexColor"
{
    Properties { }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
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

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                half4 c = v.color;
            #if !defined(UNITY_COLORSPACE_GAMMA)
                c.rgb = SRGBToLinear(c.rgb);
            #endif
                o.color = c;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return i.color;
            }
            ENDHLSL
        }
    }
}
