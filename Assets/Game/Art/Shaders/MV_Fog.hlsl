#ifndef MV_FOG_INCLUDED
#define MV_FOG_INCLUDED
// 월드 색 안개(02 §2.4 색 혼합판): k = saturate((d − start) / range), d = 카메라 앞 방향 거리(DepthMath.Depth와 같은 기준).
// 값은 DepthSorter가 매 프레임 전역으로 넣는다: _MvFogColor = 카메라 배경색(테마 fog), _MvFogParams = (start, range, 0, 0).
// range ≤ 0(DepthSorter가 아직 안 돌았을 때·에디터 미리보기)이면 안개 없음.
half4 _MvFogColor;
float4 _MvFogParams;

float MvFogK(float3 positionWS)
{
    if (_MvFogParams.y <= 0.0) return 0.0;
    float d = -TransformWorldToView(positionWS).z;
    return saturate((d - _MvFogParams.x) / _MvFogParams.y);
}

half3 MvApplyFog(half3 rgb, float k)
{
    return lerp(rgb, _MvFogColor.rgb, k);
}
#endif
