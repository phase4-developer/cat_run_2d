using System;
using System.Collections.Generic;
using MV.Data;
using static MV.Run.RunFormulas;

namespace MV.Run
{
    // 런 규칙 본체(04 §4.1~§4.13). 종류 이름 분기 없이 105 행 플래그와 106~109 컴포넌트 type으로만 동작을 고른다.
    public sealed partial class RunSim
    {
        RunParams _p;
        /// <summary>이 런 Area의 계단 게이트 M 표(302_area.sgateM). 없으면 null(107 base).</summary>
        IReadOnlyList<double> _sgateM;
        readonly List<MvDropResult> _dropBuf = new List<MvDropResult>();

        RunState S => State;
        MvTables T => State.Tables;

        partial void OnCreated()
        {
            _p = RunParams.From(T);
            foreach (var a in T.Area.Datas) if (a.areaNo == S.Area + 1) { _sgateM = a.sgateM; break; }
            S.Emit(new RunEvent(RunEventType.RunStarted, a: S.Area, b: S.Level, c: S.Tpl));
            S.Emit(new RunEvent(RunEventType.WeaponHandReset, ids: (int[])S.W.Clone()));
        }

        // ================= 프레임 (§4.13.2) =================

        partial void TickCore(double realDt, RunInput input)
        {
            var s = S;
            s.Frame++;
            if (s.Phase == RunPhase.Lobby) return;
            double dt = Math.Min(_p.DtCap, realDt);
            s.RealTime += dt;
            if (input.DragTargetX.HasValue) s.P.Tx = Clamp(input.DragTargetX.Value, -_p.LaneXMax, _p.LaneXMax);
            // 실시간 타이머(정본 setTimeout)는 프레임 처리 전에 만기를 본다: 시작한 프레임 다음 프레임부터 dt를 뺀다.
            StepRealTimers(dt);

            bool hs = s.HitStop > 0 && s.Phase == RunPhase.Run;
            if (s.HitStop > 0) s.HitStop -= dt;
            double gdt = hs ? dt * _p.HitStopScale : dt;
            int ts = Math.Max(1, s.Setup.TimeScale);
            double sdt = gdt * ts;

            if (s.Phase == RunPhase.Run) { for (int k = 0; k < ts; k++) if (s.Phase == RunPhase.Run) UpdRun(gdt); }
            else if (s.Phase == RunPhase.Climb) UpdClimb(sdt);
            if (s.Roulette != null) UpdRoulette(sdt);
            if (input.KeyAxis != 0) s.P.Tx = Clamp(s.P.Tx + input.KeyAxis * dt * _p.KeyMoveSpeed, -_p.LaneXMax, _p.LaneXMax);
            StepFx(sdt);
            SyncWells(dt);
        }

        // ================= updRun (§4.13.3) =================

        void UpdRun(double dt)
        {
            var s = S; var P = s.P;
            // 1
            s.Time += dt;
            P.X += (P.Tx - P.X) * Math.Min(1, dt * _p.MoveFollowK);
            P.X = Clamp(P.X, -_p.LaneXMax, _p.LaneXMax);
            // 2 아치 배너 → Area 돌파
            foreach (var g in s.Gates)
            {
                if (g.Passed || !(P.Z > g.Z)) continue;
                g.Passed = true; s.Floor = g.Floor;
                int prev = g.Floor - 2;
                bool ok = prev < 0 || s.Objs.TrueForAll(o => !o.IsField || o.Bar != prev || o.Rew != FieldReward.Soul || o.Dead);
                s.Emit(new RunEvent(RunEventType.FloorEnter, a: g.Floor, flag: ok));
            }
            if (P.Z > s.Gz) { Finish(FinishReason.Gate); return; }
            // 3 계단(OnArrive StartClimb)
            double nz = P.Z + (s.Setup.ForwardSpeed ?? RunSpeedAt(s)) * dt;
            foreach (var o in s.Objs)
            {
                if (o.Receive == null || o.Receive.type != ReceiveType.OnArrive || o.Receive.arriveAction != ArriveAction.StartClimb) continue;
                if (o.Used || !(nz >= o.Z)) continue;
                o.Used = true;
                if (StartClimb(o)) { P.Z = o.Z; return; }
            }
            // 4 벽 막힘
            RunObject blocked = null;
            foreach (var o in s.Objs)
            {
                if (!Blocks(o) || Math.Abs(P.X - o.X) >= o.Hw + _p.PlayerHalfW) continue;
                double face = o.Z - o.Hd - _p.WallFaceGap;
                if (P.Z <= face + _p.WallFaceEps && nz > face) { nz = face; blocked = o; }
            }
            double oz = P.Z;
            P.Z = Math.Max(P.Z, nz);
            P.Phase += dt * (blocked != null ? _p.PhaseBlocked : _p.PhaseRun);
            // 5 게이트 통과
            foreach (var o in s.Objs)
            {
                if (o.Receive == null || o.Receive.type != ReceiveType.OnPass || o.Done || !(oz < o.Z && P.Z >= o.Z)) continue;
                PassGate(o);
            }
            // 6 무기 지급
            for (int i = 0; i < s.PendingW.Count; i++)
            {
                var w = s.PendingW[i];
                w.T -= dt;
                if (w.T <= 0 && !w.Done)
                {
                    w.Done = true;
                    DeliverWeapons(w.N * s.Setup.CraftPerMade);
                }
            }
            s.PendingW.RemoveAll(w => w.Done);
            // 7 이동 AI
            foreach (var o in s.Objs)
                if (!o.Dead && o.Move != null && o.Move.type == MoveAiType.Chase) StepChase(o, dt);
            // 8 가라앉기(그림)
            foreach (var o in s.Objs) if (o.Sink > 0 && o.Sink < 1) o.Sink = Math.Min(1, o.Sink + dt);
            // 9 막힘 종료(MV-SPEC-02 6-8, Q-13): 전진만 멈추고 계속 쏜다. 연속 STUCK_END_SEC초 막히면 종료(0이면 닿는 프레임에 종료).
            if (blocked != null)
            {
                if (s.StuckT <= 0)
                {
                    s.Emit(new RunEvent(RunEventType.Blocked, objId: blocked.Id, x: P.X, z: P.Z));
                    var (a, t) = RunParams.Shake(T, "shake.blocked");
                    Shake(a, t);
                }
                s.HitWall = blocked;
                s.StuckT += dt;
                if (s.Setup.FailOnWall && s.StuckT >= _p.StuckEndSec) { Finish(FinishReason.Wall); return; }
            }
            else s.StuckT = 0;
            // 10 사격
            int vol0 = s.Vol;
            Shoot(dt);
            // 11·12 투사체
            MoveProjectiles(dt);
            // 동행 자동 사격(§4.20.5, 12와 13 사이). 동행 없으면 돌지 않는다.
            if (s.Companions.Count > 0) StepCompanions(dt, vol0);
            // 13 빛·유성(휴면 카드, D-20)
            StepBeamMeteor(dt);
            // 14 쿨다운·화상
            s.StormCd -= dt; s.BlastCd -= dt;
            StepBurn(dt);
            // 15 위성·칼날
            StepOrbSpin(dt);
            // 16 확장 지점: 모드 진행 → 종료 판정(§4.20.2). 정본 종료(2·9단계)가 먼저다.
            Rule.Tick(s, dt);
            if (s.Fin || s.Phase != RunPhase.Run) return;
            var end = Rule.CheckEnd(s);
            if (end == RunEnd.Clear) Finish(FinishReason.ModeClear);
            else if (end == RunEnd.Fail) Finish(FinishReason.ModeFail);
        }

