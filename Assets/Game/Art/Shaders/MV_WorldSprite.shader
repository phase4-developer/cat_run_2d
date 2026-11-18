// MV/WorldSprite — 월드 빌보드 스프라이트(기획서 02 §2.4.2, 03 §3.8.9).
// rgb = tex.rgb·정점색.rgb, a = tex.a·정점색.a. 안개는 색 혼합(MV_Fog.hlsl, 거리별 rgb → 테마 fog색). 알파는 먼 끝 등장 페이드만 CPU(DepthSorter)가 SpriteRenderer.color 알파에 넣는다(MPB 금지).
// 깊이 버퍼를 쓰지 않는다(ZWrite Off, ZTest Always). 앞뒤는 정렬 레이어·sortingOrder만으로 정한다.
Shader "MV/WorldSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
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
            #include "MV_Fog.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float fog : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.fog = MvFogK(positionWS);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                c.rgb = MvApplyFog(c.rgb, i.fog);   // 색 안개: 알파는 그대로(뒤가 비치지 않게)
                return c;
            }
            ENDHLSL
        }
    }
}
