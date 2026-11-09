using System;
using System.Collections.Generic;
using MV.Data;
using MV.Meta;
using MV.Save;
using Newtonsoft.Json.Linq;

namespace MV.UI
{
    /// <summary>메타 명령 결과(화면용). <see cref="MvMetaResponse"/>를 화면 코드에 그대로 노출하지 않는다.</summary>
    public readonly struct MetaResult
    {
        public readonly bool Ok;
        /// <summary>거절 코드(<see cref="MvMetaErrors"/>). 성공이면 null.</summary>
        public readonly string Code;
        /// <summary>화면에 쓰는 정수 부가 값(부활 조각 수 등).</summary>
        public readonly long Value;

        public MetaResult(bool ok, string code, long value = 0) { Ok = ok; Code = code; Value = value; }

        public static MetaResult Success(long value = 0) => new MetaResult(true, null, value);
        public static MetaResult Fail(string code) => new MetaResult(false, code ?? MvMetaErrors.BadParams);
        public override string ToString() => Ok ? "OK " + Value : Code;
    }

    /// <summary>
    /// 메타 화면이 쓰는 진행 읽기·명령의 좁은 입구(G3-INT 규칙: 모든 진행 변경은 <see cref="MvMeta.Current"/> API로만).
    /// 기본 구현은 <see cref="MvMetaUiPort"/>. 시험은 가짜를 넣는다.
    /// <see cref="Ready"/>가 false면 화면은 모든 행동 버튼을 막는다(<see cref="MetaPanelPresenter"/>).
    /// </summary>
    public interface IMetaUiPort
    {
        bool Ready { get; }
        /// <summary>메타가 없으면 <see cref="MvTables.Current"/>(목록은 그대로 그리고 버튼만 막는다).</summary>
        MvTables Tables { get; }
        /// <summary><c>save.area</c>(0기반).</summary>
        int AreaIndex { get; }
        /// <summary><c>save.cp</c>.</summary>
        int Cp { get; }
        long Wealth(int wealthId);

        int Level(MvGrowth growth, string key);
        bool IsMax(MvGrowth growth, string key);
        IReadOnlyList<ConsumeData> NextPrice(MvGrowth growth, string key);
        bool CanBuy(MvGrowth growth, string key);
        MetaResult Buy(MvGrowth growth, string key);

        string SelectedGod { get; }
        bool GodUnlocked(string key);
        MetaResult SelectGod(string key);

        int RebirthLevel { get; }
        /// <summary>부활하면 받는 쌓인 목숨 조각(<c>save.rb.pending</c>).</summary>
        int RebirthPending { get; }
        bool RebirthOpen { get; }
        long RebirthCooldownLeftMs { get; }
        bool RebirthArmed { get; }
        /// <summary>부활 버튼 1번. 첫 누름(확인 대기 시작)이면 null, 부활했으면 Value = 받은 조각.</summary>
        MetaResult? PressRebirth();

        /// <summary>오늘 남은 광고 횟수. 한도 없음 −1.</summary>
        int AdsLeft(string key);
        void WatchAdForReward(string key, Action<MetaResult> done);

        bool IsOpen(string key);
        IReadOnlyList<string> PendingUnlockShows();
        MetaResult MarkUnlockSeen(string key);

        /// <summary>개발 빌드 테스트 패널 요청(<see cref="MvDebugRequests"/>). 판정은 메타 백엔드가 한다.</summary>
        MetaResult SendDebug(string requestName, string key = null, long amount = 0);
        /// <summary>광고 공급자(테스트 패널의 가짜 광고 결과 강제용). 메타가 없으면 null.</summary>
        IMvAdService Ads { get; }
    }

    /// <summary>
    /// 테스트 패널 요청 이름. 로컬 백엔드에 처리기가 아직 없다 → 「MV-META 변경 요청」(처리기가 생기기 전에는 <c>meta.unknownRequest</c>).
    /// 진행 값을 화면이 직접 쓰지 않도록 요청으로만 보낸다.
    /// </summary>
    public static class MvDebugRequests
    {
        /// <summary>key = 재화 id 문자열, amount = 더할 양.</summary>
        public const string Grant = "debug.grant";
        public const string LevelUp = "debug.levelUp";
        public const string ResetProgress = "debug.reset";
        public const string OpenRebirth = "debug.openRebirth";
        public const string ClearRebirthCooldown = "debug.clearCooldown";
        public const string UnlockAll = "debug.unlockAll";
        public const string ResetDaily = "debug.resetDaily";
        /// <summary>amount = 더할 초(시계 보정).</summary>
        public const string AddTime = "debug.addTime";
    }

