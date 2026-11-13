using System.Collections.Generic;
using System.IO;
using MV.Data;
using MV.Run;
using NUnit.Framework;
using UnityEngine;

namespace MV.UI.Tests
{
    /// <summary>
    /// 가호 카드 룰렛 `#hc`·가호 줄 `#buffs`(05 §5.2.9·§5.2.7, 새 HTML startRoulette·updRoulette·flyCard).
    /// 그림 = `Atlas_UI_Card`(110 icon·115 frameSprite), 시간 = 시뮬 룰렛 시계.
    /// </summary>
    public sealed class BlessingCardsViewTests
    {
        const float U = MvUi.U;
        const string CardArtDir = "Game/Art/Source/Sprite/Atlas_UI_Card";

        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        static MvTables T => MetaUiTestKit.Base();

        static bool ArtExists(string sprite) =>
            !string.IsNullOrEmpty(sprite) && File.Exists(Path.Combine(Application.dataPath, CardArtDir, sprite + ".png"));

        [Test]
        public void Tables_ActiveCardIconsAndTierFrames_ExistInCardAtlas()
        {
            foreach (var c in T.Card.Datas)
                if (c.active) Assert.That(ArtExists(c.icon), c.id + " icon 「" + c.icon + "」 그림 없음");
            foreach (var r in T.CardTier.Datas)
                Assert.That(ArtExists(r.frameSprite), r.id + " frameSprite 「" + r.frameSprite + "」 그림 없음");
        }

        BlessingCardsView NewView(System.Func<RunState> state)
        {
            var go = new GameObject("BlessingCards", typeof(RectTransform));
            _made.Add(go);
            var v = go.AddComponent<BlessingCardsView>();
            v.Bind(state);
            return v;
        }

        static RunState NewState()
        {
            var s = new RunState(null, T, new Pcg32Rng(1));
            foreach (var c in T.Card.Datas) if (c.active && c.group <= 1) s.CardPool.Add(c);
            return s;
        }

        static CardData FirstOfTier(RunState s, int tier)
        {
            foreach (var c in s.CardPool) if (c.tier == tier) return c;
            Assert.Fail("등급 " + tier + " 카드 없음");
            return null;
        }

        static string Frame(int tier)
        {
            foreach (var r in T.CardTier.Datas) if (r.tierIndex == tier) return r.frameSprite;
            return null;
        }

        static RouletteState Roulette(RunState s, params int[] tiers)
        {
            var c = T.Climb.Config();
            var rl = new RouletteState();
            for (int i = 0; i < tiers.Length; i++)
            {
                rl.Tiers.Add(tiers[i]);
                rl.Cards.Add(new RouletteCard { Tier = tiers[i], Pick = FirstOfTier(s, tiers[i]), Step = i, EndAt = c["rlEndBase"] + i * c["rlEndStep"] });
            }
            s.Roulette = rl;
            return rl;
        }

        [Test]
        public void Roulette_CardsInCenterRow_FlipThroughTierPool()
        {
            var s = NewState();
            var rl = Roulette(s, 4, 2, 0);
            var v = NewView(() => s);
            rl.Cards[0].Flips = 1;
            v.Tick(0);

            Assert.AreEqual(3, v.CardCount);
            Assert.AreEqual(0, v.CardRect(1).anchoredPosition.x, 1e-3, "가운데 카드");
            Assert.AreEqual(-(25 + 3) * U, v.CardRect(0).anchoredPosition.x, 1e-3, "카드 25u, 사이 3u");
            Assert.AreEqual(Frame(4), v.CardSkin(0), "115 frameSprite");
            Assert.AreEqual(Frame(0), v.CardSkin(2));
            Assert.AreEqual("LEGENDARY", v.CardTier(0), "번역 없으면 표 이름 끝 낱말(새 HTML TIERS.n)");

            var pool = new List<CardData>();
            foreach (var c in s.CardPool) if (c.tier == 4) pool.Add(c);
            Assert.Greater(pool.Count, 1);
            Assert.AreEqual(pool[(0 + 1) % pool.Count].icon, v.CardArt(0), "넘김 = 그 등급 풀을 돈다");
            rl.Cards[0].Flips = 2;
            v.Tick(0);
            Assert.AreEqual(pool[2 % pool.Count].icon, v.CardArt(0));
        }

