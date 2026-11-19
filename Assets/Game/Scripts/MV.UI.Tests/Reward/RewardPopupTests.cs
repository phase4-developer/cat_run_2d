using System.Collections.Generic;
using System.Linq;
using MV.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MV.UI.Tests
{
    /// <summary>
    /// MV-UI3-REWARD: 공용 보상획득 팝업(디자이너 Popup_Reward를 UIPopup_Reward 래퍼로) —
    /// 직렬화 참조, 칸 목록(같은 재화 합치기·정수 버림·재화만), Holder_Reward 풀, 순차 등장(0.2초), 등장 중 닫기 잠금, 장식 raycast.
    /// 화면 배치·연출 확인은 사람 확인(NOT_RUN 별도).
    /// </summary>
    public sealed class RewardPopupTests
    {
        const string PopupPath = "Assets/Game/Addressables/Local/UI/Common/UIPopup_Reward.prefab";
        static MvTables T => MetaUiTestKit.Base();

        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        (GameObject root, RewardDesignView view) NewPopup()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupPath);
            Assert.That(prefab, Is.Not.Null, PopupPath);
            var go = Object.Instantiate(prefab);
            _made.Add(go);
            var view = go.GetComponent<RewardDesignView>();
            Assert.That(view, Is.Not.Null, "래퍼 루트의 RewardDesignView");
            return (go, view);
        }

        static int Gold => T.Wealth.GetId("gold");
        static int Dia => T.Wealth.GetId("dia");
        static int Tok => T.Wealth.GetId("goldenFish");

        static ConsumeData W(int id, double v) => new ConsumeData(ConsumeType.Wealth, id, v);

        static List<ItemSlotData> Items(params ConsumeData[] r) => RewardItems.Build(T, r);

        [Test]
        public void Prefab_NestsDesignerPopup_WithAllReferences()
        {
            var (go, view) = NewPopup();
            Assert.That(view.MissingRefs(), Is.Empty, "RewardDesignView 참조");
            var ctrl = go.GetComponent("RewardPopup"); // 형식 이름으로 찾는다(UIPopup 어셈블리를 시험 asmdef에 더하지 않음)
            Assert.That(ctrl, Is.Not.Null, "RewardPopup 컨트롤러");
            var so = new SerializedObject(ctrl);
            Assert.That(so.FindProperty("view").objectReferenceValue, Is.SameAs(view), "RewardPopup.view = RewardDesignView");
            Assert.That(so.FindProperty("PopupType").stringValue, Is.EqualTo(MvUiIds.Reward));
            Assert.That(go.transform.Find("Popup_Reward"), Is.Not.Null, "중첩 디자이너 Popup_Reward");
            Assert.That(go.transform.GetChild(0).name, Is.EqualTo("Backboard"), "Backboard가 디자이너 팝업 뒤(먼저)");
            Assert.That(view.RevealInterval, Is.EqualTo(0.2f).Within(1e-4f), "노션 「0.2s 내외」 = 프리팹 직렬화 값");
            Assert.That(view.MaxCloseLock, Is.GreaterThan(0f));
        }

        [Test]
        public void Addressables_RegistersPopupAddress()
        {
            // 시험 asmdef에 Addressables 에디터 어셈블리를 더하지 않고 그룹 자산 글자로 확인한다.
            string guid = AssetDatabase.AssetPathToGUID(PopupPath);
            Assert.That(guid, Is.Not.Empty);
            string text = System.IO.File.ReadAllText("Assets/AddressableAssetsData/AssetGroups/Local_UI.asset");
            Assert.That(text, Does.Contain("m_GUID: " + guid + "
    m_Address: UIPopup_" + MvUiIds.Reward).Or.Contain("m_GUID: " + guid + "
    m_Address: UIPopup_" + MvUiIds.Reward),
                "Local_UI 항목, 주소 = 파일 이름");
        }

        [Test]
        public void Items_MergeSameWealth_FloorAndSkipBelowOne_KeepOrder()
        {
            var list = Items(W(Dia, 3), W(Gold, 100.9), W(Dia, 2), W(Tok, 0.5));
            Assert.That(list.Select(i => i.WealthType), Is.EqualTo(new[] { "dia", "gold" }), "처음 나온 순서, 1 미만은 칸 없음");
            Assert.That(list[0].Count, Is.EqualTo(MvUiFormat.Fmt(5)), "같은 재화 합침");
            Assert.That(list[1].Count, Is.EqualTo(MvUiFormat.Fmt(100)), "소수점 버림");
        }

        [Test]
        public void Items_NonWealth_Skipped_WithWarning()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[RewardPopup\] 재화가 아닌 보상"));
            var list = Items(new ConsumeData(ConsumeType.DailyQuestPoint, 0, 10), W(Gold, 5));
            Assert.That(list.Select(i => i.WealthType), Is.EqualTo(new[] { "gold" }));
            Assert.That(Items().Count, Is.EqualTo(0));
            Assert.That(RewardItems.Build(T, null).Count, Is.EqualTo(0));
        }

        [Test]
        public void Show_RevealsOneByOne_ByInterval()
        {
            var (_, view) = NewPopup();
            view.Show(Items(W(Gold, 500), W(Dia, 3), W(Tok, 2)));
            Assert.That(view.Count, Is.EqualTo(3), "Holder_Reward 단위로 보상 수만큼");
            Assert.That(view.Holders.Distinct().Count(), Is.EqualTo(3));
            Assert.That(view.Holders.All(h => h.gameObject.activeSelf), Is.True, "자리는 처음부터 잡아 둔다(배치 흔들림 없음)");
            Assert.That(view.Revealed, Is.EqualTo(1), "첫 칸은 바로");

            float dt = view.RevealInterval;
            view.Advance(dt * 0.5f);
            Assert.That(view.Revealed, Is.EqualTo(1));
            view.Advance(dt * 0.5f);
            Assert.That(view.Revealed, Is.EqualTo(2));
            view.Advance(dt);
            Assert.That(view.Revealed, Is.EqualTo(3));
            Assert.That(view.Revealing, Is.False);

            var slots = view.ShownSlots.ToList();
            Assert.That(slots.All(s => s.gameObject.activeSelf), Is.True);
            Assert.That(slots.Select(s => s.WealthType), Is.EqualTo(new[] { "gold", "dia", "goldenFish" }));
            Assert.That(slots[0].IconSprite, Is.EqualTo(HudCurrencySlot.IconOf(T, "gold")), "아이콘 = 01_wealth.icon(공용 Slot_Item)");
            Assert.That(slots[1].CountText, Is.EqualTo("x3"), "노션 텍스트 포맷 x{0}");
        }

        [Test]
        public void Show_Again_ReusesPool()
        {
            var (_, view) = NewPopup();
            view.Show(Items(W(Gold, 1), W(Dia, 1), W(Tok, 1)));
            var first = view.Holders.ToList();
            view.Show(Items(W(Gold, 1)));
            Assert.That(view.Count, Is.EqualTo(1), "줄면 풀에 돌려준다");
            Assert.That(first.Skip(1).All(h => !h.gameObject.activeSelf), Is.True, "돌려준 칸은 꺼짐");
            view.Show(Items(W(Gold, 1), W(Dia, 1), W(Tok, 1)));
            Assert.That(view.Holders.ToList(), Is.EquivalentTo(first), "다시 늘면 같은 칸을 빌린다");
        }

        [Test]
        public void CloseLocked_WhileRevealing_UpToMax()
        {
            var (_, view) = NewPopup();
            var many = Enumerable.Range(0, 3).Select(i => W(new[] { Gold, Dia, Tok }[i], 1)).ToArray();
            view.Show(Items(many));
            Assert.That(view.CanClose, Is.False, "등장 중에는 닫기 막음(정크버스터즈처럼)");
            view.RevealAll();
            Assert.That(view.CanClose, Is.True, "다 나오면 닫힘");

            view.Show(Items(many));
            view.Advance(view.MaxCloseLock);
            Assert.That(view.CanClose, Is.True, "상한이 지나면 닫힘");

            view.Show(Items(W(Gold, 1)));
            Assert.That(view.CanClose, Is.True, "한 칸이면 바로 닫을 수 있다");
        }

        [Test]
        public void Backboard_RaisesClose_DecorDoesNotBlock()
        {
            var (go, view) = NewPopup();
            view.Show(Items(W(Gold, 1))); // EditMode에서는 Awake가 없어 처음 그릴 때 Init이 돈다
            int pressed = 0;
            view.ClosePressed += () => pressed++;
            view.Backboard.onClick.Invoke();
            Assert.That(pressed, Is.EqualTo(1));

            var designer = go.transform.Find("Popup_Reward");
            var blocking = designer.GetComponentsInChildren<Graphic>(true).Where(g => g.raycastTarget).Select(g => g.name).ToList();
            Assert.That(blocking, Is.Empty, "디자이너 장식이 Backboard 터치를 가로챔");
        }

        [Test]
        public void Title_UsesLocKey_OrKeepsDesignerText()
        {
            var (_, view) = NewPopup();
            view.Show(Items(W(Gold, 1)));
            Assert.That(view.TitleText, Is.Not.Empty, "키가 없으면 시안 글자 「Reward!」 유지");
        }
    }
}