    /// <summary><see cref="MvMeta.Current"/>를 매번 읽는 기본 입구(저장을 다시 열어 인스턴스가 바뀌어도 따라간다).</summary>
    public sealed class MvMetaUiPort : IMetaUiPort
    {
        public static readonly MvMetaUiPort Instance = new MvMetaUiPort();

        static MvMeta M => MvMeta.Current;
        static MvSaveStore S => M?.Store;

        public bool Ready => M != null && M.IsReady;
        public MvTables Tables => M?.Tables ?? MvTables.Current;
        public int AreaIndex => Ready ? S.Progress.Area : 0;
        public int Cp => Ready ? S.Progress.Cp : 0;
        public long Wealth(int wealthId)
        {
            if (!Ready) return 0;
            string type = MvWealthIds.TypeOf(wealthId);
            return type == null ? 0 : S.Wealth.Get(type);
        }

        public int Level(MvGrowth growth, string key) => M?.Level(growth, key) ?? 0;
        public bool IsMax(MvGrowth growth, string key) => M == null || M.IsMax(growth, key);
        public IReadOnlyList<ConsumeData> NextPrice(MvGrowth growth, string key) => M?.NextPrice(growth, key);
        public bool CanBuy(MvGrowth growth, string key) => Ready && M.CanBuy(growth, key);
        public MetaResult Buy(MvGrowth growth, string key) => Ready ? Of(M.Buy(growth, key)) : MetaResult.Fail(MvMetaErrors.NoStore);

        public string SelectedGod => Ready ? S.God.Selected : null;
        public bool GodUnlocked(string key) => Ready && M.GodUnlocked(key);
        public MetaResult SelectGod(string key) => Ready ? Of(M.SelectGod(key)) : MetaResult.Fail(MvMetaErrors.NoStore);

        public int RebirthLevel => Ready ? S.Rebirth.Lv : 0;
        public int RebirthPending => Ready ? S.Rebirth.Pending : 0;
        public bool RebirthOpen => Ready && M.RebirthOpen;
        public long RebirthCooldownLeftMs => Ready ? M.RebirthCooldownLeftMs : 0;
        public bool RebirthArmed => Ready && M.RebirthArmed;

        public MetaResult? PressRebirth()
        {
            if (!Ready) return MetaResult.Fail(MvMetaErrors.NoStore);
            MvMetaResponse r = M.PressRebirth();
            if (r == null) return null;
            return r.Ok ? MetaResult.Success(r.Result.Value<long>("got")) : MetaResult.Fail(r.Code);
        }

        public int AdsLeft(string key) => Ready ? M.AdsLeft(key) : 0;

        public void WatchAdForReward(string key, Action<MetaResult> done)
        {
            if (!Ready) { done?.Invoke(MetaResult.Fail(MvMetaErrors.NoStore)); return; }
            M.WatchAdForReward(key, r => done?.Invoke(Of(r)));
        }

        public bool IsOpen(string key) => Ready && M.IsOpen(key);
        public IReadOnlyList<string> PendingUnlockShows() => Ready ? M.PendingUnlockShows() : Array.Empty<string>();
        public MetaResult MarkUnlockSeen(string key) => Ready ? Of(M.MarkSeen(new[] { key })) : MetaResult.Fail(MvMetaErrors.NoStore);

        public MetaResult SendDebug(string requestName, string key = null, long amount = 0)
        {
            if (!Ready) return MetaResult.Fail(MvMetaErrors.NoStore);
            var p = new JObject();
            if (key != null) p["key"] = key;
            if (amount != 0) p["amount"] = amount;
            return Of(M.Send(new MvMetaRequest(requestName, p)));
        }

        public IMvAdService Ads => M?.Ads;

        static MetaResult Of(MvMetaResponse r) => r == null ? MetaResult.Fail(null) : r.Ok ? MetaResult.Success() : MetaResult.Fail(r.Code);
    }
}