        // ================= 판정 (§4.3.1) =================

        bool Hittable(RunObject o)
        {
            if (o.Dead) return false;
            switch (o.Kind.hittable)
            {
                case MV.Data.Hittable.Never: return false;
                case MV.Data.Hittable.BeforePass: return !o.Done && !(o.Z < S.P.Z);
                default: return true;
            }
        }

        static bool Breakable(RunObject o) => !o.Dead && o.Kind.breakable;
        static bool Blocks(RunObject o) => !o.Dead && o.Kind.blocks;

        /// <summary>같은 레인 폭 안 앞 WARN_DIST 안의 가장 가까운 막는 오브젝트(UI 경고용, §4.7.2).</summary>
        public RunObject NearestBlockerAhead()
        {
            RunObject best = null; double bd = double.MaxValue;
            foreach (var o in S.Objs)
            {
                if (!Blocks(o) || Math.Abs(S.P.X - o.X) >= o.Hw + _p.PlayerHalfW) continue;
                double d = o.Z - o.Hd - S.P.Z;
                if (d >= 0 && d < _p.WarnDist && d < bd) { bd = d; best = o; }
            }
            return best;
        }

        // ================= 게이트 통과 (§4.4.2) =================

        void PassGate(RunObject o)
        {
            var s = S;
            o.Done = true;
            bool inGate = Math.Abs(s.P.X - o.X) < o.Hw + o.Receive.margin;
            bool stair = o.Reaction != null && o.Reaction.type == HitReactionType.ChargeStairGate;
            int amount = stair ? o.N : o.Made + o.Extra;
            if (inGate)
            {
                s.Emit(new RunEvent(RunEventType.GateTake, objId: o.Id, kindId: o.Kind.id, a: amount));
                if (amount > 0)
                {
                    double delay = o.Receive.delayMs / 1000.0;
                    if (o.Receive.clock == ClockKind.Game && !stair) s.PendingW.Add(new PendingWeapon { T = delay, N = amount });
                    else s.PendingReceives.Add(new PendingReceive
                    {
                        Cond = o.Receive, PreLeft = o.Receive.preDelayMs / 1000.0, Left = delay,
                        Type = stair ? ConsumeType.RunStairs : ConsumeType.RunWeapon, Amount = amount, ObjId = o.Id,
                    });
                }
                o.Sink = 0.001;
            }
            else if (amount > 0) s.Emit(new RunEvent(RunEventType.GateMiss, objId: o.Id, kindId: o.Kind.id, a: amount));
        }

        /// <summary>
        /// 지팡이 1장을 스택에 넣고 같은 단계 MERGE_N장을 윗단계로 머지한다. 최고 단계 = W 길이 − 1(103_item, MV-SPEC-02 Q-08).
        /// 스택 칸 수 WEAPON_STACK_MAX(Q-17, 0 = 상한 없음)가 차 있으면 거절하고 false.
        /// </summary>
        bool AddWeapon(int t)
        {
            var W = S.W;
            t = Math.Max(1, Math.Min(W.Length - 1, t));
            if (_p.WeaponStackMax > 0)
            {
                int held = 0;
                for (int i = 1; i < W.Length; i++) held += W[i];
                if (held >= _p.WeaponStackMax) { S.WeaponRejected++; return false; }
            }
            W[t]++;
            while (t < W.Length - 1 && W[t] >= _p.MergeN) { W[t] -= _p.MergeN; t++; W[t]++; }
            return true;
        }

        /// <summary>공방 지급 n장(WeaponDeliver A = 실제로 들어간 장 수, 거절이 있으면 WeaponRejected).</summary>
        void DeliverWeapons(int n)
        {
            var s = S;
            int got = 0;
            for (int k = 0; k < n; k++) if (AddWeapon(s.Bt)) got++;
            s.Emit(new RunEvent(RunEventType.WeaponDeliver, a: got, b: s.Bt));
            if (got < n) s.Emit(new RunEvent(RunEventType.WeaponRejected, a: n - got, b: s.Bt));
        }

        /// <summary>전진 속도: 템플릿 중간 구역(310 midFromZ~midToZ, 템플릿 기준 z)이면 RUN_SPEED_MID(MV-SPEC-02 4-1, Q-15).</summary>
        double RunSpeedAt(RunState s)
        {
            var tpl = T.Template.Get(s.Tpl);
            if (tpl == null || !tpl.midFromZ.HasValue || !tpl.midToZ.HasValue) return _p.RunSpeed;
            double tz = s.P.Z - s.Base - s.ShiftSum;
            return tz >= tpl.midFromZ.Value && tz < tpl.midToZ.Value ? _p.RunSpeedMid : _p.RunSpeed;
        }

        // ================= 이동 AI Chase (§4.6.1) =================

        void StepChase(RunObject o, double dt)
        {
            var s = S; var P = s.P; var m = o.Move;
            if (o.Z - P.Z < m.aggro) { o.Z -= o.Spd * dt; o.X += Clamp(P.X - o.X, -1, 1) * m.xFollow * dt; }
            if (Math.Abs(o.X - P.X) < m.touchX && Math.Abs(o.Z - P.Z) < m.touchZ)
            {
                o.Dead = true;
                if (m.touchAction == TouchAction.Steal)
                {
                    double st = Math.Min(Math.Floor(s.Gold), m.stealBase + m.stealPerZone * s.Setup.Zone);
                    s.Gold -= st;
                    s.Emit(new RunEvent(RunEventType.Steal, objId: o.Id, a: (int)st, x: P.X, z: P.Z));
                    var (a, t) = RunParams.Shake(T, m.stealShake);
                    Shake(a, t);
                }
            }
            if (o.Z < P.Z - m.despawnBehind) o.Dead = true;
        }

