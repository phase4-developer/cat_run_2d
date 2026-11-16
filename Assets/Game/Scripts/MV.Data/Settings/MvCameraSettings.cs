using System;
using UnityEngine;

namespace MV.Data
{
    /// <summary>
    /// 카메라 시점 1개(옛 `320_camera` 한 행, 02 §2.2.2). 목표 위치 = (기준 x·posXMul + posX, 기준 높이 + posY, 기준 z + posZ),
    /// 주시점도 같은 식(look*). 기준은 run·climb·side = 플레이어, lobby = 월드 원점.
    /// </summary>
    [Serializable]
    public sealed class CameraData
    {
        [Tooltip("위치 x = 기준 x × posXMul + posX")] public double posXMul;
        [Tooltip("위치 높이(m, 기준 높이에 더함)")] public double posY;
        [Tooltip("위치 z(m, 기준 z에 더함)")] public double posZ;
        [Tooltip("주시점 x = 기준 x × lookXMul + lookX")] public double lookXMul;
        [Tooltip("주시점 높이(m)")] public double lookY;
        [Tooltip("주시점 z(m)")] public double lookZ;
        [Tooltip("위치 보간 속도(1/초). snap이면 쓰지 않음")] public double posLerp;
        [Tooltip("켜면 목표 위치에 바로 선다")] public bool snap;
        [Tooltip("높이(camLift) 따라가기 속도(1/초)")] public double liftLerp;
        [Tooltip("run ↔ climb 섞기 속도(1/초)")] public double blendLerp;
        [Tooltip("세로 FOV(도, 9:16 기준)")] public double fovV;
        [Tooltip("near 클리핑(m)")] public double near;
        [Tooltip("far 클리핑(m)")] public double far;
        [Tooltip("위치 x 더하기(m). side = 고양이 오른쪽 거리")] public double posX;
        [Tooltip("주시점 x 더하기(m)")] public double lookX;
        [Tooltip("이 시점으로 돌아 들어가는 시간(초, side). 0이면 즉시")] public double turnSec;

        /// <summary>런타임 사본. 게임 코드는 설정 에셋 원본을 바꾸지 않는다.</summary>
        public CameraData Clone() => (CameraData)MemberwiseClone();
    }

    /// <summary>
    /// 월드 카메라 설정 정본(2026-11-16 사용자 지시, `320_camera` 표 대체). `GameCamera`의 <c>CameraRig</c>가 씬에서 참조하므로
    /// 편집 모드·Play Mode·빌드가 같은 에셋을 읽는다. 런타임은 <see cref="CameraData.Clone"/> 사본만 쓰고 에셋을 쓰지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "MvCameraSettings", menuName = "MoonVillage/Camera Settings")]
    public sealed class MvCameraSettings : ScriptableObject
    {
        /// <summary>저장소 안 에셋 경로(시험·에디터용).</summary>
        public const string AssetPath = "Assets/Game/GameData/Settings/MvCameraSettings.asset";

        [Tooltip("달리기")] public CameraData run = new CameraData();
        [Tooltip("계단 오르기")] public CameraData climb = new CameraData();
        [Tooltip("로비(월드 원점 기준)")] public CameraData lobby = new CameraData();
        [Tooltip("계단 오르기 중 옆으로 도는 시점")] public CameraData side = new CameraData();

        [Header("화면비(가로:세로) — 기준보다 길면 가로 시야 고정으로 세로 FOV를 늘린다(D-41 ②)")]
        [Tooltip("FOV 기준 화면비")] public Vector2 refAspect = new Vector2(9, 16);
        [Tooltip("가장 긴 화면(이보다 길면 자름)")] public Vector2 minAspect = new Vector2(9, 21);
        [Tooltip("가장 넓은 화면(이보다 넓으면 자름)")] public Vector2 maxAspect = new Vector2(3, 4);

        public double RefAspect => Ratio(refAspect);
        public double MinAspect => Ratio(minAspect);
        public double MaxAspect => Ratio(maxAspect);

        /// <summary>Inspector에서 값이 바뀌면 부른다(편집 모드 구도·Play Mode 조정 반영).</summary>
        public event Action Changed;

        static double Ratio(Vector2 v) => v.y > 0 ? (double)v.x / v.y : 0;

        void OnValidate() => Changed?.Invoke();
    }
}
