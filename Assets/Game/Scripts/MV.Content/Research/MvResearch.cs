using System;
using System.Collections.Generic;
using MV.Data;
using MV.Meta;
using MV.Run;
using MV.Save;

namespace MV.Content
{
    /// <summary>
    /// 연구 입구(08 §8.5.3). 진행 변경은 MV.Meta 요청(<see cref="MvMetaRequestNames.StartResearch"/>·<see cref="MvMetaRequestNames.ClaimResearch"/>)으로만 하고,
    /// 읽기·합산은 <see cref="MvResearchRules"/> 순수 함수를 쓴다. 1차 메타 능력치 출처는 연구뿐(08 §8.8) — 연구 0이면 <see cref="RunModifiers.Zero"/>.
    /// </summary>
    public static class MvResearch
    {
        /// <summary>받은 단계(<c>mvResearch.lv</c>).</summary>
        public static int Level(MvSaveStore store, int group)
            => store == null || store.IsDisposed ? 0 : store.Research.GetLevel(MvResearchRules.Key(group));

        /// <summary>진행 중 연구(<c>mvResearch.run</c>).</summary>
        public static MvResearchRun Run(MvSaveStore store)
        {
            if (store == null || store.IsDisposed) return MvResearchRun.None;
            var r = store.Research;
            return MvResearchRules.TryParseKey(r.RunningKey, out int group) ? new MvResearchRun(group, r.RunningFinishAt) : MvResearchRun.None;
        }

        /// <summary>단계 올리기 시작(비용 먼저, 끝 시각 저장).</summary>
        public static MvMetaResponse Start(MvMeta meta, int group)
            => meta == null ? MvMetaResponse.Reject(MvMetaErrors.NoStore) : meta.StartResearch(group);

        /// <summary>완료 수령(끝 시각이 지났을 때 1번).</summary>
        public static MvMetaResponse Claim(MvMeta meta, int group)
            => meta == null ? MvMetaResponse.Reject(MvMetaErrors.NoStore) : meta.ClaimResearch(group);

        // ───────── 런 시작 메타 능력치(08 §8.5.0, 04 §4.20.4) ─────────

        /// <summary>합산 사전 → <see cref="RunModifiers"/>(% 단위 7필드).</summary>
        public static RunModifiers ToModifiers(IReadOnlyDictionary<MetaStat, double> totals)
        {
            var m = RunModifiers.Zero;
            if (totals == null) return m;
            foreach (var pair in totals)
            {
                switch (pair.Key)
                {
                    case MetaStat.Dmg: m.Dmg += pair.Value; break;
                    case MetaStat.FireRate: m.FireRate += pair.Value; break;
                    case MetaStat.Range: m.Range += pair.Value; break;
                    case MetaStat.CritDmg: m.CritDmg += pair.Value; break;
                    case MetaStat.Gold: m.Gold += pair.Value; break;
                    case MetaStat.CritChance: m.CritChance += pair.Value; break;
                    case MetaStat.DoubleChance: m.DoubleChance += pair.Value; break;
                }
            }
            return m;
        }

        /// <summary>연구 단계 → 런 메타 능력치(순수 함수). 1차 출처는 연구뿐이라 이것이 <c>RunContext.MetaModifiers</c> 전체다.</summary>
        public static RunModifiers Modifiers(MvTables t, Func<int, int> levelOf)
            => ToModifiers(MvResearchRules.Totals(t, levelOf));

        /// <summary>
        /// 저장 → 런 메타 능력치. 저장이 없으면 <see cref="RunModifiers.Zero"/>(정본).
        /// MV.Boot 연결: <c>MvRunStart.CreateContext</c>의 <c>MetaModifiers = RunModifiers.Zero</c> 자리.
        /// </summary>
        public static RunModifiers Modifiers(MvSaveStore store, MvTables t)
        {
            if (t == null || store == null || store.IsDisposed) return RunModifiers.Zero;
            return Modifiers(t, g => Level(store, g));
        }
    }
}
