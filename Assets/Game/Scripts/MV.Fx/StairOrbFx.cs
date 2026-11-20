using System.Collections.Generic;
using jjevol.Effect;
using MV.View;
using UnityEngine;

namespace MV.Fx
{
    /// <summary>
    /// 계단 경험치 오브 재생기(MV-UI3-STAIRFX2). 월드 탑 오브(<see cref="StairOrbEmitter"/>)의 출발 요청을 받아
    /// Framework <c>EffectManager</c>로 <c>Obj_Stair_Orb</c>를 UI Effect 루트(<see cref="StairOrbFlight.UiRoot"/>) 아래에 재생한다.
    /// UI 카메라가 그리므로 HUD보다 앞에 보이고, 풀·생명주기는 EffectManager가 갖는다(요청은 반복 재생, 날기가 끝나면 <see cref="StairOrb"/>가 반환).
    /// 출발 정보는 요청 표식(skinIndex, <see cref="MvFxParamBank"/>와 같은 방식)으로 오브 부품에 건넨다.
    /// 도착 신호(<see cref="StairOrbFlight.RaiseArrived"/>)는 요청이 끝날 때(정상 반환·중단·로드 실패·파괴) 정확히 한 번.
    /// </summary>
    public static class StairOrbFx
    {
        static IEffectPlayer s_effects;
        static readonly Dictionary<int, StairOrbFlight.OrbLaunch> s_pending = new Dictionary<int, StairOrbFlight.OrbLaunch>();
        static int s_next = 1;

        /// <summary>아직 끝나지 않은 오브 요청 수(시험·QA).</summary>
        public static int Pending => s_pending.Count;

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
            int token = s_next++;
            if (s_next == int.MaxValue) s_next = 1;
            s_pending[token] = r;
            var req = new EffectRequest(r.Key,
                skinIndex: token,
                loop: true, // 파티클은 반복, 끝은 오브 부품이 정한다
                sortingLayer: canvas != null ? canvas.sortingLayerName : "",
                sortingOrder: (canvas != null ? canvas.sortingOrder : 0) + r.SortingOffset,
                unscaledTime: true,
                parent: root,
                onEnded: _ => Arrive(token));
            s_effects.Play(req);
            return true;
        }

        static void Arrive(int token)
        {
            if (s_pending.Remove(token)) StairOrbFlight.RaiseArrived();
        }

        /// <summary>오브 부품이 시작할 때 출발 정보를 읽는다.</summary>
        public static bool TryGet(int token, out StairOrbFlight.OrbLaunch launch) => s_pending.TryGetValue(token, out launch);

        /// <summary>시험 정리용.</summary>
        public static void ClearForTests()
        {
            s_pending.Clear();
            s_effects = null;
            s_next = 1;
        }
    }
}