        // ================= 사격 (§4.2) =================

        void Shoot(double dt)
        {
            var s = S; var P = s.P;
            double iv = FireInterval(s);
            int tier = s.EqTier();
            double crC = CritChance(s), dbC = DoubleChance(s);
            int pierce = (int)CardSum(s, CardEffectType.Pierce);
            double pierceC = PierceChance(s);
            var (spearEff, spearN) = CardOf(s, CardEffectType.PierceEveryN);
            string el = MainElem();
            P.ThrowT = Math.Max(0, P.ThrowT - dt);
            int units = 1 + s.Clones;
            for (int i = 0; i < units; i++)
            {
                double ux = P.X, uz = P.Z;
                if (i > 0) { var off = _p.CloneOff[i - 1]; ux = Clamp(P.X + off.dx, -_p.CloneXMax, _p.CloneXMax); uz = P.Z + off.dz; }
                s.FireT[i] -= dt;
                if (s.FireT[i] > 0) continue;
                s.FireT[i] += iv;
                bool crit = s.Draw(RngUse.Crit) < crC;
                double d = Math.Floor(BaseDmg(s) * (crit ? CritMul(s) : 1)); // 치명 배율 뒤 정수(버림, MV-SPEC2-FIX1)
                // Area 강화 관통 확률(MV-SPEC-02 Q-28): 이 발사의 탄이 1개 더 뚫는다. 확률 0이면 뽑지 않는다.
                int up = pierceC > 0 && s.Draw(RngUse.PierceChance) < pierceC ? 1 : 0;
                if (i == 0) s.Vol++;
                if (i == 0 && spearN > 0 && s.Vol % (int)spearEff.p1 == 0)
                {
                    AddProj(i, ux, uz, d * (1 + spearEff.p2 * spearN), crit, tier, (int)spearEff.p3 + up, true, 0, el);
                    P.ThrowT = _p.ThrowT;
                    continue;
                }
                if (s.Draw(RngUse.DoubleShot) < dbC)
                {
                    AddProj(i, ux - _p.DblShotDx, uz, d, crit, tier, pierce + up, false, -1, el);
                    AddProj(i, ux + _p.DblShotDx, uz, d, crit, tier, pierce + up, false, 1, el);
                }
                else AddProj(i, ux, uz, d, crit, tier, pierce + up, false, 0, el);
                if (i == 0) P.ThrowT = _p.ThrowT;
            }
        }

        void AddProj(int unit, double x, double z, double d, bool crit, int tier, int pierce, bool spear, int pair, string el)
        {
            var b = new RunProjectile { Id = S.NextProjId++, X = x, Z = z + _p.ProjSpawnDz, Sz = z + _p.ProjSpawnDz, D = d, Crit = crit, Tier = tier, Pierce = pierce, Spear = spear };
            S.Projs.Add(b);
            S.Emit(new RunEvent(RunEventType.Fire, objId: b.Id, a: unit, b: tier, c: pair, x: b.X, z: b.Z, flag: spear, key: el));
        }

        /// <summary>신 고유 카드(110 group ≠ 0)가 1장 이상이면 R.el.</summary>
        string MainElem()
        {
            foreach (var id in S.CardOrder) { var c = T.Card.Get(id); if (c != null && c.group != 0) return S.El; }
            return null;
        }

        void MoveProjectiles(double dt)
        {
            var s = S;
            double range = ProjRange(s);
            for (int bi = 0; bi < s.Projs.Count; bi++)
            {
                var b = s.Projs[bi];
                if (b.Dead) continue;
                double nzb = b.Z + _p.ProjSpeed * dt;
                RunObject best = null; double bz = 1e9;
                foreach (var o in s.Objs)
                {
                    if (!Hittable(o) || Math.Abs(b.X - o.X) > o.Hw) continue;
                    if (b.HitIds != null && b.HitIds.Contains(o.Id)) continue;
                    double f = o.Z - o.Hd;
                    if (f <= nzb && o.Z + o.Hd >= b.Z && f < bz) { bz = f; best = o; }
                }
                if (best != null)
                {
                    s.Emit(new RunEvent(RunEventType.ProjImpact, objId: b.Id, x: b.X, z: best.Z - best.Hd, flag: b.Crit));
                    Hit(best, b.D, b.Crit, true, false, b.Crit ? HitColor.Crit : HitColor.Normal, b.Spear ? HitSource.Spear : HitSource.Projectile);
                    if (s.Companions.Count > 0) CompanionOnProjectileHit();
                    (b.HitIds ??= new List<int>()).Add(best.Id);
                    if (--b.Pierce < 0) { b.Dead = true; continue; }
                }
                b.Z = nzb;
                if (b.Z - b.Sz > range) b.Dead = true;
            }
            for (int i = s.Projs.Count - 1; i >= 0; i--)
                if (s.Projs[i].Dead) { s.Emit(new RunEvent(RunEventType.ProjRemoved, objId: s.Projs[i].Id)); s.Projs.RemoveAt(i); }
        }

        // ================= hit (§4.3.2~§4.3.3, §4.9.2) =================

