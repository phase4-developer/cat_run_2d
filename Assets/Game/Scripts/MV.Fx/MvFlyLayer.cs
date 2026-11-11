using System;
using System.Collections.Generic;
using System.Globalization;
using MV.Data;
using UnityEngine;

namespace MV.Fx
{
    /// <summary>CSS `cubic-bezier(x1,y1,x2,y2)`: 시간 x → 진행 y. x→t는 뉴턴 풀이(+이분법 보정, 06 §6.8.1).</summary>
    public readonly struct MvCubicBezier
    {
        public readonly float X1, Y1, X2, Y2;

        public MvCubicBezier(float x1, float y1, float x2, float y2) { X1 = x1; Y1 = y1; X2 = x2; Y2 = y2; }

        /// <summary>표 값 `.5/0/.8/.6`(또는 쉼표) 해석. 형식이 틀리면 false.</summary>
        public static bool TryParse(string s, out MvCubicBezier curve)
        {
            curve = default;
            if (string.IsNullOrEmpty(s)) return false;
            var p = s.Split('/', ',');
            if (p.Length != 4) return false;
            var v = new float[4];
            for (int i = 0; i < 4; i++)
                if (!float.TryParse(p[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v[i])) return false;
            curve = new MvCubicBezier(v[0], v[1], v[2], v[3]);
            return true;
        }

        /// <summary>표 키 값을 해석한다. 키가 없으면 예외, 형식이 틀리면 오류 로그(대체 곡선 없음).</summary>
        public static MvCubicBezier FromConfig(RunConfigTable c, string key)
        {
            string raw = c.Raw(key);
            if (!TryParse(raw, out var curve)) Debug.LogError($"[MvFlyLayer] 300_runConfig {key} 곡선 형식이 틀립니다: {raw}");
            return curve;
        }

        static float B(float a, float b, float t)
        {
            float u = 1 - t;
            return 3 * u * u * t * a + 3 * u * t * t * b + t * t * t;
        }

        static float D(float a, float b, float t)
        {
            float u = 1 - t;
            return 3 * u * u * a + 6 * u * t * (b - a) + 3 * t * t * (1 - b);
        }

        public float Evaluate(float x)
        {
            if (x <= 0) return 0;
            if (x >= 1) return 1;
            float t = x;
            for (int i = 0; i < 8; i++)
            {
                float err = B(X1, X2, t) - x;
                if (Mathf.Abs(err) < 1e-6f) return B(Y1, Y2, t);
                float d = D(X1, X2, t);
                if (Mathf.Abs(d) < 1e-6f) break;
                t -= err / d;
            }
            float lo = 0, hi = 1;
            t = x;
            for (int i = 0; i < 40; i++)
            {
                float v = B(X1, X2, t);
                if (Mathf.Abs(v - x) < 1e-6f) break;
                if (v < x) lo = t; else hi = t;
                t = (lo + hi) * 0.5f;
            }
            return B(Y1, Y2, t);
        }
    }

    /// <summary>HUD 요소 위치(MV-UI-HUD가 구현). 화면 px(왼아래 원점) 가운데와 크기.</summary>
    public interface IMvHudAnchors
    {
        bool TryGetRect(FlyTarget target, out Vector2 center, out Vector2 size);
    }

    /// <summary>비행 아이콘 그림 쪽(UGUI, D-48). id는 <see cref="MvFlyLayer"/>가 준다.</summary>
    public interface IMvFlyView
    {
        void Show(int id, FlyTarget icon, int amount);
        void Place(int id, Vector2 screen, float scale, float alpha);
        void Hide(int id);
    }

    /// <summary>
    /// HUD 보상 비행(06 §6.8, 정본 `flyTo`·`flyTo2`·`flyToWorld`). 실시간: 0.55 s 동안 베지어 `(.5,0,.8,.6)`로 이동·크기 1.3→0.6·알파 1→0.4,
    /// 560 ms에 제거하고 <see cref="Arrived"/>(도착 콜백, 정본 setTimeout). 도착 시점이 값 반영과 얽히는 것은 04가 정하고(06 §6.8.2),
    /// 여기서는 HUD 표시 쪽 콜백만 낸다.
    /// Framework 대체: effects 고정본에 EffectIconBehaviour가 없고 effects.ui는 고정본이 없어 게임 구현. 고정본이 나오면 이 층을 그 Behaviour로 교체.
    /// </summary>
    public sealed class MvFlyLayer
    {
        public enum Mode { ToHud, HudToHud, HudToWorld, Card }

        struct Job
        {
            public int Id;
            public Mode Mode;
            public FlyTarget Target;
            public Vector3 World;
            public Vector2 From, To;
            public bool Resolved;
            public float Delay, T;
            public int Amount;
            public float S0, S1, A1;
        }

        readonly List<Job> _jobs = new List<Job>(32);
        int _nextId = 1;

        /// <summary>`300_runConfig` fly.*·flyCard.*(<see cref="FromTables"/>가 채운다, 코드 기본값 없음).</summary>
        public float Time, RemoveSec, StartScale, EndScale, EndAlpha;
        public float CardTime, CardRemoveSec;
        public MvCubicBezier Curve, CardCurve;
        /// <summary>동시 비행 상한(06 §6.14, 24, 표 키 없음). 넘으면 그림 없이 도착 시점만 지킨다.</summary>
        public int MaxIcons = 24;

        public IMvFlyView View;
        public IMvHudAnchors Anchors;
        public IMvScreenProjector Projector;

        /// <summary>도착(정본 콜백 시점, 출발 + 560 ms). (대상, 값).</summary>
        public event Action<FlyTarget, int> Arrived;

        public int ActiveCount => _jobs.Count;

        /// <summary>표에서 만든다. 표가 없거나 키가 없으면 예외(대체값 없음).</summary>
        public static MvFlyLayer FromTables(MvTables t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            var c = t.RunConfig;
            float F(string k) => (float)c.D(k);
            return new MvFlyLayer
            {
                Time = F(RunConfigKeys.Fly_Time),
                RemoveSec = F(RunConfigKeys.Fly_RemoveMs) / 1000f,
                StartScale = F(RunConfigKeys.Fly_StartScale),
                EndScale = F(RunConfigKeys.Fly_EndScale),
                EndAlpha = F(RunConfigKeys.Fly_EndAlpha),
                CardTime = F(RunConfigKeys.FlyCard_Time),
                CardRemoveSec = F(RunConfigKeys.FlyCard_RemoveMs) / 1000f,
                Curve = MvCubicBezier.FromConfig(c, RunConfigKeys.Fly_Bezier),
                CardCurve = MvCubicBezier.FromConfig(c, RunConfigKeys.FlyCard_Bezier),
            };
        }

        /// <summary>정본 `flyTo(world, target)`.</summary>
        public int FlyTo(in FlyRequest r) => Add(new Job
        {
            Mode = Mode.ToHud, Target = r.Target, World = r.World, Delay = Mathf.Max(0, r.DelayMs) / 1000f, Amount = r.Amount,
            S0 = StartScale, S1 = EndScale, A1 = EndAlpha,
        });

        /// <summary>정본 `flyTo2(fromEl, targetEl)`: 시작 = 요소 top + 0.35h, 끝 = 대상 top + min(h, 30px), 크기 1.2→0.7, 알파 유지.</summary>
        public int FlyTo2(Vector2 fromCenter, Vector2 fromSize, FlyTarget target, int amount = 0) => Add(new Job
        {
            Mode = Mode.HudToHud, Target = target, From = new Vector2(fromCenter.x, fromCenter.y + fromSize.y * 0.5f - fromSize.y * 0.35f),
            Amount = amount, S0 = 1.2f, S1 = 0.7f, A1 = 1,
        });

        /// <summary>정본 `flyToWorld`(호출 없음, D-46). 크기 1.1→0.55.</summary>
        public int FlyToWorld(Vector2 fromCenter, Vector3 world, FlyTarget icon) => Add(new Job
        {
            Mode = Mode.HudToWorld, Target = icon, From = fromCenter, World = world, S0 = 1.1f, S1 = 0.55f, A1 = 1,
        });

        /// <summary>룰렛 카드 비행 `flycard`(0.6 s, `(.55,0,.75,.4)`, 620 ms 제거).</summary>
        public int FlyCard(Vector2 from, Vector2 to, int cardId) => Add(new Job
        {
            Mode = Mode.Card, Target = FlyTarget.Hand, From = from, To = to, Resolved = true, Amount = cardId, S0 = 1, S1 = 1, A1 = 1,
        });

        int Add(Job j)
        {
            j.Id = _nextId++;
            _jobs.Add(j);
            return j.Id;
        }

        /// <summary>진행 0~1에서 위치 비율(곡선 적용). 이동 시간이 지나면 1.</summary>
        public float Progress(float t, bool card = false) => (card ? CardCurve : Curve).Evaluate(Mathf.Clamp01(t / (card ? CardTime : Time)));

        public void Step(float realDt)
        {
            for (int i = 0; i < _jobs.Count; i++)
            {
                var j = _jobs[i];
                float dt = realDt;
                if (j.Delay > 0)
                {
                    j.Delay -= dt;
                    if (j.Delay > 0) { _jobs[i] = j; continue; }
                    dt = -j.Delay;
                    j.Delay = 0;
                }
                if (j.T == 0 && !j.Resolved) Resolve(ref j);
                if (j.T == 0 && View != null && i < MaxIcons) View.Show(j.Id, j.Target, j.Amount);
                if (j.T == 0 && dt == 0) dt = 1e-6f;
                j.T += dt;
                bool card = j.Mode == Mode.Card;
                float k = Mathf.Clamp01(j.T / (card ? CardTime : Time));
                float e = (card ? CardCurve : Curve).Evaluate(k);
                if (View != null)
                    View.Place(j.Id, Vector2.LerpUnclamped(j.From, j.To, e), Mathf.LerpUnclamped(j.S0, j.S1, e), Mathf.LerpUnclamped(1, j.A1, e));
                _jobs[i] = j;
            }
            for (int i = _jobs.Count - 1; i >= 0; i--)
            {
                var j = _jobs[i];
                if (j.Delay > 0 || j.T < (j.Mode == Mode.Card ? CardRemoveSec : RemoveSec)) continue;
                _jobs.RemoveAt(i);
                View?.Hide(j.Id);
                Arrived?.Invoke(j.Target, j.Amount);
            }
        }

        void Resolve(ref Job j)
        {
            j.Resolved = true;
            if (j.Mode == Mode.ToHud && Projector != null) Projector.WorldToScreen(j.World, out j.From);
            if (j.Mode == Mode.HudToWorld)
            {
                if (Projector != null) Projector.WorldToScreen(j.World, out j.To);
                return;
            }
            if (Anchors != null && Anchors.TryGetRect(j.Target, out var c, out var size))
                j.To = j.Mode == Mode.HudToHud ? new Vector2(c.x, c.y + size.y * 0.5f - Mathf.Min(size.y, 30f)) : c;
        }

        public void Clear()
        {
            foreach (var j in _jobs) View?.Hide(j.Id);
            _jobs.Clear();
        }
    }
}