        [Test]
        public void Chosen_ShowsPick_ScalesFrom135To104_WithFlash()
        {
            var s = NewState();
            var rl = Roulette(s, 2);
            var v = NewView(() => s);
            var k = rl.Cards[0];
            k.Chosen = true;
            rl.T = k.EndAt;
            v.Tick(0);
            Assert.AreEqual(k.Pick.icon, v.CardArt(0));
            Assert.That(v.CardName(0), Is.Not.Empty);
            Assert.AreEqual(1.35f, v.CardRect(0).localScale.x, 1e-4, "chosen 0%");
            Assert.AreEqual(1 - 1 / 3f, v.CardFlash(0), 1e-4, "밝기 3");
            rl.T = k.EndAt + 0.8;
            v.Tick(0);
            Assert.AreEqual(1.04f, v.CardRect(0).localScale.x, 1e-4, "chosen 100% 유지");
            Assert.AreEqual(0f, v.CardFlash(0), 1e-4);
        }

        [Test]
        public void Fly_MovesCloneToBuffSlot_ThenSlotArrives()
        {
            var s = NewState();
            var rl = Roulette(s, 1);
            var v = NewView(() => s);
            var c = T.Climb.Config();
            double flyTime = T.RunConfig.D(RunConfigKeys.FlyCard_Time), total = c["rlFlyMs"] / 1000.0;
            var k = rl.Cards[0];
            k.Chosen = true; k.Flying = true;

            k.FlyLeft = total;
            v.Tick(0);
            var fly = v.FlyRect(0);
            Assert.NotNull(fly, "날기 시작하면 복제가 난다");
            Assert.AreEqual(0f, v.CardAlpha(0), "원본은 숨김");
            Assert.That(Vector3.Distance(fly.position, v.CardRect(0).position), Is.LessThan(1e-3f));

            k.FlyLeft = total - flyTime;
            v.Tick(0);
            var slot0 = ((RectTransform)v.transform.Find("Buffs")).TransformPoint(new Vector2(8.4f * U / 2, 8.4f * U / 2));
            Assert.That(Vector3.Distance(fly.position, slot0), Is.LessThan(1e-2f), "칸이 없으면 가호 줄 첫 자리");
            Assert.AreEqual(8.4f / 25f, fly.localScale.x, 1e-4, "칸 너비 / 카드 너비");
            Assert.AreEqual(0.85f, v.FlyGroupAlpha(0), 1e-4);

            k.Applied = true; k.Flying = false;
            s.Cards[k.Pick.id] = 1; s.CardOrder.Add(k.Pick.id);
            v.Tick(0);
            Assert.IsNull(v.FlyRect(0));
            Assert.AreEqual(1, v.SlotCount);
            Assert.AreEqual(1.6f, v.SlotRect(0).localScale.x, 1e-4, "arrive 0%");
            v.Tick(0.5f);
            v.Tick(0);
            Assert.AreEqual(1f, v.SlotRect(0).localScale.x, 1e-4, "arrive .5s 끝");
        }

        [Test]
        public void Buffs_TableOrder_WrapBottomUp_BadgeAndTierColor()
        {
            var s = NewState();
            var owned = new List<CardData>();
            foreach (var c in s.CardPool) owned.Add(c);
            owned.Sort((a, b) => a.id.CompareTo(b.id));
            int per = BlessingCardsView.SlotsPerRow;
            Assert.AreEqual(6, per, "05 §5.2.7 한 줄 최대 6개(62u, 8.4u, 사이 1u)");
            Assert.Greater(owned.Count, per);
            for (int i = owned.Count - 1; i >= 0; i--) { s.Cards[owned[i].id] = i == 0 ? 2 : 1; s.CardOrder.Add(owned[i].id); }
            var v = NewView(() => s);
            v.Tick(0);

            Assert.AreEqual(owned.Count, v.SlotCount);
            for (int j = 0; j < owned.Count; j++) Assert.AreEqual(owned[j].id, v.SlotCard(j), "정본 CARDS 순서(공용 → 신 고유)");
            Assert.AreEqual(owned[0].icon, v.SlotIcon(0));
            Assert.AreEqual("×2", v.SlotBadge(0));
            Assert.AreEqual("", v.SlotBadge(1), "1장이면 배지 없음");
            Assert.AreEqual(1f, v.SlotRect(0).localScale.x, "처음 보인 칸은 arrive 없음");
            Assert.AreEqual(v.SlotRect(0).anchoredPosition.y, v.SlotRect(per - 1).anchoredPosition.y, 1e-3);
            Assert.AreEqual((8.4f + 1) * U, v.SlotRect(per).anchoredPosition.y - v.SlotRect(0).anchoredPosition.y, 1e-3, "줄바꿈은 위로");
            Assert.AreEqual(v.SlotRect(0).anchoredPosition.x, v.SlotRect(per).anchoredPosition.x, 1e-3);
            string col = null;
            foreach (var r in T.CardTier.Datas) if (r.tierIndex == owned[0].tier) col = r.color;
            Assert.AreEqual(MvUi.Hex(col), v.SlotColor(0), "테두리 = 115 color");
        }

