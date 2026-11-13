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

        [TearDown]
        public void TearDown()
        {
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
        public void CharacterPresenter_ClimbPhase_PlayerStair_ClonesWalk()
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
            view.Sync(s, 1.0);
            Assert.That(view.StateOf(0), Is.EqualTo(CharState.Stair));
            Assert.That(view.SpriteNameOf(0), Is.EqualTo(view.LookOf(0).stair));
            Assert.That(view.StateOf(1), Is.EqualTo(CharState.Walk), "분신 숨김은 5단계(카메라 궤도 회전)");
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

        ObjectViewFactory MakeFactory()
        {
            var camGo = new GameObject("TestCam");
            _made.Add(camGo);
            camGo.AddComponent<Camera>();
            camGo.transform.position = new Vector3(0, 13, -9);
            camGo.transform.LookAt(new Vector3(0, 0.5f, 5.5f));
            var go = new GameObject("Objects");
            _made.Add(go);
            var f = go.AddComponent<ObjectViewFactory>();
            f.Init(ViewTestTables.Config(), camGo.transform, new WorldSpriteCache(), WorldView.FxAtlas);
            f.SetTheme(ViewTestTables.Theme("market"));
            f.LabelText = _ => null;
            f.PrefabLoader = (a, ct) => Cysharp.Threading.Tasks.UniTask.Never<jjevol.AddressableLease<GameObject>>(ct);   // 프리팹 로드 없이 뷰 구조만
            return f;
        }

        static RunObject Obj(MvTables t, string key, int id, double z)
        {
            var o = RunObjectFactory.Create(t, t.ObjectKind.ByKey(key), 0, z);
            o.Id = id;
            return o;
        }

        [Test]
        public void HitAndWellFlush_DriveReactionsFromTable()
        {
            var t = ViewTestTables.CodeShapes();   // 우물 벽돌은 코드 모양(105.prefab 빈칸)일 때만 있다
            var f = MakeFactory();
            var objs = new List<RunObject> { Obj(t, "wall", 1, 15), Obj(t, "sgate", 2, 20), Obj(t, "tower", 3, 25) };
            f.Sync(objs, 0, 0, 0);
            f.OnRunEvents(new List<RunEvent>
            {
                new RunEvent(RunEventType.Hit, objId: 1, kindId: objs[0].Kind.id),
                new RunEvent(RunEventType.Hit, objId: 2, kindId: objs[1].Kind.id),
                new RunEvent(RunEventType.WellFlush, objId: 3, a: 3, amount: 10),
                new RunEvent(RunEventType.Hit, objId: 99),
            });
            Assert.That(f.TryReaction(1, out double shake, out double flash), Is.True);
            Assert.That(shake, Is.EqualTo(objs[0].Reaction.objShake > 0 ? ObjectReaction.ShakeSec : 0), "106 objShake → 흔들림");
            Assert.That(f.TryReaction(2, out _, out flash), Is.True);
            Assert.That(flash, Is.EqualTo(objs[1].Reaction.flash ? ObjectReaction.FlashSec : 0), "106 flash → 관문 번쩍");
            Assert.That(f.GoneBricks(3), Is.EqualTo(3), "벽돌 3개 빠짐");
            f.OnRunEvents(new List<RunEvent> { new RunEvent(RunEventType.WellFlush, objId: 3, a: 60) });
            Assert.That(f.GoneBricks(3), Is.EqualTo(53), "남은 벽돌보다 많으면 있는 만큼만");
        }

        [Test]
        public void Hit_PunchFromTable_MatchesNewHtml()
        {
            var t = ViewTestTables.CodeShapes();
            // 새 HTML hit(2026-11-13 연출 정본): o.punch 벽 1, 금화·원석 1, 게이트 0.5, 수정탑 0.6. 흔들림 벽·수정탑.
            Assert.AreEqual(1, t.HitReaction.ByKey("damageWall").punch);
            Assert.AreEqual(1, t.HitReaction.ByKey("damageBox").punch);
            Assert.AreEqual(0.5, t.HitReaction.ByKey("chargeStairGate").punch);
            Assert.AreEqual(0.5, t.HitReaction.ByKey("chargeCraftGate").punch);
            Assert.AreEqual(0.6, t.HitReaction.ByKey("chargeWell").punch);
            Assert.Greater(t.HitReaction.ByKey("chargeWell").objShake, 0);
            Assert.Greater(t.HitReaction.ByKey("damageWall").objShake, 0);

            var f = MakeFactory();
            var objs = new List<RunObject> { Obj(t, "wall", 1, 15), Obj(t, "sgate", 2, 20) };
            f.Sync(objs, 0, 0, 0);
            f.OnRunEvents(new List<RunEvent>
            {
                new RunEvent(RunEventType.Hit, objId: 1, kindId: objs[0].Kind.id),
                new RunEvent(RunEventType.Hit, objId: 2, kindId: objs[1].Kind.id),
            });
            Assert.That(f.TryMotion(1, out double p1, out double d1), Is.True);
            Assert.AreEqual(objs[0].Reaction.punch, p1, "106 punch → 눌림 시작값");
            Assert.AreEqual(-1, d1);
            Assert.That(f.TryMotion(2, out double p2, out _), Is.True);
            Assert.AreEqual(objs[1].Reaction.punch, p2);
            f.Sync(objs, 0, 0, 0);
            var s = f.ViewOf(1).transform.localScale;
            Assert.Greater(s.x, 1f, "눌림: 가로로 넓어지고");
            Assert.Less(s.y, 1f, "세로로 눌린다");
        }

        [Test]
        public void Break_KeepsViewForPaperDie_ThenReleases()
        {
            var t = ViewTestTables.CodeShapes();
            var f = MakeFactory();
            var wall = Obj(t, "wall", 1, 15);
            var objs = new List<RunObject> { wall };
            f.Sync(objs, 0, 0, 0);
            f.OnRunEvents(new List<RunEvent> { new RunEvent(RunEventType.Break, objId: 1, kindId: wall.Kind.id) });
            wall.Dead = true;
            f.Sync(objs, 0, 0, 0);
            Assert.That(f.ViewOf(1), Is.Not.Null, "죽어도 0.6초는 쓰러지는 그림이 남는다(paperDie)");
            Assert.That(f.TryMotion(1, out _, out double die), Is.True);
            Assert.GreaterOrEqual(die, 0);
            // Sync의 dt는 실시간(한 번에 최대 0.1초)이라 0.6초가 지나도록 여러 번 부른다.
            for (int i = 0; i < 12 && f.ViewOf(1) != null; i++)
            {
                System.Threading.Thread.Sleep(110);
                f.Sync(objs, 0, 0, 0);
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
        /// 빌보드는 프리팹이 정한다(2026-11-14 사용자 「프리팹 통짜가 아니라 내부 적용해야될 이미지에만으로」): 오브젝트 프리팹마다 Billboard가 있고,
        /// 루트에는 없으며, 바닥에 눕힌 이미지(로컬 회전이 있는 것)에는 없다.
        /// </summary>
        [Test]
        public void ObjectPrefabs_BillboardOnUprightImagesOnly()
        {
            const string BillboardGuid = "b6073823855d4665867abbe603050885";
            var t = ViewTestTables.Load();
            var addrs = new HashSet<string>();
            foreach (var k in t.ObjectKind.Datas) if (!string.IsNullOrEmpty(k.prefab)) addrs.Add(k.prefab);
            foreach (var s in t.StageObject.Datas) if (!string.IsNullOrEmpty(s.prefab)) addrs.Add(s.prefab);
            Assert.That(addrs, Is.Not.Empty);
            var dir = Path.Combine(Application.dataPath, "Game/Addressables/Local/BG/Prefab");
            foreach (var a in addrs)
            {
                var files = Directory.GetFiles(dir, a + ".prefab", SearchOption.AllDirectories);
                Assert.AreEqual(1, files.Length, a + " 프리팹 파일");
                var docs = System.Text.RegularExpressions.Regex.Split(File.ReadAllText(files[0]).Replace("\r\n", "\n"), @"\n(?=--- !u!)");
                var rot = new Dictionary<string, string>();
                var father = new Dictionary<string, string>();
                var boards = new List<string>();
                foreach (var d in docs)
                {
                    var go = System.Text.RegularExpressions.Regex.Match(d, @"m_GameObject: \{fileID: (\d+)");
                    if (!go.Success) continue;
                    if (d.StartsWith("--- !u!4 ") || d.StartsWith("!u!4 "))
                    {
                        rot[go.Groups[1].Value] = System.Text.RegularExpressions.Regex.Match(d, @"m_LocalRotation: (\{[^}]*\})").Groups[1].Value;
                        father[go.Groups[1].Value] = System.Text.RegularExpressions.Regex.Match(d, @"m_Father: \{fileID: (\d+)").Groups[1].Value;
                    }
                    if (d.Contains(BillboardGuid)) boards.Add(go.Groups[1].Value);
                }
                Assert.That(boards, Is.Not.Empty, a + ": Billboard 없음");
                foreach (var b in boards)
                {
                    Assert.AreNotEqual("0", father[b], a + ": 루트 통째 빌보드 금지");
                    Assert.AreEqual("{x: 0, y: 0, z: 0, w: 1}", rot[b], a + ": 눕힌 이미지에 빌보드");
                }
            }
        }

        [Test]
        public void Hit_NoShakeWhenTableTurnsItOff()
        {
            var t = ViewTestTables.WithCell(HitReactionData.Table, "damageWall", "objShake", "0");
            t.BuildIndexes();
            var f = MakeFactory();
            var wall = Obj(t, "wall", 1, 15);
            f.Sync(new List<RunObject> { wall }, 0, 0, 0);
            f.OnRunEvents(new List<RunEvent> { new RunEvent(RunEventType.Hit, objId: 1, kindId: wall.Kind.id) });
            Assert.That(f.TryReaction(1, out double shake, out _), Is.True);
            Assert.That(shake, Is.EqualTo(0), "표만 바꿔도 흔들림이 꺼진다(D-87)");
        }
    }
}
