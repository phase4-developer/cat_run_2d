using jjevol.Effect;
using MV.View;
using UnityEngine;

namespace MV.Fx
{
    /// <summary>
    /// 계단 경험치 오브 재생기(MV-UI3-STAIRFX2·STAIRFX3). 월드 탑 오브(<see cref="StairOrbEmitter"/>)의 출발 요청을 받아
    /// Framework 비행(<see cref="EffectFlight.PlayFlight"/>, com.jjevol.effects 0.1.0-pre.5)으로 <c>Obj_Stair_Orb</c>를 UI Effect 루트
    /// (<see cref="StairOrbFlight.UiRoot"/>) 아래에 재생한다. UI 카메라가 그리므로 HUD보다 앞에 보이고, 풀·생명주기·이동은 Framework가 갖는다.
    /// 경로: 출발 = 탑 오브 월드 자리, 경유 = 퍼질 월드 자리(월드 카메라로 매 프레임 투영), 도착 = HUD <c>OrbPos Anchor</c>(<see cref="StairOrbFlight.Target"/>).
    /// 구간 시간·곡선·크기·멈춤은 프리팹 <c>Obj_Stair_Orb</c>의 <see cref="EffectFlightProfile"/>이 정본이다.
    /// 도착(onArrived) 순간 또는 도착하지 못하고 끝날 때(onEnded(false), 재생 거절 포함) 정확히 한 번 <see cref="StairOrbFlight.RaiseArrived"/>로
    /// 수령 번호를 알리고, 시뮬이 그 몫만큼 실제 게이지를 올린다(시간을 재지 않는다).
    /// 월드에서 보이던 크기에 맞추는 배율(<see cref="FitScale"/>)은 Framework에 없어 여기서 요청 크기로 준다.
    /// </summary>
    public static class StairOrbFx
    {
        static IEffectPlayer s_effects;

        /// <summary>Boot(<see cref="MvFx.InstallDefault"/>)가 한 번 세운다. null이면 내린다.</summary>
        public static void Install(IEffectPlayer effects)
        {
            s_effects = effects;
            StairOrbFlight.Launcher = effects != null ? Launch : (StairOrbFlight.LaunchFn)null;
        }

        static bool Launch(in StairOrbFlight.OrbLaunch r)
        {
            var root = StairOrbFlight.UiRoot?.Invoke();
            if (s_effects == null || root == null || string.IsNullOrEmpty(r.Key)) return false;
            var canvas = root.GetComponentInParent<Canvas>(true);
            var uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            float fit = FitScale(r.WorldCam, r.Spread, root, uiCam);

            int run = r.Run, id = r.ReceiveId;
            bool reported = false;
            void Report()
            {
                if (reported) return;
                reported = true;
                StairOrbFlight.RaiseArrived(run, id);
            }

            var cam = r.WorldCam;
            var route = new EffectFlightRoute(
                from: EffectPoint.World(r.From, cam),
                to: EffectPoint.FromScreen(TargetScreen, cam), // HUD 자리가 없으면 마지막 자리(퍼진 자리)에서 도착
                onArrived: _ => Report(),          // 도착하는 순간(요청이 끝나기 전)
                onEnded: _ => Report());           // 도착하지 못하고 끝남·재생 거절(도착했으면 이미 보냄)
            route.via = new[] { EffectPoint.World(r.Spread, cam) };

            var req = new EffectRequest(r.Key,
                loop: true, // 비행이 도착에서 요청을 끝낸다(Framework 권장)
                sortingLayer: canvas != null ? canvas.sortingLayerName : "",
                sortingOrder: (canvas != null ? canvas.sortingOrder : 0) + r.SortingOffset,
                unscaledTime: false,   // 게임 시간(배속·일시정지 = Time.timeScale, MV-CLOCK-3)
                parent: root,
                localScale: Vector3.one * fit);
            s_effects.PlayFlight(req, route);
            return true;
        }

        static bool TargetScreen(out Vector2 screen)
        {
            screen = default;
            var t = StairOrbFlight.Target;
            return t != null && t(out screen);
        }

        /// <summary>
        /// 퍼질 자리에서 월드 카메라로 보이던 크기(px)를 UI 판에서도 같게 하는 localScale 배율(MV-UI3-STAIRFX2).
        /// 카메라·판이 없으면 1. Overlay 캔버스는 월드 1 = 1 px.
        /// </summary>
        public static float FitScale(Camera worldCam, Vector3 spread, Transform plane, Camera uiCam)
        {
            if (plane == null || worldCam == null) return 1;
            float worldPpu = PixelsPerUnit(worldCam, spread);
            float uiPpu = uiCam != null ? PixelsPerUnit(uiCam, plane.position) : 1f;
            float parentScale = Mathf.Abs(plane.lossyScale.x);
            if (worldPpu <= 0 || uiPpu <= 0 || parentScale <= 0) return 1;
            return worldPpu / uiPpu / parentScale;
        }

        static float PixelsPerUnit(Camera c, Vector3 at)
        {
            if (c.orthographic) return c.orthographicSize > 0 ? c.pixelHeight / (2f * c.orthographicSize) : 0;
            float depth = Vector3.Dot(at - c.transform.position, c.transform.forward);
            if (depth <= 0) return 0;
            return c.pixelHeight / (2f * depth * Mathf.Tan(c.fieldOfView * 0.5f * Mathf.Deg2Rad));
        }

        /// <summary>시험 정리용.</summary>
        public static void ClearForTests() => s_effects = null;
    }
}
