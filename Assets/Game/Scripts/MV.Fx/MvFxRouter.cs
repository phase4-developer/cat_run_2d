using System;
using System.Collections.Generic;
using System.Globalization;
using MV.Data;
using MV.Run;
using MV.View;
using UnityEngine;

namespace MV.Fx
{
    /// <summary>
    /// RunEvent → 04_effect 그룹 key → 기본 종류 재생 목록(06 §6.4.2~6.4.5, 정본 호출 지점 1:1).
    /// 자식이 1개인 그룹은 표 행 값(color·count·speed·size·life·grav·y·chance)을 쓰고, 자식이 여럿인 그룹은 표에 자식별 열이 없어
    /// 정본 호출 인자를 코드에 둔다(MV-DATA 변경 요청 후보: 자식별 파라미터 열).
    /// 흔들림·히트스톱은 MV.Run이 Shake·Hitstop 이벤트로 이미 내므로 표의 shake·hitStop 열로 다시 내지 않는다(중복 방지).
    /// </summary>
    public sealed class MvFxRouter
    {
        const string Fire = "fire", BoltEl = "bolt";

        readonly Func<MvTables> _tables;
        readonly Func<float> _rnd;
        readonly Dictionary<int, string> _projEl = new Dictionary<int, string>();
        // 계단 오르기 시작 값(ClimbEnd 때 상태가 이미 비어 있을 수 있어 이벤트에서 보관).
        int _climbK = 1;
        double _climbPw = 1, _climbZ0, _climbZEnd, _climbDH;
        string _lastEl;

        /// <param name="rnd01">표시 전용 난수 [0,1). 기본 UnityEngine.Random.value(06 §6.2.3).</param>
        public MvFxRouter(Func<MvTables> tables = null, Func<float> rnd01 = null)
        {
            _tables = tables ?? (() => MvTables.Current);
            _rnd = rnd01 ?? (() => UnityEngine.Random.value);
        }

        /// <summary>투사체 id → 원소(꼬리 trail용, Fire 이벤트에서 기억). 원소 없는 투사체는 없다.</summary>
        public IReadOnlyDictionary<int, string> ProjectileElements => _projEl;

        public void Reset()
        {
            _projEl.Clear();
            _lastEl = null;
        }

        float Rnd(float a, float b) => a + _rnd() * (b - a);

