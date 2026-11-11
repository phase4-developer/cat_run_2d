using System;
using System.Collections.Generic;
using MV.Data;
using MV.Run;

namespace MV.Sound
{
    /// <summary>재생 요청 1개: 03_sound key와 지연(ms, 0 = 즉시).</summary>
    public readonly struct MvSoundCue : IEquatable<MvSoundCue>
    {
        public readonly string Key;
        public readonly int DelayMs;

        public MvSoundCue(string key, int delayMs = 0)
        {
            Key = key;
            DelayMs = delayMs;
        }

        public bool Equals(MvSoundCue o) => string.Equals(Key, o.Key, StringComparison.Ordinal) && DelayMs == o.DelayMs;
        public override bool Equals(object obj) => obj is MvSoundCue o && Equals(o);
        public override int GetHashCode() => (Key?.GetHashCode() ?? 0) * 31 + DelayMs;
        public override string ToString() => DelayMs == 0 ? Key : Key + "@" + DelayMs + "ms";
    }

    /// <summary>
    /// RunEvent → 소리 키(정본 SND 호출 지점, 04 §4.18.1 「구독」 Sound 칸). 키는 그 이벤트를 낸 표 행의 소리 열에서 읽고,
    /// 표 열이 없는 정본 고정 키만 <see cref="MvSoundKeys"/>를 쓴다. 조건(첫 유닛·창 아님, i%4, i==0, n&gt;0 …)은 정본과 같다.
    /// BGM 레이어(RunStarted)는 <see cref="MvSoundPlayer"/>가 처리한다.
    /// </summary>
    public sealed class MvRunSoundRouter
    {
        readonly Func<MvTables> _tables;
        int _fieldBreakObj;
        bool _hasFieldBreak;

        public MvRunSoundRouter(Func<MvTables> tables = null)
        {
            _tables = tables ?? (() => MvTables.Current);
        }

        /// <summary>런 사이 상태(필드 파괴 → 보상 소리 대기)를 비운다.</summary>
        public void Reset() => _hasFieldBreak = false;

        /// <summary>이벤트 1개의 소리를 cues에 더한다(정본 호출 순서).</summary>
        public void Route(in RunEvent e, List<MvSoundCue> cues)
        {
            var t = _tables();
            if (t == null || cues == null) return;
            switch (e.Type)
            {
                case RunEventType.RunStarted:
                    Reset();
                    break;
                case RunEventType.FloorEnter: // L2028
                    Add(cues, ArriveSound(t, ArriveAction.FloorBanner));
                    break;
                case RunEventType.Fire: // L2091: 본체(i=0)만, 창은 return이라 소리 없음, 2연발은 한 번
                    if (e.A == 0 && !e.Flag && e.C != 1) Add(cues, MvSoundKeys.Throw);
                    break;
                case RunEventType.ProjImpact: // L2107
                    Add(cues, e.Flag ? MvSoundKeys.Crit : MvSoundKeys.Hit);
                    break;
                case RunEventType.Hit: // L1912 tower clank(quiet여도 울림). L1919 stair는 hittable이 아니라 안 옴
                    Add(cues, KindSound(t, e.KindId, true));
                    break;
                case RunEventType.Break: // L1925, L1936, L1964, L1966
                    Add(cues, KindSound(t, e.KindId, false));
                    _hasFieldBreak = e.A >= 0;
                    _fieldBreakObj = e.ObjId;
                    break;
                case RunEventType.Gain: // L1937~1941: 필드(wall) 파괴 보상만 07_consumeType.gainSound를 더 낸다
                    if (_hasFieldBreak && e.ObjId == _fieldBreakObj)
                    {
                        _hasFieldBreak = false;
                        Add(cues, t.ConsumeType.Get(e.A)?.gainSound);
                    }
                    break;
                case RunEventType.GateTake: // L2046 gate(n>0), L2051 forge(made>0)
                    if (e.A > 0) Add(cues, PassSound(t, e.KindId));
                    break;
                case RunEventType.GaugeLevelUp: // L1890
                    Add(cues, MvSoundKeys.Lvup);
                    break;
                case RunEventType.Steal: // L2063
                    Add(cues, StealSound(t));
                    break;
                case RunEventType.Blocked: // L2068
                    Add(cues, MvSoundKeys.Wood);
                    break;
                case RunEventType.ClimbStart: // L2260
                    Add(cues, ArriveSound(t, ArriveAction.StartClimb));
                    break;
                case RunEventType.ClimbStepShown: // L2270: i % 4 == 0
                {
                    var c = ClimbConfig(t);
                    int every = c != null ? Round(c["tickEvery"]) : 0;
                    if (every > 0 && e.A % every == 0) Add(cues, MvSoundKeys.Tick);
                    break;
                }
                case RunEventType.ClimbLap: // L2277 card(lapTier)
                {
                    var c = ClimbConfig(t);
                    if (c != null) Add(cues, CardTierSound(t, Round(c["lapTier"])));
                    break;
                }
                case RunEventType.ClimbTierUp: // L2281 tierup(t)
                    Add(cues, MvSoundKeys.Variant(MvSoundKeys.Tierup, e.A));
                    break;
                case RunEventType.ClimbWarpStage: // L3165: 단계 > 0이면 tierup(min(4, st+1)) = 330 워프 행 enterSound
                    if (e.A > 0)
                    {
                        var stages = t.Climb.WarpStages();
                        if (e.A < stages.Count) Add(cues, stages[e.A].enterSound);
                    }
                    break;
                case RunEventType.ClimbEnd: // L2286
                    Add(cues, MvSoundKeys.Land);
                    break;
                case RunEventType.RouletteFlip: // L2368: 첫 카드만
                    if (e.A == 0) Add(cues, MvSoundKeys.Tick);
                    break;
                case RunEventType.RouletteChosen: // L2369 card(pick.t)
                    Add(cues, CardTierSound(t, e.C));
                    break;
                case RunEventType.CardApplied: // L2377
                    Add(cues, MvSoundKeys.Buy);
                    break;
                case RunEventType.RunFinished: // L2408: gate면 즉시 win, 아니면 350ms 뒤 (pass||zoneEnd ? win : fail)
                {
                    bool gate = e.A == (int)FinishReason.Gate;
                    int delay = gate ? 0 : FailDelayMs(t);
                    Add(cues, gate || e.Flag ? MvSoundKeys.Win : MvSoundKeys.Fail, delay);
                    break;
                }
            }
        }