        [Test]
        public void NoRoulette_HidesRow_NoState_HidesAll()
        {
            var s = NewState();
            RunState cur = s;
            var v = NewView(() => cur);
            v.Tick(0);
            Assert.IsFalse(v.transform.Find("Cards/Row").gameObject.activeSelf, "룰렛 없으면 줄 숨김");
            cur = null;
            v.Tick(0);
            Assert.IsFalse(v.transform.Find("Buffs").gameObject.activeSelf, "런 없으면 가호 줄 숨김");
        }

        // ---------------- 마나 정산 종이 막(새 HTML drawWipes) ----------------

        [Test]
        public void ClimbWipe_Span_FollowsHtmlTimeline()
        {
            const float W = 1000;
            Assert.IsTrue(ClimbWipeView.Span(W, 0, -1, .12, -1, out float x0, out float x1));
            Assert.AreEqual(W, x0, 1e-3, "시작: 오른쪽 밖");
            Assert.IsTrue(ClimbWipeView.Span(W, .125, -1, .12, -1, out x0, out _));
            Assert.AreEqual(W * .5f, x0, 1e-3, "smoothstep(0.5) = 0.5");
            Assert.IsFalse(ClimbWipeView.Span(W, ClimbWipeView.SideIn, -1, .12, -1, out _, out _), "0.25초에 다 덮고 사라진다");
            Assert.IsFalse(ClimbWipeView.Span(W, 3, -1, .12, -1, out _, out _), "오르는 동안은 막 없음(옆 시점 계단이 보임)");
            Assert.IsTrue(ClimbWipeView.Span(W, 5.06, 5, .12, -1, out x0, out x1));
            Assert.AreEqual(W * .5f, x0, 1e-2, "다 오른 뒤 sideOut 절반");
            Assert.Greater(x1, W);
            Assert.IsTrue(ClimbWipeView.Span(W, -1, -1, .12, ClimbWipeView.OutSec / 2, out x0, out x1));
            Assert.AreEqual(W * .5f, x1, 1e-2, "끝: 왼쪽으로 빠짐");
            Assert.Less(x0, 0);
            Assert.IsFalse(ClimbWipeView.Span(W, -1, -1, .12, ClimbWipeView.OutSec, out _, out _));
            Assert.AreEqual(0f, ClimbWipeView.Ease(0)); Assert.AreEqual(1f, ClimbWipeView.Ease(1)); Assert.AreEqual(.5f, ClimbWipeView.Ease(.5f));
        }

        [Test]
        public void ClimbWipe_ShowsOnClimbStart_SlidesOutAfterEnd()
        {
            var s = NewState();
            var go = new GameObject("Wipe", typeof(RectTransform));
            _made.Add(go);
            ((RectTransform)go.transform).sizeDelta = new Vector2(1000, 2000);
            var v = go.AddComponent<ClimbWipeView>();
            v.Bind(() => s);
            v.Tick(0);
            Assert.IsFalse(v.Shown, "달리는 중에는 없음");

            s.Phase = RunPhase.Climb;
            s.Climb = new ClimbState { Time = .1 };
            v.Tick(0);
            Assert.IsTrue(v.Shown);
            Assert.AreEqual("마나 정산", v.Caption);
            s.Climb.Time = 1;
            v.Tick(0);
            Assert.IsFalse(v.Shown);

            s.Phase = RunPhase.Run;
            s.Climb = null;
            v.Tick(0);
            Assert.IsTrue(v.Shown, "정산 끝: 덮은 막이 왼쪽으로 빠지기 시작");
            Assert.AreEqual(1000, v.X1, 1e-2);
            v.Tick(ClimbWipeView.OutSec);
            Assert.IsFalse(v.Shown);
        }

        [Test]
        public void CubicBezier_MatchesCssEndpointsAndEaseOut()
        {
            Assert.AreEqual(0f, BlessingCardsView.CubicBezier(.55f, 0, .75f, .4f, 0));
            Assert.AreEqual(1f, BlessingCardsView.CubicBezier(.55f, 0, .75f, .4f, 1));
            Assert.Less(BlessingCardsView.CubicBezier(.55f, 0, .75f, .4f, .5f), .5f, "flycard 곡선은 느리게 시작");
            Assert.Greater(BlessingCardsView.CubicBezier(0, 0, .58f, 1, .5f), .5f, "ease-out은 빠르게 시작");
        }
    }
}