        public void Route(in RunEvent e, IMvFxWorld w, MvFxOutput o)
        {
            var t = _tables();
            if (t == null || o == null) return;
            double wy = w?.WorldY ?? 0;
            switch (e.Type)
            {
                case RunEventType.RunStarted:
                    Reset();
                    break;
                case RunEventType.Fire:
                    if (!string.IsNullOrEmpty(e.Key)) { _projEl[e.ObjId] = e.Key; _lastEl = e.Key; }
                    break;
                case RunEventType.ProjRemoved:
                    _projEl.Remove(e.ObjId);
                    break;
                case RunEventType.Shake:
                    o.Shakes.Add(new Vector2((float)e.Amount, (float)e.Amount2));
                    break;
                case RunEventType.ProjImpact: // L2107~2108
                {
                    Grp(t, o, "run.projHit");
                    var p = V(e.X, 1 + wy, e.Z);
                    var imp = FxSpawn.Of("fx.impact", p);
                    imp.Crit = e.Flag;
                    o.Spawns.Add(imp);
                    var sp = FxSpawn.Of("fx.sparks", p).WithColor(C(e.Flag ? "#ff6a3d" : "#ffffff"));
                    sp.Count = e.Flag ? 8 : 4; sp.Speed = 3.4f; sp.Size = e.Flag ? 0.5f : 0.3f; sp.Life = 0.3f; sp.Grav = 0;
                    o.Spawns.Add(sp);
                    break;
                }
                case RunEventType.Hit:
                    RouteHit(t, e, w, o, wy);
                    break;
                case RunEventType.Break:
                    RouteBreak(t, e, w, o, wy);
                    break;
                case RunEventType.Gain: // L1926, L1938~1942, L1965~1967
                {
                    var target = e.A == (int)ConsumeType.RunSouls ? FlyTarget.Soul : e.A == (int)ConsumeType.RunDia ? FlyTarget.Dia
                        : e.A == (int)ConsumeType.RunGold ? FlyTarget.Gold : (FlyTarget)(-1);
                    if ((int)target < 0 || (e.X == 0 && e.Z == 0)) break;
                    float y = target == FlyTarget.Gold ? 1.2f : target == FlyTarget.Soul ? 1.6f : 1.5f;
                    o.Flies.Add(new FlyRequest { Target = target, World = V(e.X, y + wy, e.Z), Amount = (int)Math.Round(e.Amount) });
                    break;
                }
                case RunEventType.GateTake: // L2046~2052
                {
                    if (!w.TryObj(e.ObjId, out var ob)) ob = new FxObj { KindKey = KindKey(t, e.KindId) };
                    string kind = ob.KindKey ?? KindKey(t, e.KindId);
                    bool clone = kind == "cgate";
                    string key = clone ? "run.cloneGatePass" : "run.statGatePass";
                    var row = Grp(t, o, key);
                    o.Spawns.Add(FromRow(row, V(ob.X, 1.5 + wy, ob.Z)));
                    if (e.A > 0)
                    {
                        if (clone)
                        {
                            int gap = t.RunConfig.Has(RunConfigKeys.Fly_CloneGapMs) ? t.RunConfig.I(RunConfigKeys.Fly_CloneGapMs) : 70;
                            for (int i = 0; i < Math.Min(e.A, 5); i++)
                                o.Flies.Add(new FlyRequest { Target = FlyTarget.Hand, World = V(ob.X + Rnd(-0.5f, 0.5f), 2 + wy, ob.Z), DelayMs = i * gap, Amount = 1 });
                        }
                        else o.Flies.Add(new FlyRequest { Target = FlyTarget.Stair, World = V(ob.X, 2 + wy, ob.Z), Amount = e.A });
                    }
                    break;
                }
                case RunEventType.WellFlush: // L1861~1880
                {
                    if (!w.TryObj(e.ObjId, out var ob)) break;
                    Grp(t, o, "run.brickFlush");
                    int n = Math.Max(1, e.A);
                    for (int i = 0; i < n; i++)
                    {
                        var b = FxSpawn.Of("fx.brickChunk", V(ob.X + Rnd(-0.3f, 0.3f), 1.2 + wy, ob.Z));
                        b.Variant = i;
                        b.Dir = i % 2 == 0 ? 1 : -1;
                        o.Spawns.Add(b);
                        o.Flies.Add(new FlyRequest { Target = FlyTarget.Gauge, World = V(ob.X + Rnd(-0.8f, 0.8f), 1.4 + Rnd(-0.4f, 0.8f) + wy, ob.Z), DelayMs = 260, Amount = i == n - 1 ? (int)Math.Round(e.Amount) : 0 });
                    }
                    var sp = FxSpawn.Of("fx.sparks", V(ob.X, 1.4 + wy, ob.Z - 0.8)).WithColor(C("#ffb04a"));
                    sp.Count = 4; sp.Speed = 3; sp.Size = 0.3f; sp.Life = 0.35f;
                    o.Spawns.Add(sp);
                    break;
                }
                case RunEventType.Steal: // L2063: 쥐 위치
                {
                    var row = Grp(t, o, "run.steal");
                    var at = w.TryObj(e.ObjId, out var ob) ? V(ob.X, 0.8 + wy, ob.Z) : V(e.X, 0.8 + wy, e.Z);
                    o.Spawns.Add(FromRow(row, at));
                    break;
                }
                case RunEventType.Blocked: // L2068
                {
                    var row = Grp(t, o, "run.blocked");
                    bool has = w.TryObj(e.ObjId, out var ob);
                    var s = FromRow(row, has ? V(ob.X, 1.2 + wy, ob.Z) : V(e.X, 1.2 + wy, e.Z));
                    s = s.WithColor(C(has && ob.KindKey == "wall" ? "#8a5a34" : "#b7b0a4"));
                    o.Spawns.Add(s);
                    break;
                }
                case RunEventType.GodProc: // L1987~1988
                {
                    if (!string.IsNullOrEmpty(e.Key)) _lastEl = e.Key;
                    if (!w.TryObj(e.ObjId, out var ob)) break;
                    double hx = ob.X, hz = ob.Z - ob.Hd;
                    if (e.Key == BoltEl)
                    {
                        Grp(t, o, "run.procBolt");
                        var b = FxSpawn.Of("fx.bolt", V(hx + Rnd(-0.4f, 0.4f), 4.5 + wy, hz));
                        b.To = V(hx, 1 + wy, hz);
                        o.Spawns.Add(b);
                    }
                    else
                    {
                        var row = Grp(t, o, "run.procFire");
                        o.Spawns.Add(FromRow(row, V(hx, 1.2 + wy, hz), e.Key));
                    }
                    break;
                }
                case RunEventType.GodChain: // L1997(bolt 연쇄선), L2000(fire 불씨 폭발)
                {
                    if (e.Key == BoltEl)
                    {
                        if (!w.TryObj(e.ObjId, out var ob)) break;
                        Grp(t, o, "run.chainBolt");
                        var b = FxSpawn.Of("fx.bolt", V(e.X, 1.3 + wy, e.Z));
                        b.To = V(ob.X, 1.3 + wy, ob.Z - ob.Hd);
                        o.Spawns.Add(b);
                    }
                    else
                    {
                        var row = Grp(t, o, "run.chainFire");
                        var s = FxSpawn.Of("fx.boom", V(e.X, 1.2 + wy, e.Z)).WithColor(ElemColor(e.Key));
                        s.Radius = (float)(e.Amount > 0 ? e.Amount : 1.6);
                        o.Spawns.Add(s);
                    }
                    break;
                }
                case RunEventType.BurnTick: // L2137
                {
                    if (!w.TryObj(e.ObjId, out var ob)) break;
                    var row = Grp(t, o, "run.burnTick");
                    o.Spawns.Add(FromRow(row, V(ob.X + Rnd(-0.5f, 0.5f), 1 + wy, ob.Z - ob.Hd)));
                    break;
                }
                case RunEventType.OrbTick: // L2143
                {
                    if (e.Ids == null) break;
                    for (int i = 0; i < e.Ids.Length; i++)
                    {
                        if (!w.TryObj(e.Ids[i], out var ob)) continue;
                        var row = Grp(t, o, "run.orbHit");
                        o.Spawns.Add(FromRow(row, V(ob.X, 1 + wy, ob.Z - ob.Hd), string.IsNullOrEmpty(e.Key) ? _lastEl : e.Key));
                    }
                    break;
                }
                case RunEventType.GodStormStart: // L2157~2162 불구름(fire만)
                {
                    if (e.Key != Fire) break;
                    Grp(t, o, "god.storm");
                    for (int i = 0; i < 6; i++)
                    {
                        var c = FxSpawn.Of("fx.dust", V(e.X + Rnd(-2, 2), 7.5 + Rnd(-0.3f, 0.4f) + wy, e.Z + 1 + Rnd(-1, 1))).WithColor(C("#4a2418"));
                        c.Variant = 1; c.Count = 1; c.Size = Rnd(2.6f, 3.8f); c.Life = (float)(e.Amount > 0 ? e.Amount : 1.7);
                        o.Spawns.Add(c);
                    }
                    break;
                }
                case RunEventType.GodStormStrike: // L2163~2172
                {
                    Grp(t, o, "god.storm");
                    var col = ElemColor(e.Key);
                    if (e.Key == BoltEl)
                    {
                        o.Spawns.Add(FxSpawn.Of("fx.paw", V(e.X, 0.1 + wy, e.Z)));
                        var d = FxSpawn.Of("fx.dust", V(e.X, 0.5 + wy, e.Z)); d.Count = 5;
                        o.Spawns.Add(d);
                    }
                    else
                    {
                        var f = FxSpawn.Of("fx.fireball", V(e.X, 0.6 + wy, e.Z)); f.Size = 1.4f;
                        o.Spawns.Add(f);
                    }
                    var bm = FxSpawn.Of("fx.boom", V(e.X, 0.6 + wy, e.Z)).WithColor(col); bm.Radius = 1.5f;
                    o.Spawns.Add(bm);
                    break;
                }
                case RunEventType.GodBlast: // L2175~2183
                {
                    Grp(t, o, "god.blast");
                    if (e.Key == BoltEl) o.Spawns.Add(FxSpawn.Of("fx.hiss", V(e.X, 0.6 + wy, e.Z)));
                    else
                        for (int i = 0; i < 3; i++)
                        {
                            var f = FxSpawn.Of("fx.fireball", V(e.X + Rnd(-1, 1), 0.6 + wy, e.Z + Rnd(-1, 1))); f.Size = 2.4f;
                            o.Spawns.Add(f);
                        }
                    var b1 = FxSpawn.Of("fx.boom", V(e.X, 1 + wy, e.Z)).WithColor(C(e.Key == Fire ? "#ff5a1f" : "#9fe0ff")); b1.Radius = 4.4f;
                    var b2 = FxSpawn.Of("fx.boom", V(e.X, 1.6 + wy, e.Z)).WithColor(C("#ffffff")); b2.Radius = 2.2f; b2.NoRing = true;
                    o.Spawns.Add(b1); o.Spawns.Add(b2);
                    o.Flashes.Add(new FlashRequest { Peak = Cfg(t, RunConfigKeys.Flash_GodBlast, 0.35f), HoldMs = (int)Cfg(t, RunConfigKeys.Flash_GodBlastMs, 60) });
                    break;
                }
                case RunEventType.Beam: // L2190~2201 (예약)
                    Grp(t, o, "fx.holyBeam");
                    o.Spawns.Add(FxSpawn.Of("fx.holyBeam", V(e.X, 1 + wy, e.Z + 0.5)));
                    break;
                case RunEventType.MeteorLaunch: // L2202~2208 (예약)
                {
                    Grp(t, o, "fx.meteor");
                    var m = FxSpawn.Of("fx.meteor", V(e.X + 6, 22 + wy, e.Z - 10));
                    m.To = V(e.X, 0.8 + wy, e.Z);
                    o.Spawns.Add(m);
                    break;
                }
                case RunEventType.MeteorImpact: // L2209
                {
                    Grp(t, o, "fx.meteor");
                    var p = V(e.X, 0.8 + wy, e.Z);
                    var b1 = FxSpawn.Of("fx.boom", p).WithColor(C("#ff5a1f")); b1.Radius = 3.6f;
                    var b2 = FxSpawn.Of("fx.boom", p).WithColor(C("#ffd27a")); b2.Radius = 2.2f; b2.NoRing = true;
                    var d = FxSpawn.Of("fx.debris", p).WithColor(C("#7a5a40")); d.Count = 10; d.Size = 0.25f;
                    o.Spawns.Add(b1); o.Spawns.Add(b2); o.Spawns.Add(d);
                    break;
                }
                case RunEventType.ClimbStart:
                    _climbK = Math.Max(1, e.B); _climbPw = e.Amount > 0 ? e.Amount : 1; _climbZ0 = e.X; _climbZEnd = e.Z; _climbDH = e.Amount2;
                    o.WarpBegin = true;
                    break;
                case RunEventType.ClimbWarpStage:
                    o.WarpStage = e.A;
                    if (e.A > 0) Grp(t, o, "climb.stage");
                    break;
                case RunEventType.ClimbLap: // L2275~2276: x 0, 꼭대기 칸
                {
                    Grp(t, o, "climb.lap");
                    int lap = ClimbCfg(t, "lap", 75), lapTier = ClimbCfg(t, "lapTier", 4);
                    int si = Math.Min(_climbK - 1, (int)Math.Ceiling(e.A * lap / _climbPw) - 1);
                    si = Math.Max(0, si);
                    double y = (si + 1) * ClimbCfgD(t, "stepH", 0.42), z = _climbZ0 + si * ClimbCfgD(t, "stepD", 0.72);
                    var col = TierColor(t, lapTier);
                    var b = FxSpawn.Of("fx.boom", V(0, y + 2, z)).WithColor(col); b.Radius = 3; b.NoRing = true;
                    var s = FxSpawn.Of("fx.sparks", V(0, y + 3, z)).WithColor(col); s.Count = 24; s.Speed = 7; s.Size = 0.55f; s.Life = 1.1f;
                    o.Spawns.Add(b); o.Spawns.Add(s);
                    break;
                }
                case RunEventType.ClimbTierUp: // L2281~2283
                {
                    Grp(t, o, "climb.tier");
                    int si = Math.Max(0, Math.Min(_climbK - 1, (int)Math.Floor(e.B / _climbPw)));
                    double y = (si + 1) * ClimbCfgD(t, "stepH", 0.42), z = _climbZ0 + si * ClimbCfgD(t, "stepD", 0.72);
                    double px = w?.PlayerX ?? 0;
                    var col = TierColor(t, e.A);
                    var b = FxSpawn.Of("fx.boom", V(px, y + 0.6, z)).WithColor(col); b.Radius = 2.2f;
                    var s = FxSpawn.Of("fx.sparks", V(px, y + 1, z)).WithColor(col); s.Count = 16; s.Speed = 5; s.Size = 0.5f; s.Life = 0.9f;
                    o.Spawns.Add(b); o.Spawns.Add(s);
                    o.Flashes.Add(new FlashRequest { Peak = Cfg(t, RunConfigKeys.Flash_ClimbTier, 0.25f), HoldMs = (int)Cfg(t, RunConfigKeys.Flash_ClimbTierMs, 90), Color = TierHex(t, e.A) });
                    break;
                }
                case RunEventType.ClimbEnd: // L2286
                {
                    var row = Grp(t, o, "climb.land");
                    var s = FromRow(row, V(w?.PlayerX ?? 0, _climbDH + 0.3, _climbZEnd + 0.5));
                    o.Spawns.Add(s);
                    o.WarpEnd = true;
                    break;
                }
                case RunEventType.RouletteChosen: // L2370: 플레이어 위치
                {
                    var row = Grp(t, o, "roulette.pick");
                    var s = FromRow(row, V(w?.PlayerX ?? 0, 1.4 + wy, w?.PlayerZ ?? 0)).WithColor(TierColor(t, e.C));
                    o.Spawns.Add(s);
                    break;
                }
            }
        }