        static void Add(List<MvSoundCue> cues, string key, int delayMs = 0)
        {
            if (!string.IsNullOrEmpty(key)) cues.Add(new MvSoundCue(key, delayMs));
        }

        /// <summary>105 hitSound/breakSound. enemyId가 있으면 102 행 값이 덮어쓴다(빈칸이면 105 값).</summary>
        static string KindSound(MvTables t, int kindId, bool hit)
        {
            var k = t.ObjectKind.Get(kindId);
            if (k == null) return null;
            var en = k.enemyId != 0 ? t.Enemy.Get(k.enemyId) : null;
            string v = en == null ? null : hit ? en.hitSound : en.breakSound;
            if (string.IsNullOrEmpty(v)) v = hit ? k.hitSound : k.breakSound;
            return v;
        }

        /// <summary>게이트 통과 소리 = 105 receiveId → 108 passSound.</summary>
        static string PassSound(MvTables t, int kindId)
        {
            var k = t.ObjectKind.Get(kindId);
            return k != null && k.receiveId != 0 ? t.ReceiveCondition.Get(k.receiveId)?.passSound : null;
        }

        /// <summary>도착 동작(108 arriveAction)의 passSound. ClimbStart·FloorEnter는 kindId가 없어 동작으로 찾는다.</summary>
        static string ArriveSound(MvTables t, ArriveAction action)
        {
            foreach (var r in t.ReceiveCondition.Sorted)
                if (r.arriveAction == action && !string.IsNullOrEmpty(r.passSound)) return r.passSound;
            return null;
        }

        /// <summary>
        /// 109 touchAction Steal 행의 stealSound. Steal 이벤트에 kindId가 없어 첫 행을 쓴다
        /// (MV-RUN 변경 요청 후보: Steal에 kindId를 실으면 105 → moveAiId로 정확히 찾는다).
        /// </summary>
        static string StealSound(MvTables t)
        {
            foreach (var r in t.MoveAi.Sorted)
                if (r.touchAction == TouchAction.Steal && !string.IsNullOrEmpty(r.stealSound)) return r.stealSound;
            return null;
        }

        /// <summary>115 tierIndex 행의 cardSound.</summary>
        static string CardTierSound(MvTables t, int tier)
        {
            foreach (var r in t.CardTier.Datas)
                if (r.tierIndex == tier) return r.cardSound;
            return null;
        }

        static int Round(double v) => (int)Math.Floor(v + 0.5);

        /// <summary>`330_climb` 설정 행(tickEvery·lapTier). 없으면 오류 로그 후 null(대체값 없음).</summary>
        static ClimbData ClimbConfig(MvTables t)
        {
            var c = t.Climb.Config();
            if (c == null) UnityEngine.Debug.LogError("[MvRunSoundRouter] 330_climb 설정(Config) 행이 없습니다.");
            return c;
        }

        static int FailDelayMs(MvTables t) => t.RunConfig.I(RunConfigKeys.FAIL_SFX_DELAY_MS);
    }
}