        void Hit(RunObject o, double dmg, bool crit, bool proc, bool quiet, HitColor color, HitSource source)
        {
            var s = S;
            bool wasDead = o.Dead;
            if (o.Chill > 0) { var (fe, fn) = CardOf(s, CardEffectType.ProcChill); if (fe != null) dmg *= 1 + fe.p3 * fn; }
            dmg = Math.Min(_p.DmgMax, Math.Max(_p.DmgMin, Math.Floor(dmg))); // 최종 피해 정수(버림) + 최소 DMG_MIN(MV-SPEC2-FIX1)
            double gm = GoldMul(s), hx = o.X, hz = o.Z - o.Hd;
            s.Emit(new RunEvent(RunEventType.Hit, objId: o.Id, kindId: o.Kind.id, x: hx, z: hz, amount: dmg, flag: crit, quiet: quiet, color: color));
            var type = o.Reaction?.type ?? HitReactionType.None;
            switch (type)
            {
                case HitReactionType.ChargeWell:
                    o.Pend += dmg;
                    break;
                case HitReactionType.Damage:
                    if (o.HasHp) o.Hp -= dmg;
                    if (o.HasHp && o.Hp <= 0) Break(o, gm);
                    else if (o.HasHp) PayShares(o, SharesDue(o.Max - o.Hp, o.Max, o.Shares), gm);
                    break;
                case HitReactionType.ChargeStairGate:
                {
                    // MV-SPEC-02 6-2(Q-06): M = Area × 계단 Lv 표, M마다 +Lv(냥환생 「계단 2배」 확률로 +2×Lv), 넘친 양 이월.
                    var g = o.Charge;
                    double M = StairGateM(g, _sgateM, s.GaugeLv, s.Bal, s.ZoneHp);
                    o.F += dmg / (1 + o.N * (s.Bal.diff * g.diffMul));
                    double dblC = StairDoubleChance(s);
                    int got = 0;
                    while (o.F >= M)
                    {
                        o.F -= M;
                        int add = s.GaugeLv;
                        if (dblC > 0 && s.Draw(RngUse.StairDouble) < dblC) add *= 2;
                        o.N += add; got += add;
                    }
                    s.Emit(new RunEvent(RunEventType.GateFill, objId: o.Id, kindId: o.Kind.id, a: got, amount: Clamp(o.F / M, 0, 1), quiet: quiet || got == 0));
                    break;
                }
                case HitReactionType.ChargeCraftGate:
                {
                    var g = o.Charge;
                    o.F += dmg;
                    double dblC = CraftDoubleChance(s);
                    while (o.Made < g.cap && o.F >= Threshold(g, o.Made, s.Bal, s.ZoneHp))
                    {
                        o.F -= Threshold(g, o.Made, s.Bal, s.ZoneHp);
                        o.Made++;
                        // 냥환생 「지팡이 제작 2배」(Q-27): 이번 생산이 2장. 문턱 단계(Made)는 1만 오른다.
                        int made = 1;
                        if (dblC > 0 && s.Draw(RngUse.CraftDouble) < dblC) { o.Extra++; made = 2; }
                        s.Emit(new RunEvent(RunEventType.GateFill, objId: o.Id, kindId: o.Kind.id, a: made, amount: 0));
                    }
                    if (o.Made >= g.cap) o.F = 0;
                    break;
                }
            }
            bool killed = !wasDead && o.Dead;
            if (proc) GodOnHit(o, dmg, hx, hz);
            // ③ 피격 훅: 가호까지 끝난 뒤(중첩 hit은 안쪽이 먼저 불린다).
            Rule.OnHit(s, new HitInfo(o.Id, o.Kind.id, (int)dmg, crit, killed, source));
        }

        void Break(RunObject o, double gm)
        {
            var s = S;
            o.Dead = true;
            var r = o.Reaction;
            switch (r.breakStat) { case BreakStat.Walls: s.Walls++; break; }
            s.Emit(new RunEvent(RunEventType.Break, objId: o.Id, kindId: o.Kind.id, x: o.X, z: o.Z, a: o.IsField ? (int)o.Rew : -1));
            PayShares(o, Math.Max(1, o.Shares), gm);
            if (!string.IsNullOrEmpty(r.breakShake)) { var (a, t) = RunParams.Shake(T, r.breakShake); Shake(a, t); }
            if (!string.IsNullOrEmpty(r.breakHitstop)) Hitstop(T.RunConfig.D(r.breakHitstop));
        }

        /// <summary>
        /// 보상 등분 지급(2026-11-11 사용자 결정, MV-RUN-SPLITREWARD). 이미 준 몫 다음부터 upTo번째 몫까지 준다(한 번에 문턱 여럿을 넘으면 모두).
        /// 몫마다 Grant를 불러 획득 특성(goldMul·DiaCap)이 몫마다 붙는다. dropId 추첨은 마지막 몫(= 파괴) 때 1번.
        /// </summary>
        void PayShares(RunObject o, int upTo, double gm)
        {
            var s = S;
            int n = Math.Max(1, o.Shares);
            upTo = Math.Min(n, upTo);
            while (o.SharesPaid < upTo)
            {
                o.SharesPaid++;
                if (o.RewardType != ConsumeType.None)
                {
                    double v = RewardShare(o.RewardAmount, n, o.SharesPaid);
                    if (v > 0 || o.SharesPaid == n) Grant(o.RewardType, o.RewardId, v, gm, o); // 0인 중간 몫은 건너뜀, 마지막 몫은 지금까지처럼 항상
                }
                if (o.SharesPaid == n && o.DropGroup != 0)
                {
                    _dropBuf.Clear();
                    (T.Drops ?? new MvDropRoller(T)).RollDrop(o.DropGroup, () => s.Draw(RngUse.EnemyDrop), _dropBuf);
                    foreach (var d in _dropBuf) Grant(d.Type, d.Id, DropValue(d), gm, o);
                }
            }
        }

        double DropValue(MvDropResult d)
        {
            switch (d.Rule)
            {
                case RewardValueRule.DiaCap: return DiaCap(T, S.Setup.PerkDcap);
                case RewardValueRule.HalfDiaCapCeil: return Math.Ceiling(DiaCap(T, S.Setup.PerkDcap) / 2);
                default: return d.Value;
            }
        }

        /// <summary>런 안 보상 지급(§4.3.3). 602만 × goldMul. 런 재화가 아니면 정산 대기 목록(07 §7.9).</summary>
        void Grant(ConsumeType type, int id, double value, double gm, RunObject at)
        {
            var s = S;
            double shown = value;
            switch (type)
            {
                case ConsumeType.RunGold: shown = value * gm; s.Gold += shown; break;
                case ConsumeType.RunSouls: s.Souls += (int)value; break;
                case ConsumeType.RunDia: s.Dia += (int)value; break;
                case ConsumeType.None: return;
                default: s.PendingSettle.Add(new ConsumeData(type, id, value)); break;
            }
            s.Emit(new RunEvent(RunEventType.Gain, objId: at?.Id ?? 0, a: (int)type, amount: shown, x: at?.X ?? 0, z: at?.Z ?? 0));
        }

        void Hitstop(double t) { S.HitStop = Math.Max(S.HitStop, t); S.Emit(new RunEvent(RunEventType.Hitstop, amount: t)); }
        void Shake(double a, double t) { if (a > 0 || t > 0) S.Emit(new RunEvent(RunEventType.Shake, amount: a, amount2: t)); }

