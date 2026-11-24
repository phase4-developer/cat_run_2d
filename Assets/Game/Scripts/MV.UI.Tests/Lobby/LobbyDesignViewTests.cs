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
            // 무기 스택 = 인게임과 같은 공용 부품(MV-UI3-LOBBY-WEAPONFX). 인게임 런 연결(WeaponSection)은 로비에 없다.
            var ws = view.WeaponStack;
            Assert.That(ws, Is.Not.Null, "LobbyScreen/Mid/Weapon 로비 연결");
            Assert.That(ws.View, Is.Not.Null, "공용 부품 WeaponStackView");
            Assert.That(ws.View.gameObject, Is.SameAs(ws.gameObject), "공용 부품과 로비 연결은 같은 Weapon 오브젝트");
            Assert.That(go.GetComponentsInChildren<WeaponSection>(true), Is.Empty, "로비에 인게임 런 연결을 두지 않는다");
            Assert.That(PrefabUtility.IsPartOfPrefabAsset(ws.View.StackPrefab), Is.True, "스택 복제 원본 = Slot_Weapon_Stack.prefab 에셋");
            Assert.That(ws.View.QueueAnchor.name, Is.EqualTo("Weapon_QueueAnchor"));
            Assert.That(ws.Spawn.name, Is.EqualTo("Spwan"), "노션: Weapon_GetAnchor/Spwan");
            Assert.That(ws.Dst.name, Is.EqualTo("Dst"), "노션: Weapon_GetAnchor/Dst");
            Assert.That(ws.Spawn.parent.name, Is.EqualTo("Weapon_GetAnchor"));
            Assert.That(ws.Spawn.IsChildOf(view.Upgrades.Single(u => u.key == "weapon").root.transform), Is.True, "Spwan은 무기 강화 칸 안");
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
            foreach (var u in view.Upgrades) // PriceBtn은 자기가 잠그는 Button과 같은 객체(강화 칸 루트)에 둔다
                Assert.That(u.button.GetComponent("PriceBtn"), Is.Not.Null, u.key + ": 버튼 루트에 PriceBtn");
            foreach (var u in AssetDatabase.LoadAssetAtPath<GameObject>(LobbyPath).GetComponent<LobbyDesignView>().Upgrades)
                Assert.That(PrefabUtility.IsAddedComponentOverride(u.button), Is.False, u.key + ": Button은 강화 칸 프리팹 원본에 있다(래퍼 덮어쓰기 아님)");
            var element = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Art/UI/Prefab/HUD/Element_MainUpgrade_Stair.prefab");
            Assert.That(element.GetComponent<UnityEngine.UI.Button>(), Is.Not.Null, "강화 칸 원본(변형 Weapon·Income이 이어받음)에 Button");
            Assert.That(element.GetComponent("PriceBtn"), Is.Not.Null, "강화 칸 원본에 PriceBtn");
        }

        [Test]
        public void Render_FillsUpgradeSlots_AndRebirthUnlock()
        {
            var (_, view) = NewLobby();
            var m = new LobbyMenuModel();
            var first = view.Upgrades[0];
            m.Upgrades.Add(new LobbyUpgradeItem { Key = first.key, Enabled = false, InputAllowed = true, LevelNo = 7, CurValue = "7", NextValue = "8", Cost = new[] { new ConsumeData(ConsumeType.Wealth, 1001001, 25) } });
            m.Rebirth.Visible = true;
            m.Rebirth.Locked = true;
            m.Rebirth.UnlockNow = 3;
            m.Rebirth.UnlockNeed = 15;
            m.Rebirth.Pending = 4;
            view.Render(m);

            Assert.That(first.root.activeSelf, Is.True);
            Assert.That(first.level.text, Is.EqualTo(string.Format(LobbyDesignView.UpgradeLevelFormat, 7)));
            Assert.That(first.currentValue.text, Is.EqualTo("7"));
            Assert.That(first.price.activeSelf, Is.False, "재화 서비스가 없으면 PriceBtn이 그릴 수 없어 가격 칸을 끔");
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
        public void SetState_FillsLevelArea_AndDrawsDeckWithSharedStack()
        {
            var (_, view) = NewLobby();
            var stack = view.WeaponStack.View;
            int lo = T.Item.Datas.Min(i => i.tier);
            int mergeN = T.RunConfig.I(RunConfigKeys.MERGE_N);
            var deck = new int[lo + 2];
            deck[lo + 1] = 1;           // 장착 칸 = 가장 높은 등급
            deck[lo] = mergeN - 1;      // 머지 직전 장수(저장 덱은 머지가 끝난 값)
            var s = new LobbyState { Level = 5, AreaNo = 1, ZoneNo = 1, AreaName = "테스트", Deck = deck };

            view.SetLevel(string.Format(LobbyScreen.LevelFormat, s.Level));
            view.SetState(s, T);

            Assert.That(view.LevelText, Is.EqualTo("Level 5"));
            Assert.That(view.AreaNameText, Is.EqualTo("테스트"));
            var expect = new[] { lo + 1 }.Concat(Enumerable.Repeat(lo, mergeN - 1)).ToArray();
            Assert.That(stack.StackSlots.Select(x => x.Tier), Is.EqualTo(expect), "손패 = 덱(높은 등급이 위)");
            Assert.AreEqual(lo + 1, stack.Equip.Tier, "장착 칸 = 최고 등급");
            Assert.That(stack.StackSlots.Skip(1).All(x => x.gameObject.activeSelf), Is.True, "맨 위(장착) 카드를 뺀 나머지가 스택에 보인다");
            foreach (Transform c in stack.Stack)
                if (c.GetComponent<HudWeaponSlot>() is { } slot && !stack.StackSlots.Contains(slot))
                    Assert.That(c.gameObject.activeSelf, Is.False, "디자이너 예시 칸은 숨김");

            // 무기 Level이 올라 낮은 카드를 버린 덱: 쉬는 중이면 바로 맞춘다(실행 중이면 빠지는 칸이 discardFadeSec 동안 흐려짐).
            deck[lo] = 0;
            view.SetState(s, T);
            Assert.That(stack.StackSlots.Select(x => x.Tier), Is.EqualTo(new[] { lo + 1 }));
            Assert.That(stack.StackSlots.Count(x => x.gameObject.activeSelf), Is.EqualTo(0), "장착 1장뿐이면 스택은 비어 보인다");
        }

        /// <summary>
        /// 노션 「LobbyScreen : WeaponUpgrade시 무기 획득 및 Stack 연출」: 스크래쳐 구매 카드는 Spwan → Dst → 줄(Weapon_QueueAnchor) → 스택으로 들어가고,
        /// 같은 등급이 MERGE_N장이 되면 인게임처럼 머지한다. 편집기 정지 중에는 비행 없이 바로 줄에 선다(비행·Flash·Fx 모양은 사람 확인).
        /// </summary>
        [Test]
        public void AddDeckCard_QueuesThenDealsAndMerges_LikeIngame()
        {
            var (_, view) = NewLobby();
            var stack = view.WeaponStack.View;
            int lo = T.Item.Datas.Min(i => i.tier);
            int mergeN = T.RunConfig.I(RunConfigKeys.MERGE_N);
            Assert.That(MV.Run.RunFormulas.WeaponTierMax(T), Is.GreaterThan(lo), "머지할 다음 등급");
            var deck = new int[lo + 2];
            deck[lo] = 1;
            view.SetState(new LobbyState { AreaNo = 1, ZoneNo = 1, Deck = deck }, T);

            view.AddDeckCard(lo);
            Assert.AreEqual(1, stack.QueuedCards, "줄에 선다");
            Assert.AreEqual(1, stack.PendingUnits, "받을 장 1장");
            RunUntilIdle(stack);
            Assert.That(stack.Hand.Select(c => c.Tier), Is.EqualTo(Enumerable.Repeat(lo, 2)), "줄 맨 앞 카드가 스택으로 들어감");
            Assert.AreEqual(0, stack.QueuedCards);

            for (int i = 2; i < mergeN; i++) view.AddDeckCard(lo);
            RunUntilIdle(stack);
            Assert.That(stack.Hand.Select(c => c.Tier), Is.EqualTo(new[] { lo + 1 }), "같은 등급 MERGE_N장 → 다음 등급 1장");
            Assert.AreEqual(lo + 1, stack.Equip.Tier, "머지 결과가 장착 칸");
            // MV-UI3-FXFIX: 맨 위 카드 칸은 장착 칸 자리에 숨어 있어 결과 연출을 장착 칸이 한다(편집기 정지 중이라 트윈은 멈춘 시작 모습).
            Assert.AreEqual(1, stack.EquipMergedCount, "머지 결과 연출을 장착 칸이 시작");
            Assert.That(stack.Equip.HasFlash && stack.Equip.HasSparkle, Is.True, "장착 칸 Flash·Effect_Sparkle 연결");
            Assert.That(stack.Equip.FlashVisibleAlpha, Is.GreaterThan(0.5f), "덮개가 보인다(디자이너 색 알파 0이어도 flashPeak로 밝힌다)");
            Assert.That(stack.Equip.SparkleOn, Is.True, "반짝임 켜짐");
            stack.Equip.ResetLook();
            Assert.That(stack.Equip.FlashVisibleAlpha, Is.EqualTo(0f), "끝나면 덮개 꺼짐");
        }

        static void RunUntilIdle(WeaponStackView v, float dt = 0.01f, float limit = 20)
        {
            for (float t = 0; t < limit; t += dt)
            {
                v.Step(dt, null);
                if (!v.Busy) return;
            }
            Assert.Fail("손패가 끝나지 않음");
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
                // Icon_Dummy는 디자이너가 Slot_Currency에서 지웠다(2026-11-23 8cc83d5d). 남아 있으면 꺼져 있어야 한다.
                var dummy = c.slot.transform.Find("Icon_Dummy");
                if (dummy) Assert.That(dummy.gameObject.activeSelf, Is.False, c.wealthType + ": Icon_Dummy 끔");
                var icon = c.slot.transform.Find("Icon");
                Assert.That(icon, Is.Not.Null, c.wealthType);
                Assert.That(icon.gameObject.activeSelf, Is.True, c.wealthType + ": 디자이너 원본이 꺼 둔 표 아이콘 칸 Icon을 래퍼 프리팹이 켬(둘 다 꺼지면 아이콘이 안 보임)");
                Assert.That(c.slot.IconSprite, Is.EqualTo(HudCurrencySlot.IconOf(T, c.wealthType)), c.wealthType + ": 01_wealth.icon");
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
                Assert.That(label.ZoneText, Is.EqualTo(MvUiText.ZoneNumber(3)));
                Assert.That(label.AreaCountText, Is.EqualTo(MvUiText.AreaNumber(2)), "Area 번호는 Area Count Text 따로");
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
            _ = view.WeaponStack; // Init(): EditMode는 Awake가 돌지 않는다
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
