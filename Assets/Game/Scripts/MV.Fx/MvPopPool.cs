using System.Collections.Generic;
using MV.Data;
using UnityEngine;

namespace MV.Fx
{
    /// <summary>월드 점 → 화면 점(px, 왼아래 원점). 카메라 뒤면 false(정본 `toScreen().vis`).</summary>
    public interface IMvScreenProjector
    {
        bool WorldToScreen(Vector3 world, out Vector2 screen);
    }

    /// <summary>Unity 카메라 투영(GameCamera = MV.View CameraRig.Cam). 뷰 깊이 ≤ 0이면 숨김(06 §6.7.2).</summary>
    public sealed class CameraScreenProjector : IMvScreenProjector
    {
        readonly System.Func<Camera> _cam;

        public CameraScreenProjector(System.Func<Camera> cam) => _cam = cam;

        public bool WorldToScreen(Vector3 world, out Vector2 screen)
        {
            var cam = _cam?.Invoke();
            if (cam == null) { screen = default; return false; }
            var p = cam.WorldToScreenPoint(world);
            screen = new Vector2(p.x, p.y);
            return p.z > 0;
        }
    }

    /// <summary>
    /// 숫자 팝업 그림 쪽(UGUI TMP, D-48 권장안: HUD 캔버스 `DamagePopLayer`). MV-UI-HUD 또는 통합이 구현한다.
    /// slot은 0 ~ 최대−1. Show 뒤 매 프레임 Place, 끝나면 Hide.
    /// </summary>
    public interface IMvPopView
    {
        void Show(int slot, string text, Color32 color, float fontU);
        /// <summary>screen = 화면 px(왼아래 원점), scale = 크기 곡선, alpha = 알파 곡선(숨김이면 0).</summary>
        void Place(int slot, Vector2 screen, float scale, float alpha);
        void Hide(int slot);
    }

    /// <summary>
    /// 숫자 팝업 풀(06 §6.7, 정본 `pop`·`stepPops` L1702~1711). 새로 넣기 전 활성이 60개를 넘으면(= pop.max − 1)
    /// 가장 오래된 것을 다시 쓴다 → 동시 최대 61개(pop.max). 실시간 dt, 월드 y가 초당 1.4 m 오른다.
    /// Framework 대체: Framework에 숫자 팝업 풀이 없어 게임 풀로 둔다(effects.ui 고정본이 나오면 비교).
    /// </summary>
    public sealed class MvPopPool
    {
        struct Entry
        {
            public Vector3 World;
            public float T;
            public bool Live;
        }

        readonly Entry[] _items;
        readonly List<int> _order;
        readonly Stack<int> _free;
        public readonly int Max;
        /// <summary>`300_runConfig` pop.*(<see cref="FromTables"/>가 채운다, 코드 기본값 없음).</summary>
        public float Life, Rise, ScaleIn, ScaleBoost, FadeFrom, FontU;

        public IMvPopView View;
        public IMvScreenProjector Projector;

        public MvPopPool(int max)
        {
            Max = Mathf.Max(1, max);
            _items = new Entry[Max];
            _order = new List<int>(Max);
            _free = new Stack<int>(Max);
            for (int i = Max - 1; i >= 0; i--) _free.Push(i);
        }

        /// <summary>표에서 만든다. 표가 없거나 키가 없으면 예외(대체값 없음).</summary>
        public static MvPopPool FromTables(MvTables t)
        {
            if (t == null) throw new System.ArgumentNullException(nameof(t));
            var c = t.RunConfig;
            float F(string k) => (float)c.D(k);
            return new MvPopPool(c.I(RunConfigKeys.Pop_Max))
            {
                Life = F(RunConfigKeys.Pop_Life), Rise = F(RunConfigKeys.Pop_Rise), ScaleIn = F(RunConfigKeys.Pop_ScaleIn),
                ScaleBoost = F(RunConfigKeys.Pop_ScaleBoost), FadeFrom = F(RunConfigKeys.Pop_FadeFrom), FontU = F(RunConfigKeys.Pop_FontU),
            };
        }

        public int ActiveCount => _order.Count;

        /// <summary>활성 슬롯(오래된 순).</summary>
        public IReadOnlyList<int> Order => _order;

        public float Age(int slot) => _items[slot].T;
        public Vector3 WorldOf(int slot) => _items[slot].World;

        /// <summary>팝업 1개. 반환 = 쓴 슬롯.</summary>
        public int Add(in PopRequest r)
        {
            if (_order.Count > Max - 1 || _free.Count == 0)
            {
                int old = _order[0];
                _order.RemoveAt(0);
                _items[old].Live = false;
                View?.Hide(old);
                _free.Push(old);
            }
            int slot = _free.Pop();
            _items[slot] = new Entry { World = r.World, T = 0, Live = true };
            _order.Add(slot);
            View?.Show(slot, r.Text, r.Color, FontU * (r.Size > 0 ? r.Size : 1));
            return slot;
        }

        /// <summary>크기 곡선 `k &lt; 0.15 ? 1 + (0.15 − k)·3 : 1`.</summary>
        public float ScaleAt(float k) => k < ScaleIn ? 1 + (ScaleIn - k) * ScaleBoost : 1;

        /// <summary>알파 곡선 `1 − max(0, (k − 0.6)/0.4)`.</summary>
        public float AlphaAt(float k) => 1 - Mathf.Max(0, (k - FadeFrom) / (1 - FadeFrom));

        public void Step(float realDt)
        {
            for (int i = 0; i < _order.Count; i++)
            {
                int s = _order[i];
                ref var e = ref _items[s];
                e.T += realDt;
                e.World.y += realDt * Rise;
                if (View == null) continue;
                float k = e.T / Life;
                Vector2 sp = default;
                bool vis = Projector != null && Projector.WorldToScreen(e.World, out sp);
                View.Place(s, sp, ScaleAt(k), vis ? AlphaAt(k) : 0);
            }
            for (int i = _order.Count - 1; i >= 0; i--)
            {
                int s = _order[i];
                if (_items[s].T < Life) continue;
                _items[s].Live = false;
                _order.RemoveAt(i);
                _free.Push(s);
                View?.Hide(s);
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _order.Count; i++) { _items[_order[i]].Live = false; View?.Hide(_order[i]); _free.Push(_order[i]); }
            _order.Clear();
        }
    }
}
