using System;
using System.Collections.Generic;
using System.Linq;
using MV.Data;
using MV.Meta;
using MV.Run;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MV.Content
{
    /// <summary>퀘스트 카운터 1건(<see cref="MvMetaRequestNames.QuestRecord"/> ev 한 칸).</summary>
    public readonly struct MvQuestEvent
    {
        public readonly QuestClearType Type;
        public readonly int Condition;
        public readonly long Amount;

        public MvQuestEvent(QuestClearType type, long amount, int condition = 0)
        {
            Type = type; Amount = amount; Condition = condition;
        }

        public JObject ToJson() => new JObject { ["t"] = (int)Type, ["c"] = Condition, ["v"] = Amount };
    }

    /// <summary>
    /// 퀘스트 로컬 백엔드 처리기(<see cref="MvMetaRequestNames.QuestRecord"/>·<see cref="MvMetaRequestNames.QuestClaim"/>).
    /// 상태를 바꾸지 않고 새 값만 돌려준다(반영은 <see cref="MvMeta"/>). 서버가 생기면 같은 이름의 서버 함수가 받는다.
    /// </summary>
    public static class MvQuestBackend
    {
        /// <param name="isOpen">해금 확인(05_contentOpen key). null이면 확인하지 않는다.</param>
        public static void Register(MvLocalMetaBackend backend, Func<string, bool> isOpen)
        {
            if (backend == null) throw new ArgumentNullException(nameof(backend));
            backend.RegisterContent(MvMetaRequestNames.QuestRecord, p =>
                MvQuestRules.Record(Snapshot(backend), p["ev"] as JArray));
            backend.RegisterContent(MvMetaRequestNames.QuestClaim, p =>
            {
                int id = p.Value<int?>("id") ?? 0;
                if (id <= 0) return MvMetaResponse.Reject(MvMetaErrors.BadParams, "id");
                return MvQuestRules.Claim(backend.Tables, Snapshot(backend), backend.Resources, id, isOpen);
            });
        }

        static MvQuestSnapshot Snapshot(MvLocalMetaBackend b) => MvQuestRules.Read(b.Store.Quest, b.Clock.NowMs);
    }

    /// <summary>
    /// 퀘스트 입구(08 §8.5.1). 화면은 <see cref="Board"/>·<see cref="Claim"/>만 쓴다.
    /// 메타 요청 결과(<see cref="MvMeta.Applied"/>)에서 정산·강화·부활·광고 카운터를 세고, 런 안 값(계단·벽)은 <see cref="RecordRun"/>,
    /// 모드 클리어는 <see cref="RecordModeClear"/>로 받는다. 진행 변경은 모두 메타 요청이다(직접 저장 없음).
    /// Boot 연결: 메타를 만들 때마다 <c>MvQuest.Install(meta)</c> 한 줄.
    /// </summary>
    public sealed class MvQuest : IDisposable
    {
        public static MvQuest Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        readonly MvMeta _meta;
        bool _disposed;

        public MvMeta Meta => _meta;

        /// <summary>처리기 등록 + 카운터 연결. 이전 <see cref="Current"/>는 끊는다.</summary>
        public static MvQuest Install(MvMeta meta)
        {
            Current?.Dispose();
            return Current = new MvQuest(meta);
        }

        public MvQuest(MvMeta meta)
        {
            _meta = meta ?? throw new ArgumentNullException(nameof(meta));
            MvQuestBackend.Register(meta.Local, meta.IsOpen);
            meta.Applied += OnApplied;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _meta.Applied -= OnApplied;
            if (ReferenceEquals(Current, this)) Current = null;
        }

        public bool IsReady => !_disposed && _meta.IsReady;
        public long NowMs => _meta.Clock.NowMs;

        // ───────── 읽기 ─────────

        public MvQuestSnapshot Snapshot() => MvQuestRules.Read(_meta.Store?.Quest, NowMs);

        /// <summary>1차 묶음 전부(410 순서).</summary>
        public List<MvQuestGroupView> Board() => IsReady ? MvQuestRules.Board(_meta.Tables, Snapshot(), NowMs) : new List<MvQuestGroupView>();

        public bool IsOpen(QuestCategory category) => _meta.IsOpen(MvQuestRules.UnlockKey(category));

        /// <summary>받을 수 있는 칸이 있는지(로비 빨간 점). 잠긴 분류는 뺀다.</summary>
        public bool HasClaimable() => Board().Any(g => IsOpen(g.Row.category) && g.Entries.Any(e => e.State == MvQuestState.Claimable));

        // ───────── 명령 ─────────

        public MvMetaResponse Claim(int id) => _meta.Send(new MvMetaRequest(MvMetaRequestNames.QuestClaim, new JObject { ["id"] = id }));

        public MvMetaResponse Record(IEnumerable<MvQuestEvent> events)
        {
            var ev = new JArray(events.Where(e => e.Amount > 0).Select(e => (object)e.ToJson()).ToArray());
            if (ev.Count == 0) return MvMetaResponse.Success(new JObject());
            return _meta.Send(new MvMetaRequest(MvMetaRequestNames.QuestRecord, new JObject { ["ev"] = ev }));
        }

        public MvMetaResponse Record(params MvQuestEvent[] events) => Record((IEnumerable<MvQuestEvent>)events);

        /// <summary>
        /// 본편 런 안 값(정산 시점, <c>RunResult</c>): 계단 오르기 칸 수(<c>Climbed</c>), 벽 파괴(<c>Walls</c>).
        /// 제작 게이트 무기 수(weaponCraft)는 <c>RunResult</c>에 없어 <paramref name="weaponsCrafted"/>로 받는다(MV-RUN 요청).
        /// 모드 런은 부르지 않는다(「본편」 카운터).
        /// </summary>
        public MvMetaResponse RecordRun(RunResult result, int weaponsCrafted = 0)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            return Record(new MvQuestEvent(QuestClearType.StairClimb, result.Climbed), new MvQuestEvent(QuestClearType.WallBreak, result.Walls),
                new MvQuestEvent(QuestClearType.WeaponCraft, weaponsCrafted));
        }

        /// <summary>모드 클리어 1번(clearCondition = contentType).</summary>
        public MvMetaResponse RecordModeClear(ContentType type) => Record(new MvQuestEvent(QuestClearType.ModeClear, 1, (int)type));

        // ───────── 메타 요청 → 카운터 ─────────

        /// <summary>성공한 메타 요청에서 셀 카운터(순수 함수, 시험용 공개).</summary>
        public static List<MvQuestEvent> EventsOf(MvMetaRequest request, MvMetaResponse response)
        {
            var list = new List<MvQuestEvent>();
            if (request == null || response == null || !response.Ok) return list;
            JObject r = response.Result;
            switch (request.Name)
            {
                case MvMetaRequestNames.Settle: // collect L2424·L2430
                    list.Add(new MvQuestEvent(QuestClearType.RunComplete, 1));
                    list.Add(new MvQuestEvent(QuestClearType.GoldEarn, r.Value<long?>("gold") ?? 0));
                    list.Add(new MvQuestEvent(QuestClearType.SoulEarn, r.Value<long?>("souls") ?? 0));
                    if (r.Value<bool?>("pass") == true) list.Add(new MvQuestEvent(QuestClearType.AreaOpen, 1));
                    break;
                case MvMetaRequestNames.BuyLobbyUp: // L2641, 조건 1 캣타워 / 2 스크래쳐 / 3 생선 건조대
                    list.Add(new MvQuestEvent(QuestClearType.LobbyUpgrade, 1, LobbyCondition(request.Params.Value<string>("key"))));
                    break;
                case MvMetaRequestNames.BuyAreaUp: // L2530
                    list.Add(new MvQuestEvent(QuestClearType.AreaUpgrade, 1));
                    break;
                case MvMetaRequestNames.Rebirth: // L2559
                    list.Add(new MvQuestEvent(QuestClearType.Rebirth, 1));
                    break;
                case MvMetaRequestNames.GrantAd:
                case MvMetaRequestNames.UseAd: // 보상 광고 끝까지 봄
                    list.Add(new MvQuestEvent(QuestClearType.AdWatch, 1));
                    break;
            }
            list.RemoveAll(e => e.Amount <= 0);
            return list;
        }

        public static int LobbyCondition(string key)
        {
            switch (key)
            {
                case MvMetaRules.LobbyStair: return 1;
                case MvMetaRules.LobbyWeapon: return 2;
                case MvMetaRules.LobbyIncome: return 3;
                default: return 0;
            }
        }

        void OnApplied(MvMetaRequest request, MvMetaResponse response)
        {
            if (_disposed) return;
            var events = EventsOf(request, response);
            if (events.Count == 0) return;
            var r = Record(events);
            if (!r.Ok) Debug.LogWarning("[Quest] 카운터 기록 실패 " + r + " ← " + request.Name);
        }
    }
}