        HitColor GodColor => S.El == "fire" ? HitColor.GodFire : HitColor.GodBolt;

        // ================= 신 가호 (§4.9.2) =================

        void GodOnHit(RunObject o, double dmg, double hx, double hz)
        {
            var s = S;
            foreach (var id in s.CardOrder.ToArray())
            {
                var c = T.Card.Get(id); var e = c != null ? T.CardEffect.Get(c.effectId) : null;
                if (e == null) continue;
                int n = s.CardN(id);
                if (e.type == CardEffectType.ProcExtraHit || e.type == CardEffectType.ProcBurn)
                {
                    if (!(s.Draw(RngUse.Proc) < e.p1)) continue;
                    s.Emit(new RunEvent(RunEventType.GodProc, objId: o.Id, key: s.El));
                    if (e.type == CardEffectType.ProcExtraHit) { if (!o.Dead) Hit(o, dmg * e.p2 * n, false, false, true, GodColor, HitSource.GodProc); }
                    else { o.Burning = true; o.BurnT = e.p3; o.BurnDps = dmg * e.p2 * n; o.BurnTick = 0; }
                }
            }
            var (cj, cjn) = CardOf(s, CardEffectType.ChainJump);
            if (cjn > 0)
            {
                var used = new HashSet<int> { o.Id }; var ids = new List<int>();
                int jumps = (int)cj.p1 + cjn;
                for (int i = 0; i < jumps; i++)
                {
                    RunObject best = null; double bd = 1e9;
                    foreach (var t in s.Objs)
                    {
                        if (used.Contains(t.Id) || !Hittable(t)) continue;
                        double d = Math.Sqrt((t.X - hx) * (t.X - hx) + (t.Z - hz) * (t.Z - hz));
                        if (d < cj.p3 && d < bd) { bd = d; best = t; }
                    }
                    if (best == null) break;
                    used.Add(best.Id); ids.Add(best.Id);
                    s.Emit(new RunEvent(RunEventType.GodChain, objId: best.Id, key: s.El, x: hx, z: hz));
                    Hit(best, dmg * cj.p2, false, false, true, GodColor, HitSource.Chain);
                }
            }
            var (cs, csn) = CardOf(s, CardEffectType.ChainSplash);
            if (csn > 0)
            {
                s.Emit(new RunEvent(RunEventType.GodChain, key: s.El, x: hx, z: hz, amount: cs.p2 + cs.p3 * csn));
                foreach (var t in s.Objs)
                    if (t != o && Hittable(t) && Math.Abs(t.X - hx) < _p.ChainFireBox + t.Hw && Math.Abs(t.Z - hz) < _p.ChainFireBox + t.Hd)
                        Hit(t, dmg * cs.p1 * csn, false, false, true, HitColor.Normal, HitSource.Chain);
            }
            var (st, stn) = CardOf(s, CardEffectType.Storm);
            if (stn > 0 && s.StormCd <= 0 && s.Draw(RngUse.Storm) < st.p1)
            {
                s.StormCd = st.p2;
                s.Storms.Add(new StormJob { Cx = hx, Cz = hz, N = stn });
                s.Emit(new RunEvent(RunEventType.GodStormStart, x: hx, z: hz, key: s.El, amount: _p.StormLife));
            }
            var (bl, bln) = CardOf(s, CardEffectType.Blast);
            if (bln > 0 && s.BlastCd <= 0 && s.Draw(RngUse.Blast) < bl.p1 + bl.p2 * (bln - 1))
            {
                s.BlastCd = bl.p3;
                GodBlast(hx, hz, bl, bln);
            }
        }

        void GodBlast(double x, double z, CardEffectData e, int n)
        {
            var s = S;
            s.Emit(new RunEvent(RunEventType.GodBlast, x: x, z: z, key: s.El));
            var (a, t) = RunParams.Shake(T, "shake.godBlast");
            Shake(a, t);
            foreach (var u in s.Objs)
                if (Breakable(u) && Math.Abs(u.X - x) < _p.BlastBox + u.Hw && Math.Abs(u.Z - z) < _p.BlastBox + u.Hd)
                    Hit(u, BaseDmg(s) * e.p4 * n, false, false, false, HitColor.Blast, HitSource.Blast);
        }

        // ================= 화상·위성·칼날·빛·유성 (§4.9) =================

        void StepBurn(double dt)
        {
            foreach (var o in S.Objs)
            {
                if (!o.Burning || o.Dead) continue;
                o.BurnT -= dt; o.BurnTick += dt;
                if (o.BurnTick >= _p.BurnTick)
                {
                    o.BurnTick -= _p.BurnTick;
                    S.Emit(new RunEvent(RunEventType.BurnTick, objId: o.Id));
                    Hit(o, o.BurnDps * _p.BurnTick, false, false, true, HitColor.Burn, HitSource.Burn);
                }
                if (o.BurnT <= 0) o.Burning = false;
            }
        }

        void StepOrbSpin(double dt)
        {
            var s = S; var P = s.P;
            // 정본 순서: orb → spin, R.spinT 공유(§4.9.5)
            foreach (var type in new[] { CardEffectType.Orbit, CardEffectType.Spin })
            {
                var (e, n) = CardOf(s, type);
                if (n <= 0) continue;
                s.SpinT += dt;
                if (s.SpinT < e.p1) continue;
                s.SpinT = 0;
                var ids = new List<int>();
                foreach (var o in s.Objs)
                    if (Breakable(o) && Math.Abs(o.X - P.X) < o.Hw + e.p3 && o.Z - o.Hd < P.Z + e.p4 && o.Z + o.Hd > P.Z - _p.OrbZBack)
                    {
                        ids.Add(o.Id);
                        Hit(o, BaseDmg(s) * e.p2 * n, false, false, true, HitColor.Normal, type == CardEffectType.Orbit ? HitSource.Orb : HitSource.Spin);
                    }
                s.Emit(new RunEvent(RunEventType.OrbTick, ids: ids.ToArray()));
            }
        }

