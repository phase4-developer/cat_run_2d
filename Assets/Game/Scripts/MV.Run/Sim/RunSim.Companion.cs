using System;
using System.Collections.Generic;
using static MV.Run.RunFormulas;

namespace MV.Run
{
    // 동행 자동 사격(04 §4.20.5). 정본 경로(동행 없음)에서는 한 줄도 돌지 않는다. 난수는 CompanionRng만 쓴다.
    public sealed partial class RunSim
    {
        readonly List<RunObject> _compBuf = new List<RunObject>();
        readonly List<int> _compIdx = new List<int>();

        /// <summary>Chance: 플레이어 투사체(본체·분신·창)가 명중할 때마다 판정, 성공하면 1발 예약.</summary>
        void CompanionOnProjectileHit()
        {
            var s = S;
            for (int i = 0; i < s.Companions.Count; i++)
            {
                var c = s.Companions[i];
                if (c.Spec.Trigger != CompanionTrigger.Chance) continue;
                if (s.CompanionRng.Chance(c.Spec.Every)) c.Armed = true;
            }
        }

        /// <summary>updRun 12단계(죽은 투사체 제거)와 13단계 사이. vol0 = 이번 updRun 사격 전 R.vol.</summary>
        void StepCompanions(double dt, int vol0)
        {
            var s = S;
            for (int i = 0; i < s.Companions.Count; i++)
            {
                var c = s.Companions[i];
                if (c.Spec.Trigger == CompanionTrigger.Seconds)
                {
                    c.T -= dt;
                    if (c.T > 0) continue;
                    // 대상이 없으면 보류: 0에 멈춰 두고(시간 빚을 쌓지 않아 몰아 쏘지 않는다) 대상이 생기는 프레임에 쏜다.
                    if (!PickCompanionTargets(c.Spec.Targets)) { c.T = 0; continue; }
                    FireCompanion(c);
                    c.T += c.Spec.Every;
                    continue;
                }
                if (c.Spec.Trigger == CompanionTrigger.Hits)
                {
                    int n = (int)Math.Round(c.Spec.Every);
                    if (n > 0 && s.Vol != vol0 && s.Vol % n == 0) c.Armed = true;
                }
                if (!c.Armed) continue;
                c.Armed = false; // Hits·Chance는 대상이 없으면 버린다
                if (PickCompanionTargets(c.Spec.Targets)) FireCompanion(c);
            }
        }

        /// <summary>
        /// 대상 고르기 → <see cref="_compBuf"/>. 후보: hittable·breakable·충전 게이트(107) 없음(우물·게이트 제외).
        /// 앞쪽 RANGE 안. 정렬 ① z−hd ② |x−P.x| ③ R.objs 순서. 앞에서 n개.
        /// </summary>
        bool PickCompanionTargets(int n)
        {
            var s = S; var P = s.P;
            _compBuf.Clear(); _compIdx.Clear();
            double range = ProjRange(s);
            for (int i = 0; i < s.Objs.Count; i++)
            {
                var o = s.Objs[i];
                if (!Hittable(o) || !Breakable(o) || o.Charge != null) continue;
                double f = o.Z - o.Hd;
                if (f < P.Z || f - P.Z > range) continue;
                _compBuf.Add(o); _compIdx.Add(i);
            }
            if (_compBuf.Count == 0) return false;
            var order = new int[_compBuf.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) =>
            {
                var oa = _compBuf[a]; var ob = _compBuf[b];
                int c = (oa.Z - oa.Hd).CompareTo(ob.Z - ob.Hd);
                if (c != 0) return c;
                c = Math.Abs(oa.X - P.X).CompareTo(Math.Abs(ob.X - P.X));
                return c != 0 ? c : _compIdx[a].CompareTo(_compIdx[b]);
            });
            int take = Math.Min(Math.Max(1, n), order.Length);
            var picked = new List<RunObject>(take);
            for (int i = 0; i < take; i++) picked.Add(_compBuf[order[i]]);
            _compBuf.Clear();
            _compBuf.AddRange(picked);
            return true;
        }

        /// <summary>즉시 명중. 이벤트를 먼저 쌓고 정렬 순서대로 hit(proc 없음, 치명·2연발 없음).</summary>
        void FireCompanion(CompanionRuntime c)
        {
            var s = S;
            double dmg = BaseDmg(s) * c.Spec.DmgPct / 100;
            var ids = new int[_compBuf.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = _compBuf[i].Id;
            var targets = _compBuf.ToArray();
            c.Shots++;
            s.Emit(new RunEvent(RunEventType.CompanionShot, a: c.Spec.SourceId, x: s.P.X, z: s.P.Z, ids: ids, amount: dmg));
            foreach (var o in targets)
                if (!o.Dead) Hit(o, dmg, false, false, false, HitColor.Normal, HitSource.Companion);
        }
    }
}
