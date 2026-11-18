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
    /// MV-UI2-HUD: 디자이너 IngameScreen을 런 HUD(UIHud_Run) 겉으로 쓰기 — 등급 그림 매핑, 지팡이 머지 과정, 계단 등급 카드, 스킬 롤링 결과, 배속 토글.
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

        (RunHud hud, IngameHudView view) NewHud()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            Assert.That(prefab, Is.Not.Null, HudPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            _made.Add(go);
            var hud = go.GetComponent<RunHud>();
            Assert.That(hud, Is.Not.Null, "RunHud");
            var view = hud.Presenter.View as IngameHudView;
            Assert.That(view, Is.Not.Null, "UIHud_Run 겉 = 자식 IngameScreen의 IngameHudView");
            return (hud, view);
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
            Assert.That(hud.GetComponentsInChildren<RunHudView>(true), Is.Empty, "1차 코드 겉을 만들지 않는다");
            var so = new SerializedObject(view);
            var it = so.GetIterator();
            var missing = new List<string>();
            while (it.NextVisible(true))
                if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == null && it.propertyPath != "m_Script")
                    missing.Add(it.propertyPath);
            Assert.That(missing, Is.Empty, "IngameHudView 참조");
            Assert.That(view.SpeedStateCount, Is.GreaterThan(1), "1x/2x 상태 자식");
            Assert.That(view.Equip, Is.Not.Null, "Equip/Slot_Weapon");
            Assert.That(view.StackSlots, Is.Not.Empty, "Stack/Slot_Weapon_Stack");
            Assert.That(view.SkillCard, Is.Not.Null, "Skill Section/Card_Skill");
            foreach (var slot in hud.GetComponentsInChildren<HudWeaponSlot>(true)) AssertRefs(slot);
            foreach (var card in hud.GetComponentsInChildren<HudClimbCard>(true)) AssertRefs(card);
            foreach (var card in hud.GetComponentsInChildren<HudSkillCard>(true)) AssertRefs(card);
        }

        static void AssertRefs(Object o)
        {
            var it = new SerializedObject(o).GetIterator();
            while (it.NextVisible(true))
                if (it.propertyType == SerializedPropertyType.ObjectReference && it.propertyPath != "m_Script")
                    Assert.That(it.objectReferenceValue, Is.Not.Null, o.name + "." + it.propertyPath);
        }

        [Test]
        public void IngameHud_ShowsValuesInDesignerFormats_AndSpeedMatchesState()
        {
            var (hud, view) = NewHud();
            var src = new FakeSource
            {
                State = new RunHudState { Level = 3, Gold = 12345, Dia = 7, Stairs = 15, GaugeLv = 2, GaugeFill = .5f, WeaponTier = 3, TimeScale = 2 },
            };
            hud.Presenter.Bind(src);
            hud.Presenter.Tick();
            Assert.AreEqual("Level 3", view.LevelText);
            Assert.AreEqual("12.3K", view.GoldText);
            Assert.AreEqual("7", view.DiaText);
            Assert.AreEqual("x15", view.StairCountText, "Count Text 서식 「x」+값");
            Assert.AreEqual("+2", view.StairValueText, "Slot_Stair Value 서식 「+」+칸당 계단");
            Assert.AreEqual(1, view.ShownSpeedState, "2배속 → 2x 자식만 켬");

            int asked = 0;
            MvRunSpeed.Set = x => asked = x;
            view.ClickSpeed();
            Assert.AreEqual(1, asked, "2x에서 누르면 1x 요청");
            var s = src.State; s.TimeScale = 1; src.State = s;
            hud.Presenter.Tick();
            Assert.AreEqual(0, view.ShownSpeedState, "표시는 런 배속 값을 따른다");
            view.ClickSpeed();
            Assert.AreEqual(2, asked);
        }

        [Test]
        public void IngameHud_RunState_DrivesWeaponsClimbCardsAndRoulette()
        {
            var (_, view) = NewHud();
            var s = NewState();
            view.BindRun(() => s);

            // 지팡이: 장착 = 최고 단계, 스택 = 나머지. 그림은 표(103 icon·등급 틀).
            s.W[1] = 2; s.W[3] = 1;
            view.TickRun(0);
            Assert.AreEqual(3, view.Equip.Tier);
            var item3 = T.Item.Datas.First(i => i.tier == 3);
            Assert.AreEqual(item3.icon, view.Equip.IconSprite);
            Assert.AreEqual(HudGrade.Frame(T, 3).sprite, view.Equip.GradeSprite);
            Assert.That(view.StackSlots.Where(x => x.gameObject.activeSelf).Select(x => x.Tier), Is.EqualTo(new[] { 1, 1 }));
            int mergeN = T.RunConfig.I(RunConfigKeys.MERGE_N);
            HudWeaponHand.Add(s.W, 1, mergeN, null);   // 시뮬이 넣고 머지한 결과(1단계 3장 → 2단계)
            view.TickRun(0);
            Assert.AreEqual(s.W[1], 0);
            Assert.That(view.StackSlots.Where(x => x.gameObject.activeSelf).Select(x => x.Tier), Is.EqualTo(new[] { 2 }), "머지 결과가 손패에 반영");

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
            Assert.AreEqual(HudGrade.CardName(T, pick), view.SkillCard.Title);
            k.Flying = true; k.FlyLeft = .62;
            view.TickRun(0);
            k.Applied = true; s.Cards[pick.id] = 1; s.CardOrder.Add(pick.id);
            s.Roulette = null;
            view.TickRun(0);
            Assert.That(view.PosCards.Count(c => c.gameObject.activeSelf), Is.EqualTo(1));
            Assert.AreEqual(pick.id, view.PosCards[0].CardId, "얻은 카드가 Card Pos에");
        }
    }
}
