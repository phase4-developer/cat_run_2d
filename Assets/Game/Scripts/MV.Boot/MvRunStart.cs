using System;
using System.Collections.Generic;
using MV.Data;
using MV.Run;
using MV.Save;
using Newtonsoft.Json.Linq;

namespace MV.Boot
{
    /// <summary>
    /// 런 시작에 쓰는 save 스냅숏(04 §4.14.5 RunSetup 주석의 save 필드). MV.Meta는 MV.Run을 참조하지 않으므로(10 §10.4)
    /// 저장 → 스냅숏 → <see cref="RunContext"/> 옮기기는 MV.Boot가 한다(MV-G3-INT 빈칸).
    /// 값만 담는다(MV.Run 타입 없음). 런 동안 바뀌지 않는다.
    /// </summary>
    public sealed class MvRunSnapshot
    {
        /// <summary>0기반 Area(<c>save.area</c>).</summary>
        public int Area;
        public int Level = 1;
        public int Cp;
        /// <summary>누적 생선(재화 <c>souls</c>).</summary>
        public int Souls;
        public string God = "thunder";
        /// <summary>시작 덱 9칸(등급별 장수, 0번 미사용).</summary>
        public int[] Deck = new int[9];
        public int UpStair, UpWeapon, UpIncome;
        public int AupRate, AupRange, AupClone, AupCrit, AupDbl;
        public int PerkPower, PerkCrit, PerkObst, PerkDcap;
        /// <summary>이 Area 벽 기록 <c>save.bars['A'+area]</c>: Floor별 칸 idx → 남은 HP. 없으면 null.</summary>
        public Dictionary<int, double>[] Bars;
        /// <summary>정산이 끝난 마지막 런 순번(시드 기준).</summary>
        public long LastSettledRunSeq;

        /// <summary>열린 저장에서 읽는다.</summary>
        public static MvRunSnapshot FromStore(MvSaveStore store, int floorCount = 3)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            var s = new MvRunSnapshot
            {
                Area = store.Progress.Area,
                Level = Math.Max(1, store.Progress.Level),
                Cp = store.Progress.Cp,
                Souls = (int)Math.Min(int.MaxValue, Math.Max(0, store.Wealth.Get("rescueCat"))), // 01_wealth.type
                God = store.God.Selected,
                UpStair = store.Lobby.GetUp("stair"),
                UpWeapon = store.Lobby.GetUp("weapon"),
                UpIncome = store.Lobby.GetUp("income"),
                AupRate = store.AreaUp.GetLevel("rate"),
                AupRange = store.AreaUp.GetLevel("range"),
                AupClone = store.AreaUp.GetLevel("clone"),
                AupCrit = store.AreaUp.GetLevel("crit"),
                AupDbl = store.AreaUp.GetLevel("dbl"),
                PerkPower = store.Rebirth.GetPerk("power"),
                PerkCrit = store.Rebirth.GetPerk("crit"),
                PerkObst = store.Rebirth.GetPerk("obst"),
                PerkDcap = store.Rebirth.GetPerk("dcap"),
                LastSettledRunSeq = store.Progress.LastSettledRunSeq,
            };
            var deck = store.Lobby.Deck;
            for (int i = 0; i < s.Deck.Length && i < deck.Count; i++) s.Deck[i] = deck[i];
            // 교체 지점: MV.Save가 벽 기록 읽기(GetBars)를 내면 JSON을 거치지 않는다(MV-SAVE 변경 요청 후보).
            s.Bars = ParseBars(store.Progress.ToJsonString(), s.Area, floorCount);
            return s;
        }

        /// <summary>저장을 열지 못했을 때(복구 대기·Editor 데이터 없음): 새 저장 기본값과 같은 스냅숏.</summary>
        public static MvRunSnapshot Defaults(MvSaveConfig config)
        {
            var s = new MvRunSnapshot();
            if (config != null)
            {
                if (!string.IsNullOrEmpty(config.StartGodKey)) s.God = config.StartGodKey;
                if (config.StartDeck != null)
                    for (int i = 0; i < s.Deck.Length && i < config.StartDeck.Length; i++) s.Deck[i] = config.StartDeck[i];
            }
            return s;
        }