        void RouteHit(MvTables t, in RunEvent e, IMvFxWorld w, MvFxOutput o, double wy)
        {
            var kind = t.ObjectKind.Get(e.KindId);
            string kk = kind?.key;
            bool has = w.TryObj(e.ObjId, out var ob);
            double ox = has ? ob.X : e.X, oz = has ? ob.Z : e.Z + 0, hz = e.Z;
            // 종류별 맞음 이펙트(105 hitEffect, 102가 있으면 102 우선)
            string fx = KindEffect(t, e.KindId, true);
            if (!string.IsNullOrEmpty(fx))
            {
                var row = t.Effect.ByKey(fx);
                if (row != null && (row.chance >= 1 || _rnd() < row.chance))
                {
                    o.Groups.Add(fx);
                    Vector3 at;
                    switch (fx)
                    {
                        case "run.towerHit": at = V(ox + Rnd(-0.6f, 0.6f), 1.2 + wy, oz - 1); break; // L1912
                        case "run.coinHit": at = V(ox, 0.8 + wy, hz); break; // L1961
                        default: at = V(ox, (row.y > 0 ? row.y : 1) + wy, oz); break; // L1933 wall
                    }
                    var s = FromRow(row, at);
                    if (fx == "run.wallHit") s = s.WithColor(C(has && ob.Rew == FieldReward.Soul ? "#5a5490" : "#93673f"));
                    if (fx == "run.coinHit") s = s.WithColor(C(IsGem(kk) ? "#5fd6c9" : "#ffd65a"));
                    o.Spawns.Add(s);
                }
            }
            // 피해 숫자(quiet면 생략, 06 §6.7.1). 위치·크기는 hit() 종류별 pop 호출.
            if (e.Quiet) return;
            int v = (int)Math.Min(99999, Math.Max(1, Math.Round(e.Amount)));
            bool crit = e.Flag;
            float y, size; float dx;
            switch (kk)
            {
                case "tower": dx = Rnd(-0.6f, 0.6f); y = 1.5f + Rnd(0, 1.6f); size = crit ? 1.3f : 1f; break;
                case "wall": dx = Rnd(-0.3f, 0.3f); y = 1.6f; size = crit ? 1.2f : 0.9f; break;
                case "enemy": dx = Rnd(-0.6f, 0.6f); y = 2.0f + Rnd(0, 1.6f); size = crit ? 1.3f : 1f; break;
                default:
                    if (kind != null && kind.enemyId != 0) { dx = Rnd(-0.6f, 0.6f); y = 2.0f + Rnd(0, 1.6f); size = crit ? 1.3f : 1f; }
                    else { dx = Rnd(-0.4f, 0.4f); y = 1.3f + Rnd(0, 1.2f); size = crit ? 1.2f : 0.9f; }
                    break;
            }
            o.Pops.Add(new PopRequest { World = V(ox + dx, y + wy, hz), Text = Fmt(v), Color = C(HitHex(t, e.Color, crit)), Size = size });
        }

