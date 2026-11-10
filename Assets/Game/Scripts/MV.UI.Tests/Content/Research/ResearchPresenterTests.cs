using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MV.Data;
using MV.Meta;
using NUnit.Framework;

namespace MV.UI.Tests
{
    /// <summary>
    /// 연구 화면(<c>UIPopup_Research</c>, 08 §8.5.3, 05 §5.16): 탭 = 계열, 본문 = 단계 목록, 다음 단계 칸 버튼(올리기·받기·진행 중·다른 연구 중),
    /// 누름 1번 = 메타 요청 1번, 잠김·메타 없음이면 버튼 꺼짐, 표 행 추가 → 탭·단계 칸 추가(12 §12.6-C).
    /// </summary>
    public sealed class ResearchPresenterTests
    {
        const long Now = 1_700_000_000_000L;

        static ResearchPresenter Open(ResearchFakePort port)
        {
            var p = new ResearchPresenter();
            p.SetPort(port);
            p.Open();
            return p;
        }

        static int Steps(MetaPanelModel m) => m.Rows.Count(r => r.Id.StartsWith(ResearchPresenter.StepPrefix, StringComparison.Ordinal));

        static MvTables WithRow(string kind)
        {
            string row = kind == "group" ? "440006001\tresearch test\t6\t1\t\tCritChance\t3\t1\t1001002\t10\t50\t1"
                : "440001006\tresearch test\t1\t6\t\tDmg\t15\t1\t1001002\t60\t6000\t1";
            string dir = MvTableLoader.TsvDirectory;
            string text = File.ReadAllText(Path.Combine(dir, ResearchData.Table + ".txt"), new UTF8Encoding(false)).Replace("\r\n", "\n").TrimEnd('\n');
            var t = new MvTables();
            t.LoadAll(new MvMemoryTableSource(new MvTsvDirectorySource(dir)).Set(ResearchData.Table, text + "\n" + row + "\n"));
            return t;
        }

        [TestCase("none", 5, 5)]
        [TestCase("group", 6, 5)]
        [TestCase("level", 5, 6)]
        public void TableRowAdded_AddsTabOrStep(string extra, int tabs, int stepsInGroup1)
        {
            var tables = extra == "none" ? MetaUiTestKit.Base() : WithRow(extra);
            var m = Open(new ResearchFakePort(tables)).Model;
            Assert.That(m.Tabs.Count, Is.EqualTo(tabs));
            Assert.That(m.Tabs[0].Selected, Is.True, "처음은 첫 계열");
            Assert.That(Steps(m), Is.EqualTo(stepsInGroup1));
        }

        [Test]
        public void Idle_StartButtonOnNextStepOnly_PressSendsOneRequest()
        {
            var port = new ResearchFakePort(MetaUiTestKit.Base());
            port.Levels[1] = 2;
            var p = Open(port);
            Assert.That(p.Model.FindAction(ResearchPresenter.DonePrefix + "2").Enabled, Is.False, "받은 단계는 완료 표시");
            Assert.That(p.Model.FindRow(ResearchPresenter.StepPrefix + "3").Highlight, Is.True);
            Assert.That(p.Model.FindRow(ResearchPresenter.StepPrefix + "4").Locked, Is.True);
            Assert.That(p.Model.AllActions().Count(a => a.Enabled), Is.EqualTo(1));

            Assert.That(p.Press(ResearchPresenter.StartPrefix + "1"), Is.True);
            Assert.That(port.Calls, Is.EqualTo(new[] { "start:1" }));
        }

        [TestCase(-1, "wait:", false)]
        [TestCase(0, "claim:", true)]
        public void Running_ShowsWaitUntilFinish_ThenClaim(int nowMinusFinish, string prefix, bool enabled)
        {
            var port = new ResearchFakePort(MetaUiTestKit.Base()) { Run = new MvResearchRun(1, Now - nowMinusFinish) };
            var p = Open(port);
            var a = p.Model.FindAction(prefix + "1");
            Assert.That(a, Is.Not.Null);
            Assert.That(a.Enabled, Is.EqualTo(enabled));
            Assert.That(p.Press(prefix + "1"), Is.EqualTo(enabled));
            Assert.That(port.Calls.Count, Is.EqualTo(enabled ? 1 : 0));
            if (enabled) Assert.That(port.Calls[0], Is.EqualTo("claim:1"));
        }

