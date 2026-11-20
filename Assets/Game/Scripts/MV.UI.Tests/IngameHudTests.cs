using System.Collections.Generic;
using System.Linq;
using MV.Data;
using MV.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MV.UI.Tests
{
    /// <summary>
    /// MV-UI2-HUD(무기 손패는 MV-UI3-HUD4 무기 섹션): 디자이너 IngameScreen을 런 HUD(UIHud_Run) 겉으로 쓰기 — 등급 그림 매핑, 지팡이 머지 과정, 계단 등급 카드, 스킬 롤링 결과, 배속 토글.
    /// 연출(움직임·템포)은 재생 중에만 돌아 여기서는 끝 상태만 본다. 화면 연출 확인은 사람 확인(NOT_RUN 별도).
    /// </summary>
    public sealed class IngameHudTests
    {
        const string HudPath = "Assets/Game/Addressables/Local/UI/Hud/UIHud_Run.prefab";
        static MvTables T => MetaUiTestKit.Base();

        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
            MvRunSpeed.Set = null;
        }

        sealed class FakeSource : IRunHudSource
        {
            public RunHudState State;
            public bool TryRead(out RunHudState state) { state = State; return true; }
        }

        // RunHud(UIHudBase 파생)는 이 시험 어셈블리가 참조하지 않는 jjevol 어셈블리에 걸려 타입으로 쓰지 않는다(asmdef는 통합 Writer).
        // RunHud.ResolveView와 같은 규칙(참조가 비면 자식의 IRunHudView)으로 겉을 찾고, 화면 코드는 같은 RunHudPresenter를 쓴다.
        (GameObject hud, IngameHudView view) NewHud()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            Assert.That(prefab, Is.Not.Null, HudPath);
            // 런타임(Addressables Instantiate)과 같이 일반 복제로 만든다. 프리팹 인스턴스는 에디터에서 자식 부모 변경이 막힌다.
            var go = Object.Instantiate(prefab);
            _made.Add(go);
            var ctrl = go.GetComponent("RunHud");
            Assert.That(ctrl, Is.Not.Null, "RunHud");
            Assert.That(new SerializedObject(ctrl).FindProperty("view").objectReferenceValue, Is.Null, "참조가 비어 자식 겉을 쓴다");
            var views = go.GetComponentsInChildren<IRunHudView>(true);
            Assert.That(views.Length, Is.EqualTo(1), "겉은 하나");
            var view = views[0] as IngameHudView;
            Assert.That(view, Is.Not.Null, "UIHud_Run 겉 = 자식 IngameScreen의 IngameHudView");
            return (go, view);
        }

        static RunState NewState()
        {
            var s = new RunState(null, T, new Pcg32Rng(1));
            foreach (var c in T.Card.Datas) if (c.active && c.group <= 1) s.CardPool.Add(c);
            return s;
        }

        static CardTierData[] Tiers() => T.CardTier.Datas.OrderBy(r => r.tierIndex).ToArray();

        // ---------------- 표 → 디자이너 그림 ----------------

        [Test]
        public void GradeFrames_CoverEveryWeaponTier_AndReuseLastRow()
        {
            int maxTier = T.Item.Datas.Max(i => i.tier);
            for (int tier = 1; tier <= maxTier; tier++)
            {
                var r = HudGrade.Frame(T, tier);
                Assert.That(r, Is.Not.Null, "단계 " + tier + " 등급 틀 행");
                Assert.That(r.sort, Is.EqualTo(tier), "등급 틀 sort = 등급 번호");
                Assert.That(HudArtTestKit.AtlasHas(r.atlas, r.sprite), r.sprite + "가 " + r.atlas + "에 없음");
            }
            int rows = T.UiResource.GetGroup(HudGrade.FrameGroup).Count;
            Assert.That(HudGrade.Frame(T, rows + 3).sprite, Is.EqualTo(HudGrade.Frame(T, rows).sprite), "모자란 등급은 마지막 그림을 다시 쓴다");
        }

        [Test]
        public void WeaponIconsAndCardTierFrames_AreInTheirAtlases()
        {
            foreach (var it in T.Item.Datas)
                Assert.That(HudArtTestKit.AtlasHas(MvAtlas.UiIcon, it.icon), it.id + " icon 「" + it.icon + "」");
            foreach (var r in T.CardTier.Datas)
                Assert.That(HudArtTestKit.AtlasHas(MvAtlas.UiCard, r.frameSprite), r.id + " frameSprite 「" + r.frameSprite + "」");
        }

        // ---------------- 계산 ----------------

        [Test]
        public void ClimbCurrent_FillsTowardTheNextTierNeed()
        {
            var tiers = Tiers();
            int need0 = tiers[0].need, need1 = tiers[1].need, max = tiers[tiers.Length - 1].tierIndex;
            Assert.AreEqual(tiers[0].tierIndex, HudGrade.ClimbCurrent(T, -1, 0, out float f));
            Assert.AreEqual(0f, f);
            HudGrade.ClimbCurrent(T, -1, need0 - 1, out f);
            Assert.AreEqual((need0 - 1) / (float)need0, f, 1e-5, "첫 등급은 0칸부터 need까지");
            Assert.AreEqual(tiers[1].tierIndex, HudGrade.ClimbCurrent(T, tiers[0].tierIndex, need0, out f), "얻은 등급 + 1을 채운다");
            Assert.AreEqual(0f, f, 1e-5);
            HudGrade.ClimbCurrent(T, tiers[0].tierIndex, (need0 + need1) / 2, out f);
            Assert.AreEqual(((need0 + need1) / 2 - need0) / (float)(need1 - need0), f, 1e-5, "앞 등급 need부터");
            Assert.AreEqual(max, HudGrade.ClimbCurrent(T, max, need0, out f), "최고 등급");
            Assert.AreEqual(1f, f);
        }

        [Test]
        public void WeaponHand_ReplaysAddAndMergeCascade()
        {
            int mergeN = T.RunConfig.I(RunConfigKeys.MERGE_N);
            int len = T.Item.Datas.Max(i => i.tier) + 1;
            var from = new int[len];
            from[1] = mergeN - 1; from[2] = mergeN - 1;
            var to = (int[])from.Clone();
            HudWeaponHand.Add(to, 1, mergeN, null);
            var steps = HudWeaponHand.Steps(from, to, mergeN, 1);
            Assert.That(steps, Is.Not.Null);
            Assert.That(steps.Select(x => x.ToString()), Is.EqualTo(new[] { "add 1", "merge 1", "merge 2" }), "넣고 → 1단계 머지 → 2단계 머지");
            Assert.That(HudWeaponHand.Steps(from, from, mergeN, 1), Is.Empty);
            Assert.That(HudWeaponHand.Steps(to, from, mergeN, 1), Is.Null, "줄어든 손패(초기화)는 바로 맞춘다");

            var w = new int[len];
            w[1] = 2; w[3] = 1;
            Assert.AreEqual(3, HudWeaponHand.Equipped(w));
            Assert.That(HudWeaponHand.Stack(w), Is.EqualTo(new[] { 1, 1 }), "장착 1장을 뺀 나머지");
        }

        [Test]
        public void SpeedToggle_CyclesTheDesignerStates()
        {
            Assert.AreEqual(2, MvRunSpeed.Next(1, 2));
            Assert.AreEqual(1, MvRunSpeed.Next(2, 2));
            Assert.AreEqual(1, MvRunSpeed.Next(3, 2), "테스트 패널 ×3에서 누르면 1x");
            Assert.AreEqual(0, MvRunSpeed.StateIndex(1, 2));
            Assert.AreEqual(1, MvRunSpeed.StateIndex(3, 2));
        }

        // ---------------- 프리팹 ----------------

        [Test]
        public void RunHudPrefab_UsesDesignerIngameScreen_WithAllReferences()
        {
            var (hud, view) = NewHud();
            var so = new SerializedObject(view);
            var it = so.GetIterator();
            var missing = new List<string>();
            while (it.NextVisible(true))
                if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == null && it.propertyPath != "m_Script")
                    missing.Add(it.propertyPath);
            Assert.That(missing, Is.Empty, "IngameHudView 참조");
            Assert.That(view.TopBar.SpeedStateCount, Is.GreaterThan(1), "1x/2x 상태 자식");
            Assert.That(view.TopBar, Is.Not.Null, "Top 상단 줄 섹션");
            AssertRefs(view.TopBar);
            Assert.That(view.Stair, Is.Not.Null, "Mid/Stair 계단 섹션");
            AssertRefs(view.Stair);
            Assert.That(view.TopBar.Gold, Is.Not.SameAs(view.TopBar.Dia), "재화 칸 2개 = 서로 다른 Slot_Currency(공용 HudCurrencySlot)");
            Assert.That(view.GradeStep, Is.Not.Null, "ClimbStair/Grade Step Section 계단 카드 섹션");
            AssertRefs(view.GradeStep);
            Assert.That(PrefabUtility.IsPartOfPrefabAsset(view.GradeStep.CardPrefab), Is.True, "계단 카드 복제 원본 = Card_Class.prefab 에셋");
            Assert.That(view.SkillRoll, Is.Not.Null, "ClimbStair/Skill Section 스킬 롤링 섹션");
            AssertRefs(view.SkillRoll);
            Assert.That(view.CardPos, Is.Not.Null, "Mid/Card Pos 얻은 카드 섹션");
            AssertRefs(view.CardPos);
            Assert.That(PrefabUtility.IsPartOfPrefabAsset(view.CardPos.FlyPrefab), Is.True, "나는 카드 복제 원본 = Card_Skill.prefab 에셋");
            Assert.That(view.Weapon, Is.Not.Null, "Mid/Weapon 무기 섹션");
            AssertRefs(view.Weapon);
            AssertRefs(view.Weapon.View);
            Assert.That(view.Weapon.View.Equip, Is.Not.Null, "Equip/Slot_Weapon");
            Assert.That(PrefabUtility.IsPartOfPrefabAsset(view.Weapon.View.StackPrefab), Is.True, "스택 복제 원본 = Slot_Weapon_Stack.prefab 에셋");
            Assert.That(view.SkillCard, Is.Not.Null, "Skill Section/Card_Skill");
            // flash·sparkle은 디자이너 리소스가 아직 없는 선택 항목(없으면 크기 연출만, 경고 로그).
            foreach (var slot in hud.GetComponentsInChildren<HudWeaponSlot>(true)) AssertRefs(slot, "flash", "sparkle");
            foreach (var card in hud.GetComponentsInChildren<HudClimbCard>(true)) AssertRefs(card);
            // maskBack(Card Texture (1))은 디자이너가 일부 인스턴스에서 지운 선택 항목이라 빼고 본다(코드가 없으면 건너뜀).
            foreach (var card in hud.GetComponentsInChildren<HudSkillCard>(true)) AssertRefs(card, "maskBack");
            // 계단 정산 글자·런 안내도 자식 프리팹(HUD_ClimbNotice)이다. RunHud가 실행 중에 만들지 않는다.
            var notices = hud.GetComponentsInChildren<ClimbNoticeView>(true);
            Assert.That(notices.Length, Is.EqualTo(1), "자식 프리팹 HUD_ClimbNotice");
            AssertRefs(notices[0]);
            Assert.That(new SerializedObject(hud.GetComponent("RunHud")).FindProperty("notice").objectReferenceValue, Is.SameAs(notices[0]), "RunHud.notice 직렬화 참조");
            Assert.That(notices[0].transform.GetSiblingIndex(), Is.EqualTo(hud.transform.childCount - 1), "HUD 맨 위(마지막 자식)");
        }

        static void AssertRefs(Object o, params string[] optional)
        {
            var it = new SerializedObject(o).GetIterator();
            while (it.NextVisible(true))
                if (it.propertyType == SerializedPropertyType.ObjectReference && it.propertyPath != "m_Script" && System.Array.IndexOf(optional, it.propertyPath) < 0)
                    Assert.That(it.objectReferenceValue, Is.Not.Null, o.name + "." + it.propertyPath);
        }

        [Test]
        public void IngameHud_ShowsValuesInDesignerFormats_AndSpeedMatchesState()
        {
            var (_, view) = NewHud();
            var src = new FakeSource
            {
                State = new RunHudState { Level = 3, Gold = 12345, Dia = 7, Stairs = 15, GaugeLv = 2, GaugeFill = .5f, WeaponTier = 3, TimeScale = 2 },
            };
            var p = new RunHudPresenter();
            p.SetView(view);
            p.Bind(src);
            p.Tick();
            Assert.AreEqual("Level 3", view.TopBar.LevelText);
            Assert.AreEqual("12.3K", view.TopBar.Gold.ValueText);
            Assert.AreEqual("7", view.TopBar.Dia.ValueText);
            Assert.AreEqual("x15", view.Stair.StairCountText, "Count Text 서식 「x」+값");
            Assert.AreEqual("+2", view.Stair.StairValueText, "Slot_Stair Value 서식 「+」+칸당 계단");
            Assert.AreEqual(1, view.TopBar.ShownSpeedState, "2배속 → 2x 자식만 켬");

            int asked = 0;
            MvRunSpeed.Set = x => asked = x;
            view.TopBar.ClickSpeed();
            Assert.AreEqual(1, asked, "2x에서 누르면 1x 요청");
            var s = src.State; s.TimeScale = 1; src.State = s;
            p.Tick();
            Assert.AreEqual(0, view.TopBar.ShownSpeedState, "표시는 런 배속 값을 따른다");
            view.TopBar.ClickSpeed();
            Assert.AreEqual(2, asked);

            // 재화 칸 그림 = 01_wealth.icon(상단 줄이 종류를 넘긴다, 로비와 같은 컴포넌트)
            view.TopBar.BindTables(T);
            Assert.AreEqual(HudCurrencySlot.IconOf(T, "gold"), view.TopBar.Gold.IconSprite);
            Assert.AreEqual(HudCurrencySlot.IconOf(T, "dia"), view.TopBar.Dia.IconSprite);
        }

        /// <summary>첫 그리기(BindRun 직후, TickRun 전)에도 계단 등급 틀(백판)이 보인다. 예전에는 표를 못 잡아 게이지가 움직여야 보였다.</summary>
        [Test]
        public void IngameHud_StairGradeFrame_ShownOnFirstDraw()
        {
            var (_, view) = NewHud();
            var s = NewState();
            view.BindRun(() => s);
            view.SetState(new RunHudState { GaugeLv = 1, GaugeFill = 0 });
            string want = HudGrade.Frame(T, 1)?.sprite;
            Assert.That(want, Is.Not.Null.And.Not.Empty, "표의 1단계 등급 틀");
            Assert.AreEqual(want, view.Stair.StairGradeSprite, "계단 칸 등급 틀이 시작부터");
            Assert.AreEqual(want, view.Stair.GaugeGradeSprite, "게이지 등급 틀이 시작부터");
        }

        [Test]
        public void IngameHud_RunState_DrivesWeaponsClimbCardsAndRoulette()
        {
            var (_, view) = NewHud();
            var s = NewState();
            view.BindRun(() => s);

            // 지팡이(무기 섹션): 장착 = 최고 단계, 스택 = 손패 전부(05 §5.3.1, 높은 단계가 위). 그림은 표(103 icon·등급 틀).
            s.W[1] = 2; s.W[3] = 1;
            view.TickRun(0);
            var weapon = view.Weapon.View;
            Assert.AreEqual(3, weapon.Equip.Tier);
            var item3 = T.Item.Datas.First(i => i.tier == 3);
            Assert.AreEqual(item3.icon, weapon.Equip.IconSprite);
            Assert.AreEqual(HudGrade.Frame(T, 3).sprite, weapon.Equip.GradeSprite);
            Assert.That(weapon.StackSlots.Select(x => x.Tier), Is.EqualTo(new[] { 3, 1, 1 }));
            int mergeN = T.RunConfig.I(RunConfigKeys.MERGE_N);
            HudWeaponHand.Add(s.W, 1, mergeN, null);   // 이벤트 없이 시뮬 값만 바뀜(1단계 3장 → 2단계) → 쉬는 중이면 시뮬 손패로 맞춘다
            view.TickRun(0);
            Assert.AreEqual(s.W[1], 0);
            Assert.That(weapon.StackSlots.Select(x => x.Tier), Is.EqualTo(new[] { 3, 2 }), "머지 결과가 손패에 반영");

            // 계단: 첫 등급부터, 등급이 오르면 Prev·Current·Next가 한 칸씩.
            var tiers = Tiers();
            s.Phase = RunPhase.Climb;
            s.Climb = new ClimbState { N = 30, Tier = -1, Rem = 0 };
            view.TickRun(0);
            Assert.That(view.ClimbCards.Select(c => c.Tier).OrderBy(x => x), Is.EqualTo(new[] { tiers[0].tierIndex, tiers[1].tierIndex }), "Current = 첫 등급, Next = 다음");
            Assert.That(view.SkillCard.QuestionShown, Is.True, "롤링 전 Question");
            s.Climb.Tier = tiers[0].tierIndex; s.Climb.Rem = tiers[0].need;
            view.TickRun(0);
            Assert.That(view.ClimbCards.Select(c => c.Tier).OrderBy(x => x), Is.EqualTo(new[] { tiers[0].tierIndex, tiers[1].tierIndex, tiers[2].tierIndex }), "Prev·Current·Next");
            var current = view.ClimbCards.First(c => c.Tier == tiers[1].tierIndex);
            Assert.AreEqual(0f, current.Fill, 1e-5);
            Assert.AreEqual(1f, view.ClimbCards.First(c => c.Tier == tiers[0].tierIndex).Fill, 1e-5);

            // 룰렛: 롤링 → 확정(시뮬이 고른 카드) → Card Pos.
            s.Phase = RunPhase.Run;
            s.Climb = null;
            var pick = s.CardPool.First(c => c.tier == tiers[1].tierIndex);
            var k = new RouletteCard { Tier = pick.tier, Pick = pick };
            var rl = new RouletteState();
            rl.Cards.Add(k);
            s.Roulette = rl;
            view.TickRun(0);
            Assert.That(view.SkillCard.Rolling, Is.True);
            Assert.That(view.SkillCard.QuestionShown, Is.False, "롤링이 시작되면 Question을 끈다");
            k.Flips = 3;
            view.TickRun(0);
            k.Chosen = true;
            view.TickRun(0);
            Assert.AreEqual(pick.id, view.SkillCard.CardId, "확정 = 시뮬 결과");
            Assert.AreEqual(pick.icon, view.SkillCard.FaceSprite);
            Canvas.ForceUpdateCanvases();   // 띠(Vertical Layout) 칸 자리를 실제 배치로
            Assert.That(view.SkillCard.FaceVisible, Is.True, "확정 카드 그림 칸(첫 칸)이 Mask 창 안");
            Assert.AreEqual(HudGrade.CardName(T, pick), view.SkillCard.Title);
            k.Flying = true; k.FlyLeft = .62;
            view.TickRun(0);
            k.Applied = true; s.Cards[pick.id] = 1; s.CardOrder.Add(pick.id);
            s.Roulette = null;
            view.TickRun(0);
            Assert.That(view.PosCards.Count(c => c.gameObject.activeSelf), Is.EqualTo(1));
            Assert.AreEqual(pick.id, view.PosCards[0].CardId, "얻은 카드가 Card Pos에");
            Canvas.ForceUpdateCanvases();
            Assert.That(view.PosCards[0].FaceVisible, Is.True, "Card Pos 자리 카드에 그림이 보인다(원본에서 꺼진 Mask를 켠다)");
        }

        [Test]
        public void CardPos_FlyTarget_IsTheSlotTheCardWillTake_EvenWhileEarlierCardsAreStillFlying()
        {
            // MV-UI3-CLIMBFX2: 시뮬은 도착 때 CardOrder에 넣는다(330 rlEndStep < rlFlyMs라 비행이 겹친다).
            var order = new List<int> { 10 };
            var none = new List<int>();
            Assert.AreEqual(0, CardPosSection.SlotFor(order, none, 10), "이미 얻은 카드는 그 자리");
            Assert.AreEqual(1, CardPosSection.SlotFor(order, none, 20), "새 카드는 다음 자리");
            Assert.AreEqual(2, CardPosSection.SlotFor(order, new List<int> { 20 }, 30), "앞 새 카드가 날고 있으면 그다음 자리");
            Assert.AreEqual(1, CardPosSection.SlotFor(order, new List<int> { 20, 30 }, 20), "같은 새 카드는 먼저 날고 있는 카드와 같은 자리");
            Assert.AreEqual(1, CardPosSection.SlotFor(order, new List<int> { 10 }, 30), "이미 얻은 카드의 비행은 새 자리를 차지하지 않는다");
            Assert.AreEqual(2, CardPosSection.SlotFor(order, new List<int> { 20, 20 }, 30), "같은 새 카드 두 장은 자리 하나");
        }

        [Test]
        public void CardPos_NewHolders_AreLaidOutApartRightAway()
        {
            var (_, view) = NewHud();
            var pos = view.CardPos;
            var s = NewState();
            var picks = s.CardPool.Take(3).ToList();
            Assert.That(picks.Count, Is.EqualTo(3));
            foreach (var c in picks) { s.Cards[c.id] = 1; s.CardOrder.Add(c.id); }
            pos.Tick(s);   // Holder_Card는 디자이너 1개, 나머지는 복제
            Assert.That(pos.PosCards.Count, Is.GreaterThanOrEqualTo(3));
            var p = pos.PosCards.Take(3).Select(c => ((RectTransform)c.Rect.parent).position).ToArray();
            Assert.That(Vector3.Distance(p[0], p[1]), Is.GreaterThan(1f), "복제한 Holder가 바로 레이아웃 자리를 잡아 첫 Holder와 겹치지 않는다");
            Assert.That(Vector3.Distance(p[1], p[2]), Is.GreaterThan(1f));
        }

        [Test]
        public void IngameHud_ClimbFx_ProgressOnlyOnCurrent_AndRouletteCardsRollTogether()
        {
            var (_, view) = NewHud();
            var s = NewState();
            view.BindRun(() => s);
            var tiers = Tiers();

            // Grade Step: Progress_Level은 Current 자리 카드에만(노션 ClimbStair 연출 보강 1).
            s.Phase = RunPhase.Climb;
            s.Climb = new ClimbState { N = 30, Tier = tiers[0].tierIndex, Rem = tiers[0].need };
            view.TickRun(0);
            int cur = view.GradeStep.Current;
            Assert.That(view.ClimbCards.Count, Is.GreaterThanOrEqualTo(2));
            foreach (var c in view.ClimbCards)
                Assert.AreEqual(c.Tier == cur, c.ProgressShown, "등급 " + c.Tier + " 게이지 표시");

            // Skill Section: 룰렛 카드 2장 → Holder 2개(같은 HorizontalLayout 아래)에서 동시에 롤링.
            s.Phase = RunPhase.Run;
            s.Climb = null;
            var a = s.CardPool.First(c => c.tier == tiers[0].tierIndex);
            var b = s.CardPool.First(c => c.tier == tiers[1].tierIndex);
            var k0 = new RouletteCard { Tier = a.tier, Pick = a };
            var k1 = new RouletteCard { Tier = b.tier, Pick = b };
            var rl = new RouletteState();
            rl.Cards.Add(k0); rl.Cards.Add(k1);
            s.Roulette = rl;
            view.TickRun(0);
            var roll = view.SkillRoll.RollCards.ToArray();   // RollCards는 부를 때마다 다시 채우는 목록
            Assert.AreEqual(2, roll.Length, "룰렛 카드마다 자리 하나");
            Assert.AreSame(view.SkillCard, roll[0], "첫 자리 = 디자이너 Holder_CardSkill의 카드");
            Assert.AreNotSame(roll[0].transform.parent, roll[1].transform.parent, "카드마다 Holder");
            Assert.AreSame(roll[0].transform.parent.parent, roll[1].transform.parent.parent, "Holder는 같은 Skill Section(HorizontalLayout) 아래");
            Assert.That(roll.All(c => c.Rolling && c.gameObject.activeSelf && c.transform.parent.gameObject.activeSelf), Is.True, "두 장 동시에 롤링");

            k0.Chosen = true;
            view.TickRun(0);
            Assert.AreEqual(a.id, roll[0].CardId, "첫 장 확정 = 시뮬 결과");
            Assert.That(roll[1].Rolling, Is.True, "둘째 장은 계속 롤링");
            Assert.That(roll[0].HasFlash && roll[0].HasSparkle, Is.True, "완료 연출 리소스(Flash·Effect_Sparkle) 연결");

            k0.Flying = true; k0.FlyLeft = .62;
            var holder0 = roll[0].transform.parent;
            view.TickRun(0);
            Assert.That(roll[0].gameObject.activeSelf, Is.False, "날아간 카드는 자리에서 숨김");
            Assert.That(holder0.gameObject.activeSelf, Is.True, "자리(Holder)는 남아 다른 카드가 밀리지 않음");
            Assert.That(view.SkillRoll.Active, Is.True, "둘째 장이 남음");

            k1.Chosen = true; k1.Flying = true; k1.FlyLeft = .62;
            view.TickRun(0);
            Assert.That(view.SkillRoll.Active, Is.False);
            s.Roulette = null;
            view.TickRun(0);
            Assert.AreEqual(0, view.SkillRoll.RollCards.Count, "룰렛이 끝나면 자리를 비움");
            Assert.That(roll[1].transform.parent.gameObject.activeSelf, Is.False, "복제 Holder는 풀로 반환(꺼짐)");
        }
    }
}
