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
    /// 티켓 09: 런 고양이 표시(02 §2.1.4 고양이 행)와 오브젝트 맞음·막힘 반응(06 §6.13).
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

        [Test]
        public void IdleBreath_MatchesProto()
        {
            CatLayout.IdleBreath(1.3, 2, out double sx, out double sy);
            Assert.That(sx, Is.EqualTo(1.00750202340).Within(1e-9));
            Assert.That(sy, Is.EqualTo(0.985163121088).Within(1e-9));
            CatLayout.IdleBreath(10.25, 4, out sx, out sy);
            Assert.That(sx, Is.EqualTo(0.992669624821).Within(1e-9));
            Assert.That(sy, Is.EqualTo(1.01482354370).Within(1e-9));
        }

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 11)]
        [TestCase(3, 0)]
        [TestCase(4, 6)]
        public void FrameIndex_NearestOfTwelvePerTurn(int c, int frame)
        {
            double[] ph = { 0, 0.27, -0.6, -0.1, Math.PI * 2 * 5 + Math.PI };
            Assert.That(CatLayout.FrameIndex(ph[c]), Is.EqualTo(frame));
        }

        [Test]
        public void AnimAndTier_FollowProtoRules()
        {
            Assert.That(CatLayout.AnimOf(true, true, 0.2, true), Is.EqualTo(CatAnim.Climb), "오르기가 먼저");
            Assert.That(CatLayout.AnimOf(false, true, 0.2, true), Is.EqualTo(CatAnim.Throw));
            Assert.That(CatLayout.AnimOf(false, true, 0.2, false), Is.EqualTo(CatAnim.Run), "친구는 thr 0");
            Assert.That(CatLayout.AnimOf(false, false, 0.2, true), Is.EqualTo(CatAnim.Idle), "런 끝(R.fin)·막힘 종료");
            var art = CatArt.From(ViewTestTables.Load());
            Assert.That(art.PlayerTier(0), Is.EqualTo(1));
            Assert.That(art.PlayerTier(9), Is.EqualTo(8));
            Assert.That(art.PlayerTier(12), Is.EqualTo(8), "103 12등급 → 그림 8단계(101 tierCount) 재사용");
            Assert.That(art.FriendTier(1), Is.EqualTo(1));
            Assert.That(art.FriendTier(5), Is.EqualTo(3));
            Assert.That(art.FriendTier(8), Is.EqualTo(6));
            Assert.That(art.FriendTier(12), Is.EqualTo(6));
            Assert.That(art.FrameName(0, 3, CatAnim.Throw), Is.EqualTo("World_Cat_P0_T3_Run_B_"));
            Assert.That(art.FrameName(2, 1, CatAnim.Throw), Is.EqualTo("World_Cat_P2_T1_Run_A_"));
            Assert.That(art.FrameName(2, 1, CatAnim.Idle), Is.EqualTo("World_Cat_P2_T1_Idle"));
            Assert.That(art.AtlasOf(0, 7), Is.EqualTo("Atlas_World_Cat_T7"));
            Assert.That(art.AtlasOf(3, 2), Is.EqualTo("Atlas_World_Friend_T2"));
        }

        [TestCase(0, 1)]
        [TestCase(3, 4)]
        [TestCase(4, 4)]
        [TestCase(29, 4)]
        public void FriendPattern_ReusesLastPatternBeyondArt(int friendIndex, int pattern)
        {
            // 101 friend patternCount 4: 친구 5번째부터 마지막 무늬(그림 수가 분신 수를 막지 않는다)
            Assert.That(CatArt.From(ViewTestTables.Load()).FriendPattern(friendIndex), Is.EqualTo(pattern));
        }

        [Test]
        public void CatArt_FollowsCatTableCounts()
        {
            var t = ViewTestTables.WithCell(CatData.Table, "friend", "patternCount", "2");
            t.BuildIndexes();
            var art = CatArt.From(t);
            Assert.That(art.FriendPatterns, Is.EqualTo(2));
            Assert.That(art.FriendPattern(2), Is.EqualTo(2), "표만 바꿔 무늬 수가 따라 바뀐다");
            t = ViewTestTables.WithCell(CatData.Table, "player", "tierCount", "5");
            t.BuildIndexes();
            Assert.That(CatArt.From(t).PlayerTier(8), Is.EqualTo(5));
        }

        /// <summary>코드가 고르는 모든 프레임 이름이 그림 목록(`sprite_catalog.tsv`)에 같은 아틀라스로 있다.</summary>
        [Test]
        public void FrameNames_AllExistInSpriteCatalog()
        {
            string path = Path.Combine(Application.dataPath, "Game/Art/Source/sprite_catalog.tsv");
            var atlasOf = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in File.ReadAllLines(path).Skip(1))
            {
                var cells = line.Split('\t');
                if (cells.Length >= 2) atlasOf[cells[1]] = cells[0];
            }
            int checkedN = 0;
            var art = CatArt.From(ViewTestTables.Load());
            for (int pat = 0; pat <= art.FriendPatterns; pat++)
            {
                int tMax = pat == 0 ? art.PlayerTiers : art.FriendTiers;
                for (int t = 1; t <= tMax; t++)
                    foreach (CatAnim a in Enum.GetValues(typeof(CatAnim)))
                    {
                        string atlas = art.AtlasOf(pat, t), name = art.FrameName(pat, t, a);
                        var names = a == CatAnim.Idle ? new[] { name } : Enumerable.Range(0, CatLayout.Frames).Select(i => name + i.ToString("00")).ToArray();
                        foreach (var n in names)
                        {
                            Assert.That(atlasOf.TryGetValue(n, out var at), Is.True, n);
                            Assert.That(at, Is.EqualTo(atlas), n);
                            checkedN++;
                        }
                    }
            }
            Assert.That(checkedN, Is.GreaterThan(800));
        }

        // ───────── 고양이 뷰 구조 ─────────

        [Test]
        public void CatView_SortsAsUnitWithoutFog_AndHidesWithoutAtlas()
        {
            var t = ViewTestTables.Load();
            var cfg = ViewTestTables.Config();
            var go = new GameObject("Cats");
            _made.Add(go);
            var view = go.AddComponent<CatView>();
            view.Init(t, cfg, null, null);
            Assert.That(view.FriendSlots, Is.EqualTo(30), "친구 자리 = 314 clone 행 수(MAX_CLONES 30)");
            for (int i = 0; i <= view.FriendSlots; i++)
            {
                var ds = view.CatObject(i).GetComponent<DepthSortable>();
                Assert.That(ds.Rank, Is.EqualTo(DepthMath.RankUnit), "순위 " + i);
                Assert.That(ds.SortOffsetY, Is.EqualTo((float)cfg.CatProbeY), "탐침 " + i);
                Assert.That(ds.Bias, Is.EqualTo(0f));
                Assert.That(ds.Fog, Is.False, "안개 알파 없음");
            }
            var s = new RunState(null, t, new Pcg32Rng(1));
            s.Clones = 2;
            s.P.Phase = 0.27;
            view.Sync(s, 0);
            Assert.That(view.VisibleCount, Is.EqualTo(0), "그림이 아직 없으면 그리지 않는다");
            Assert.That(view.CatObject(0).activeSelf, Is.False);
            s.Phase = RunPhase.Result;
            s.Fin = true;
            view.Sync(s, 0);
            Assert.That(view.SpriteNameOf(0), Is.EqualTo("World_Cat_P0_T1_Idle"), "런이 끝나면 정지 그림");
            s.Clones = 6;
            view.Sync(s, 0);
            Assert.That(view.SpriteNameOf(6), Is.EqualTo("World_Cat_P4_T1_Idle"), "친구 6번째 = 마지막 무늬(4) 재사용");
            view.Sync(null, 0);
            Assert.That(view.VisibleCount, Is.EqualTo(0));
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
            var t = ViewTestTables.Load();
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
