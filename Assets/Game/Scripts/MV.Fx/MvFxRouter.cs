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
    /// 자식이 1개인 그룹은 표 행 값(color·count·speed·size·life·grav·y·chance)을 쓰고, 자식이 여럿인 그룹은 자식마다 part 행
    /// (07 §7.11.2: when·y·dz·jx·jz·인자)을 읽는다(<see cref="PlayParts"/>). 기준점·색 토큰 풀이·벽돌 수·불구름·chainFire 반지름은 호출부 계산.
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
                    PlayParts(t, Grp(t, o, "run.projHit"), o, e.X, wy, e.Z, e.Flag ? s_crit : s_normal, default, crit: e.Flag);
                    break;
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
                            int gap = t.RunConfig.I(RunConfigKeys.Fly_CloneGapMs), max = t.RunConfig.I(RunConfigKeys.CRAFT_ICON_MAX);
                            for (int i = 0; i < Math.Min(e.A, max); i++)
                                o.Flies.Add(new FlyRequest { Target = FlyTarget.Hand, World = V(ob.X + Rnd(-0.5f, 0.5f), 2 + wy, ob.Z), DelayMs = i * gap, Amount = 1 });
                        }
                        else o.Flies.Add(new FlyRequest { Target = FlyTarget.Stair, World = V(ob.X, 2 + wy, ob.Z), Amount = e.A });
                    }
                    break;
                }
                case RunEventType.WellFlush: // L1861~1880
                {
                    if (!w.TryObj(e.ObjId, out var ob)) break;
                    int n = Math.Max(1, e.A);
                    PlayParts(t, Grp(t, o, "run.brickFlush"), o, ob.X, wy, ob.Z, s_none, default, bricks: n);
                    for (int i = 0; i < n; i++)
                        o.Flies.Add(new FlyRequest { Target = FlyTarget.Gauge, World = V(ob.X + Rnd(-0.8f, 0.8f), 1.4 + Rnd(-0.4f, 0.8f) + wy, ob.Z), DelayMs = 260, Amount = i == n - 1 ? (int)Math.Round(e.Amount) : 0 });
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
                        c.Variant = 1; c.Count = 1; c.Size = Rnd(2.6f, 3.8f); c.Life = (float)(e.Amount > 0 ? e.Amount : t.RunConfig.D(RunConfigKeys.STORM_LIFE));
                        o.Spawns.Add(c);
                    }
                    break;
                }
                case RunEventType.GodStormStrike: // L2163~2172
                    PlayParts(t, Grp(t, o, "god.storm"), o, e.X, wy, e.Z, ElemFacts(e.Key), ElemColor(e.Key));
                    break;
                case RunEventType.GodBlast: // L2175~2183
                {
                    PlayParts(t, Grp(t, o, "god.blast"), o, e.X, wy, e.Z, ElemFacts(e.Key), ElemColor(e.Key));
                    o.Flashes.Add(new FlashRequest { Peak = (float)t.RunConfig.D(RunConfigKeys.Flash_GodBlast), HoldMs = t.RunConfig.I(RunConfigKeys.Flash_GodBlastMs) });
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
                    var grp = Grp(t, o, "climb.lap");
                    var cc = ClimbConfig(t);
                    if (cc == null) break;
                    int lap = Round(cc["lap"]), lapTier = Round(cc["lapTier"]);
                    int si = Math.Min(_climbK - 1, (int)Math.Ceiling(e.A * lap / _climbPw) - 1);
                    si = Math.Max(0, si);
                    double y = (si + 1) * cc["stepH"], z = _climbZ0 + si * cc["stepD"];
                    PlayParts(t, grp, o, 0, y, z, s_none, TierColor(t, lapTier));   // 기준점 = 꼭대기 칸 위(x 0)
                    break;
                }
                case RunEventType.ClimbTierUp: // L2281~2283
                {
                    var grp = Grp(t, o, "climb.tier");
                    var cc = ClimbConfig(t);
                    if (cc == null) break;
                    int si = Math.Max(0, Math.Min(_climbK - 1, (int)Math.Floor(e.B / _climbPw)));
                    double y = (si + 1) * cc["stepH"], z = _climbZ0 + si * cc["stepD"];
                    PlayParts(t, grp, o, w?.PlayerX ?? 0, y, z, s_none, TierColor(t, e.A));   // 기준점 = 오른 칸 위
                    o.Flashes.Add(new FlashRequest { Peak = (float)t.RunConfig.D(RunConfigKeys.Flash_ClimbTier), HoldMs = t.RunConfig.I(RunConfigKeys.Flash_ClimbTierMs), Color = TierHex(t, e.A) });
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
            int v = (int)Math.Min(t.RunConfig.D(RunConfigKeys.DMG_MAX), Math.Max(t.RunConfig.D(RunConfigKeys.DMG_MIN), Math.Round(e.Amount)));
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
            var row = Grp(t, o, fx);
            double x = e.X, z = e.Z;
            // 필드 보상(Break A ≥ 0: 벽): `$rew` = 보상별 파편 색(L1935 생선 #5a5490 / 그 밖 #93673f).
            var rew = (FieldReward)Math.Max(0, e.A);
            if (HasParts(t, row))
                // L1925 enemyKill, L1935~1939 wallBreak, L1963~1966 coinBreak: part 행. 상자 종류(coin/gem)는 when으로 가른다.
                PlayParts(t, row, o, x, wy, z, IsGem(kk) ? s_gem : s_coin, C(e.A >= 0 && rew == FieldReward.Soul ? "#5a5490" : "#93673f"));
            else if (row != null && row.children != null && row.children.Count > 0)
                o.Spawns.Add(FromRow(row, V(x, (row.y > 0 ? row.y : 1) + wy, z)));   // 자식 1개 묶음: 묶음 행 값
            if (e.A >= 0 && rew == FieldReward.Soul) o.Spawns.Add(FromRow(Grp(t, o, "run.wallRewardSoul"), V(x, 2 + wy, z)));
            else if (e.A >= 0 && rew == FieldReward.Dia) o.Spawns.Add(FromRow(Grp(t, o, "run.wallRewardDia"), V(x, 2 + wy, z)));
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
            if (!string.IsNullOrEmpty(row.color) && row.color == "$elem") s = s.WithColor(ElemColor(el));
            return Apply(row, s, null);
        }

        static readonly string[] s_none = new string[0], s_normal = { "normal" }, s_crit = { "crit" }, s_coin = { "coin" }, s_gem = { "gem" },
            s_fire = { Fire }, s_bolt = { BoltEl };

        /// <summary>원소 사건의 when 사실(bolt가 아니면 fire — <see cref="ElemColor"/>와 같은 규칙).</summary>
        static string[] ElemFacts(string el) => el == BoltEl ? s_bolt : s_fire;

        /// <summary>묶음 자식에 part 행이 있는지(07 §7.11.2 자식이 여럿인 묶음).</summary>
        static bool HasParts(MvTables t, EffectData group)
        {
            if (group?.children == null) return false;
            for (int i = 0; i < group.children.Count; i++)
                if (t.Effect.ByKey(group.children[i])?.type == EffectData.PartType) return true;
            return false;
        }

        /// <summary>
        /// part 묶음 재생(07 §7.11.2): 묶음 `children`(part key, 같은 key는 그 수만큼)을 차례로 돌며 `when`이 모두 <paramref name="facts"/>에 있는
        /// part 행만 재생한다. 위치 = 기준점(x, baseY, z) + (±jx, y, dz ± jz). `$tier`·`$rew`·`$elem` 색은 <paramref name="token"/>.
        /// 자식이 `fx.brickChunk`인 행은 <paramref name="bricks"/>개(사건 벽돌 수, 순번 색·좌우 교대)로 낸다. 치명 사건이면 <c>Crit</c>(impact 표시).
        /// </summary>
        void PlayParts(MvTables t, EffectData group, MvFxOutput o, double x, double baseY, double z, string[] facts, Color32? token,
            bool crit = false, int bricks = 1)
        {
            if (group?.children == null) return;
            for (int c = 0; c < group.children.Count; c++)
            {
                var part = t.Effect.ByKey(group.children[c]);
                if (part == null || part.type != EffectData.PartType || part.children == null || part.children.Count == 0) continue;
                if (!WhenHolds(part.when, facts)) continue;
                string kind = part.children[0];
                int n = kind == BrickChunk ? Math.Max(1, bricks) : 1;
                for (int i = 0; i < n; i++)
                {
                    double px = x + (part.jx > 0 ? Rnd((float)-part.jx, (float)part.jx) : 0);
                    double pz = z + part.dz + (part.jz > 0 ? Rnd((float)-part.jz, (float)part.jz) : 0);
                    var s = Apply(part, FxSpawn.Of(kind, V(px, baseY + part.y, pz)), token);
                    s.Crit = crit;
                    if (kind == BrickChunk) { s.Variant = i; s.Dir = i % 2 == 0 ? 1 : -1; }
                    o.Spawns.Add(s);
                }
            }
        }

        const string BrickChunk = "fx.brickChunk";

        static bool WhenHolds(IReadOnlyList<string> when, string[] facts)
        {
            if (when == null) return true;
            for (int i = 0; i < when.Count; i++)
                if (!string.IsNullOrEmpty(when[i]) && Array.IndexOf(facts, when[i]) < 0) return false;
            return true;
        }

        /// <summary>행 값(색·count·speed·size·life·grav·radius·flag)을 덮는다. 빈칸은 종류 기본값, grav는 빈칸과 0이 다르다(hasGrav).</summary>
        static FxSpawn Apply(EffectData row, FxSpawn s, Color32? token)
        {
            if (!string.IsNullOrEmpty(row.color))
            {
                if (row.color[0] == '$') { if (!s.HasColor && token.HasValue) s = s.WithColor(token.Value); }
                else if (row.color[0] == '#') s = s.WithColor(C(row.color));
            }
            if (row.count > 0) s.Count = (float)row.count;
            if (row.speed > 0) s.Speed = (float)row.speed;
            if (row.size > 0) s.Size = (float)row.size;
            if (row.life > 0) s.Life = (float)row.life;
            if (row.hasGrav) s.Grav = (float)row.grav;
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
                case HitColor.Crit: return t.RunConfig.Raw(RunConfigKeys.Pop_CritColor);
                case HitColor.GodBolt: return "#bfeaff";
                case HitColor.GodFire: return "#ffb36b";
                case HitColor.Burn: return "#ff9a4a";
                case HitColor.Beam: return "#ffe08a";
                case HitColor.Meteor: return "#ffb36b";
                case HitColor.Blast: return "#ffe08a";
                default: return crit ? t.RunConfig.Raw(RunConfigKeys.Pop_CritColor) : "#ffffff";
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

        static int Round(double v) => (int)Math.Round(v);

        /// <summary>`330_climb` 설정 행(lap·lapTier·stepH·stepD). 없으면 오류 로그 후 null(대체값 없음).</summary>
        static ClimbData ClimbConfig(MvTables t)
        {
            var c = t.Climb.Config();
            if (c == null) Debug.LogError("[MvFxRouter] 330_climb 설정(Config) 행이 없습니다.");
            return c;
        }

        static Vector3 V(double x, double y, double z) => new Vector3((float)x, (float)y, (float)z);

        public static Color32 C(string hex) => MvColor.TryColor32(hex, out var c) ? c : new Color32(255, 255, 255, 255);
    }
}