        void RouteBreak(MvTables t, in RunEvent e, IMvFxWorld w, MvFxOutput o, double wy)
        {
            string fx = KindEffect(t, e.KindId, false);
            if (string.IsNullOrEmpty(fx)) return;
            string kk = t.ObjectKind.Get(e.KindId)?.key;
            o.Groups.Add(fx);
            double x = e.X, z = e.Z;
            switch (fx)
            {
                case "run.enemyKill": // L1925
                {
                    var d = FxSpawn.Of("fx.debris", V(x, 0.8 + wy, z)).WithColor(C("#5a8a3a")); d.Count = 8; d.Size = 0.2f;
                    var s = FxSpawn.Of("fx.sparks", V(x, 1 + wy, z)).WithColor(C("#ffd65a")); s.Count = 12; s.Speed = 5; s.Size = 0.4f; s.Life = 0.6f;
                    var u = FxSpawn.Of("fx.dust", V(x, 0.8 + wy, z)); u.Count = 4;
                    o.Spawns.Add(d); o.Spawns.Add(s); o.Spawns.Add(u);
                    break;
                }
                case "run.wallBreak": // L1935~1939
                {
                    var rew = (FieldReward)Math.Max(0, e.A);
                    bool soul = e.A >= 0 && rew == FieldReward.Soul;
                    var d1 = FxSpawn.Of("fx.debris", V(x, 1.3 + wy, z)).WithColor(C(soul ? "#5a5490" : "#93673f")); d1.Count = 14; d1.Size = 0.32f;
                    var d2 = FxSpawn.Of("fx.debris", V(x, 1.6 + wy, z)).WithColor(C("#5b606c")); d2.Count = 4; d2.Size = 0.22f;
                    var u = FxSpawn.Of("fx.dust", V(x, 1.0 + wy, z)); u.Count = 7;
                    var b = FxSpawn.Of("fx.boom", V(x, 1.2 + wy, z)).WithColor(C("#ffd6a0")); b.Radius = 1.5f; b.NoRing = true;
                    o.Spawns.Add(d1); o.Spawns.Add(d2); o.Spawns.Add(u); o.Spawns.Add(b);
                    if (e.A >= 0 && rew == FieldReward.Soul) o.Spawns.Add(FromRow(Grp(t, o, "run.wallRewardSoul"), V(x, 2 + wy, z)));
                    else if (e.A >= 0 && rew == FieldReward.Dia) o.Spawns.Add(FromRow(Grp(t, o, "run.wallRewardDia"), V(x, 2 + wy, z)));
                    break;
                }
                case "run.coinBreak": // L1963~1966
                {
                    var u = FxSpawn.Of("fx.dust", V(x, 0.6 + wy, z)); u.Count = 4;
                    o.Spawns.Add(u);
                    if (IsGem(kk))
                    {
                        var s = FxSpawn.Of("fx.sparks", V(x, 1.2 + wy, z)).WithColor(C("#7feaff")); s.Count = 20; s.Speed = 6; s.Size = 0.45f; s.Life = 0.9f;
                        o.Spawns.Add(s);
                    }
                    else
                    {
                        var s = FxSpawn.Of("fx.sparks", V(x, 1 + wy, z)).WithColor(C("#ffd65a")); s.Count = 18; s.Speed = 6; s.Size = 0.4f; s.Life = 0.8f;
                        o.Spawns.Add(s);
                    }
                    break;
                }
                default:
                {
                    // 새 종류: 표 그룹 행이 자식 1개면 그 값으로.
                    var row = t.Effect.ByKey(fx);
                    if (row != null && row.children != null && row.children.Count > 0) o.Spawns.Add(FromRow(row, V(x, (row.y > 0 ? row.y : 1) + wy, z)));
                    break;
                }
            }
        }