        void StepBeamMeteor(double dt)
        {
            var s = S; var P = s.P;
            var (be, bn) = CardOf(s, CardEffectType.Beam);
            if (bn > 0)
            {
                s.BeamT += dt;
                if (s.BeamT >= be.p1)
                {
                    s.BeamT = 0;
                    s.Emit(new RunEvent(RunEventType.Beam, x: P.X, z: P.Z));
                    foreach (var o in s.Objs)
                        if (Breakable(o) && o.Z > P.Z && o.Z < P.Z + be.p3 && Math.Abs(o.X - P.X) < o.Hw + be.p4)
                            Hit(o, BaseDmg(s) * be.p2 * bn, false, false, false, HitColor.Beam, HitSource.Beam);
                }
            }
            var (me, mn) = CardOf(s, CardEffectType.Meteor);
            if (mn > 0)
            {
                s.MeteorT += dt;
                if (s.MeteorT >= Math.Max(me.p3, me.p1 - me.p2 * (mn - 1)))
                {
                    RunObject tg = null; double bd = 1e9;
                    foreach (var o in s.Objs)
                        if (Breakable(o) && o.Z > P.Z + _p.MeteorMinDz && o.Z < P.Z + _p.MeteorMaxDz) { double d = o.Z - P.Z; if (d < bd) { bd = d; tg = o; } }
                    if (tg != null)
                    {
                        s.MeteorT = 0;
                        // [추정] 착탄점 = 대상 앞면(x, z−hd), 피해는 착탄 시점 baseDmg.
                        s.Meteors.Add(new MeteorJob { X = tg.X, Z = tg.Z - tg.Hd, N = mn });
                        s.Emit(new RunEvent(RunEventType.MeteorLaunch, objId: tg.Id, x: tg.X, z: tg.Z - tg.Hd));
                    }
                }
            }
        }

        // ================= stepFx(sdt): 폭풍·유성·수령 앞 지연 =================

        void StepFx(double dt)
        {
            var s = S;
            for (int i = 0; i < s.Storms.Count; i++)
            {
                var j = s.Storms[i];
                j.K += dt;
                var (st, _) = CardOf(s, CardEffectType.Storm);
                int strikes = st != null ? (int)st.p4 : 4;
                int due = Math.Min(strikes, (int)Math.Floor((j.K - _p.StormK0) / _p.StormDk) + 1);
                while (j.Done < due)
                {
                    j.Done++;
                    var cands = s.Objs.FindAll(t => Breakable(t) && Math.Abs(t.Z - j.Cz) < _p.StormPickDz && t.Z > s.P.Z);
                    RunObject t0 = cands.Count > 0 ? cands[(int)Math.Floor(s.Draw(RngUse.StormPick) * cands.Count)] : null;
                    // 흔들림 ±STORM_JIT_TARGET(대상)·±STORM_JIT_FREE(빈 곳), 300_runConfig(정본 ±0.3·±2)
                    double x = t0 != null ? t0.X + (-_p.StormJitTarget + s.Draw(RngUse.StormJitter) * (2 * _p.StormJitTarget)) : j.Cx + (-_p.StormJitFree + s.Draw(RngUse.StormJitter) * (2 * _p.StormJitFree));
                    double z = t0 != null ? t0.Z - t0.Hd : j.Cz + (-_p.StormJitFree + s.Draw(RngUse.StormJitter) * (2 * _p.StormJitFree));
                    s.Emit(new RunEvent(RunEventType.GodStormStrike, x: x, z: z, key: s.El));
                    double mul = st != null ? st.p3 : 1.5;
                    foreach (var u in s.Objs)
                        if (Breakable(u) && Math.Abs(u.X - x) < _p.StormBox + u.Hw && Math.Abs(u.Z - z) < _p.StormBox + u.Hd)
                            Hit(u, BaseDmg(s) * mul * j.N, false, false, true, GodColor, HitSource.Storm);
                }
                if (j.K >= _p.StormLife) j.Finished = true;
            }
            s.Storms.RemoveAll(j => j.Finished);
            for (int i = 0; i < s.Meteors.Count; i++)
            {
                var m = s.Meteors[i];
                m.T += dt;
                if (m.T < _p.MeteorFall) continue;
                m.Finished = true;
                var (me, _) = CardOf(s, CardEffectType.Meteor);
                s.Emit(new RunEvent(RunEventType.MeteorImpact, x: m.X, z: m.Z));
                foreach (var u in s.Objs)
                    if (Breakable(u) && Math.Abs(u.X - m.X) < _p.MeteorBox + u.Hw && Math.Abs(u.Z - m.Z) < _p.MeteorBox + u.Hd)
                        Hit(u, BaseDmg(s) * (me != null ? me.p4 : 5) * m.N, false, false, false, HitColor.Meteor, HitSource.Meteor);
            }
            s.Meteors.RemoveAll(m => m.Finished);
            foreach (var r in s.PendingReceives)
            {
                if (r.Done || r.PreLeft <= 0) continue;
                r.PreLeft -= dt;
                if (r.PreLeft <= 0 && s.Fin) r.Done = true; // 비행 시작 때 R.fin이면 중단(§4.5.1 ⑤)
            }
            foreach (var r in s.PendingReceives)
                if (!r.Done && r.PreLeft <= 0 && r.Cond.clock == ClockKind.Game) { r.Left -= dt; if (r.Left <= 0) Deliver(r); }
            s.PendingReceives.RemoveAll(r => r.Done);
        }

        // ================= syncScene(dt): 우물 묶음 (§4.5.1 ②③) =================

        void SyncWells(double dt)
        {
            var s = S;
            foreach (var o in s.Objs)
            {
                if (o.Dead || o.Receive == null || o.Receive.type != ReceiveType.OnFlush) continue;
                o.Ft += dt;
                if (!(o.Pend > 0) || !(o.Ft > _p.BrickFlushInterval || o.Z < s.P.Z - _p.WellFlushBehind)) continue;
                o.Ft = 0;
                double amt = o.Pend; o.Pend = 0;
                int lv = s.GaugeLv;
                int bricks = (int)Math.Min(_p.WellBrickMax, 1 + Math.Floor(amt / (Threshold(o.Charge, lv - 1, s.Bal, s.ZoneHp) * _p.WellBrickDiv)));
                s.Emit(new RunEvent(RunEventType.WellFlush, objId: o.Id, amount: amt, a: bricks));
                s.PendingReceives.Add(new PendingReceive
                {
                    Cond = o.Receive, PreLeft = o.Receive.preDelayMs / 1000.0, Left = o.Receive.delayMs / 1000.0,
                    Type = ConsumeType.GaugeFill, Amount = amt, ObjId = o.Id, Charge = o.Charge,
                });
            }
        }

        // ================= 실시간 타이머: 수령 비행·카드 비행 =================

