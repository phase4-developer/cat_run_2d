using System.Collections.Generic;
using System.Linq;
using MV.Data;
using MV.Run;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEngine;

namespace MV.View.Tests
{
    /// <summary>
    /// 티켓 04: 오브젝트 뷰(02 §2.10, 03 §3.5.3)와 월드 프리팹 연결. 골든 값은 정본 `drawJunk`·우물 생성·`drawWarpGate` 식을 Node(V8)로 돌린 결과다.
    /// 아틀라스를 로드하지 않으므로 스프라이트는 비어 있고, 뷰 구조·배치·정렬만 본다.
    /// </summary>
    public sealed class ObjectViewTests
    {
        const string PrefabPath = "Assets/Game/Prefabs/WorldRoot.prefab";
        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        // (lane, row, bar, idx) → 스프라이트 번호, x 편차, 크기. 시험 이름은 정수만(실행기 기대 목록).
        static readonly int[,] s_junkIn = { { 0, 0, 0, 0, 0 }, { 2, 1, 0, 3, 1 }, { 1, 2, 2, 5, 1 } };
        static readonly double[,] s_junkOut = { { 0.111759106, 1.013346391 }, { 0.050974881, 0.965285031 }, { -0.150897754, 0.908919907 } };

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Junk_VariantAndJitter_MatchProto(int c)
        {
            int lane = s_junkIn[c, 0], row = s_junkIn[c, 1], bar = s_junkIn[c, 2], idx = s_junkIn[c, 3], sprite = s_junkIn[c, 4];
            double jx = s_junkOut[c, 0], scale = s_junkOut[c, 1];
            Assert.That(ObjectLayout.JunkSpriteIndex(lane, row, bar, idx), Is.EqualTo(sprite));
            ObjectLayout.JunkJitter(idx, bar, out double gx, out double gs);
            Assert.That(gx, Is.EqualTo(jx).Within(1e-8));
            Assert.That(gs, Is.EqualTo(scale).Within(1e-8));
        }

        [Test]
        public void WellBricks_RingMatchesProto()
        {
            var list = new List<WellBrick>();
            ObjectLayout.WellBricks(list);
            Assert.That(list.Count, Is.EqualTo(53));
            Assert.That(list.Count(b => b.Junk), Is.EqualTo(9));
            void Check(int k, double x, double y, double z, int v)
            {
                Assert.That(list[k].X, Is.EqualTo(x).Within(1e-8), "x " + k);
                Assert.That(list[k].Y, Is.EqualTo(y).Within(1e-8), "y " + k);
                Assert.That(list[k].Z, Is.EqualTo(z).Within(1e-8), "z " + k);
                Assert.That(list[k].Variant, Is.EqualTo(v), "v " + k);
            }
            Check(0, 1, 0.17, 0, 0);
            Check(5, -0.959492974, 0.17, -0.281732557, 1);
            Check(12, 0.654860734, 0.49, -0.755749574, 2);
            Check(43, 0.959492974, 1.13, 0.281732557, 1);
        }

        static readonly double[] s_warpPz = { -5, -0.6, -0.1, 2.9, 3.1 }, s_warpAlpha = { 1, 1, 0.3, 0, 0 };

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void WarpGate_AlphaNearPlayer_MatchesProto(int c)
        {
            double pz = s_warpPz[c], alpha = s_warpAlpha[c];
            bool shown = c != 4;
            Assert.That(WarpGateLayout.Alpha(pz, out double a), Is.EqualTo(shown));
            Assert.That(a, Is.EqualTo(alpha).Within(1e-9));
        }

        static string StageMap(MvTables t) => t.StageObject.Datas.First(r => r.kind == "arch").stageMap;