        static bool IsGem(string kindKey) => kindKey == "gem" || kindKey == "gemBonus";

        /// <summary>105 hitEffect/breakEffect. enemyId가 있으면 102 값이 덮어쓴다(빈칸이면 105).</summary>
        public static string KindEffect(MvTables t, int kindId, bool hit)
        {
            var k = t.ObjectKind.Get(kindId);
            if (k == null) return null;
            var en = k.enemyId != 0 ? t.Enemy.Get(k.enemyId) : null;
            string v = en == null ? null : hit ? en.hitEffect : en.breakEffect;
            if (string.IsNullOrEmpty(v)) v = hit ? k.hitEffect : k.breakEffect;
            return v;
        }

        static string KindKey(MvTables t, int kindId) => t.ObjectKind.Get(kindId)?.key;

        static EffectData Grp(MvTables t, MvFxOutput o, string key)
        {
            o.Groups.Add(key);
            return t.Effect.ByKey(key);
        }

        /// <summary>자식 1개 그룹 행 → 기본 종류 재생. 색 토큰 `$elem`은 el로, `$tier`·`$rew`는 호출부가 덮는다.</summary>
        FxSpawn FromRow(EffectData row, Vector3 at, string el = null)
        {
            if (row == null || row.children == null || row.children.Count == 0) return FxSpawn.Of("fx.sparks", at);
            var s = FxSpawn.Of(row.children[0], at);
            if (!string.IsNullOrEmpty(row.color))
            {
                if (row.color == "$elem") s = s.WithColor(ElemColor(el));
                else if (row.color[0] == '#') s = s.WithColor(C(row.color));
            }
            if (row.count > 0) s.Count = (float)row.count;
            if (row.speed > 0) s.Speed = (float)row.speed;
            if (row.size > 0) s.Size = (float)row.size;
            if (row.life > 0) s.Life = (float)row.life;
            if (row.grav != 0) s.Grav = (float)row.grav;
            if (row.radius > 0) s.Radius = (float)row.radius;
            if (row.flag == "noRing") s.NoRing = true;
            return s;
        }