        void StepRealTimers(double dt)
        {
            var s = S;
            foreach (var r in s.PendingReceives)
                if (!r.Done && r.PreLeft <= 0 && r.Cond.clock == ClockKind.Real) { r.Left -= dt; if (r.Left <= 0) Deliver(r); }
            s.PendingReceives.RemoveAll(r => r.Done);
            var rl = s.Roulette;
            if (rl == null) return;
            for (int i = 0; i < rl.Cards.Count; i++)
            {
                var c = rl.Cards[i];
                if (!c.Flying || c.Applied) continue;
                c.FlyLeft -= dt;
                if (c.FlyLeft <= 0) ApplyCard(c);
            }
        }

        void Deliver(PendingReceive r)
        {
            var s = S;
            r.Done = true;
            switch (r.Type)
            {
                case ConsumeType.RunStairs:
                    s.Stairs += (int)r.Amount;
                    s.Emit(new RunEvent(RunEventType.StairsReceived, a: (int)r.Amount, b: s.Stairs));
                    break;
                case ConsumeType.RunWeapon:
                    DeliverWeapons((int)r.Amount * s.Setup.CraftPerMade);
                    break;
                case ConsumeType.GaugeFill:
                    GaugeAdd(r.Amount, r.Charge ?? GaugeRow(T));
                    s.Emit(new RunEvent(RunEventType.GaugeReceive, amount: r.Amount, a: s.GaugeLv, amount2: s.GaugeFill));
                    break;
            }
        }

        /// <summary>gaugeAdd(§4.5.2).</summary>
        void GaugeAdd(double d, ChargeGateData g)
        {
            var s = S;
            if (s.GaugeLv >= g.cap) { s.GaugeFill = 0; return; }
            s.GaugeFill += d;
            while (s.GaugeLv < g.cap && s.GaugeFill >= Threshold(g, s.GaugeLv - 1, s.Bal, s.ZoneHp))
            {
                s.GaugeFill -= Threshold(g, s.GaugeLv - 1, s.Bal, s.ZoneHp);
                s.GaugeLv++;
                s.Emit(new RunEvent(RunEventType.GaugeLevelUp, a: s.GaugeLv));
            }
            if (s.GaugeLv >= g.cap) s.GaugeFill = 0;
        }

        // ================= 계단 (§4.8.1~§4.8.3) =================

        bool StartClimb(RunObject o)
        {
            var s = S; var c = _p.Climb;
            int n = s.Stairs;
            if (n <= 0) { s.Emit(new RunEvent(RunEventType.Toast, key: "run.toast.noStairs")); return false; }
            foreach (var b in s.Projs) s.Emit(new RunEvent(RunEventType.ProjRemoved, objId: b.Id));
            s.Projs.Clear();
            s.Stairs = 0;
            s.Phase = RunPhase.Climb;
            int k = ClimbK(c, n);
            double pw = (double)n / k, z0 = o.Z + c["z0Gap"], zEnd = z0 + k * c["stepD"], dH = k * c["stepH"];
            s.Cliff = new RunCliff(zEnd, s.WorldY, s.WorldY + dH); s.HasCliff = true; // R.cliff(shiftWorld L2227), View용
            ShiftWorld(o.Z + c["shiftFrom"], k * c["stepD"] + c["shiftExtra"]);
            s.Climb = new ClimbState { StairObjId = o.Id, N = n, K = k, Pw = pw, Z0 = z0, ZEnd = zEnd, DH = dH, T = ClimbT(c, k), StepEvery = ClimbStepEvery(c, k) };
            s.Emit(new RunEvent(RunEventType.ClimbStart, a: n, b: k, amount: pw, x: z0, z: zEnd, amount2: dH, amount3: s.Climb.T));
            return true;
        }

        /// <summary>shiftWorld(§4.8.1): cut 이하 오브젝트 제거, 나머지·아치·gz를 L만큼 앞으로.</summary>
        void ShiftWorld(double cut, double L)
        {
            var s = S;
            s.Objs.RemoveAll(o => o.Z <= cut);
            foreach (var o in s.Objs) o.Z += L;
            foreach (var g in s.Gates) g.Z += L;
            s.Gz += L;
            s.ShiftSum += L;
        }

        void UpdClimb(double dt)
        {
            var s = S; var C = s.Climb; var c = _p.Climb; var P = s.P;
            // MV-SPEC-02 4-1f·6-5l(Q-11): 오르는 중에도 좌우 조작을 멈추지 않는다(가운데로 당기지 않음, 이동 폭 안).
            P.X += (P.Tx - P.X) * Math.Min(1, dt * _p.MoveFollowK);
            P.X = Clamp(P.X, -_p.LaneXMax, _p.LaneXMax);
            P.Phase += dt * _p.PhaseClimb;
            C.Time += dt;
            double accel = c["accelP"];
            double p = Math.Min(1, C.Time / C.T), e = p < accel ? p * p / (accel * 2) : p - accel / 2;
            C.Pos = Math.Min(C.K, e / (1 - accel / 2) * C.K);
            // 칸 생성: 0초에 1칸, 그 뒤 StepEvery마다 1칸(MV-SPEC-02 6-5e). 마지막 칸은 T − riseLead에 나온다.
            int show = C.StepEvery > 0 ? (int)Math.Min(C.K, 1 + Math.Floor(C.Time / C.StepEvery + 1e-9)) : C.K;
            for (; C.StepsShown < show; C.StepsShown++) s.Emit(new RunEvent(RunEventType.ClimbStepShown, a: C.StepsShown));
            C.Shown = (int)Math.Min(C.N, Math.Floor(C.Pos * C.Pw + 0.0001));
            if (p >= 1) C.Shown = C.N;
            int lap = (int)c["lap"];
            while (C.Lap < (int)c["maxCards"] - 1 && C.Shown >= (C.Lap + 1) * lap)
            {
                C.Lap++; C.Cards.Add((int)c["lapTier"]); C.Tier = -1;
                s.Emit(new RunEvent(RunEventType.ClimbLap, a: C.Lap, b: C.Shown));
                var (a, t) = RunParams.Shake(T, "shake.climbLap"); Shake(a, t);
            }
            C.Rem = Math.Min(lap, C.Shown - C.Lap * lap);
            int tier = TierFor(T, C.Rem);
            if (tier > C.Tier)
            {
                C.Tier = tier;
                s.Emit(new RunEvent(RunEventType.ClimbTierUp, a: tier, b: C.Shown));
                var (a, t) = RunParams.Shake(T, "shake.climbTier"); Shake(a, t);
            }
            int stage = -1;
            for (int i = 0; i < _p.WarpStages.Count; i++) if (C.Shown >= _p.WarpStages[i].at) stage = i;
            if (stage != C.WarpStage)
            {
                C.WarpStage = stage;
                s.Emit(new RunEvent(RunEventType.ClimbWarpStage, a: stage));
                if (stage > 0) { var (a, t) = RunParams.Shake(T, "shake.warpStage"); Shake(a, t); }
            }
            if (p >= 1)
            {
                s.Emit(new RunEvent(RunEventType.ClimbEnd, a: C.N, b: C.Tier));
                EndClimb();
            }
        }