        /// <summary>
        /// <c>mvProgress</c> JSON의 <c>bars["A{area}"]</c>(Floor 배열 → 칸 배열, 빈 칸 null, 09 §9.3.1)를 Floor별 사전으로 읽는다.
        /// 기록이 없으면 null. Floor가 <paramref name="floorCount"/>보다 많으면 넘는 Floor는 버린다.
        /// </summary>
        public static Dictionary<int, double>[] ParseBars(string progressJson, int area, int floorCount)
        {
            if (string.IsNullOrEmpty(progressJson) || floorCount <= 0) return null;
            JObject root;
            try { root = JObject.Parse(progressJson); }
            catch (Newtonsoft.Json.JsonException) { return null; }
            if (!(root["bars"] is JObject bars) || !(bars[MvProgressContainer.BarKey(area)] is JArray floors)) return null;
            var result = new Dictionary<int, double>[floorCount];
            for (int f = 0; f < floorCount; f++)
            {
                result[f] = new Dictionary<int, double>();
                if (f >= floors.Count || !(floors[f] is JArray cells)) continue;
                for (int idx = 0; idx < cells.Count; idx++)
                {
                    var cell = cells[idx];
                    if (cell.Type != JTokenType.Integer && cell.Type != JTokenType.Float) continue;
                    result[f][idx] = Math.Max(0, cell.Value<double>());
                }
            }
            return result;
        }

        /// <summary>
        /// 런 시드: 런 순번에서 결정적으로 만든다(같은 저장·같은 순번 = 같은 런, MV-PLAY-INT 계약).
        /// 04 §4.17.2-3의 「MV.Meta 무작위 시드」로 바꿀 때는 이 함수만 바꾼다. SplitMix64.
        /// </summary>
        public static ulong SeedFor(long runSeq)
        {
            unchecked
            {
                ulong z = (ulong)runSeq + 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }

    /// <summary>스냅숏 → <see cref="RunContext"/>(정본 <see cref="MainRunRule.Build"/>의 입력).</summary>
    public static class MvRunStart
    {
        /// <summary>06_balancePreset 기본(D-03 easy).</summary>
        public const string DefaultPreset = "easy";

        /// <summary>
        /// 본편 런 입력. 메타 능력치(펫·장비)와 동행은 아직 없어 <see cref="RunModifiers.Zero"/>·빈 목록이다
        /// (교체 지점: MV.Meta가 §4.20.4 합산·§4.20.5 동행 목록을 내면 여기서 넣는다).
        /// </summary>
        public static RunContext CreateContext(MvRunSnapshot snap, MvTables tables, ulong seed)
        {
            if (snap == null) throw new ArgumentNullException(nameof(snap));
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            var setup = new RunSetup
            {
                Seed = seed,
                Area = snap.Area,
                Zone = Math.Max(0, tables.ZoneIndexOfArea(snap.Area + 1)),
                Level = Math.Max(1, snap.Level),
                Souls = snap.Souls,
                Cp = snap.Cp,
                UpStair = snap.UpStair, UpWeapon = snap.UpWeapon, UpIncome = snap.UpIncome,
                AupRate = snap.AupRate, AupRange = snap.AupRange, AupClone = snap.AupClone, AupCrit = snap.AupCrit, AupDbl = snap.AupDbl,
                PerkPower = snap.PerkPower, PerkCrit = snap.PerkCrit, PerkObst = snap.PerkObst, PerkDcap = snap.PerkDcap,
                God = string.IsNullOrEmpty(snap.God) ? "thunder" : snap.God,
                Preset = DefaultPreset,
            };
            for (int i = 0; i < setup.Deck.Length && snap.Deck != null && i < snap.Deck.Length; i++) setup.Deck[i] = snap.Deck[i];
            if (snap.Bars != null)
            {
                var bars = new BarRecord(snap.Bars.Length);
                for (int f = 0; f < snap.Bars.Length; f++)
                    if (snap.Bars[f] != null)
                        foreach (var kv in snap.Bars[f]) bars.Set(f, kv.Key, kv.Value);
                setup.Bars = bars;
            }
            return new RunContext { Tables = tables, Snapshot = setup, MetaModifiers = RunModifiers.Zero };
        }
    }
}