        ObjectViewFactory MakeFactory(out Camera cam)
        {
            var camGo = new GameObject("TestCam");
            _made.Add(camGo);
            cam = camGo.AddComponent<Camera>();
            camGo.transform.position = new Vector3(0, 13, -9);
            camGo.transform.LookAt(new Vector3(0, 0.5f, 5.5f));
            var go = new GameObject("Objects");
            _made.Add(go);
            var f = go.AddComponent<ObjectViewFactory>();
            f.Init(ViewTestTables.Config(), camGo.transform, new WorldSpriteCache(), WorldView.FxAtlas);
            f.SetTheme(ViewTestTables.Theme("market"));
            f.HideLabels = true;   // TMP 글꼴 없이 구조만 본다
            f.PrefabLoader = (a, ct) => Cysharp.Threading.Tasks.UniTask.Never<PrefabHandle>(ct);   // 프리팹 로드 없이 뷰 구조만
            return f;
        }

        [Test]
        public void Sync_ShowsLiveObjectsInRange_AndReleasesDeadOrPassed()
        {
            var t = ViewTestTables.Load();
            var cfg = ViewTestTables.Config();
            var f = MakeFactory(out _);
            f.SetStage(t, StageMap(t));
            var objs = new List<RunObject>();
            int id = 1;
            foreach (var key in new[] { "wall", "tower", "arch", "stair", "sgate", "coin", "gem" })
            {
                var o = RunObjectFactory.Create(t, t.ObjectKind.ByKey(key), 0, 10 + id * 5);
                o.Id = id++;
                if (key == "wall") { o.Idx = 0; o.Rew = FieldReward.Gold; }   // 필드 벽 = 305 보상 행
                objs.Add(o);
            }
            var far = RunObjectFactory.Create(t, t.ObjectKind.ByKey("wall"), 0, cfg.ObjDrawFront + 50);
            far.Id = id++;
            objs.Add(far);

            f.Sync(objs, 0, 0, 0);
            Assert.That(f.ViewCount, Is.EqualTo(7), "그리기 범위 밖은 뷰 없음");
            var wall = f.ViewOf(1);
            Assert.That(wall, Is.Not.Null);
            Assert.That(wall.transform.position.z, Is.EqualTo(15f).Within(1e-4));
            var ds = wall.GetComponent<DepthSortable>();
            Assert.That(ds.Rank, Is.EqualTo(DepthMath.RankObject));
            Assert.That(ds.SortOffsetY, Is.EqualTo((float)cfg.ObjProbeY));

            objs[0].Dead = true;
            f.Sync(objs, 0, 0, 0);
            Assert.That(f.ViewOf(1), Is.Null, "죽은 오브젝트는 뷰를 돌려준다");
            Assert.That(f.ViewCount, Is.EqualTo(6));

            f.Sync(objs, 200, 200, 0);
            Assert.That(f.ViewCount, Is.EqualTo(0), "지나간 오브젝트는 뷰를 돌려준다");
        }

        // ---------------- 오브젝트 프리팹(105.prefab · 305_stageObject) ----------------

        static RunObject Make(MvTables t, string key, int id, double z)
        {
            var o = RunObjectFactory.Create(t, t.ObjectKind.ByKey(key), 0, z);
            o.Id = id;
            return o;
        }

        [Test]
        public void PrefabFor_StageRowFirst_ThenKindPrefab_ElseNull()
        {
            var t = ViewTestTables.Load();
            var wall = Make(t, "wall", 1, 10);
            wall.Idx = 0;
            foreach (var row in t.StageObject.Datas)
            {
                if (row.kind != "wall" || row.reward == StageObjectReward.Any) continue;
                wall.Rew = (FieldReward)System.Enum.Parse(typeof(FieldReward), row.reward.ToString());
                Assert.That(ObjectViewFactory.PrefabFor(t, row.stageMap, wall), Is.EqualTo(row.prefab), row.id + ": 305 보상 행");
            }
            foreach (var k in t.ObjectKind.Sorted)
            {
                var o = Make(t, k.key, 2, 10);
                bool staged = t.StageObject.Datas.Any(r => r.kind == k.key && r.reward == StageObjectReward.Any);
                string expected = string.IsNullOrEmpty(k.prefab) ? null : k.prefab;
                if (!staged) Assert.That(ObjectViewFactory.PrefabFor(t, "Stage1", o), Is.EqualTo(expected), k.key + ": 105.prefab(빈칸 = null, 만들지 않음)");
                Assert.That(ObjectViewFactory.PrefabFor(t, null, o), Is.EqualTo(expected), k.key + ": 맵이 없으면 105만");
            }
            var archRow = t.StageObject.Datas.First(r => r.kind == "arch");
            Assert.That(ObjectViewFactory.PrefabFor(t, archRow.stageMap, Make(t, "arch", 3, 10)), Is.EqualTo(archRow.prefab), "305 Any 행");
        }

