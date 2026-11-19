using System.Collections.Generic;
using System.Linq;
using MV.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MV.UI.Tests
{
    /// <summary>
    /// MV-UI2-LOBBY: 디자이너 LobbyScreen을 로비 겉(UIScreen_Lobby 래퍼, 구조안 A)으로 쓰기 — 직렬화 참조, 표 행 ↔ 강화 칸, 값 넣기, 스택 풀, 배속.
    /// 화면 배치·연출 확인은 사람 확인(NOT_RUN 별도).
    /// </summary>
    public sealed class LobbyDesignViewTests
    {
        const string LobbyPath = "Assets/Game/Addressables/Local/UI/Lobby/UIScreen_Lobby.prefab";
        static MvTables T => MetaUiTestKit.Base();

        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
            MvRunSpeed.Set = null;
            MvRunSpeed.Get = null;
        }

        (GameObject root, LobbyDesignView view) NewLobby()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPath);
            Assert.That(prefab, Is.Not.Null, LobbyPath);
            var go = Object.Instantiate(prefab);
            _made.Add(go);
            var view = go.GetComponent<LobbyDesignView>();
            Assert.That(view, Is.Not.Null, "래퍼 루트의 LobbyDesignView");
            return (go, view);
        }

        [Test]
        public void LobbyPrefab_NestsDesignerLobbyScreen_WithAllReferences()
        {
            var (go, view) = NewLobby();
            Assert.That(view.MissingRefs(), Is.Empty, "LobbyDesignView 참조");
            var ctrl = go.GetComponent("LobbyScreen");
            Assert.That(ctrl, Is.Not.Null, "LobbyScreen 컨트롤러");
            Assert.That(new SerializedObject(ctrl).FindProperty("view").objectReferenceValue, Is.SameAs(view), "LobbyScreen.view = LobbyDesignView");
            Assert.That(go.transform.Find("LobbyScreen"), Is.Not.Null, "중첩 디자이너 LobbyScreen");
            Assert.That(view.StartArea.transform.GetSiblingIndex(), Is.EqualTo(0), "출발 영역은 디자이너 화면 뒤(첫 자식)");
            Assert.That(view.SpeedStateCount, Is.GreaterThan(1), "1x/2x 상태 자식");
            Assert.That(view.StackSlots, Is.Not.Empty, "Stack/Slot_Weapon_Stack");
            foreach (var u in view.Upgrades)
                Assert.That(u.button.gameObject, Is.SameAs(u.root), u.key + ": 버튼은 칸 루트에 덧붙인다");
        }

        [Test]
        public void UpgradeSlots_MatchActiveLobbyUpgradeRows()
        {
            var (_, view) = NewLobby();
            var rows = T.LobbyUpgrade.Datas.Where(r => r.active).Select(r => r.key).ToList();
            var keys = view.Upgrades.Select(u => u.key).ToList();
            Assert.That(keys, Is.Unique);
            foreach (var k in rows) Assert.That(keys, Does.Contain(k), "10_lobbyUpgrade " + k + "에 맞는 디자이너 칸");
        }

        [Test]
        public void Render_FillsUpgradeSlots_AndRebirthUnlock()
        {
            var (_, view) = NewLobby();
            var m = new LobbyMenuModel();
            var first = view.Upgrades[0];
            m.Upgrades.Add(new LobbyUpgradeItem { Key = first.key, Enabled = false, LevelNo = 7, CurValue = "7", NextValue = "8", PriceValue = "25" });
            m.Rebirth.Visible = true;
            m.Rebirth.Locked = true;
            m.Rebirth.UnlockNow = 3;
            m.Rebirth.UnlockNeed = 15;
            m.Rebirth.Pending = 4;
            view.Render(m);

            Assert.That(first.root.activeSelf, Is.True);
            Assert.That(first.level.text, Is.EqualTo(string.Format(LobbyDesignView.UpgradeLevelFormat, 7)));
            Assert.That(first.currentValue.text, Is.EqualTo("7"));
            Assert.That(first.priceText.text, Is.EqualTo("25"));
            Assert.That(first.disable.activeSelf, Is.True, "살 수 없으면 Disable");
            Assert.That(first.button.interactable, Is.False);
            foreach (var other in view.Upgrades.Skip(1)) Assert.That(other.root.activeSelf, Is.False, other.key + ": 모델에 없는 칸은 숨김");

            Assert.That(view.RebirthShown, Is.True);
            Assert.That(view.RebirthUnlockShown, Is.True);
            Assert.That(view.RebirthUnlockText, Is.EqualTo("3/15"));
            Assert.That(view.RebirthRewardText, Is.EqualTo("+4"));

            m.Rebirth.Visible = false;
            view.Render(m);
            Assert.That(view.RebirthShown, Is.False, "잠금 표시 조건 전이면 숨김");
        }

        [Test]
        public void SetState_FillsLevelArea_AndGrowsStackFromPool()
        {
            var (_, view) = NewLobby();
            int designerSlots = view.StackSlots.Count;
            int tier = T.Item.Datas.Min(i => i.tier);
            var deck = new int[tier + 1];
            deck[tier] = designerSlots + 2; // 장착 1장 + 스택 designerSlots+1장
            var s = new LobbyState { Level = 5, AreaNo = 1, ZoneNo = 1, AreaName = "테스트", Deck = deck };

            view.SetLevel(string.Format(LobbyScreen.LevelFormat, s.Level));
            view.SetState(s, T);

            Assert.That(view.LevelText, Is.EqualTo("Level 5"));
            Assert.That(view.AreaNameText, Is.EqualTo("테스트"));
            Assert.That(view.StackSlots.Count, Is.EqualTo(designerSlots + 1), "모자란 칸은 풀에서 빌림");
            Assert.That(view.StackSlots.All(x => x.gameObject.activeSelf && x.Tier == tier), Is.True);

            deck[tier] = 1;
            view.SetState(s, T);
            Assert.That(view.StackSlots.Count(x => x.gameObject.activeSelf), Is.EqualTo(0), "스택이 없으면 모두 끔");
            Assert.That(view.StackSlots.Count, Is.EqualTo(designerSlots), "빌린 칸은 풀로 돌려줌");
        }

        [Test]
        public void SetWealth_PutsValueInConfiguredCurrencySlots()
        {
            var (_, view) = NewLobby();
            view.SetWealth("10", "20", "30");
            var s = new LobbyState { Level = 0, AreaNo = 1, ZoneNo = 1, Deck = System.Array.Empty<int>() };
            view.SetState(s, T);
            var expect = new Dictionary<string, string> { ["gold"] = "10", ["rescueCat"] = "20", ["dia"] = "30" };
            foreach (var c in view.Currencies)
            {
                Assert.That(c.slot, Is.Not.Null, c.wealthType + ": 공용 재화 칸 HudCurrencySlot(인게임과 같은 컴포넌트)");
                Assert.That(c.slot.gameObject.name, Does.StartWith("Slot_Currency"), c.wealthType);
                var dummy = c.slot.transform.Find("Icon_Dummy");
                Assert.That(dummy, Is.Not.Null, c.wealthType);
                Assert.That(dummy.gameObject.activeSelf, Is.False, c.wealthType + ": 로비 래퍼 프리팹이 Icon_Dummy를 꺼 둠(코드 제어 없음, 사용자 2026-11-19)");
                Assert.That(c.slot.WealthType, Is.EqualTo(c.wealthType), "SetState가 공용 칸에 재화 종류를 넣음");
                if (expect.TryGetValue(c.wealthType, out var v)) Assert.That(c.slot.ValueText, Is.EqualTo(v), c.wealthType + ": SetWealth 글자가 SetState 뒤에도 남음");
            }
            Assert.That(view.Currencies.Count(c => expect.ContainsKey(c.wealthType)), Is.EqualTo(view.Currencies.Count), "칸의 재화는 로비가 읽는 01_wealth.type");
        }

        /// <summary>프리팹 단위 관리: 공용 Slot_AreaLabel은 루트 HudAreaLabel 하나가 그리고, 로비는 그 컴포넌트에 데이터만 넘긴다.</summary>
        [Test]
        public void AreaLabel_IsSharedPrefabRootComponent()
        {
            var common = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Art/UI/Prefab/Common/Slot_AreaLabel.prefab");
            Assert.That(common, Is.Not.Null);
            var part = common.GetComponent<HudAreaLabel>();
            Assert.That(part, Is.Not.Null, "Slot_AreaLabel 루트의 HudAreaLabel");
            Assert.That(part.HasRefs, Is.True, "Area 그림·Zone·Area 글자 참조");

            var (_, view) = NewLobby();
            foreach (var label in new[] { view.AreaLabel, view.AreaUpgradeLabel })
            {
                Assert.That(label, Is.Not.Null);
                Assert.That(label.gameObject.name, Does.StartWith("Slot_AreaLabel"));
            }
            view.SetState(new LobbyState { AreaNo = 2, ZoneNo = 3, AreaName = "테스트", Deck = System.Array.Empty<int>() }, T);
            foreach (var label in new[] { view.AreaLabel, view.AreaUpgradeLabel })
            {
                Assert.That(label.ZoneText, Is.EqualTo(string.Format(HudAreaLabel.ZoneFormat, 3)));
                Assert.That(label.AreaText, Is.EqualTo("테스트"));
            }
        }

        [Test]
        public void SpeedToggle_CyclesTheDesignerStates()
        {
            var (_, view) = NewLobby();
            int speed = 1;
            MvRunSpeed.Get = () => speed;
            MvRunSpeed.Set = v => speed = v;
            view.ClickSpeed();
            Assert.That(speed, Is.EqualTo(2));
            Assert.That(view.ShownSpeedState, Is.EqualTo(1));
            for (int i = 1; i < view.SpeedStateCount; i++) view.ClickSpeed();
            Assert.That(speed, Is.EqualTo(1), "마지막 상태 다음은 1x");
            Assert.That(view.ShownSpeedState, Is.EqualTo(0));
        }

        /// <summary>
        /// 공용 이름표(HudAreaLabel)는 클릭을 받지 않는다. 이름표 그림을 누르면 클릭이 위로 올라가
        /// 「Area 업그레이드」 버튼 → AreaUpgrade, 상단 이름표는 래퍼가 붙인 Button → 지도(Map).
        /// (사용자 확인 2026-11-19 「팝업이 아예 안열리는데」, 「구조로 바꿔」)
        /// </summary>
        [Test]
        public void AreaLabelClicks_GoToTheirButtons()
        {
            var (_, view) = NewLobby();
            _ = view.StackSlots; // Init(): EditMode는 Awake가 돌지 않는다
            Assert.That(view.MissingRefs(), Is.Empty);
            Assert.That(typeof(UnityEngine.EventSystems.IPointerClickHandler).IsAssignableFrom(typeof(HudAreaLabel)), Is.False, "공용 이름표는 클릭을 받지 않는다");
            var got = new List<string>();
            view.SidePressed += got.Add;
            var e = new UnityEngine.EventSystems.PointerEventData(null) { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
            void Click(Component c) => UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(
                c.GetComponentInChildren<UnityEngine.UI.Graphic>(true).gameObject, e, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);

            Click(view.AreaUpgradeLabel);
            Assert.That(got, Is.EqualTo(new[] { LobbySideIds.AreaUpgrade }), "버튼 안 이름표 누름 → Area 업그레이드");

            got.Clear();
            Click(view.AreaLabel);
            Assert.That(got, Is.EqualTo(new[] { LobbySideIds.Map }), "상단 이름표 → 지도");
        }
    }
}