        /// <summary>원소색(06 §6.4: fire `#ff7a2a` / bolt `#9fe0ff`).</summary>
        public static Color32 ElemColor(string el) => C(el == BoltEl ? "#9fe0ff" : "#ff7a2a");

        static Color32 TierColor(MvTables t, int tier) => C(TierHex(t, tier) ?? "#ffffff");

        static string TierHex(MvTables t, int tier)
        {
            foreach (var r in t.CardTier.Datas)
                if (r.tierIndex == tier && !string.IsNullOrEmpty(r.color)) return r.color;
            return null;
        }

        /// <summary>피해 숫자 색(정본 `hit()` dcol·tcol).</summary>
        static string HitHex(MvTables t, HitColor c, bool crit)
        {
            switch (c)
            {
                case HitColor.Crit: return t.RunConfig.Has(RunConfigKeys.Pop_CritColor) ? t.RunConfig.Raw(RunConfigKeys.Pop_CritColor) : "#ff6a3d";
                case HitColor.GodBolt: return "#bfeaff";
                case HitColor.GodFire: return "#ffb36b";
                case HitColor.Burn: return "#ff9a4a";
                case HitColor.Beam: return "#ffe08a";
                case HitColor.Meteor: return "#ffb36b";
                case HitColor.Blast: return "#ffe08a";
                default: return crit ? "#ff6a3d" : "#ffffff";
            }
        }

