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
        [Header("카메라 위치 — 기준(고양이, 로비는 월드 원점)에서 얼마나 떨어질지")]
        [MvLabel("좌우 따라가기 비율 (posXMul)"), Tooltip("고양이가 옆으로 1m 움직일 때 카메라가 따라가는 거리(m). 0 = 가운데 고정, 1 = 그대로 따라감")] public double posXMul;
        [MvLabel("높이 (posY)"), Tooltip("카메라 높이(m). 키우면 더 위에서 내려다본다")] public double posY;
        [MvLabel("앞뒤 거리 (posZ)"), Tooltip("고양이 기준 앞뒤 위치(m). 음수 = 뒤. 더 작게(예 -9 → -12) 하면 더 뒤에서 넓게 본다")] public double posZ;

        [Header("바라보는 곳 — 카메라가 향하는 지점")]
        [MvLabel("좌우 따라보기 비율 (lookXMul)"), Tooltip("고양이가 옆으로 1m 움직일 때 바라보는 지점이 따라가는 거리(m)")] public double lookXMul;
        [MvLabel("바라보는 높이 (lookY)"), Tooltip("바라보는 지점의 높이(m). 키우면 화면이 위쪽을 본다(고양이가 화면 아래로)")] public double lookY;
        [MvLabel("바라보는 앞 거리 (lookZ)"), Tooltip("고양이보다 얼마나 앞을 볼지(m). 키우면 앞이 더 많이 보이고 고양이가 화면 아래로 내려간다")] public double lookZ;

        [Header("따라가는 빠르기")]
        [MvLabel("위치 따라가기 속도 (posLerp)"), Tooltip("새 자리로 옮겨 가는 빠르기(1/초). 10이면 약 0.3초에 95%. 「바로 이동」을 켜면 쓰지 않는다")] public double posLerp;
        [MvLabel("바로 이동 (snap)"), Tooltip("켜면 부드럽게 따라가지 않고 그 자리에 바로 선다")] public bool snap;
        [MvLabel("높이 따라가기 속도 (liftLerp)"), Tooltip("계단을 올라 지면 높이가 바뀔 때 카메라 높이가 따라가는 빠르기(1/초)")] public double liftLerp;
        [MvLabel("시점 섞기 속도 (blendLerp)"), Tooltip("달리기 ↔ 계단 시점이 바뀔 때 섞이는 빠르기(1/초). 이 시점으로 들어갈 때 쓴다")] public double blendLerp;

        [Header("렌즈")]
        [MvLabel("화각 (fovV)"), Tooltip("세로 시야각(도, 9:16 화면 기준). 키우면 넓게, 줄이면 확대돼 보인다. 지금 게임은 달리기 값을 쓴다")] public double fovV;
        [MvLabel("가장 가까이 그리는 거리 (near)"), Tooltip("이보다 가까운 것은 그리지 않는다(m)")] public double near;
        [MvLabel("가장 멀리 그리는 거리 (far)"), Tooltip("이보다 먼 것은 그리지 않는다(m)")] public double far;

        [Header("좌우 더하기·옆 시점")]
        [MvLabel("카메라 좌우 더하기 (posX)"), Tooltip("카메라 x에 더할 거리(m). 옆 시점은 고양이 오른쪽으로 얼마나 떨어질지")] public double posX;
        [MvLabel("바라보는 곳 좌우 더하기 (lookX)"), Tooltip("바라보는 지점 x에 더할 거리(m)")] public double lookX;
        [MvLabel("돌아 들어가는 시간 (turnSec)"), Tooltip("계단 오르기 중 이 시점(옆)으로 돌아 들어가는 시간(초). 0이면 바로")] public double turnSec;

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

        [Header("시점별 카메라 — 펼쳐서 수정. 평소 구도는 「달리기」의 높이·앞뒤 거리·바라보는 앞 거리·화각")]
        [MvLabel("달리기 (run)"), Tooltip("달리는 중 시점. 출발하면 로비 시점에서 이 시점으로 부드럽게 올라간다")] public CameraData run = new CameraData();
        [MvLabel("계단 오르기 (climb)"), Tooltip("계단을 오르는 중 시점")] public CameraData climb = new CameraData();
        [MvLabel("로비 (lobby)"), Tooltip("로비 화면 시점(고양이 뒤 낮은 자리). 기준은 월드 원점")] public CameraData lobby = new CameraData();
        [MvLabel("계단 옆 시점 (side)"), Tooltip("계단을 오르는 중 옆으로 돌아 보는 시점")] public CameraData side = new CameraData();

        [Header("화면 비율(가로:세로) — 기준보다 긴 폰은 세로 시야를 늘리고 좌우 시야는 같게(D-41 ②)")]
        [MvLabel("기준 화면 비율"), Tooltip("화각(fovV)을 정한 기준 비율. 9:16")] public Vector2 refAspect = new Vector2(9, 16);
        [MvLabel("가장 긴 화면"), Tooltip("이보다 세로로 긴 화면은 이 비율로 계산한다(9:21)")] public Vector2 minAspect = new Vector2(9, 21);
        [MvLabel("가장 넓은 화면"), Tooltip("이보다 넓은 화면(태블릿)은 이 비율로 계산한다(3:4)")] public Vector2 maxAspect = new Vector2(3, 4);

        public double RefAspect => Ratio(refAspect);
        public double MinAspect => Ratio(minAspect);
        public double MaxAspect => Ratio(maxAspect);

        /// <summary>Inspector에서 값이 바뀌면 부른다(편집 모드 구도·Play Mode 조정 반영).</summary>
        public event Action Changed;

        static double Ratio(Vector2 v) => v.y > 0 ? (double)v.x / v.y : 0;

        void OnValidate() => Changed?.Invoke();
    }
}