        void EndClimb()
        {
            var s = S; var C = s.Climb;
            s.Climbed += C.N; s.LvClimbed += C.N; s.Climbs.Add(C.N);
            s.P.Z = C.ZEnd + _p.Climb["landGap"];
            s.WorldY += C.DH;
            s.Phase = RunPhase.Run;
            s.Climb = null;
            var tiers = new List<int>(C.Cards);
            if (C.Tier >= 0) tiers.Add(C.Tier);
            StartRoulette(tiers, C.N);
        }

        // ================= 카드 룰렛 (§4.8.4~§4.8.5) =================

        bool ValidCard(CardData c)
        {
            var e = T.CardEffect.Get(c.effectId);
            return !(e != null && e.type == CardEffectType.CloneAdd && S.Clones >= _p.MaxClones);
        }

        CardData DrawCard(int tier)
        {
            var pool = new List<CardData>();
            for (int k = tier; k >= 0; k--)
            {
                pool.Clear();
                foreach (var c in S.CardPool) if (c.tier == k && ValidCard(c)) pool.Add(c);
                if (pool.Count > 0) return pool[(int)Math.Floor(S.Draw(RngUse.CardDraw) * pool.Count)];
            }
            pool.Clear();
            foreach (var c in S.CardPool) if (ValidCard(c)) pool.Add(c);
            return pool.Count > 0 ? pool[(int)Math.Floor(S.Draw(RngUse.CardDraw) * pool.Count)] : null;
        }

        void StartRoulette(List<int> tiers, int m)
        {
            var s = S; var c = _p.Climb;
            if (tiers.Count == 0) { s.Emit(new RunEvent(RunEventType.Toast, key: "run.toast.noCard", a: m)); return; }
            // D-23(고친 쪽): 앞 룰렛의 미적용 카드는 즉시 적용
            if (s.Roulette != null)
            {
                foreach (var old in s.Roulette.Cards) if (!old.Applied && old.Pick != null) ApplyCard(old);
                s.Emit(new RunEvent(RunEventType.RouletteEnd));
            }
            var rl = new RouletteState { M = m };
            rl.Tiers.AddRange(tiers);
            var picks = new int[tiers.Count];
            for (int i = 0; i < tiers.Count; i++)
            {
                var pick = DrawCard(tiers[i]);
                picks[i] = pick?.id ?? 0;
                rl.Cards.Add(new RouletteCard { Tier = tiers[i], Pick = pick, Step = i, EndAt = c["rlEndBase"] + i * c["rlEndStep"] });
            }
            s.Roulette = rl;
            s.Emit(new RunEvent(RunEventType.RouletteStart, ids: tiers.ToArray(), ids2: picks));
        }

        void UpdRoulette(double dt)
        {
            var s = S; var rl = s.Roulette; var c = _p.Climb;
            rl.T += dt;
            bool all = true;
            for (int i = 0; i < rl.Cards.Count; i++)
            {
                var k = rl.Cards[i];
                if (!k.Chosen)
                {
                    if (rl.T >= k.Next)
                    {
                        k.Flips++;
                        s.Emit(new RunEvent(RunEventType.RouletteFlip, a: i));
                        double rem = k.EndAt - rl.T;
                        k.Next = rl.T + c["rlFlipSec"] + (rem < c["rlSlowWin"] ? (c["rlSlowWin"] - rem) * c["rlSlowK"] : 0);
                    }
                    if (rl.T >= k.EndAt)
                    {
                        k.Chosen = true; k.FlyAt = rl.T + c["rlFlyDelay"];
                        s.Emit(new RunEvent(RunEventType.RouletteChosen, a: i, b: k.Pick?.id ?? 0, c: k.Tier));
                    }
                }
                else if (!k.Flying && rl.T >= k.FlyAt)
                {
                    k.Flying = true; k.FlyLeft = c["rlFlyMs"] / 1000.0;
                    s.Emit(new RunEvent(RunEventType.RouletteFly, a: i, b: k.Pick?.id ?? 0));
                }
                if (!k.Applied) all = false;
            }
            if (!all) return;
            if (rl.EndT < 0) rl.EndT = rl.T + c["rlEndPad"];
            if (rl.T >= rl.EndT) { s.Roulette = null; s.Emit(new RunEvent(RunEventType.RouletteEnd)); }
        }

        void ApplyCard(RouletteCard k)
        {
            var s = S;
            k.Applied = true;
            var card = k.Pick;
            if (card == null) return;
            if (!s.Cards.ContainsKey(card.id)) { s.Cards[card.id] = 0; s.CardOrder.Add(card.id); }
            s.Cards[card.id]++;
            var e = T.CardEffect.Get(card.effectId);
            if (e != null && e.type == CardEffectType.CloneAdd) s.Clones = Math.Min(_p.MaxClones, s.Clones + (int)e.p1);
            s.Emit(new RunEvent(RunEventType.CardApplied, a: s.Cards[card.id], b: card.id));
        }

        // ================= finish (§4.11.1, §4.7.4) =================

        /// <summary>finish(reason)(§4.11.1): 상태를 닫고 ⑤ Settle(정본 = <see cref="RunSettlement.Main"/>)로 결과를 만든다.</summary>
        void Finish(FinishReason reason)
        {
            var s = S;
            if (s.Fin) return;
            s.Fin = true; s.Phase = RunPhase.Result; s.Reason = reason;
            var r = Rule.Settle(s, reason) ?? throw new InvalidOperationException($"{Rule.GetType().Name}.Settle이 RunResult를 내지 않았습니다");
            s.Result = r;
            s.Emit(new RunEvent(RunEventType.RunFinished, a: (int)reason, flag: r.Pass || r.ZoneEnd));
        }
    }
}
