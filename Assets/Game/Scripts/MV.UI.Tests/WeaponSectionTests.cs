using System.Collections.Generic;
using System.Linq;
using MV.Data;
using MV.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI.Tests
{
    /// <summary>
    /// 무기 섹션(MV-UI3-HUD4): 풀에서 손패 장 수만큼만 칸을 꺼낸다, 날아온 만큼 지급한다, 머지 결과가 시뮬과 같다.
    /// 움직임·반짝임은 재생 중에만 돌아 여기서는 끝 상태와 순서만 본다. 화면 연출은 사람 확인(NOT_RUN).
    /// </summary>
    public sealed class WeaponSectionTests
    {
        const string HudPath = "Assets/Game/Addressables/Local/UI/Hud/UIHud_Run.prefab";
        static MvTables T => MetaUiTestKit.Base();

        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
            MvHudFly.ClearForTests();
        }

        WeaponSection NewSection(out RunState s)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            Assert.That(prefab, Is.Not.Null, HudPath);
            var go = Object.Instantiate(prefab);
            _made.Add(go);
            var w = go.GetComponentInChildren<WeaponSection>(true);
            Assert.That(w, Is.Not.Null, "IngameScreen/Mid/Weapon 무기 섹션");
            var run = new RunState(null, T, new Pcg32Rng(1));
            s = run;
            w.BindRun(() => run);
            return w;
        }

        static void RunUntilIdle(WeaponSection w, float dt = 0.01f, float limit = 20)
        {
            for (float t = 0; t < limit; t += dt)
            {
                w.Tick(dt);
                if (!w.Busy) return;
            }
            Assert.Fail("손패가 끝나지 않음");
        }

        static Transform Stack(WeaponSection w) => w.StackPrefab ? w.transform.Find("Stack") : null;

        [Test]
        public void Start_ShowsOneSlotPerCard_DesignerExamplesHidden()
        {
            var w = NewSection(out var s);
            s.W[1] = 1;
            w.Tick(0);
            Assert.That(w.StackSlots.Select(x => x.Tier), Is.EqualTo(new[] { 1 }), "시작 = 시작 무기 1장");
            var stack = Stack(w);
            Assert.That(stack, Is.Not.Null, "Mid/Weapon/Stack");
            int shown = 0;
            foreach (Transform c in stack) if (c.gameObject.activeSelf) shown++;
            Assert.AreEqual(1, shown, "디자이너 예시 칸은 숨기고 풀에서 꺼낸 칸만 보인다");
            Assert.AreEqual(1, w.Equip.Tier);
        }

        [Test]
        public void Depth_FollowsCurrentHeight_UpperSlotInFront()
        {
            // 사용자 2026-11-19 「위로 올라가면서 한칸씩 밀면서 뎁스도 한칸 앞으로 나와야」: 깊이 = 지금 높이(위 칸이 앞).
            var w = NewSection(out var s);
            s.W[3] = 1;
            s.W[2] = 1;
            s.W[1] = 1;
            w.Tick(0);
            var slots = w.StackSlots.ToList();   // 위 → 아래
            Assert.That(slots.Select(x => x.Tier), Is.EqualTo(new[] { 3, 2, 1 }));
            Assert.That(w.SlotsBackToFront(), Is.EqualTo(new[] { slots[2], slots[1], slots[0] }), "맨 아래 칸이 맨 뒤, 맨 위 칸이 맨 앞");

            // 맨 아래 칸이 가운데 칸보다 높이 올라간 순간(올라가는 중): 가운데 칸 앞으로 한 칸 나온다.
            float mid = slots[1].rectTransform.anchoredPosition.y, top = slots[0].rectTransform.anchoredPosition.y;
            slots[2].rectTransform.anchoredPosition = new Vector2(0, (mid + top) * 0.5f);
            w.Tick(0);
            Assert.That(w.SlotsBackToFront(), Is.EqualTo(new[] { slots[1], slots[2], slots[0] }), "가운데 칸을 지나면 그 앞, 아직 맨 위 칸 뒤");
        }

        [Test]
        public void Deliver_WithoutFlight_DealsThenMergesLikeSim()
        {
            var w = NewSection(out var s);
            int mergeN = T.RunConfig.I(RunConfigKeys.MERGE_N);
            s.W[1] = 1;
            w.Tick(0);
            // 시뮬: 1단계 mergeN×2 − 1장을 더 받으면 1단계 mergeN×2장 → 2단계 2장(mergeN = 3 기준).
            int add = mergeN * 2 - 1;
            for (int i = 0; i < add; i++) HudWeaponHand.Add(s.W, 1, mergeN, null);
            w.OnRunEvent(new RunEvent(RunEventType.WeaponDeliver, a: add, b: 1));
            RunUntilIdle(w);
            var expect = new List<int>();
            for (int t = s.W.Length - 1; t >= 1; t--) for (int k = 0; k < s.W[t]; k++) expect.Add(t);
            Assert.That(w.Hand.Select(c => c.Tier), Is.EqualTo(expect), "모델 머지 결과 = 시뮬 손패");
            Assert.That(w.StackSlots.Select(x => x.Tier), Is.EqualTo(expect), "칸 = 손패");
            Assert.AreEqual(expect[0], w.Equip.Tier, "장착 = 최고 단계");
        }

        [Test]
        public void Deliver_WaitsForFlightArrivals_OneCardPerArrival()
        {
            var w = NewSection(out var s);
            s.W[1] = 1;
            w.Tick(0);
            int inFlight = 2;
            MvHudFly.InFlight = t => t == HudFlyTarget.Hand ? inFlight : 0;
            int mergeN = T.RunConfig.I(RunConfigKeys.MERGE_N);
            HudWeaponHand.Add(s.W, 1, mergeN, null);   // 시뮬은 이미 넣고 머지했다
            HudWeaponHand.Add(s.W, 1, mergeN, null);
            w.OnRunEvent(new RunEvent(RunEventType.WeaponDeliver, a: 2, b: 1));
            w.Tick(0.01f);
            Assert.AreEqual(2, w.PendingUnits, "날고 있는 동안은 손패에 넣지 않는다");

            inFlight = 1;
            w.OnFlyArrived(HudFlyTarget.Hand, 1);
            w.Tick(0.01f);
            Assert.AreEqual(1, w.PendingUnits, "도착 1번 = 1장");
            w.OnFlyArrived(HudFlyTarget.Gold, 1);
            w.Tick(0.01f);
            Assert.AreEqual(1, w.PendingUnits, "다른 대상 도착은 세지 않는다");

            inFlight = 0;
            w.OnFlyArrived(HudFlyTarget.Hand, 1);
            w.Tick(0.01f);
            Assert.AreEqual(0, w.PendingUnits);
        }

        /// <summary>
        /// 문 카드 줄서기(MV-UI3-HUD9): 문 위 카드가 HUD 카드로 바뀌면 스택 맨 아래 칸보다 밑에 한 줄로 서고(알파 1), 시뮬이 준 장마다 맨 앞 카드가
        /// 그대로(같은 칸 객체) 스택으로 올라간다. 받을 장보다 많이 선 카드는 잠시 뒤 치운다.
        /// </summary>
        [Test]
        public void Launch_QueuesBelowStack_ThenSameSlotsEnterUpward()
        {
            var w = NewSection(out var s);
            s.W[1] = 1;
            w.Tick(0);
            var rects = new List<Rect> { new Rect(100, 900, 60, 50), new Rect(170, 900, 60, 50), new Rect(240, 900, 60, 50) };
            w.OnLaunched(HudFlyTarget.Gold, rects);
            Assert.AreEqual(0, w.QueuedCards, "다른 대상은 받지 않는다");
            w.OnLaunched(HudFlyTarget.Hand, rects);
            Assert.AreEqual(3, w.QueuedCards);
            var queued = w.QueueSlots.ToList();
            var bottom = w.StackSlots.Last().rectTransform.anchoredPosition.y;
            float prev = bottom;
            foreach (var q in queued)
            {
                var y = q.rectTransform.anchoredPosition.y;
                Assert.Less(y, prev, "줄은 스택 맨 아래 칸 밑으로 차례로 선다");
                prev = y;
                Assert.AreEqual(1f, q.transform.localScale.x, 1e-4f, "도착하면 스택 칸 크기");
                foreach (var g in q.GetComponentsInChildren<Graphic>(false))   // 켜진 그림만(꺼 둔 머지 플래시는 제외)
                    Assert.AreEqual(1f, g.canvasRenderer.GetAlpha(), 1e-4f, "알파 변화 없음");
            }

            int mergeN = T.RunConfig.I(RunConfigKeys.MERGE_N);
            HudWeaponHand.Add(s.W, 1, mergeN, null);   // 시뮬은 2장만 받았다(1장은 거절)
            HudWeaponHand.Add(s.W, 1, mergeN, null);
            w.OnRunEvent(new RunEvent(RunEventType.WeaponDeliver, a: 2, b: 1));
            for (int i = 0; i < 2000 && w.StackSlots.Count() < 2; i++) w.Tick(0.005f);
            Assert.That(w.StackSlots.ToList(), Does.Contain(queued[0]), "줄 맨 앞 카드가 그대로 스택으로 들어간다(새 칸을 꺼내지 않는다)");
            Assert.AreEqual(1f, queued[0].transform.localScale.x, 1e-4f);
            RunUntilIdle(w);
            Assert.AreEqual(0, w.QueuedCards, "받지 못한 카드는 치운다");
            var expect = new List<int>();
            for (int t = s.W.Length - 1; t >= 1; t--) for (int k = 0; k < s.W[t]; k++) expect.Add(t);
            Assert.That(w.Hand.Select(c => c.Tier), Is.EqualTo(expect), "손패 = 시뮬");
        }

        [Test]
        public void FlyHandler_RentsFromPool_AndReturnsOnHide()
        {
            var w = NewSection(out var s);
            s.W[1] = 1;
            w.Tick(0);
            Assert.That(w.TryGetRect(out _, out var size), Is.True, "도착 자리 = 스택 다음 칸");
            Assert.That(size.x, Is.GreaterThan(0));
            w.Show(7, 1);
            w.Place(7, new Vector2(100, 100), 1.3f, 1f);
            Assert.AreEqual(1, w.ActiveFlies);
            w.Place(99, Vector2.zero, 1, 1);   // 남의 id는 무시
            w.Hide(7);
            Assert.AreEqual(0, w.ActiveFlies);
            Assert.That(w.StackSlots.Count(), Is.EqualTo(1), "비행 칸 반환은 손패를 바꾸지 않는다");
        }

        [Test]
        public void HandReset_Event_RebuildsFromIds()
        {
            var w = NewSection(out var s);
            s.W[1] = 1;
            w.Tick(0);
            var ids = new int[s.W.Length];
            ids[2] = 2;
            s.W[1] = 0; s.W[2] = 2;
            w.OnRunEvent(new RunEvent(RunEventType.WeaponHandReset, ids: ids));
            Assert.That(w.StackSlots.Select(x => x.Tier), Is.EqualTo(new[] { 2, 2 }));
            Assert.AreEqual(2, w.Equip.Tier);
        }
    }
}
