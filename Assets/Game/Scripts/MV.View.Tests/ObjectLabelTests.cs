using System.Collections.Generic;
using MV.Data;
using MV.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MV.View.Tests
{
    /// <summary>
    /// 오브젝트 글자판(MV-VIEW-LABEL): 내용(<see cref="ObjectLabel"/>, 정본 `updObjMesh` L1474~1534)과
    /// 글자판 프리팹 `Obj_Text_Count` 연결(<see cref="CountLabel"/>), 글자판을 쓰는 오브젝트 프리팹이 글자판을 품었는지.
    /// </summary>
    public sealed class ObjectLabelTests
    {
        const string LabelPrefab = "Assets/Game/Addressables/Local/BG/Prefab/Object/Obj_Text_Count.prefab";
        const string PrefabRoot = "Assets/Game/Addressables/Local/BG/Prefab";
        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        static RunObject Make(string key)
        {
            var t = ViewTestTables.Load();
            return RunObjectFactory.Create(t, t.ObjectKind.ByKey(key), 0, 10);
        }

        [Test]
        public void StairGate_ShowsCollectedStairsAndCharge_KeepsPrefabTitle()
        {
            var o = Make("sgate");
            o.N = 8;
            o.Prog = 0.5;
            var c = ObjectLabel.Of(o, 0, 3, 0, 1);
            Assert.That(c.Visible && c.KeepTitle, Is.True);
            Assert.That(c.Number, Is.EqualTo("x8"), "노션 Obj_StairGate UI 「x{value}」");
            Assert.That(c.Gauge, Is.EqualTo(0.5f).Within(1e-6));
        }

        [Test]
        public void CraftGate_NoNumber_TitleAndGaugeOnly()
        {
            var o = Make("cgate");
            o.Made = 3;
            o.Extra = 1;
            o.Prog = 2;   // 범위 밖은 0~1로 자른다
            var c = ObjectLabel.Of(o, 0, 3, 0, 1);
            Assert.That(c.Number, Is.Null, "노션 Obj_CraftGate UI: 숫자 없음(아이콘·막대만)");
            Assert.That(c.Visible && c.KeepTitle, Is.True, "제목(「무기」)과 막대는 보인다");
            Assert.That(c.Gauge, Is.EqualTo(1f));
        }

        [Test]
        public void StairZone_TextHidden_NotInNotionDesign()
        {
            var t = ViewTestTables.Load();
            int checkedKinds = 0;
            foreach (var k in t.ObjectKind.Datas)
            {
                if (k.labelKind != LabelKind.StairText) continue;
                var o = RunObjectFactory.Create(t, k, 0, 10);
                Assert.That(ObjectLabel.Of(o, 0, 3, 0, 1).Visible, Is.False, k.key + ": 계단 구역 위 문구 숨김(노션 시안에 없음)");
                checkedKinds++;
            }
            Assert.That(checkedKinds, Is.GreaterThan(0), "StairText 종류가 표에 있다");
        }

        [Test]
        public void Wall_FrontTwoRowsOnly_RewardTimesGoldMul()
        {
            var o = Make("wall");
            o.HasHp = true;
            o.Max = 40;
            o.Hp = 30;
            o.Rew = FieldReward.Gold;
            o.RewardAmount = 12;
            var front = ObjectLabel.Of(o, 0, 3, ObjectLabel.WallShownRows - 1, 2.5);
            Assert.That(front.Visible, Is.True);
            Assert.That(front.Title, Is.EqualTo("+30"), "골드 = floor(12 × 2.5)");
            Assert.That(front.Number, Is.EqualTo("30"));
            Assert.That(front.Gauge, Is.EqualTo(0.75f).Within(1e-6));
            Assert.That(ObjectLabel.Of(o, 0, 3, ObjectLabel.WallShownRows, 2.5).Visible, Is.False, "앞에 2칸이 남아 있으면 숨김");
            o.Rew = FieldReward.Soul;
            o.RewardAmount = 2;
            Assert.That(ObjectLabel.Of(o, 0, 3, 0, 2.5).Title, Is.EqualTo("+2"), "골드가 아니면 배율 없음");
        }

        [Test]
        public void Arch_TextFromLabelKey_HiddenNearPlayer()
        {
            var o = Make("arch");
            o.LabelKey = ObjectLabel.FloorNKey;
            o.LabelArg = 2;
            var far = ObjectLabel.Of(o, o.Z - 10, 3, 0, 1);
            Assert.That(far.Visible, Is.True);
            Assert.That(far.Title, Is.EqualTo("골목 2"));
            Assert.That(ObjectLabel.Of(o, o.Z - 2, 3, 0, 1).Visible, Is.False, "관문 3 m 앞을 지나면 숨김");
            o.LabelKey = ObjectLabel.AreaGateKey;
            Assert.That(ObjectLabel.ArchText(o), Is.EqualTo((MvTables.Current?.AreaName(2) ?? "") + " 돌파"), "Area 이름 = 언어팩 name:<302 id>");
        }

        [Test]
        public void Tower_TitleOnly_HiddenAfterPass()
        {
            var o = Make("tower");
            var c = ObjectLabel.Of(o, o.Z - 5, 3, 0, 1);
            Assert.That(c.Visible, Is.True);
            Assert.That(c.Title, Is.EqualTo(ObjectLabel.TowerText));
            Assert.That(c.Number, Is.Null);
            Assert.That(c.Gauge, Is.LessThan(0));
            Assert.That(ObjectLabel.Of(o, o.Z + 2, 3, 0, 1).Visible, Is.False);
        }

        // 시험 이름은 정수만(실행기 기대 목록): (입력, 기대) 번호로 고른다.
        static readonly double[] s_fmtIn = { 999.9, 12345, 2500000 };
        static readonly string[] s_fmtOut = { "999", "12.3K", "2.5M" };

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Fmt_MatchesProto(int c) => Assert.That(ObjectLabel.Fmt(s_fmtIn[c]), Is.EqualTo(s_fmtOut[c]));

        /// <summary>글자판 컴포넌트·참조·정렬 순서는 프리팹이 원본이다(실행 중에 붙이거나 이름으로 찾지 않는다).</summary>
        [Test]
        public void CountLabelPrefab_HasComponentRefsAndTopSortOrder()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CountLabel.PrefabPath);
            Assert.That(prefab, Is.Not.Null, CountLabel.PrefabPath);
            var label = prefab.GetComponent<CountLabel>();
            Assert.That(label, Is.Not.Null, "글자판 프리팹 뿌리에 CountLabel");
            var so = new SerializedObject(label);
            foreach (var field in new[] { "_title", "_number", "_gauge", "_pivot" })
                Assert.That(so.FindProperty(field).objectReferenceValue, Is.Not.Null, "CountLabel." + field);
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                Assert.That(r.sortingOrder, Is.GreaterThanOrEqualTo(CountLabel.SortBase), r.name + ": 그룹 안 맨 위");
        }

        [Test]
        public void CountLabel_BindsPrefab_FillsNumberAndGauge()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LabelPrefab);
            Assert.That(prefab, Is.Not.Null, LabelPrefab);
            var go = Object.Instantiate(prefab);
            _made.Add(go);
            var label = CountLabel.Find(go.transform);
            Assert.That(label, Is.Not.Null);
            Assert.That(label.Title, Is.Not.Null, "Title");
            Assert.That(label.Number, Is.Not.Null, "Count_Number");
            Assert.That(label.Pivot, Is.Not.Null, "Gauge_Pivot");
            float baseX = label.Pivot.localScale.x;
            string prefabTitle = label.Title.text;

            label.Apply(new LabelContent(true, true, null, "7", 0.25f));
            Assert.That(label.Number.text, Is.EqualTo("7"));
            Assert.That(label.Title.text, Is.EqualTo(prefabTitle), "게이트는 프리팹 제목 유지");
            Assert.That(label.Pivot.localScale.x, Is.EqualTo(baseX * 0.25f).Within(1e-6));

            label.Apply(new LabelContent(true, false, null, "3", LabelContent.NoGauge));
            Assert.That(label.Title.gameObject.activeSelf, Is.False, "제목 없음 → 숨김");
            Assert.That(label.GaugeRoot.activeSelf, Is.False, "막대 없음 → 숨김");

            label.Apply(LabelContent.Hidden);
            Assert.That(label.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void LabelledObjectPrefabs_ContainCountLabel()
        {
            // 글자판을 쓰는 종류(105.labelKind ≠ None)의 프리팹(105.prefab·305 스테이지 프리팹)은 글자판 프리팹을 품는다.
            var t = ViewTestTables.Load();
            var labelled = new HashSet<string>();
            foreach (var k in t.ObjectKind.Datas)
                if (k.labelKind != LabelKind.None) labelled.Add(k.key);
            var prefabs = new HashSet<string>();
            foreach (var k in t.ObjectKind.Datas)
                if (labelled.Contains(k.key) && !string.IsNullOrEmpty(k.prefab)) prefabs.Add(k.prefab);
            foreach (var s in t.StageObject.Datas)
                if (labelled.Contains(s.kind) && !string.IsNullOrEmpty(s.prefab)) prefabs.Add(s.prefab);
            Assume.That(prefabs.Count, Is.GreaterThan(0));
            foreach (var name in prefabs)
            {
                var guids = AssetDatabase.FindAssets(name + " t:Prefab", new[] { PrefabRoot });
                GameObject asset = null;
                foreach (var g in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(g);
                    if (System.IO.Path.GetFileNameWithoutExtension(path) == name) { asset = AssetDatabase.LoadAssetAtPath<GameObject>(path); break; }
                }
                Assert.That(asset, Is.Not.Null, "프리팹 없음: " + name);
                var go = Object.Instantiate(asset);
                _made.Add(go);
                Assert.That(CountLabel.Find(go.transform), Is.Not.Null, name + "에 글자판(Obj_Text_Count) 없음");
            }
        }
    }
}
