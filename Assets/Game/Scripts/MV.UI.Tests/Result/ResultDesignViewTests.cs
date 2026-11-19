using System;
using System.Collections.Generic;
using System.Linq;
using MV.Data;
using MV.Meta;
using MV.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MV.UI.Tests
{
    /// <summary>
    /// MV-UI3-RESULT: 디자이너 Popup_LevelComplete를 결과 팝업 겉(UIPopup_Result 래퍼)으로 쓰기 —
    /// 직렬화 참조, 보상 칸(Slot_Item 풀·아이콘·등급 틀·「x{0}」), 진척도(이전 → 새 값, % 버림), Backboard 터치 = ×1, 광고 버튼 = ×N, 등급 표(15_grade).
    /// 화면 배치·연출 확인은 사람 확인(NOT_RUN 별도).
    /// </summary>
    public sealed class ResultDesignViewTests
    {
        const string PopupPath = "Assets/Game/Addressables/Local/UI/Result/UIPopup_Result.prefab";
        const string SlotPath = "Assets/Game/Art/UI/Prefab/Common/Slot_Item.prefab";
        static MvTables T => MetaUiTestKit.Base();

        readonly List<GameObject> _made = new List<GameObject>();

        sealed class Settler : IResultSettler
        {
            public readonly List<int> Muls = new List<int>();
            public MvSettleResult Settle(MvRunOutcome outcome, int mul) { Muls.Add(mul); return new MvSettleResult { Mul = mul }; }
        }

        sealed class Watcher : IResultAdWatcher
        {
            public bool Ready { get; set; } = true;
            public int Mul => 2;
            public int Calls;
            public void WatchX3(Action<int> done) { Calls++; }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        (GameObject root, ResultDesignView view) NewPopup()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupPath);
            Assert.That(prefab, Is.Not.Null, PopupPath);
            var go = Object.Instantiate(prefab);
            _made.Add(go);
            var view = go.GetComponent<ResultDesignView>();
            Assert.That(view, Is.Not.Null, "래퍼 루트의 ResultDesignView");
            return (go, view);
        }

        static ResultViewData Data(Settler s, Watcher w, int savedSouls = 40)
        {
            var r = new RunResult { Reason = FinishReason.Gate, Gold = 1500, Dia = 3, Souls = 20, Tok = 2 };
            var d = ResultViewData.Create(r, 5, 0, savedSouls, T);
            d.Settler = s;
            d.AdWatcher = w;
            return d;
        }

        ResultPresenter Open(ResultDesignView view, ResultViewData d)
        {
            var p = new ResultPresenter();
            p.SetView(view);
            view.CollectPressed += () => p.TryCollect(1);
            view.X3Pressed += () => p.TryCollectX3();
            p.Open(d);
            return p;
        }

        [Test]
        public void Prefabs_NestDesignerPopup_WithAllReferences()
        {
            var (go, view) = NewPopup();
            Assert.That(view.MissingRefs(), Is.Empty, "ResultDesignView 참조");
            var ctrl = go.GetComponent("ResultPopup"); // 형식 이름으로 찾는다(UIPopup 어셈블리를 시험 asmdef에 더하지 않음)
            Assert.That(ctrl, Is.Not.Null, "ResultPopup 컨트롤러");
            var so = new SerializedObject(ctrl);
            Assert.That(so.FindProperty("view").objectReferenceValue, Is.SameAs(view), "ResultPopup.view = ResultDesignView");
            Assert.That(so.FindProperty("transition").objectReferenceValue, Is.Not.Null, "디자이너 UITransition");
            Assert.That(go.transform.Find("Popup_LevelComplete"), Is.Not.Null, "중첩 디자이너 Popup_LevelComplete");
            Assert.That(go.transform.GetChild(0).name, Is.EqualTo("Backboard"), "Backboard가 디자이너 팝업 뒤(먼저)");
            Assert.That(view.AdButton.GetComponent<Image>(), Is.Not.Null, "광고 버튼 = 디자이너 Button_RewardAds 위에 붙인 Button");

            var slot = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPath).GetComponent<ItemSlot>();
            Assert.That(slot, Is.Not.Null, "Slot_Item 루트의 ItemSlot(공용 부품)");
            Assert.That(slot.MissingRefs(), Is.Empty);
        }

        [Test]
        public void DecorDoesNotBlockBackboard_ExceptAdButton()
        {
            var (go, view) = NewPopup();
            Open(view, Data(new Settler(), new Watcher())); // EditMode에서는 Awake가 없어 처음 그릴 때 Init이 돈다
            var designer = go.transform.Find("Popup_LevelComplete");
            var blocking = designer.GetComponentsInChildren<Graphic>(true)
                .Where(g => g.raycastTarget && !g.transform.IsChildOf(view.AdButton.transform)).Select(g => g.name).ToList();
            Assert.That(blocking, Is.Empty, "디자이너 장식이 Backboard 터치를 가로챔");
        }

        [Test]
        public void Rewards_OneSlotPerItem_IconGradeCount()
        {
            var (_, view) = NewPopup();
            Open(view, Data(new Settler(), new Watcher()));
            Assert.That(view.Slots.Count, Is.EqualTo(3), "정본 3칸");
            Assert.That(view.Slots.Distinct().Count(), Is.EqualTo(3));
            Assert.That(view.Slots.All(s => s.gameObject.activeSelf), Is.True);
            Assert.That(view.Slots.Select(s => s.WealthType), Is.EqualTo(new[] { "gold", "dia", "goldenFish" }));
            Assert.That(view.Slots[0].IconSprite, Is.EqualTo(HudCurrencySlot.IconOf(T, "gold")), "아이콘 = 01_wealth.icon");
            Assert.That(view.Slots[1].CountText, Is.EqualTo("x3"), "노션 텍스트 포맷 x{0}");
            foreach (var s in view.Slots)
            {
                int grade = ItemSlot.GradeOf(T, s.WealthType);
                if (grade > 0) Assert.That(s.GradeSprite, Is.EqualTo(MvUiGrade.Frame(T, grade).sprite), s.WealthType + " 등급 틀");
            }

            view.SetRewards(ResultPresenter.Rewards(Data(new Settler(), new Watcher())).Take(1).ToList());
            Assert.That(view.Slots.Count, Is.EqualTo(1), "줄면 풀에 돌려준다");
            view.SetRewards(ResultPresenter.Rewards(Data(new Settler(), new Watcher())));
            Assert.That(view.Slots.Count, Is.EqualTo(3));
        }

        [Test]
        public void Progress_StartsAtBefore_EndsAtAfter()
        {
            var (_, view) = NewPopup();
            var d = Data(new Settler(), new Watcher(), savedSouls: 40);
            Open(view, d);
            var p = ResultPresenter.Progress(d);
            Assert.That(p.To01, Is.GreaterThan(p.From01), "이번 런 생선만큼 오른다");
            Assert.That(view.ProgressValue, Is.EqualTo(p.From01).Within(1e-4f), "열린 직후 = 정산 전 값");
            view.CompleteFill();
            Assert.That(view.ProgressValue, Is.EqualTo(p.To01).Within(1e-4f));
            Assert.That(view.ProgressText, Is.EqualTo(p.Percent + "%"));
            Assert.That(view.AreaLabel.ZoneText, Does.Contain(d.ZoneNo.ToString()));
            Assert.That(view.AreaLabel.AreaText, Is.EqualTo(d.AreaName));
            Assert.That(view.FillDuration, Is.GreaterThan(0f), "연출 시간은 프리팹 직렬화 값");
        }

        [Test]
        public void Backboard_CollectsOnce_AdButton_WatchesAd()
        {
            var (_, view) = NewPopup();
            var s = new Settler();
            var w = new Watcher();
            Open(view, Data(s, w));
            Assert.That(view.AdButton.interactable, Is.True);
            Assert.That(view.AdText, Does.Contain("2x"), "배수 = 08_adPlacement mul(가짜 입구 2)");

            view.AdButton.onClick.Invoke();
            Assert.That(w.Calls, Is.EqualTo(1));
            Assert.That(view.Backboard.interactable, Is.False, "광고 중에는 받기 막음");
        }

        [Test]
        public void Backboard_Press_SettlesTimesOne()
        {
            var (_, view) = NewPopup();
            var s = new Settler();
            Open(view, Data(s, new Watcher()));
            view.Backboard.onClick.Invoke();
            view.Backboard.onClick.Invoke();
            Assert.That(s.Muls, Is.EqualTo(new[] { 1 }), "Backboard 터치 = ×1 받기 1번");
            Assert.That(view.Backboard.interactable, Is.False, "받은 뒤 막음");
        }

        [Test]
        public void Title_TextOnly_LevelDoneMatchesDesigner()
        {
            var (_, view) = NewPopup();
            var d = Data(new Settler(), new Watcher());
            d.Kind = ResultKind.LevelDone;
            Open(view, d);
            Assert.That(view.TitleText, Is.EqualTo(ResultPresenter.LevelDoneFallback));
            d.Kind = ResultKind.Fail;
            view.SetTitle(ResultPresenter.TitleText(d), Color.white);
            Assert.That(view.TitleText, Is.EqualTo("아지트로 귀환"));
        }

        [Test]
        public void GradeTable_WealthGradeIds_PointTo15Grade()
        {
            Assert.That(T.Grade.Datas.Count, Is.EqualTo(12), "노션 등급 표 1~12");
            Assert.That(T.Grade.Datas.Select(g => g.grade), Is.EqualTo(Enumerable.Range(1, 12)));
            foreach (var g in T.Grade.Datas)
                Assert.That(MvUiGrade.Frame(T, g.grade).sort, Is.EqualTo(g.grade), "09_uiResource 등급 틀 = 같은 번호");
            Assert.That(ItemSlot.GradeOf(T, "gold"), Is.GreaterThan(0), "골드 등급(임시값)");
            Assert.That(ItemSlot.GradeOf(T, "medal"), Is.EqualTo(0), "등급 없음 = 0");
        }
    }
}