        /// <summary>정본 `fmt`(L855): 1e6 이상 x.xM, 1e4 이상 x.xK, 그 밖은 천 단위 쉼표.</summary>
        public static string Fmt(double n)
        {
            n = Math.Floor(n);
            if (n >= 1e6) return (n / 1e6).ToString("0.0", CultureInfo.InvariantCulture) + "M";
            if (n >= 1e4) return (n / 1e3).ToString("0.0", CultureInfo.InvariantCulture) + "K";
            return n.ToString("#,0", CultureInfo.InvariantCulture);
        }

        static float Cfg(MvTables t, string key, float fallback) => t.RunConfig.Has(key) ? (float)t.RunConfig.D(key) : fallback;

        static int ClimbCfg(MvTables t, string col, int fallback) => (int)Math.Round(ClimbCfgD(t, col, fallback));

        static double ClimbCfgD(MvTables t, string col, double fallback)
        {
            var c = t.Climb.Config();
            if (c == null) return fallback;
            try { return c[col]; }
            catch (KeyNotFoundException) { return fallback; }
        }

        static Vector3 V(double x, double y, double z) => new Vector3((float)x, (float)y, (float)z);

        public static Color32 C(string hex) => MvColor.TryColor32(hex, out var c) ? c : new Color32(255, 255, 255, 255);
    }
}