        [Test]
        public void OtherGroupRunning_TabShowsBusy()
        {
            var port = new ResearchFakePort(MetaUiTestKit.Base()) { Run = new MvResearchRun(3, Now + 10_000) };
            var p = Open(port);
            Assert.That(p.Model.FindAction(ResearchPresenter.BusyPrefix + "1").Enabled, Is.False);
            p.SelectTab(ResearchPresenter.TabPrefix + "3");
            Assert.That(p.Group, Is.EqualTo(3));
            Assert.That(p.Model.FindAction(ResearchPresenter.WaitPrefix + "3"), Is.Not.Null);
        }

        [TestCase(true, false, true)]  // 잠김: 안내 칸 + 버튼 꺼짐
        [TestCase(false, true, false)] // 메타 없음: 모두 꺼짐
        [TestCase(false, false, false)] // 비용 부족
        public void StartDisabled_WhenLockedOrNotReadyOrPoor(bool locked, bool notReady, bool lockedRow)
        {
            var port = new ResearchFakePort(MetaUiTestKit.Base()) { Open = !locked, Ready = !notReady, Afford = locked || notReady };
            var p = Open(port);
            Assert.That(p.Model.FindAction(ResearchPresenter.StartPrefix + "1").Enabled, Is.False);
            Assert.That(p.Model.FindRow(ResearchPresenter.LockedId) != null, Is.EqualTo(lockedRow));
            Assert.That(p.Press(ResearchPresenter.StartPrefix + "1"), Is.False);
            Assert.That(port.Calls, Is.Empty);
        }

        [Test]
        public void RejectedRequest_ShowsToast()
        {
            var port = new ResearchFakePort(MetaUiTestKit.Base()) { Reject = MvMetaErrors.Busy };
            var p = Open(port);
            var toasts = new List<string>();
            p.Toast += toasts.Add;
            Assert.That(p.Press(ResearchPresenter.StartPrefix + "1"), Is.False);
            Assert.That(toasts, Is.EqualTo(new[] { ResearchPresenter.FailText(MvMetaErrors.Busy) }));
        }
    }

    /// <summary>메타 입구 + 연구 입구 가짜. 요청은 기록만 한다.</summary>
    internal sealed class ResearchFakePort : IMetaUiPort, IResearchUiPort
    {
        public bool Ready { get; set; } = true;
        public MvTables Tables { get; set; }
        public int AreaIndex => 4;
        public int Cp => 0;
        public long Wealth(int wealthId) => 100;

        public bool Open = true;
        public bool Afford = true;
        public string Reject;
        public MvResearchRun Run = MvResearchRun.None;
        public readonly Dictionary<int, int> Levels = new Dictionary<int, int>();
        public readonly List<string> Calls = new List<string>();

        public ResearchFakePort(MvTables tables) { Tables = tables; }

        // IResearchUiPort
        public long NowMs => 1_700_000_000_000L;
        public int ResearchLevel(int group) => Levels.TryGetValue(group, out var v) ? v : 0;
        public MvResearchRun ResearchRun => Run;
        public bool CanAfford(IReadOnlyList<ConsumeData> price) => Afford;
        public MetaResult StartResearch(int group) => Call("start:" + group);
        public MetaResult ClaimResearch(int group) => Call("claim:" + group);

        MetaResult Call(string what)
        {
            if (Reject != null) return MetaResult.Fail(Reject);
            Calls.Add(what);
            return MetaResult.Success();
        }

        // IMetaUiPort(연구 화면이 쓰지 않는 것은 기본값)
        public int Level(MvGrowth growth, string key) => 0;
        public bool IsMax(MvGrowth growth, string key) => false;
        public IReadOnlyList<ConsumeData> NextPrice(MvGrowth growth, string key) => null;
        public bool CanBuy(MvGrowth growth, string key) => false;
        public MetaResult Buy(MvGrowth growth, string key) => MetaResult.Fail(null);
        public string SelectedGod => "thunder";
        public bool GodUnlocked(string key) => true;
        public MetaResult SelectGod(string key) => MetaResult.Fail(null);
        public int RebirthLevel => 0;
        public int RebirthPending => 0;
        public bool RebirthOpen => false;
        public long RebirthCooldownLeftMs => 0;
        public bool RebirthArmed => false;
        public MetaResult? PressRebirth() => null;
        public int AdsLeft(string key) => 0;
        public void WatchAdForReward(string key, Action<MetaResult> done) => done?.Invoke(MetaResult.Fail(null));
        public bool IsOpen(string key) => Open;
        public IReadOnlyList<string> PendingUnlockShows() => Array.Empty<string>();
        public MetaResult MarkUnlockSeen(string key) => MetaResult.Success();
        public MetaResult SendDebug(string requestName, string key = null, long amount = 0) => MetaResult.Fail(null);
        public IMvAdService Ads => null;
    }
}
