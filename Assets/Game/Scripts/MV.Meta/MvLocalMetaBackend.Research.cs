using MV.Data;
using MV.Save;
using Newtonsoft.Json.Linq;

namespace MV.Meta
{
    /// <summary>
    /// 연구 처리기(08 §8.5.3, MV-META-RESEARCH). 규칙은 <see cref="MvResearchRules"/>, 저장은 <c>mvResearch {lv:{group: 단계}, run:{key, finishAt}}</c>(09 §9.3.5).
    /// 시작 = 비용 차감 + 끝 시각 저장(같은 ApplyAndSave), 수령 = 끝 시각이 지났으면 단계 +1·run 비움. 즉시 완료 없음.
    /// 저장 <c>run</c>이 1칸이라 한 번에 1계열만 진행한다(08은 계열마다 따로 — D-RESEARCH-1 후보, 저장 형식 변경은 MV-SAVE).
    /// </summary>
    public sealed partial class MvLocalMetaBackend
    {
        /// <summary>저장의 진행 중 연구. key가 계열 숫자가 아니면 없음으로 본다.</summary>
        internal MvResearchRun ResearchRun()
        {
            var r = S.Research;
            return MvResearchRules.TryParseKey(r.RunningKey, out int group) ? new MvResearchRun(group, r.RunningFinishAt) : MvResearchRun.None;
        }

        internal bool ResearchOpen()
        {
            MvSaveStore s = S;
            return IsOpen(_t.ContentOpen.ByKey(MvResearchRules.UnlockKey), s.Progress.Area, s.Progress.Runs, s.Unlock.Opened);
        }

        static bool TryGroup(JObject p, out int group)
        {
            group = 0;
            JToken v = p["group"];
            if (v == null || (v.Type != JTokenType.Integer && v.Type != JTokenType.String)) return false;
            return int.TryParse(v.ToString(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out group) && group > 0;
        }

        private MvMetaResponse StartResearch(JObject p)
        {
            if (!TryGroup(p, out int group)) return MvMetaResponse.Reject(MvMetaErrors.BadParams, "group");
            if (MvResearchRules.Steps(_t, group).Count == 0) return MvMetaResponse.Reject(MvMetaErrors.UnknownKey, "group " + group);
            if (!ResearchOpen()) return MvMetaResponse.Reject(MvMetaErrors.Locked, MvResearchRules.UnlockKey);
            if (ResearchRun().Exists) return MvMetaResponse.Reject(MvMetaErrors.Busy, "run " + S.Research.RunningKey);

            string key = MvResearchRules.Key(group);
            ResearchData row = MvResearchRules.Next(_t, group, S.Research.GetLevel(key));
            if (row == null) return MvMetaResponse.Reject(MvMetaErrors.Max, key);

            var wealth = new WealthDelta();
            foreach (var cost in row.price)
            {
                if (cost.Type != ConsumeType.Wealth) return MvMetaResponse.Reject(MvMetaErrors.BadParams, "비용 종류 " + cost);
                wealth.Cost(cost.Id, (long)System.Math.Floor(cost.Value));
            }
            long finishAt = MvResearchRules.FinishAt(row, _clock.NowMs);
            var data = new JObject { [MvSaveKeys.Research] = new JObject { ["run"] = new JObject { ["key"] = key, ["finishAt"] = finishAt } } };
            if (!wealth.TryWrite(Res, data, out string fail)) return MvMetaResponse.Reject(MvMetaErrors.NotEnough, fail);
            return MvMetaResponse.Success(data, new JObject { ["group"] = group, ["level"] = row.level, ["finishAt"] = finishAt });
        }

        private MvMetaResponse ClaimResearch(JObject p)
        {
            if (!TryGroup(p, out int group)) return MvMetaResponse.Reject(MvMetaErrors.BadParams, "group");
            MvResearchRun run = ResearchRun();
            if (!run.Exists || run.Group != group) return MvMetaResponse.Reject(MvMetaErrors.NotDone, "group " + group);
            if (!MvResearchRules.IsDone(run.FinishAtMs, _clock.NowMs))
                return MvMetaResponse.Reject(MvMetaErrors.NotDone, "left " + MvResearchRules.LeftMs(run.FinishAtMs, _clock.NowMs) + "ms");

            string key = MvResearchRules.Key(group);
            int before = S.Research.GetLevel(key);
            // 진행 중에 표에서 다음 행이 빠졌으면 단계는 그대로 두고 run만 비운다(다른 계열이 막히지 않게).
            ResearchData row = MvResearchRules.Next(_t, group, before);
            int after = row?.level ?? before;

            var lv = new JObject();
            foreach (int g in _t.Research.Groups)
            {
                string k = MvResearchRules.Key(g);
                int v = S.Research.GetLevel(k);
                if (v > 0) lv[k] = v;
            }
            if (after > 0) lv[key] = after;
            var data = new JObject { [MvSaveKeys.Research] = new JObject { ["lv"] = lv, ["run"] = null } };
            return MvMetaResponse.Success(data, new JObject { ["group"] = group, ["before"] = before, ["level"] = after });
        }
    }
}
