using System;
using MV.Data;
using MV.Meta;
using MV.Save;

namespace MV.UI
{
    /// <summary>
    /// 테스트 패널의 런 쪽 연결 지점(배속·밸런스 프리셋). 런 쪽(MV-RUN·MV-PLAY-INT)이 넣는다. 비면 그 버튼은 꺼진다.
    /// </summary>
    public static class MvDebugHooks
    {
        public static Action<float> SetTimeScale;
        /// <summary><c>06_balancePreset.key</c>.</summary>
        public static Action<string> SelectBalance;
    }

    /// <summary>
    /// 테스트 패널 화면 코드(05 §5.14, 개발 빌드 전용). 진행·재화를 바꾸는 버튼은 모두 <see cref="IMetaUiPort.SendDebug"/>(메타 요청)로만 보낸다.
    /// 밸런스 행 버튼은 <c>06_balancePreset</c>(sort)에서 만든다. 연사 슬라이더·카드 시험·캡처·표 다시 읽기는 런 쪽 계약 몫(1차 없음).
    /// </summary>
    public sealed class DebugPanelPresenter : MetaPanelPresenter
    {
        public const string ResetId = "progress.reset";

        bool _resetArmed;

        protected override void OnOpen() => _resetArmed = false;

        protected override void Build(MetaPanelModel m, MvTables tables)
        {
            m.Title = "테스트"; // ui.debug.btn
            bool speed = MvDebugHooks.SetTimeScale != null;
            m.Add(new MetaRow("speed", MetaRowKind.Row, "배속"))
                .Act("speed.1", "×1", speed).Act("speed.2", "×2", speed).Act("speed.3", "×3", speed);

            var bal = m.Add(new MetaRow("balance", MetaRowKind.Row, "밸런스"));
            foreach (var p in tables.BalancePreset.Sorted)
                if (p.active) bal.Act("bal." + p.key, NameOf(p.id, p.key), MvDebugHooks.SelectBalance != null);

            m.Add(new MetaRow("wealth", MetaRowKind.Row, "재화"))
                .Act("grant.gold", "츄르 +500", true).Act("grant.souls", "생선 +50", true);
            m.Add(new MetaRow("progress", MetaRowKind.Row, "진행"))
                .Act("progress.level", "레벨 +1", true).Act(ResetId, _resetArmed ? "정말 초기화" : "초기화", true);
            m.Add(new MetaRow("rebirth", MetaRowKind.Row, "부활·캣닢"))
                .Act("rebirth.open", "부활 열기", true).Act("rebirth.cd", "쿨타임 해제", true)
                .Act("grant.dia", "캣닢 +300", true).Act("grant.token", "조각 +200", true);
            m.Add(new MetaRow("content", MetaRowKind.Row, "컨텐츠"))
                .Act("content.all", "전부 해금", true).Act("content.daily", "하루 초기화", true);

            var fake = Port.Ads as MvFakeAdService;
            m.Add(new MetaRow("ad", MetaRowKind.Row, "광고 결과") { Value = fake == null ? "가짜 광고 아님" : fake.NextRewardResult ? "성공" : "실패" })
                .Act("ad.ok", "성공", fake != null).Act("ad.fail", "실패", fake != null);
            m.Add(new MetaRow("time", MetaRowKind.Row, "시간")).Act("time.hour", "+1시간", true);
        }

        protected override bool OnAction(string id)
        {
            if (id != ResetId) _resetArmed = false;
            switch (id)
            {
                case "speed.1": MvDebugHooks.SetTimeScale?.Invoke(1); return true;
                case "speed.2": MvDebugHooks.SetTimeScale?.Invoke(2); return true;
                case "speed.3": MvDebugHooks.SetTimeScale?.Invoke(3); return true;
                case "grant.gold": return Send(MvDebugRequests.Grant, MvWealthIds.Gold, 500);
                case "grant.souls": return Send(MvDebugRequests.Grant, MvWealthIds.Souls, 50);
                case "grant.dia": return Send(MvDebugRequests.Grant, MvWealthIds.Dia, 300);
                case "grant.token": return Send(MvDebugRequests.Grant, MvWealthIds.RbToken, 200);
                case "progress.level": return Send(MvDebugRequests.LevelUp);
                case ResetId:
                    if (!_resetArmed) { _resetArmed = true; return true; } // 2번 누름
                    _resetArmed = false;
                    return Send(MvDebugRequests.ResetProgress);
                case "rebirth.open": return Send(MvDebugRequests.OpenRebirth);
                case "rebirth.cd": return Send(MvDebugRequests.ClearRebirthCooldown);
                case "content.all": return Send(MvDebugRequests.UnlockAll);
                case "content.daily": return Send(MvDebugRequests.ResetDaily);
                case "time.hour": return Send(MvDebugRequests.AddTime, 0, 3600);
                case "ad.ok":
                case "ad.fail":
                    if (!(Port.Ads is MvFakeAdService fake)) return false;
                    fake.NextRewardResult = id == "ad.ok";
                    return true;
            }
            if (id.StartsWith("bal.") && MvDebugHooks.SelectBalance != null)
            {
                MvDebugHooks.SelectBalance(id.Substring(4));
                return true;
            }
            return false;
        }

        bool Send(string request, int wealthId = 0, long amount = 0)
        {
            var r = Port.SendDebug(request, wealthId != 0 ? wealthId.ToString() : null, amount);
            if (r.Ok) Say("debug: " + request);
            else Say(r.Code == MvMetaErrors.UnknownRequest ? "미구현(MV-META 처리기 대기): " + request : r.Code); // toastMessage:debug.*
            return r.Ok;
        }
    }
}
