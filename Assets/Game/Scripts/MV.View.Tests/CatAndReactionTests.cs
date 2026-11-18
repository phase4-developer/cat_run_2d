using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MV.Data;
using MV.Run;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MV.View.Tests
{
    /// <summary>
    /// 티켓 09: 런 고양이 표시(02 §2.1.4 고양이 행, 그림은 MV-VIEW-CHAR-1 캐릭터 상태머신)와 오브젝트 맞음·막힘 반응(06 §6.13).
    /// 골든 값은 정본 `syncScene` L2763·`stairY` L2241·`mochiBack` L3682·`updObjMesh` L1464~1539·`drawRollGate` L3635 식을 Node(V8)로 돌린 결과다.
    /// 아틀라스를 로드하지 않으므로 스프라이트는 비어 있고 구조·계산만 본다.
    /// </summary>
    public sealed class CatAndReactionTests
    {
        readonly List<GameObject> _made = new List<GameObject>();
        readonly List<HitWorldRig> _rigs = new List<HitWorldRig>();

        [TearDown]
        public void TearDown()
        {
            foreach (var r in _rigs) r.Dispose();
            _rigs.Clear();
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        // ───────── 고양이 순수 계산 ─────────

        [Test]
        public void CloneOffsets_ReadSpawnGroupTable_AsProtoCloneOff()
        {
            var offs = new List<(double dx, double dz)>();
            CatLayout.CloneOffsets(ViewTestTables.Load(), offs);
            double[,] proto = { { -0.95, -0.9 }, { 0.95, -0.9 }, { -1.9, -1.8 }, { 1.9, -1.8 } };   // CLONE_OFF L831
            Assert.That(offs.Count, Is.EqualTo(30)); // MV-SPEC-02 Q-09: 30자리, 앞 4자리는 정본 값
            for (int i = 0; i < 4; i++)
            {
                Assert.That(offs[i].dx, Is.EqualTo(proto[i, 0]).Within(1e-12), "dx " + i);
                Assert.That(offs[i].dz, Is.EqualTo(proto[i, 1]).Within(1e-12), "dz " + i);
            }
        }

        [Test]
        public void ClonePos_ClampsXAndOffsetsZ_MatchesProto()
        {
            double[,] off = { { -0.95, -0.9 }, { 0.95, -0.9 }, { -1.9, -1.8 }, { 1.9, -1.8 } };
            double[,] goldA = { { 2.05, 9.1 }, { 3.4, 9.1 }, { 1.1, 8.2 }, { 3.4, 8.2 } };          // px 3, pz 10
            double[,] goldB = { { -1.35, 21.6 }, { 0.55, 21.6 }, { -2.3, 20.7 }, { 1.5, 20.7 } };    // px −0.4, pz 22.5
            for (int i = 0; i < 4; i++)
            {
                CatLayout.ClonePos(3, 10, off[i, 0], off[i, 1], 3.4, out double x, out double z);
                Assert.That(x, Is.EqualTo(goldA[i, 0]).Within(1e-9), "A x " + i);
                Assert.That(z, Is.EqualTo(goldA[i, 1]).Within(1e-9), "A z " + i);
                CatLayout.ClonePos(-0.4, 22.5, off[i, 0], off[i, 1], 3.4, out x, out z);
                Assert.That(x, Is.EqualTo(goldB[i, 0]).Within(1e-9), "B x " + i);
                Assert.That(z, Is.EqualTo(goldB[i, 1]).Within(1e-9), "B z " + i);
            }
        }

        [Test]
        public void StairY_OnlyLandedStepsInRange_MatchesProto()
        {
            // z0 5, stepD 0.72, stepH 0.42, k 20, 칸 0..5 내려앉음, 칸 6은 막 보임(t 0.1)
            bool Landed(int i) => i >= 0 && i < 6;
            double Y(double z)
            {
                int i = CatLayout.StairIndex(z, 5, 0.72);
                return CatLayout.StairY(i, 20, 0.42, Landed(i));
            }
            Assert.That(Y(4.9), Is.EqualTo(0).Within(1e-12), "첫 칸 앞");
            Assert.That(Y(5), Is.EqualTo(0.42).Within(1e-12));
            Assert.That(Y(7), Is.EqualTo(1.26).Within(1e-12));
            Assert.That(Y(9.4), Is.EqualTo(0).Within(1e-12), "아직 떨어지는 칸");
            Assert.That(CatLayout.StairY(34, 20, 0.42, true), Is.EqualTo(0).Within(1e-12), "k 밖");
        }

        // ───────── 캐릭터 상태머신(MV-VIEW-CHAR-1, 디자이너 PDF 재생 규칙) ─────────

        static CharAnimParams Anim() => CharAnimParams.From(ViewTestTables.Load());

        [Test]
        public void CharAnim_ReadsCharKeysFromRunConfig()
        {
            var t = ViewTestTables.Load();
            var p = CharAnimParams.From(t);
            Assert.That(p.FrameSec, Is.EqualTo(t.RunConfig.D(RunConfigKeys.Char_FrameSec)));
            Assert.That(p.DipAt, Is.EqualTo(t.RunConfig.D(RunConfigKeys.Char_DipAt)));
            Assert.That(p.DipM, Is.EqualTo(t.RunConfig.D(RunConfigKeys.Char_DipM)));
            Assert.That(p.AtkFrames, Is.EqualTo((int)t.RunConfig.D(RunConfigKeys.Char_AtkFrames)));
        }

        [Test]
        public void Walk_AlternatesAAndBEachFrame_OffsetByUnit()
        {
            var p = Anim();
            var sm = new CharStateMachine();
            sm.Set(CharState.Walk, 0);
            double f = p.FrameSec;
            Assert.That(sm.Evaluate(p, f * 0.5, 0).Pose, Is.EqualTo(CharPose.WalkA));
            Assert.That(sm.Evaluate(p, f * 1.5, 0).Pose, Is.EqualTo(CharPose.WalkB));
            Assert.That(sm.Evaluate(p, f * 2.5, 0).Pose, Is.EqualTo(CharPose.WalkA));
            Assert.That(sm.Evaluate(p, f * 0.5, 1).Pose, Is.EqualTo(CharPose.WalkB), "분신 1은 한 칸 어긋난다");
        }

        [Test]
        public void Attack_ShowsCOnAAndDOnB_ForAtkFrames()
        {
            var p = Anim();
            var sm = new CharStateMachine();
            sm.Set(CharState.Walk, 0);
            double f = p.FrameSec;
            sm.Shot(f * 4.2);
            Assert.That(sm.Evaluate(p, f * 4.3, 0).Pose, Is.EqualTo(CharPose.AtkA), "A 차례 → C");
            Assert.That(sm.Evaluate(p, f * (4.2 + p.AtkFrames) + f * 0.01, 0).Pose, Is.Not.EqualTo(CharPose.AtkA).And.Not.EqualTo(CharPose.AtkB), "유지 칸이 지나면 걷기");
            sm.Shot(f * 5.1);
            Assert.That(sm.Evaluate(p, f * 5.2, 0).Pose, Is.EqualTo(CharPose.AtkB), "B 차례 → D");
            Assert.That(sm.Evaluate(p, f * 5.0, 0).Pose, Is.EqualTo(CharPose.WalkB), "쏘기 전 시각은 걷기");
        }

        [Test]
        public void Dip_PeaksAtDipAt_ZeroAtFrameEdges()
        {
            var p = Anim();
            Assert.That(CharStateMachine.DipCurve(p.DipAt, p.DipAt), Is.EqualTo(1).Within(1e-12));
            Assert.That(CharStateMachine.DipCurve(0, p.DipAt), Is.EqualTo(0));
            Assert.That(CharStateMachine.DipCurve(0.999999, p.DipAt), Is.EqualTo(0).Within(1e-4));
            var sm = new CharStateMachine();
            sm.Set(CharState.Walk, 0);
            Assert.That(sm.Evaluate(p, p.FrameSec * (3 + p.DipAt), 0).Dip, Is.EqualTo(p.DipM).Within(1e-9));
        }

        [Test]
        public void StandAndHidden_ShowWalkAWithoutDip()
        {
            var p = Anim();
            var sm = new CharStateMachine();
            foreach (var st in new[] { CharState.Hidden, CharState.Stand })
            {
                sm.Set(st, 0);
                sm.Shot(p.FrameSec * 1.1);
                var fr = sm.Evaluate(p, p.FrameSec * (1 + p.DipAt), 0);
                Assert.That(fr.Pose, Is.EqualTo(CharPose.WalkA), st.ToString());
                Assert.That(fr.Dip, Is.EqualTo(0), st.ToString());
            }
        }

        [Test]
        public void SpriteOf_ReadsLookRowColumns()
        {
            var t = ViewTestTables.Load();
            var row = t.CharLook.Datas[0];
            Assert.That(CharStateMachine.SpriteOf(row, CharPose.WalkA), Is.EqualTo(row.walkA));
            Assert.That(CharStateMachine.SpriteOf(row, CharPose.WalkB), Is.EqualTo(row.walkB));
            Assert.That(CharStateMachine.SpriteOf(row, CharPose.AtkA), Is.EqualTo(row.atkA));
            Assert.That(CharStateMachine.SpriteOf(row, CharPose.AtkB), Is.EqualTo(row.atkB));
            Assert.That(CharStateMachine.SpriteOf(row, CharPose.Happy), Is.EqualTo(row.happy));
            Assert.That(CharStateMachine.SpriteOf(row, CharPose.Stair), Is.EqualTo(row.stair));
            Assert.That(CharStateMachine.SpriteOf(row, CharPose.HappyBack), Is.EqualTo(row.happyBack));
        }

        // ───────── 기쁨·계단 상태(MV-VIEW-CHAR-2) ─────────

        [Test]
        public void CharAnim_ReadsHappyAndStairKeysFromRunConfig()
        {
            var t = ViewTestTables.Load();
            var c = t.RunConfig;
            var p = CharAnimParams.From(t);
            Assert.That(p.HappySpinSec, Is.EqualTo(c.D(RunConfigKeys.Char_HappySpinSec)));
            Assert.That(p.StairRate, Is.EqualTo(c.D(RunConfigKeys.Char_StairRate)));
            Assert.That(p.StairRockRad, Is.EqualTo(c.D(RunConfigKeys.Char_StairRockRad)));
            Assert.That(p.StairBobM, Is.EqualTo(c.D(RunConfigKeys.Char_StairBobM)));
            Assert.That(p.StairSwayM, Is.EqualTo(c.D(RunConfigKeys.Char_StairSwayM)));
            Assert.That(p.StairIdleRate, Is.EqualTo(c.D(RunConfigKeys.Char_StairIdleRate)));
            Assert.That(p.StairIdleRockRad, Is.EqualTo(c.D(RunConfigKeys.Char_StairIdleRockRad)));
        }

        [Test]
        public void Happy_SpinsHappyPoseOverWalk_ThenReturnsToWalk()
        {
            var p = Anim();
            var sm = new CharStateMachine();
            sm.Set(CharState.Walk, 0);
            double t0 = 3.3;
            sm.Happy(t0);
            var start = sm.Evaluate(p, t0, 0);
            Assert.That(start.State, Is.EqualTo(CharState.Happy));
            Assert.That(start.Pose, Is.EqualTo(CharPose.Happy));
            Assert.That(start.FlipX, Is.EqualTo(1).Within(1e-12), "시작은 앞면");
            Assert.That(start.Dip, Is.EqualTo(0), "포즈 유지");
            double half = 1 - Math.Pow(0.5, 1.0 / 3);   // e = 0.5(반 바퀴) 시점
            Assert.That(sm.Evaluate(p, t0 + half * p.HappySpinSec, 0).FlipX, Is.EqualTo(-1).Within(1e-9), "반 바퀴면 뒷면");
            var after = sm.Evaluate(p, t0 + p.HappySpinSec + 1e-6, 0);
            Assert.That(after.State, Is.EqualTo(CharState.Walk), "1바퀴 뒤 걷기");
            Assert.That(after.FlipX, Is.EqualTo(1));
            Assert.That(sm.Evaluate(p, t0 - 0.01, 0).State, Is.EqualTo(CharState.Walk), "받기 전 시각은 걷기");
            Assert.That(CharStateMachine.SpinFlip(0.999), Is.EqualTo(1).Within(1e-6), "끝에서 앞면으로 돌아온다");
        }

        [Test]
        public void Happy_AlsoOverStand_NotOverStairOrHidden()
        {
            var p = Anim();
            var sm = new CharStateMachine();
            sm.Set(CharState.Stand, 0);
            sm.Happy(1);
            Assert.That(sm.Evaluate(p, 1.1, 0).State, Is.EqualTo(CharState.Happy));
            sm.Set(CharState.Stair, 1);
            Assert.That(sm.Evaluate(p, 1.1, 0).State, Is.EqualTo(CharState.Stair));
            sm.Set(CharState.Hidden, 1);
            Assert.That(sm.Evaluate(p, 1.1, 0).State, Is.EqualTo(CharState.Hidden));
        }

        [Test]
        public void Stair_HoldsStairPose_ShakesWhileClimbing_RocksBeforeClimb()
        {
            var p = Anim();
            var sm = new CharStateMachine();
            sm.Set(CharState.Stair, 0);
            sm.Shot(0.1);
            double idleAt = Math.PI / 2 / p.StairIdleRate;
            var idle = sm.Evaluate(p, idleAt, 0);
            Assert.That(idle.Pose, Is.EqualTo(CharPose.Stair), "공격 그림이 겹치지 않는다");
            Assert.That(idle.Roll, Is.EqualTo(p.StairIdleRockRad).Within(1e-9));
            Assert.That(idle.Dip, Is.EqualTo(0));
            Assert.That(idle.SwayX, Is.EqualTo(0));
            sm.Climbing = true;
            double at = Math.PI / 2 / p.StairRate;   // sin = 1
            var up = sm.Evaluate(p, at, 0);
            Assert.That(up.Pose, Is.EqualTo(CharPose.Stair));
            Assert.That(up.Roll, Is.EqualTo(p.StairRockRad).Within(1e-9));
            Assert.That(up.Dip, Is.EqualTo(-p.StairBobM).Within(1e-9), "위로 튐");
            Assert.That(up.SwayX, Is.EqualTo(p.StairSwayM).Within(1e-9));
            Assert.That(sm.Evaluate(p, at * 3, 0).Dip, Is.EqualTo(-p.StairBobM).Within(1e-9), "sin = −1에서도 위로");
        }

        // ───────── 기쁨 뒷면·정산 바퀴(MV-VIEW-CHAR-3, 노션 「신작 캐릭터 리소스 관리」) ─────────

        [Test]
        public void CharAnim_ReadsWheelTurnsPerStepFromRunConfig()
        {
            var t = ViewTestTables.Load();
            Assert.That(CharAnimParams.From(t).WheelTurnsPerStep, Is.EqualTo(t.RunConfig.D(RunConfigKeys.Char_WheelTurnsPerStep)));
        }

        [Test]
        public void Happy_ShowsBackPoseWhileFlipIsNegative()
        {
            var p = Anim();
            var sm = new CharStateMachine();
            sm.Set(CharState.Walk, 0);
            sm.Happy(0);
            double half = 1 - Math.Pow(0.5, 1.0 / 3);   // e = 0.5(반 바퀴)
            var back = sm.Evaluate(p, half * p.HappySpinSec, 0);
            Assert.That(back.FlipX, Is.LessThan(0));
            Assert.That(back.Pose, Is.EqualTo(CharPose.HappyBack));
            double quarter = 1 - Math.Pow(0.9, 1.0 / 3);   // e = 0.1, 앞면
            var front = sm.Evaluate(p, quarter * p.HappySpinSec, 0);
            Assert.That(front.FlipX, Is.GreaterThan(0));
            Assert.That(front.Pose, Is.EqualTo(CharPose.Happy));
        }

        [Test]
        public void Stair_WheelTurnsWithClimbedSteps_StopsWhenClimbStops()
        {
            var p = Anim();
            var sm = new CharStateMachine();
            sm.Set(CharState.Walk, 0);
            Assert.That(sm.Evaluate(p, 0.3, 0).HasWheel, Is.False, "걷기에는 바퀴 없음");
            sm.Set(CharState.Stair, 0);
            sm.ClimbedSteps = 0;
            Assert.That(sm.Evaluate(p, 0.3, 0).WheelTurns, Is.EqualTo(0), "오르기 전 바퀴 멈춤");
            sm.Climbing = true;
            sm.ClimbedSteps = 2.5;
            var a = sm.Evaluate(p, 1.0, 0);
            Assert.That(a.HasWheel, Is.True);
            Assert.That(a.WheelTurns, Is.EqualTo(2.5 * p.WheelTurnsPerStep).Within(1e-12), "오른 칸 × 칸당 바퀴");
            Assert.That(sm.Evaluate(p, 2.0, 0).WheelTurns, Is.EqualTo(a.WheelTurns), "칸이 그대로면 시간이 흘러도 그대로");
        }

        // ───────── 캐릭터 뷰 구조 ─────────

        [Test]
        public void CharacterPresenter_PicksLookByTierAndSlot_StateByPhase_HidesWithoutArt()
        {
            var t = ViewTestTables.Load();
            var go = new GameObject("Cats");
            _made.Add(go);
            var view = go.AddComponent<CharacterPresenter>();
            view.Init(t, ViewTestTables.Config(), null, null);
            int cloneRows = t.SpawnGroup.Sorted.Count(g => g.key == "clone");
            Assert.That(view.FriendSlots, Is.EqualTo(cloneRows), "분신 자리 = 314 clone 행 수");

            var s = new RunState(null, t, new Pcg32Rng(1));
            s.Clones = 2;
            view.Sync(s, 0.1);
            Assert.That(view.VisibleCount, Is.EqualTo(0), "그림이 아직 없으면 그리지 않는다");
            int player = t.Cat.ByKey(CharacterPresenter.PlayerKey).id, friend = t.Cat.ByKey(CharacterPresenter.FriendKey).id;
            int tier = s.EqTier();
            Assert.That(view.LookOf(0), Is.SameAs(CharLookResolver.Resolve(t.CharLook.Datas, player, 0, tier)));
            Assert.That(view.LookOf(2), Is.SameAs(CharLookResolver.Resolve(t.CharLook.Datas, friend, 1, tier)), "분신 2번째 = slot 1(없으면 마지막 slot)");
            Assert.That(view.StateOf(0), Is.EqualTo(CharState.Walk), "런 중 걷기");
            Assert.That(view.StateOf(3), Is.EqualTo(CharState.Hidden), "분신 수 밖 자리는 숨김");
            Assert.That(view.SpriteNameOf(0), Is.Not.Empty);

            s.Phase = RunPhase.Result;
            s.Fin = true;
            view.Sync(s, 0.2);
            Assert.That(view.StateOf(0), Is.EqualTo(CharState.Stand), "런이 끝나면 서 있음");
            Assert.That(view.SpriteNameOf(0), Is.EqualTo(view.LookOf(0).walkA));
            view.Sync(null, 0.3);
            Assert.That(view.StateOf(0), Is.EqualTo(CharState.Hidden));
            Assert.That(view.VisibleCount, Is.EqualTo(0));
        }

        [Test]
        public void CharacterPresenter_FireEventMarksShooterUnit()
        {
            var t = ViewTestTables.Load();
            var go = new GameObject("Cats");
            _made.Add(go);
            var view = go.AddComponent<CharacterPresenter>();
            view.Init(t, ViewTestTables.Config(), null, null);
            var p = Anim();
            var s = new RunState(null, t, new Pcg32Rng(1));
            s.Clones = 1;
            double at = p.FrameSec * 6.2;   // 칸 6: 플레이어(unit 0) A, 분신(unit 1) B
            view.OnRunEvents(new[] { new RunEvent(RunEventType.Fire, a: 1) }, at);
            view.Sync(s, at + p.FrameSec * 0.1);
            Assert.That(view.SpriteNameOf(1), Is.EqualTo(view.LookOf(1).atkB), "쏜 분신은 공격 그림");
            Assert.That(view.SpriteNameOf(0), Is.EqualTo(view.LookOf(0).walkA), "쏘지 않은 플레이어는 걷기");
        }

        [TestCase(RunEventType.GateTake)]
        [TestCase(RunEventType.GaugeLevelUp)]
        public void CharacterPresenter_HappyEventSpinsEveryone(RunEventType type)
        {
            var t = ViewTestTables.Load();
            var go = new GameObject("Cats");
            _made.Add(go);
            var view = go.AddComponent<CharacterPresenter>();
            view.Init(t, ViewTestTables.Config(), null, null);
            var p = Anim();
            var s = new RunState(null, t, new Pcg32Rng(1));
            s.Clones = 1;
            view.OnRunEvents(new[] { new RunEvent(type) }, 2.0);
            view.Sync(s, 2.0 + p.HappySpinSec * 0.5);
            Assert.That(view.StateOf(0), Is.EqualTo(CharState.Happy));
            Assert.That(view.StateOf(1), Is.EqualTo(CharState.Happy));
            Assert.That(view.SpriteNameOf(0), Is.EqualTo(view.LookOf(0).happy));
            view.Sync(s, 2.0 + p.HappySpinSec + 1e-6);
            Assert.That(view.StateOf(0), Is.EqualTo(CharState.Walk), "1바퀴 뒤 걷기");
        }

        [Test]
        public void CharacterPresenter_ClimbPhase_PlayerStair_ClonesHiddenUntilRun()
        {
            var t = ViewTestTables.Load();
            var go = new GameObject("Cats");
            _made.Add(go);
            var view = go.AddComponent<CharacterPresenter>();
            view.Init(t, ViewTestTables.Config(), null, null);
            var s = new RunState(null, t, new Pcg32Rng(1));
            s.Clones = 1;
            s.Phase = RunPhase.Climb;
            s.Climb = new ClimbState { N = 3, K = 3, Z0 = s.P.Z, ZEnd = s.P.Z + 3, T = 1 };
            // 새 HTML drawClimbCat: 옆 시점으로 돌기 전(e < 0.5)은 뒷모습(걷기), e ≥ 0.5면 옆모습(계단 그림).
            view.Sync(s, 1.0, 0.2);
            Assert.That(view.StateOf(0), Is.EqualTo(CharState.Walk), "돌기 전반: 뒷모습을 접는다");
            Assert.That(view.StateOf(1), Is.EqualTo(CharState.Walk), "e ≤ 0.3이면 분신이 아직 보인다(새 HTML)");
            view.Sync(s, 1.0, 1.0);
            Assert.That(view.StateOf(0), Is.EqualTo(CharState.Stair));
            Assert.That(view.SpriteNameOf(0), Is.EqualTo(view.LookOf(0).stair));
            Assert.That(view.StateOf(1), Is.EqualTo(CharState.Hidden), "사용자 계단 규칙 6: 정산 중 분신 숨김");
            s.Phase = RunPhase.Run;
            s.Climb = null;
            view.Sync(s, 2.0, 0.6);
            Assert.That(view.StateOf(0), Is.EqualTo(CharState.Stair), "끝난 뒤 되돌아오는 동안(e ≥ 0.5)은 옆모습");
            Assert.That(view.StateOf(1), Is.EqualTo(CharState.Hidden), "되돌아오는 중 e > 0.3이면 분신은 아직 숨김");
            view.Sync(s, 2.0, 0);
            Assert.That(view.StateOf(0), Is.EqualTo(CharState.Walk));
            Assert.That(view.StateOf(1), Is.EqualTo(CharState.Walk), "정산이 끝나면 다시 나타남");
        }

        [Test]
        public void OrbitFlip_FoldsBackThenUnfoldsSide()
        {
            Assert.That(CharacterPresenter.OrbitFlip(0), Is.EqualTo(1).Within(1e-12));
            Assert.That(CharacterPresenter.OrbitFlip(0.25), Is.EqualTo(Math.Cos(0.25 * Math.PI)).Within(1e-12));
            Assert.That(CharacterPresenter.OrbitFlip(0.5), Is.EqualTo(0).Within(1e-12), "가운데서 종이가 옆으로 선다");
            Assert.That(CharacterPresenter.OrbitFlip(0.75), Is.EqualTo(-Math.Cos(0.75 * Math.PI)).Within(1e-12));
            Assert.That(CharacterPresenter.OrbitFlip(1), Is.EqualTo(1).Within(1e-12));
        }

        // ───────── 로비 트랙(MV-VIEW-LOBBY-TRACK) ─────────

        [Test]
        public void LobbyTrack_KeepsObjectsWithinZMaxOfBase_InOrder()
        {
            var t = ViewTestTables.Load();
            double zMax = t.RunConfig.D(RunConfigKeys.Lobby_ObjZMax);
            const double baseZ = 12;
            var objs = new List<RunObject>();
            foreach (double z in new[] { baseZ, baseZ + zMax * 0.5, baseZ + zMax - 1e-6, baseZ + zMax, baseZ + zMax * 2 })
                objs.Add(new RunObject { Z = z });
            var picked = new List<RunObject> { new RunObject() };
            LobbyTrack.Pick(objs, baseZ, zMax, picked);
            Assert.That(picked, Is.EqualTo(objs.Take(3).ToList()), "시작점 + zMax 미만만, 순서 그대로");
            LobbyTrack.Pick(null, baseZ, zMax, picked);
            Assert.That(picked, Is.Empty);
        }

        // ───────── 오브젝트 반응 순수 계산 ─────────

        [Test]
        public void Shake_WallAndWell_MatchProto()
        {
            Assert.That(ObjectReaction.WallShakeX(0.1), Is.EqualTo(0.020605924262087832).Within(1e-12));
            Assert.That(ObjectReaction.WellShakeX(0.05), Is.EqualTo(-0.04540814971847569).Within(1e-12));
            Assert.That(ObjectReaction.WallShakeX(0), Is.EqualTo(0));
        }

        [Test]
        public void GateSinkAndPickupScale_MatchProto()
        {
            Assert.That(ObjectReaction.GateSinkY(0.3), Is.EqualTo(-1.8).Within(1e-12));
            Assert.That(ObjectReaction.GateHidden(0.5), Is.False);
            Assert.That(ObjectReaction.GateHidden(0.51), Is.True);
            Assert.That(ObjectReaction.PickupScale(5, 10, false), Is.EqualTo(0.775).Within(1e-12));
            Assert.That(ObjectReaction.PickupScale(10, 10, false), Is.EqualTo(1).Within(1e-12));
            Assert.That(ObjectReaction.PickupScale(3, 10, true), Is.EqualTo(0));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void GateStripeY_ScrollsUp_MatchesProto(int c)
        {
            double[] t = { 1.5, 0, 3.7 };
            int[] i = { 2, 0, 4 };
            double[] y = { -0.5600000000000005, -1.4, -1.3439999999999985 };
            Assert.That(ObjectReaction.StripeY(i[c], t[c], 1.4), Is.EqualTo(y[c]).Within(1e-9));
        }

        [Test]
        public void BrickStep_HiddenThenRegrowsAtFivePerSecond()
        {
            double gone = ObjectReaction.BrickGoneSec, grow = 0;
            Assert.That(ObjectReaction.BrickStep(ref gone, ref grow, 1.0), Is.EqualTo(0), "숨김 중");
            Assert.That(ObjectReaction.BrickStep(ref gone, ref grow, 1.3), Is.EqualTo(0.01).Within(1e-12), "2.2 s 뒤 다시 보임");
            Assert.That(ObjectReaction.BrickStep(ref gone, ref grow, 0.1), Is.EqualTo(0.5).Within(1e-12));
            Assert.That(ObjectReaction.BrickStep(ref gone, ref grow, 0.2), Is.EqualTo(1).Within(1e-12));
        }

        // ───────── 오브젝트 뷰 이벤트 ─────────

        // 반응 상태는 그 오브젝트 뷰의 모듈이 가진다(관리자는 모르고 묻지 않는다).
        static RunObjectView ViewComp(ObjectViewFactory f, int id) => f.ViewOf(id) != null ? f.ViewOf(id).GetComponent<RunObjectView>() : null;

        static T Mod<T>(ObjectViewFactory f, int id) where T : RunObjectModule => ViewComp(f, id)?.Module<T>();

        static RunObject Obj(MvTables t, string key, int id, double z)
        {
            var o = RunObjectFactory.Create(t, t.ObjectKind.ByKey(key), 0, z);
            o.Id = id;
            if (key == "wall")
            {
                o.Idx = 0;                  // 필드 벽(305 보상 행)
                o.Rew = FieldReward.Gold;   // → Stage1_Block_Gold
            }
            return o;
        }

        /// <summary>게임과 같은 경로: 관리자 → 305·105 프리팹 주소 → 실제 프리팹 인스턴스 → 프리팹 모듈.</summary>
        HitWorldRig PrefabRig(MvTables t)
        {
            var rig = new HitWorldRig(t);
            _rigs.Add(rig);
            return rig;
        }

        /// <summary>프레임 사이 실시간 경과(Sync dt는 실시간, 한 번에 최대 0.1초).</summary>
        static void Frames(HitWorldRig rig, IReadOnlyList<RunObject> objs, int n)
        {
            for (int i = 0; i < n; i++)
            {
                System.Threading.Thread.Sleep(20);
                rig.Frame(0, objs);
            }
        }

        static Transform Board(ObjectViewFactory f, int id) => f.ViewOf(id).GetComponentInChildren<Billboard>(true).transform;

        static Transform Hitbox(ObjectViewFactory f, int id) => f.ViewOf(id).GetComponentInChildren<RunHitbox>(true).transform;

        /// <summary>
        /// 실제 프리팹 모듈이 오브젝트 대상 이벤트만 받아 표대로 반응한다: 블록 흔들림(WallBlockFx), 관문 막 번쩍(GateCurtainFx),
        /// 수정탑 흔들림·구슬 빠짐(TowerOrbs). 같은 번호를 실은 투사체 이벤트와 뷰 없는 번호는 아무 뷰에도 가지 않는다.
        /// </summary>
        [Test]
        public void PrefabModules_ReactToObjectEventsOnly_FromTable()
        {
            var t = ViewTestTables.Load();
            var rig = PrefabRig(t);
            var f = rig.Factory;
            var objs = new List<RunObject> { Obj(t, "wall", 1, 15), Obj(t, "sgate", 2, 20), Obj(t, "tower", 3, 25) };
            rig.Frame(0, objs);
            Assert.That(f.TryPrefabView(1, out string a1, out var i1) && i1 != null, Is.True, "블록 프리팹 인스턴스");
            Assert.That(a1, Is.EqualTo("Stage1_Block_Gold"));
            Assert.That(f.TryPrefabView(2, out _, out var i2) && i2 != null, Is.True, "관문 프리팹 인스턴스");
            Assert.That(f.TryPrefabView(3, out _, out var i3) && i3 != null, Is.True, "수정탑 프리팹 인스턴스");
            var wall = Mod<WallBlockFx>(f, 1);
            var gate = Mod<GateCurtainFx>(f, 2);
            var tower = Mod<TowerOrbs>(f, 3);
            Assert.That(wall, Is.Not.Null, "블록 프리팹 WallBlockFx");
            Assert.That(gate, Is.Not.Null, "관문 프리팹 GateCurtainFx");
            Assert.That(tower, Is.Not.Null, "수정탑 프리팹 TowerOrbs");
            Assert.That(gate.Curtain, Is.Not.Null, "막 그림이 프리팹 안 직렬화 참조로 연결");
            Assert.That(tower.OrbCount, Is.GreaterThan(3), "구슬 참조");

            // 투사체 번호(Fire·ProjImpact·ProjRemoved)는 오브젝트 번호와 겹쳐도 오브젝트 뷰로 가지 않는다(RunEvent.TargetsObject).
            f.OnRunEvents(new List<RunEvent>
            {
                new RunEvent(RunEventType.Fire, objId: 1),
                new RunEvent(RunEventType.ProjImpact, objId: 2),
                new RunEvent(RunEventType.ProjRemoved, objId: 3),
            });
            Assert.That(wall.Shake, Is.EqualTo(0));
            Assert.That(gate.Flash, Is.EqualTo(0));
            Assert.That(tower.Shake, Is.EqualTo(0));
            Assert.That(Mod<ObjectMotion>(f, 1).Punch, Is.EqualTo(0), "투사체 이벤트로 눌리지 않음");

            f.OnRunEvents(new List<RunEvent>
            {
                new RunEvent(RunEventType.Hit, objId: 1, kindId: objs[0].Kind.id),
                new RunEvent(RunEventType.Hit, objId: 2, kindId: objs[1].Kind.id),
                new RunEvent(RunEventType.Hit, objId: 3, kindId: objs[2].Kind.id),
                new RunEvent(RunEventType.WellFlush, objId: 3, a: 3, amount: 10),
                new RunEvent(RunEventType.Hit, objId: 99),
            });
            Assert.That(wall.Shake, Is.EqualTo(objs[0].Reaction.objShake > 0 ? ObjectReaction.ShakeSec : 0), "106 objShake → 블록 흔들림");
            Assert.That(gate.Flash, Is.EqualTo(objs[1].Reaction.flash ? ObjectReaction.FlashSec : 0), "106 flash → 관문 막 번쩍");
            Assert.That(tower.Shake, Is.EqualTo(objs[2].Reaction.objShake > 0 ? ObjectReaction.ShakeSec : 0), "106 objShake → 구슬 묶음 흔들림");
            Assert.That(tower.GoneCount, Is.EqualTo(3), "구슬 3개 빠짐");
            f.OnRunEvents(new List<RunEvent> { new RunEvent(RunEventType.WellFlush, objId: 3, a: 60) });
            Assert.That(tower.GoneCount, Is.EqualTo(tower.OrbCount), "남은 구슬보다 많으면 있는 만큼만");
        }

        /// <summary>
        /// 무기문 위 카드 묶음(MV-UI3-HUD9, 임시 리소스, 사용자 2026-11-19 「CreaftGate용 … 다른덴 추가하지마」): Obj_CraftGate 변형에만 있다.
        /// 장 수 = Made + Extra(상한 = 프리팹 MaxCards), 왼쪽 기둥부터 오른쪽으로 왼쪽 정렬하고 오른쪽 기둥을 넘으면 위 줄, 통과(Done)하면 카드마다 화면 자리를 넘기고 숨긴다.
        /// </summary>
        [Test]
        public void CraftGateBundle_ShowsMadePlusExtra_ReleasesOnPass()
        {
            var t = ViewTestTables.Load();
            var rig = PrefabRig(t);
            var f = rig.Factory;
            var gate = Obj(t, "cgate", 1, 20);
            gate.Made = 6;
            gate.Extra = 2;
            var stair = Obj(t, "sgate", 2, 24);
            var objs = new List<RunObject> { gate, stair };
            rig.Frame(0, objs);
            Assert.That(Mod<GateRewardDisplay>(f, 2), Is.Null, "계단문에는 묶음이 없다(무기문 전용)");
            var bundle = Mod<CraftGateBundle>(f, 1);
            Assert.That(bundle, Is.Not.Null, "Obj_CraftGate 프리팹 CraftGateBundle");
            Assert.AreEqual(GateRewardKind.Weapon, bundle.Kind);
            Assert.That(bundle.Template, Is.Not.Null, "카드 원본 직렬화 참조");
            Assert.That(bundle.Template.Frame, Is.Not.Null, "카드 틀 참조");
            Assert.That(bundle.Template.Icon, Is.Not.Null, "카드 그림 참조");
            Assert.That(bundle.Template.gameObject.activeSelf, Is.False, "원본은 꺼 둔다");
            Assert.AreEqual(System.Math.Min(8, bundle.MaxCards), bundle.Shown, "Made + Extra(프리팹 상한까지)");

            // 왼쪽 정렬: 첫 장 = 왼쪽 기둥, 오른쪽으로 같은 간격, 오른쪽 기둥을 넘기 전에 위 줄
            var cards = bundle.Cards;
            float x0 = cards[0].transform.localPosition.x, y0 = cards[0].transform.localPosition.y;
            int cols = 0;
            foreach (var c in cards) if (UnityEngine.Mathf.Approximately(c.transform.localPosition.y, y0)) cols++;
            Assert.AreEqual(bundle.Columns, cols, "한 줄 장 수 = 기둥 사이에 간격으로 들어가는 수");
            Assert.That(cols, Is.GreaterThan(1).And.LessThan(8), "한 줄에 늘어뜨리지 않고 줄을 늘린다");
            Assert.AreEqual(-1.25f, x0, 1e-4f, "왼쪽 끝 = 왼쪽 기둥");
            float gap = cards[1].transform.localPosition.x - x0;
            Assert.Greater(gap, 0f);
            Assert.LessOrEqual(cards[cols - 1].transform.localPosition.x, 1.25f + 1e-4f, "오른쪽 기둥을 넘지 않는다");
            Assert.Greater(cards[cols - 1].transform.localPosition.x + gap, 1.25f, "한 장 더 놓으면 오른쪽 기둥을 넘는다");
            Assert.Greater(cards[cols].transform.localPosition.y, y0, "넘치면 위 줄");
            Assert.AreEqual(x0, cards[cols].transform.localPosition.x, 1e-4f, "위 줄도 왼쪽 기둥부터");

            gate.Made = 3; gate.Extra = 0;
            rig.Frame(0.1, objs);
            Assert.AreEqual(3, bundle.Shown, "줄면 풀로 돌려준다");

            var got = new List<int>();
            System.Action<GateRewardKind, IReadOnlyList<GateCardRelease>> on = (k, list) => got.Add(list.Count);
            GateRewardDisplay.Released += on;
            try
            {
                gate.Done = true;
                rig.Frame(0.2, objs);
                rig.Frame(0.3, objs);
            }
            finally { GateRewardDisplay.Released -= on; }
            Assert.AreEqual(0, bundle.Shown, "통과하면 숨김(HUD 카드로 바뀐다)");
            Assert.That(got, Is.EqualTo(new[] { 3 }), "통과 순간 한 번, 보이던 장 수만큼 넘긴다");
        }

        /// <summary>눌림(106.punch)은 실제 프리팹의 빌보드 묶음 크기만 바꾸고, 뷰 뿌리와 Hitbox Collider의 위치·크기는 그대로다.</summary>
        [Test]
        public void Hit_PunchFromTable_MatchesNewHtml()
        {
            var t = ViewTestTables.Load();
            // 새 HTML hit(2026-11-13 연출 정본): o.punch 벽 1, 금화·원석 1, 게이트 0.5, 수정탑 0.6. 흔들림 벽·수정탑.
            Assert.AreEqual(1, t.HitReaction.ByKey("damageWall").punch);
            Assert.AreEqual(1, t.HitReaction.ByKey("damageBox").punch);
            Assert.AreEqual(0.5, t.HitReaction.ByKey("chargeStairGate").punch);
            Assert.AreEqual(0.5, t.HitReaction.ByKey("chargeCraftGate").punch);
            Assert.AreEqual(0.6, t.HitReaction.ByKey("chargeWell").punch);
            Assert.Greater(t.HitReaction.ByKey("chargeWell").objShake, 0);
            Assert.Greater(t.HitReaction.ByKey("damageWall").objShake, 0);

            var rig = PrefabRig(t);
            var f = rig.Factory;
            var objs = new List<RunObject> { Obj(t, "wall", 1, 15), Obj(t, "sgate", 2, 20) };
            rig.Frame(0, objs);
            var board = Board(f, 1);
            var hb = Hitbox(f, 1);
            var hbCol = hb.GetComponent<Collider>();
            Physics.SyncTransforms();
            Vector3 boardScale = board.localScale, hbPos = hb.position, hbScale = hb.lossyScale;
            var hbBounds = hbCol.bounds;
            f.OnRunEvents(new List<RunEvent>
            {
                new RunEvent(RunEventType.Hit, objId: 1, kindId: objs[0].Kind.id),
                new RunEvent(RunEventType.Hit, objId: 2, kindId: objs[1].Kind.id),
            });
            Assert.AreEqual(objs[0].Reaction.punch, Mod<ObjectMotion>(f, 1).Punch, "106 punch → 눌림 시작값");
            Assert.AreEqual(-1, Mod<ObjectMotion>(f, 1).Die);
            Assert.AreEqual(objs[1].Reaction.punch, Mod<ObjectMotion>(f, 2).Punch);
            Frames(rig, objs, 1);
            Assert.AreEqual(Vector3.one, f.ViewOf(1).transform.localScale, "뷰 뿌리는 그대로(판정·위치 기준)");
            var s = board.localScale;   // 눌림은 그림 묶음만
            Assert.Greater(s.x, boardScale.x, "눌림: 가로로 넓어지고");
            Assert.Less(s.y, boardScale.y, "세로로 눌린다");
            Physics.SyncTransforms();
            Assert.That(hb.position, Is.EqualTo(hbPos), "Hitbox 위치 그대로");
            Assert.That(hb.lossyScale, Is.EqualTo(hbScale), "Hitbox 크기 그대로");
            Assert.That(hbCol.bounds.center, Is.EqualTo(hbBounds.center), "Collider 범위 그대로");
            Assert.That(hbCol.bounds.size, Is.EqualTo(hbBounds.size));
        }

        [Test]
        public void Break_KeepsViewForPaperDie_ThenReleases()
        {
            var t = ViewTestTables.Load();
            var rig = PrefabRig(t);
            var f = rig.Factory;
            var wall = Obj(t, "wall", 1, 15);
            var objs = new List<RunObject> { wall };
            rig.Frame(0, objs);
            f.OnRunEvents(new List<RunEvent> { new RunEvent(RunEventType.Break, objId: 1, kindId: wall.Kind.id) });
            wall.Dead = true;
            rig.Frame(0, objs);
            Assert.That(f.ViewOf(1), Is.Not.Null, "죽어도 0.6초는 쓰러지는 그림이 남는다(paperDie)");
            Assert.That(Mod<ObjectMotion>(f, 1).Die, Is.GreaterThanOrEqualTo(0));
            Assert.That(ViewComp(f, 1).Dying, Is.True);
            // Sync의 dt는 실시간(한 번에 최대 0.1초)이라 0.6초가 지나도록 여러 번 부른다.
            for (int i = 0; i < 12 && f.ViewOf(1) != null; i++)
            {
                System.Threading.Thread.Sleep(110);
                rig.Frame(0, objs);
            }
            Assert.That(f.ViewOf(1), Is.Null, "다 쓰러지면 뷰를 돌려준다");
        }

        [Test]
        public void ObjectReaction_NewHtmlMotionFormulas()
        {
            // paperDie(L3663): k = 0 그대로, k = 1/6 가장 높이(0.35), k = 1 사라짐·눌림 0.05·기울기 dir·0.5
            ObjectReaction.DiePose(0, 1, out double hop, out double sx, out double sy, out double roll, out double a);
            Assert.AreEqual(0, hop, 1e-9); Assert.AreEqual(1, sx, 1e-9); Assert.AreEqual(1, sy, 1e-9); Assert.AreEqual(0, roll, 1e-9); Assert.AreEqual(1, a, 1e-9);
            ObjectReaction.DiePose(1 / 6.0, 1, out hop, out _, out _, out _, out _);
            Assert.AreEqual(0.35, hop, 1e-9);
            ObjectReaction.DiePose(1, -1, out hop, out sx, out sy, out roll, out a);
            Assert.AreEqual(0, a, 1e-9); Assert.AreEqual(0.05, sy, 1e-9); Assert.AreEqual(-0.5, roll, 1e-9); Assert.AreEqual(Math.Cos(Math.PI * 1.5), sx, 1e-9);
            // 눌림 초당 9
            Assert.AreEqual(0.1, ObjectReaction.PunchStep(1, 0.1), 1e-9);
            Assert.AreEqual(0, ObjectReaction.PunchStep(0.05, 0.1));
            // 결계 블록 자리(L3677)
            ObjectReaction.WallJitter(3, 2, out double jx, out double jz);
            double hs = 3 * 7.13 + 2 * 3.1;
            Assert.AreEqual((FieldGen.Hash(hs) - 0.5) * 0.35, jx, 1e-12);
            Assert.AreEqual(-0.4 + (FieldGen.Hash(hs + 1) - 0.5) * 0.4, jz, 1e-12);
            // 게이트 막·포탈 빛 비율
            Assert.AreEqual(0.55 / 0.32, ObjectReaction.CurtainRatio(true), 1e-12);
            Assert.AreEqual(1, ObjectReaction.CurtainRatio(false));
            Assert.AreEqual(0.15 / 0.35, ObjectReaction.PortalGlowRatio(true), 1e-12);
            for (int i = 0; i < ObjectReaction.GateSparks; i++)
            {
                double ph = ObjectReaction.GateSparkPhase(12.3, i);
                Assert.That(ph, Is.InRange(0.0, 1.0));
                Assert.That(ObjectReaction.GateSparkX(i, 40, 2), Is.InRange(-2.0, 2.0));
            }
            Assert.AreEqual(1, ObjectReaction.TowerGlowRatio(0), 1e-12);
            Assert.AreEqual(0.85 + 0.15 * Math.Sin(6 * 2.0 + 3), ObjectReaction.OrbPulse(2, 3), 1e-12);
        }

        /// <summary>
        /// 빌보드는 프리팹이 정한다(2026-11-14 사용자 「빌보드 적용할거는 묶어서 처리해야지 … 붙어서 보여야되는 하나의 오브젝트가 벌어져서 보이게돼」):
        /// 오브젝트 프리팹마다 Billboard는 1개, 루트 바로 아래 묶음(루트 아님)에 있고, 모든 이미지는 그 묶음 아래이거나 눕힌 것(로컬 회전)이다.
        /// <see cref="PendingArtCheck"/>에 하나씩 적은 이미지는 아트 배치(6034a953 등)가 이 규칙과 부딪혀 사람 확인을 기다린다.
        /// 그 항목이 남아 있으면 이 시험은 PASS가 아니라 Inconclusive로 끝난다. 목록 밖 위반은 실패, 목록에 있는데 더는 위반이 아니면 목록 정리를 요구한다.
        /// </summary>
        /// <summary>
        /// 사람 확인 대기(2026-11-17 사용자 「기존 아트 배치를 유지 … 수동 확인 결과가 나오기 전에는 아트 구조 변경이나 규칙 변경을 확정하지 마세요」).
        /// 프리팹 주소 + 루트 아래 이미지 경로(같은 이름은 경로로 구분). 확인이 끝난 항목은 프리팹을 고치거나 규칙을 정한 뒤 여기서 지운다.
        /// </summary>
        static readonly HashSet<string> PendingArtCheck = new HashSet<string>
        {
            "Obj_Tower:Billboard/Shadow",
            "Obj_CoinPile:Billboard/Image/Pile_1/Pile_1",
            "Obj_CoinPile:Billboard/Image/Pile_2/Pile_2",
            "Obj_CoinPile:Billboard/Image/Pile_3/Pile_3",
            "Obj_GemRock:Billboard/Rock/Rock/Rock",
            "Obj_GemRock:Billboard/Rock/Crystal_C/Crystal_C",
            "Obj_GemRock:Billboard/Rock/Crystal_R/Crystal_C (1)",
            "Obj_GemRock:Billboard/Rock/Crystal_L/Crystal_C (2)",
            "Obj_StairGate:Billboard/Shadow/Pillar_L",
            "Obj_StairGate:Billboard/Shadow/Pillar_R",
            "Obj_StairGate:Billboard/Shadow/Ribbon",
            // Obj_CraftGate는 Obj_StairGate의 변형 프리팹이라 위 세 이미지를 그대로 물려받는다(원본을 고치면 함께 바뀐다).
            "Obj_CraftGate:Billboard/Shadow/Pillar_L",
            "Obj_CraftGate:Billboard/Shadow/Pillar_R",
            "Obj_CraftGate:Billboard/Shadow/Ribbon",
            "Obj_StairZone:Billboard/Lantern_L/Shadow",
            "Obj_StairZone:Billboard/Lantern_R/Shadow",
            "Obj_StairZone:Billboard/Glow (1)",
            "Obj_StairZone:Billboard/Glow (2)",
            "Stage1_Block_Blue:Billboard/Chest_1",
            "Stage1_Block_Cat:Billboard/Body (2)",
            "Stage1_Block_Gold:Billboard/Chest_2",
        };

        [Test]
        public void ObjectPrefabs_OneBillboardGroup_FlatImagesOutside()
        {
            const string BillboardGuid = "b6073823855d4665867abbe603050885";
            // 눕힘 = 루트 기준 누적 회전이 화면 면에서 기울어짐(x·y 회전 성분). 부모가 눕혀져 있으면 자식도 눕는다(예: Obj_StairZone Circle 아래 선).
            // z만 돈 것은 면 안에서 돈 것이라 서 있다. -0도 0으로 본다.
            Quaternion Q(string q)
            {
                var m = System.Text.RegularExpressions.Regex.Match(q, @"x: ([^,]+), y: ([^,]+), z: ([^,]+), w: ([^,}]+)");
                Assert.IsTrue(m.Success, "회전 값을 읽지 못했다: " + q);
                float F(int i) => float.Parse(m.Groups[i].Value.Trim(), System.Globalization.CultureInfo.InvariantCulture);
                return new Quaternion(F(1), F(2), F(3), F(4));
            }
            bool Laid(Quaternion q) => Math.Abs(q.x) > 1e-6 || Math.Abs(q.y) > 1e-6;
            var t = ViewTestTables.Load();
            var addrs = new HashSet<string>();
            foreach (var k in t.ObjectKind.Datas) if (!string.IsNullOrEmpty(k.prefab)) addrs.Add(k.prefab);
            foreach (var s in t.StageObject.Datas) if (!string.IsNullOrEmpty(s.prefab)) addrs.Add(s.prefab);
            Assert.That(addrs, Is.Not.Empty);
            var dir = Path.Combine(Application.dataPath, "Game/Addressables/Local/BG/Prefab");
            System.Text.RegularExpressions.Match M(string d, string pat) => System.Text.RegularExpressions.Regex.Match(d, pat);
            var pending = new List<string>();
            var errors = new List<string>();
            foreach (var a in addrs)
            {
                var files = Directory.GetFiles(dir, a + ".prefab", SearchOption.AllDirectories);
                Assert.AreEqual(1, files.Length, a + " 프리팹 파일");
                // 변형 프리팹(예: Obj_CraftGate ← Obj_StairGate)은 파일에 원본 구조가 없다. 원본 파일을 읽고 변형이 덮어쓴 회전만 얹는다.
                // 구조를 바꾼 변형(오브젝트·컴포넌트 추가·삭제)은 이 글자 읽기로 판정할 수 없으므로 통과시키지 않고 오류로 낸다.
                var rotOverride = new Dictionary<string, Dictionary<string, string>>();
                // 변형이 더한 오브젝트(m_AddedGameObjects, 예: Obj_CraftGate의 무기 카드 묶음)는 변형 파일에 문서로 있고,
                // 부모는 원본 오브젝트를 가리키는 stripped Transform이다. 그 문서를 원본 구조에 붙여 같이 읽는다(바깥 변형 한 단계만).
                var addedDocs = new List<string>();
                string text = File.ReadAllText(files[0]).Replace("\r\n", "\n");
                for (int depth = 0; ; depth++)
                {
                    var src = M(text, @"m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-f]{32})");
                    // 변형 = 뿌리가 부모 없는 PrefabInstance인 파일(더한 오브젝트 문서가 있어도 변형이다).
                    bool variant = src.Success && System.Text.RegularExpressions.Regex.IsMatch(text, @"PrefabInstance:\n(?:  .*\n)*?    m_TransformParent: \{fileID: 0\}");
                    if (!variant) break;
                    Assert.Less(depth, 8, a + ": 변형 프리팹 단계가 너무 깊다");
                    foreach (var list in new[] { "m_RemovedComponents", "m_RemovedGameObjects", "m_AddedComponents" })
                        Assert.IsTrue(M(text, list + @": \[\]").Success, a + ": 구조를 바꾼 변형 프리팹(" + list + ")은 이 시험이 읽지 못한다");
                    if (!M(text, @"m_AddedGameObjects: \[\]").Success)
                    {
                        Assert.AreEqual(0, depth, a + ": 더한 오브젝트는 바깥 변형 한 단계만 읽는다");
                        var strippedSrc = new Dictionary<string, string>();
                        foreach (var d in System.Text.RegularExpressions.Regex.Split(text, @"\n(?=--- !u!)"))
                        {
                            var h = M(d, @"^--- !u!(\d+) &(\d+)( stripped)?");
                            if (!h.Success || h.Groups[1].Value == "1001") continue;
                            if (h.Groups[3].Success) strippedSrc[h.Groups[2].Value] = M(d, @"m_CorrespondingSourceObject: \{fileID: (\d+)").Groups[1].Value;
                            else addedDocs.Add(d);
                        }
                        for (int k = 0; k < addedDocs.Count; k++)
                            foreach (var kv in strippedSrc)
                                addedDocs[k] = addedDocs[k].Replace("m_Father: {fileID: " + kv.Key + "}", "m_Father: {fileID: " + kv.Value + "}");
                    }
                    foreach (System.Text.RegularExpressions.Match o in System.Text.RegularExpressions.Regex.Matches(text,
                        @"- target: \{fileID: (\d+), guid: [0-9a-f]{32}, type: 3\}\n\s+propertyPath: m_LocalRotation\.([xyzw])\n\s+value: (\S+)"))
                    {
                        if (!rotOverride.TryGetValue(o.Groups[1].Value, out var axes)) rotOverride[o.Groups[1].Value] = axes = new Dictionary<string, string>();
                        if (!axes.ContainsKey(o.Groups[2].Value)) axes[o.Groups[2].Value] = o.Groups[3].Value;   // 바깥 변형이 먼저
                    }
                    string guid = src.Groups[1].Value;
                    var metas = Directory.GetFiles(dir, "*.prefab.meta", SearchOption.AllDirectories)
                        .Where(f => File.ReadAllText(f).Contains("guid: " + guid)).ToArray();
                    Assert.AreEqual(1, metas.Length, a + ": 원본 프리팹(" + guid + ")");
                    text = File.ReadAllText(metas[0].Substring(0, metas[0].Length - ".meta".Length)).Replace("\r\n", "\n");
                }
                string Rot(string id, string q)
                {
                    if (!rotOverride.TryGetValue(id, out var axes)) return q;
                    foreach (var kv in axes) q = System.Text.RegularExpressions.Regex.Replace(q, @"(?<=[{ ])" + kv.Key + @": [^,}]+", kv.Key + ": " + kv.Value);
                    return q;
                }
                var docs = new List<string>(System.Text.RegularExpressions.Regex.Split(text, @"\n(?=--- !u!)"));
                docs.AddRange(addedDocs);
                var trOfGo = new Dictionary<string, string>();
                var rot = new Dictionary<string, string>();
                var father = new Dictionary<string, string>();
                var boards = new List<string>();
                var images = new List<string>();
                var goName = new Dictionary<string, string>();
                var goOfTr = new Dictionary<string, string>();
                foreach (var d in docs)
                {
                    var head = M(d, @"^--- !u!(\d+) &(\d+)");
                    if (head.Success && head.Groups[1].Value == "1") goName[head.Groups[2].Value] = M(d, @"m_Name: (.*)").Groups[1].Value.Trim();
                    var go = M(d, @"m_GameObject: \{fileID: (\d+)");
                    if (!head.Success || !go.Success) continue;
                    string type = head.Groups[1].Value, id = head.Groups[2].Value, g = go.Groups[1].Value;
                    if (type == "4")
                    {
                        trOfGo[g] = id;
                        goOfTr[id] = g;
                        rot[id] = Rot(id, M(d, @"m_LocalRotation: (\{[^}]*\})").Groups[1].Value);
                        father[id] = M(d, @"m_Father: \{fileID: (\d+)").Groups[1].Value;
                    }
                    if (type == "212") images.Add(g);
                    if (d.Contains(BillboardGuid)) boards.Add(g);
                }
                Assert.AreEqual(1, boards.Count, a + ": Billboard 묶음은 1개");
                string bt = trOfGo[boards[0]];
                Assert.AreNotEqual("0", father[bt], a + ": 루트 통째 빌보드 금지(묶음은 루트 아래)");
                Assert.AreEqual("0", father[father[bt]], a + ": 묶음은 루트 바로 아래");
                foreach (var img in images)
                {
                    string tr = trOfGo[img];
                    bool under = false;
                    for (string x = tr; x != "0"; x = father[x]) if (x == bt) { under = true; break; }
                    var acc = Quaternion.identity;
                    for (string x = tr; father[x] != "0"; x = father[x]) acc = Q(rot[x]) * acc;   // 루트 자신의 회전은 넣지 않는다
                    bool laid = Laid(acc);
                    var parts = new List<string>();
                    for (string x = tr; father[x] != "0"; x = father[x]) parts.Insert(0, goName[goOfTr[x]]);
                    string key = a + ":" + string.Join("/", parts);
                    if (!under && !laid) errors.Add(key + " 서 있는 이미지는 Billboard 묶음 아래");
                    if (laid && under)
                    {
                        if (PendingArtCheck.Contains(key)) pending.Add(key + " " + rot[tr]);
                        else errors.Add(key + " 눕힌 이미지는 묶음 밖");
                    }
                }
            }
            foreach (var key in PendingArtCheck)
                if (!pending.Exists(p => p.StartsWith(key + " ", StringComparison.Ordinal)))
                    errors.Add(key + " 확인 대기 목록에 있으나 위반이 아님(또는 프리팹이 표에 없음): 목록에서 지운다");
            Assert.IsEmpty(errors, string.Join("\n", errors));
            if (pending.Count > 0)
                Assert.Inconclusive("아트 배치 사람 확인 대기 " + pending.Count + "건(PASS 아님):\n" + string.Join("\n", pending));
        }

        /// <summary>
        /// 런마다 오브젝트 번호를 1부터 다시 쓴다. 같은 번호의 새 오브젝트는 관리자가 이전 뷰를 회수해 풀에서 다시 꺼내고,
        /// 실제 프리팹 모듈의 흔들림·번쩍·눌림·구슬 빠짐과 빌보드 묶음 자세가 처음으로 돌아간다
        /// (2026-11-14 사용자 「장애물 흔들림 처리가 오브젝트 재활용해서 스폰할때 살짝 보이는거 같아」).
        /// </summary>
        [Test]
        public void SameIdNewObject_StartsWithoutPreviousMotion()
        {
            var t = ViewTestTables.Load();
            var rig = PrefabRig(t);
            var f = rig.Factory;
            var old = new List<RunObject> { Obj(t, "wall", 1, 15), Obj(t, "sgate", 2, 20), Obj(t, "tower", 3, 25) };
            rig.Frame(0, old);
            var views = new[] { ViewComp(f, 1), ViewComp(f, 2), ViewComp(f, 3) };
            var boardPos = new Vector3[3];
            var boardScale = new Vector3[3];
            for (int i = 0; i < 3; i++)
            {
                boardPos[i] = Board(f, i + 1).localPosition;
                boardScale[i] = Board(f, i + 1).localScale;
            }
            f.OnRunEvents(new List<RunEvent>
            {
                new RunEvent(RunEventType.Hit, objId: 1, kindId: old[0].Kind.id),
                new RunEvent(RunEventType.Hit, objId: 2, kindId: old[1].Kind.id),
                new RunEvent(RunEventType.Hit, objId: 3, kindId: old[2].Kind.id),
                new RunEvent(RunEventType.WellFlush, objId: 3, a: 2),
            });
            Frames(rig, old, 1);
            Assert.Greater(Mod<WallBlockFx>(f, 1).Shake, 0, "이전 런: 맞아서 흔들리는 중");
            Assert.Greater(Mod<GateCurtainFx>(f, 2).Flash, 0, "이전 런: 막 번쩍");
            Assert.Greater(Mod<ObjectMotion>(f, 1).Punch, 0, "이전 런: 눌림");
            Assert.That(Mod<TowerOrbs>(f, 3).GoneCount, Is.EqualTo(2), "이전 런: 구슬 빠짐");
            Assert.That(Board(f, 1).localScale, Is.Not.EqualTo(boardScale[0]), "이전 런: 그림 묶음 눌림");

            var fresh = new List<RunObject> { Obj(t, "wall", 1, 18), Obj(t, "sgate", 2, 22), Obj(t, "tower", 3, 27) };   // 새 런, 같은 번호
            rig.Frame(0, fresh);
            for (int i = 0; i < 3; i++)
            {
                string k = fresh[i].Kind.key;
                Assert.That(ViewComp(f, i + 1), Is.SameAs(views[i]), k + ": 풀에서 같은 뷰를 다시 씀");
                Assert.That(ViewComp(f, i + 1).Obj, Is.SameAs(fresh[i]));
                Assert.AreEqual(0, Mod<ObjectMotion>(f, i + 1).Punch, k + ": 눌림 없이");
                Assert.AreEqual(-1, Mod<ObjectMotion>(f, i + 1).Die);
                Assert.AreEqual(Vector3.one, f.ViewOf(i + 1).transform.localScale);
                Assert.That(Board(f, i + 1).localScale, Is.EqualTo(boardScale[i]), k + ": 그림 묶음 크기 처음");
            }
            Assert.AreEqual(0, Mod<WallBlockFx>(f, 1).Shake, "새 오브젝트는 흔들림 없이 시작");
            Assert.AreEqual(0, Mod<GateCurtainFx>(f, 2).Flash, "막 번쩍 없이");
            Assert.AreEqual(0, Mod<TowerOrbs>(f, 3).Shake);
            Assert.AreEqual(0, Mod<TowerOrbs>(f, 3).GoneCount, "구슬 모두 다시 보임");
            Assert.That(Board(f, 1).localPosition, Is.EqualTo(boardPos[0]), "블록 그림 묶음 흔들림 없이(자리 흔들기만)");
            Assert.AreEqual(18f, f.ViewOf(1).transform.position.z, 1e-4f, "새 오브젝트 자리");
        }

        [Test]
        public void Hit_NoShakeWhenTableTurnsItOff()
        {
            var t = ViewTestTables.WithCell(HitReactionData.Table, "damageWall", "objShake", "0");
            t.BuildIndexes();
            var rig = PrefabRig(t);
            var wall = Obj(t, "wall", 1, 15);
            rig.Frame(0, new List<RunObject> { wall });
            rig.Factory.OnRunEvents(new List<RunEvent> { new RunEvent(RunEventType.Hit, objId: 1, kindId: wall.Kind.id) });
            var fx = Mod<WallBlockFx>(rig.Factory, 1);
            Assert.That(fx, Is.Not.Null, "실제 블록 프리팹 모듈");
            Assert.That(fx.Shake, Is.EqualTo(0), "표만 바꿔도 흔들림이 꺼진다(D-87)");
        }
    }
}