        [Test]
        public void SetStage_ObjectsWithPrefabUsePrefabView_NoPrefabNotCreated()
        {
            var t = ViewTestTables.Load();
            var f = MakeFactory(out _);
            var arch = t.StageObject.Datas.First(r => r.kind == "arch");
            f.SetStage(t, arch.stageMap);
            var objs = new List<RunObject> { Make(t, "arch", 1, 15), Make(t, "tower", 2, 20), Make(t, "cardbox", 3, 25), Make(t, "enemy", 4, 30) };
            // cardbox는 305·105 프리팹이 비어 있다 → 만들지 않고, 채울 표 칸과 프리팹 위치를 경고로 한 번만 알린다.
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "프리팹 주소가 없어 만들지 않습니다 — 종류 cardbox.*" + StageObjectData.Table + ".*" + ObjectKindData.Table + ".*Assets/Game/Addressables/Local/"));
            f.Sync(objs, 0, 0, 0);
            f.Sync(objs, 0, 0, 0);   // 같은 종류는 다시 알리지 않는다(LogAssert: 경고 1개)
            Assert.That(f.TryPrefabView(1, out string a1, out _), Is.True);
            Assert.That(a1, Is.EqualTo(arch.prefab));
            Assert.That(f.TryPrefabView(2, out string a2, out _), Is.True);
            Assert.That(a2, Is.EqualTo(t.ObjectKind.ByKey("tower").prefab));
            Assert.That(f.TryPrefabView(3, out _, out _), Is.False, "프리팹 주소가 없으면 뷰를 만들지 않는다");
            Assert.That(f.ViewOf(3), Is.Null);
            Assert.That(f.ViewCount, Is.EqualTo(3));
            Assert.That(f.TryPrefabView(4, out string a4, out _), Is.True);
            Assert.That(a4, Is.EqualTo("Obj_Enemy"), "적은 프리팹(그림 자리·Hitbox)");
            Assert.That(f.ViewOf(1).GetComponent<DepthSortable>(), Is.Not.Null, "프리팹 뷰도 같은 정렬 루트");
        }

        /// <summary>
        /// 오브젝트 동작은 프리팹 컴포넌트로 구성한다: 표가 쓰는 오브젝트 프리팹마다 모듈(<see cref="RunObjectModule"/>)이 붙어 있고,
        /// 모듈이 가리키는 그림(빛·막·구슬·몸 그림 등)은 이름 찾기가 아니라 같은 프리팹 안 참조다. 피격 가능 종류의 프리팹은 Hitbox가 그림 묶음 밖에 있다.
        /// </summary>
        [Test]
        public void ObjectPrefabs_BehaviourIsPrefabModules_WithInPrefabReferences()
        {
            var t = ViewTestTables.Load();
            var used = new Dictionary<string, bool>();   // 주소 → 피격 가능 종류가 쓰는가
            foreach (var k in t.ObjectKind.Datas)
                if (!string.IsNullOrEmpty(k.prefab)) used[k.prefab] = (used.TryGetValue(k.prefab, out bool h) && h) || k.hittable != Hittable.Never;
            foreach (var s in t.StageObject.Datas)
            {
                if (string.IsNullOrEmpty(s.prefab)) continue;
                var k = t.ObjectKind.ByKey(s.kind);
                used[s.prefab] = (used.TryGetValue(s.prefab, out bool h) && h) || (k != null && k.hittable != Hittable.Never);
            }
            Assert.That(used, Is.Not.Empty);
            foreach (var kv in used)
            {
                var prefab = HitWorldRig.LoadPrefab(kv.Key);
                Assert.That(prefab, Is.Not.Null, kv.Key);
                var mods = prefab.GetComponentsInChildren<RunObjectModule>(true);
                Assert.That(mods, Is.Not.Empty, kv.Key + ": 모듈 없음");
                Assert.That(prefab.GetComponentsInChildren<ObjectMotion>(true).Length, Is.EqualTo(1), kv.Key + ": 눌림·쓰러짐(ObjectMotion) 1개");
                foreach (var m in mods)
                {
                    var it = new SerializedObject(m).GetIterator();
                    while (it.NextVisible(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null || it.name == "m_Script") continue;
                        var obj = it.objectReferenceValue;
                        var tr = obj is Component c ? c.transform : obj is GameObject g ? g.transform : null;
                        Assert.That(tr != null && tr.IsChildOf(prefab.transform), Is.True, $"{kv.Key}: {m.GetType().Name}.{it.propertyPath}는 프리팹 안을 가리켜야 함");
                    }
                }
                if (!kv.Value) continue;
                var hbs = prefab.GetComponentsInChildren<RunHitbox>(true);
                Assert.That(hbs, Is.Not.Empty, kv.Key + ": 피격 가능 종류의 Hitbox");
                foreach (var hb in hbs)
                    Assert.That(hb.GetComponentInParent<Billboard>(true), Is.Null, kv.Key + ": Hitbox는 그림 묶음(빌보드) 밖");
            }
        }

        [TestCase(3, 10, 10, 3)]
        [TestCase(3, 7, 10, 3)]
        [TestCase(3, 6, 10, 2)]
        [TestCase(3, 3, 10, 1)]
        [TestCase(3, 1, 30, 1)]
        [TestCase(3, 0, 10, 0)]
        [TestCase(2, 5, 10, 1)]
        public void PrefabRig_ShownStages_CeilOfHpRatio(int n, double hp, double max, int shown) =>
            Assert.That(ObjectPrefabRig.ShownStages(n, hp, max), Is.EqualTo(shown));

        /// <summary>줄어드는 그림이 있는 오브젝트 프리팹(츄르 더미·마법석)은 ObjectPrefabRig 단계가 모두 프리팹 안 오브젝트로 연결된다.</summary>
        [TestCase("Assets/Game/Addressables/Local/BG/Prefab/Object/Obj_CoinPile.prefab")]
        [TestCase("Assets/Game/Addressables/Local/BG/Prefab/Object/Obj_GemRock.prefab")]
        public void ShrinkingObjectPrefab_HasRigStages(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            var rig = prefab.GetComponent<ObjectPrefabRig>();
            Assert.That(rig, Is.Not.Null, "ObjectPrefabRig");
            var stages = new SerializedObject(rig).FindProperty("_stages");
            Assert.That(stages.arraySize, Is.GreaterThan(1));
            for (int i = 0; i < stages.arraySize; i++)
            {
                var go = stages.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                Assert.That(go, Is.Not.Null, "단계 " + i);
                Assert.That(go.transform.IsChildOf(prefab.transform), Is.True, "단계 " + i + "는 프리팹 안");
            }
        }

        [Test]
        public void HpLabel_ShowsFloorOfHp_OnlyWhenHurt()
        {
            var t = ViewTestTables.Load();
            var kind = t.ObjectKind.Sorted.FirstOrDefault(k => k.labelKind == LabelKind.EnemyHp || k.labelKind == LabelKind.BoxHp);
            Assume.That(kind, Is.Not.Null, "체력 글자판 종류가 표에 없음");
            var o = RunObjectFactory.Create(t, kind, 0, 10);
            o.HasHp = true;
            o.Max = 20;
            o.Hp = 20;
            Assert.That(ObjectLabel.Of(o, 0, 3, 0, 1).Visible, Is.False, "다치기 전에는 숨김(정본 L1521·L1525 hp < max)");
            o.Hp = 12.7;
            var c = ObjectLabel.Of(o, 0, 3, 0, 1);
            Assert.That(c.Visible, Is.True);
            Assert.That(c.Number, Is.EqualTo("12"), "정본 fmt = floor");
        }

        [Test]
        public void WorldRootPrefab_WiresAllParts()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, PrefabPath);
            var wv = prefab.GetComponent<WorldView>();
            Assert.That(wv, Is.Not.Null);
            var so = new SerializedObject(wv);
            foreach (var field in new[] { "_rig", "_sorter", "_ground", "_tint", "_screenMultiply", "_objects", "_screen", "_cats", "_projs", "_stairs", "_hits" })
                Assert.That(so.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            Assert.That(prefab.GetComponentInChildren<CameraRig>().GetComponent<Camera>(), Is.Not.Null);
            var objs = new SerializedObject(prefab.GetComponentInChildren<ObjectViewFactory>());
            foreach (var field in new[] { "_spriteMaterial" })
                Assert.That(objs.FindProperty(field).objectReferenceValue, Is.Not.Null, "ObjectViewFactory." + field);
            Assert.That(prefab.GetComponentInChildren<GroundStripMesh>().GetComponent<MeshRenderer>().sharedMaterial, Is.Not.Null);
            var cats = new SerializedObject(prefab.GetComponentInChildren<CharacterPresenter>());
            Assert.That(cats.FindProperty("_spriteMaterial").objectReferenceValue, Is.Not.Null, "CharacterPresenter._spriteMaterial");
        }

        /// <summary>캐릭터 프리팹(MV-VIEW-CHAR-1): 루트에 CharacterRig, 몸 그림 연결, 기본 그림 직접 참조 없음(아틀라스 그림만 쓰므로 원본 PNG가 번들에 따로 들어가지 않게).</summary>
        [TestCase("Assets/Game/Addressables/Local/Character/Prefab/Char_Cat.prefab")]
        [TestCase("Assets/Game/Addressables/Local/Character/Prefab/Char_Clone.prefab")]
        public void CharacterPrefab_HasRigWithBodyAndNoDefaultSprite(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            var rig = prefab.GetComponent<CharacterRig>();
            Assert.That(rig, Is.Not.Null, "CharacterRig");
            var body = new SerializedObject(rig).FindProperty("_body").objectReferenceValue as SpriteRenderer;
            Assert.That(body, Is.Not.Null, "CharacterRig._body");
            Assert.That(body.sprite, Is.Null, "기본 그림 직접 참조 없음");
        }

        /// <summary>정산 바퀴(MV-VIEW-CHAR-3): 116_charLook에 wheel이 있는 행의 프리팹은 CharacterRig._wheel이 몸 아래 SpriteRenderer로 연결되고 기본 그림이 없다.</summary>
        [Test]
        public void WheelLookRows_PrefabHasWheelUnderBody()
        {
            var t = ViewTestTables.Load();
            int checkedRows = 0;
            foreach (var row in t.CharLook.Datas)
            {
                if (string.IsNullOrEmpty(row.wheel)) continue;
                string path = "Assets/Game/Addressables/Local/Character/Prefab/" + row.prefab + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                var rig = prefab.GetComponent<CharacterRig>();
                Assert.That(rig, Is.Not.Null, path);
                var so = new SerializedObject(rig);
                var body = so.FindProperty("_body").objectReferenceValue as SpriteRenderer;
                var wheel = so.FindProperty("_wheel").objectReferenceValue as SpriteRenderer;
                Assert.That(wheel, Is.Not.Null, row.id + ": CharacterRig._wheel");
                Assert.That(wheel.transform.IsChildOf(body.transform), Is.True, row.id + ": 바퀴는 몸과 함께 기울도록 몸 아래");
                Assert.That(wheel.sprite, Is.Null, row.id + ": 기본 그림 직접 참조 없음");
                checkedRows++;
            }
            Assert.That(checkedRows, Is.GreaterThan(0), "wheel 행 없음");
        }
    }
}
