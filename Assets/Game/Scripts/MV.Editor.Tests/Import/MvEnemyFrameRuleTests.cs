using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MV.Data;
using MV.Editor.Import;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MV.Editor.Tests
{
    /// <summary>
    /// 적 프레임 규칙(MV-FIX-RESCHECK): 게임(<c>ObjectViewFactory.EnemyFrames</c>)과 리소스 검사기가 같은 <see cref="EnemyFrameRule"/>을 쓴다.
    /// 승인된 순서 = ① 105(viewShape Mouse·enemyId) → 맵마다 305 → 105.prefab 프리팹 ② 몸 그림 `Image` 위 SpriteFrameList의 접두 그림 ③ 없으면 테마 아틀라스 접두 + 번호.
    /// </summary>
    public sealed class MvEnemyFrameRuleTests
    {
        /// <summary>마녀의 하수인(102_enemy). 디자이너 b6ab1254: sprite = 접두 `Stage1_Enemy`, frameCount 2, 그림은 Obj_Enemy 프리팹 목록.</summary>
        const int Minion = 102001001;
        /// <summary>수비전 쥐: 105에 연결된 종류가 없어(모드 전용) 프리팹 규칙을 쓰지 않는다.</summary>
        const int DefenceMouse = 102001002;
        const string EnemyPrefab = "Obj_Enemy";

        readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        static string ReadTsv(string table) => File.ReadAllText(Path.Combine(MvTableLoader.TsvDirectory, table + ".txt"), new UTF8Encoding(false));

        static MvTables Load(MvMemoryTableSource source = null)
        {
            var t = new MvTables();
            t.LoadAll(source ?? new MvMemoryTableSource(new MvTsvDirectorySource(MvTableLoader.TsvDirectory)));
            return t;
        }

        static MvResourceCheckReport Check(MvTables t, Func<string, GameObject> prefabs) =>
            MvResourceColumnChecker.Check(t, MvArtCatalog.FromProject(), MvArtManifest.ReadTempList(), null, MvResourceColumnChecker.ProjectArtFileNames(), prefabs);

        static IEnumerable<MvResIssue> RowIssues(MvResourceCheckReport r, int id) =>
            r.Issues.Where(i => i.Table == EnemyData.Table && i.RowId == id.ToString() && i.Column == "sprite");

        Sprite NewSprite(string name)
        {
            var s = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f));
            s.name = name;
            _made.Add(s);
            return s;
        }

        /// <summary>적 프리팹 모양: 뿌리 / Billboard / Image(SpriteRenderer). list가 있으면 뿌리에 SpriteFrameList.</summary>
        GameObject NewEnemyPrefab(bool body, params Sprite[] list)
        {
            var root = new GameObject("FakeEnemy");
            _made.Add(root);
            var board = new GameObject("Billboard");
            board.transform.SetParent(root.transform, false);
            if (body)
            {
                var img = new GameObject(EnemyFrameRule.BodyName, typeof(SpriteRenderer));
                img.transform.SetParent(board.transform, false);
            }
            if (list != null) root.AddComponent<SpriteFrameList>().Frames = list;
            return root;
        }

        // ---------------- 프리팹 우선 ----------------

        [Test]
        public void Minion_UsesObjEnemyListFramesInNameOrder_AndAtlasIsNotRequired()
        {
            var t = Load();
            var enemy = t.Enemy.Datas.First(e => e.id == Minion);
            var kinds = t.ObjectKind.Datas.Where(k => EnemyFrameRule.UsesEnemyFrames(k) && k.enemyId == Minion).ToList();
            Assert.That(kinds, Is.Not.Empty, "105에서 적 모양으로 이 적을 그리는 종류");
            var maps = t.Area.Datas.Select(a => a.stageMap).Where(m => !string.IsNullOrEmpty(m)).Distinct().ToList();
            var addresses = kinds.SelectMany(k => maps.Select(m => StageObjectData.PrefabFor(t, m, k, StageObjectReward.Any))).Distinct().ToList();
            Assert.That(addresses, Is.EqualTo(new[] { EnemyPrefab }), "305에 적 행이 없어 모든 맵에서 105.prefab");

            var prefab = MvResourceColumnChecker.ProjectPrefabs()(EnemyPrefab);
            Assert.That(prefab, Is.Not.Null, "Addressables 주소 " + EnemyPrefab);
            var body = EnemyFrameRule.FindBody(prefab.transform);
            Assert.That(body, Is.Not.Null);
            Assert.That(body.GetComponent<SpriteRenderer>(), Is.Not.Null, "몸 그림 SpriteRenderer");
            var frames = EnemyFrameRule.FromPrefab(body, enemy);
            Assert.That(frames?.Select(f => f.name), Is.EqualTo(new[] { "Stage1_Enemy_00", "Stage1_Enemy_01" }), "목록에서 접두 `Stage1_Enemy_` 그림을 이름순 frameCount장");

            var report = MvResourceColumnChecker.CheckProject();
            Assert.That(RowIssues(report, Minion).Select(i => i.ToString()), Is.Empty, "프리팹 목록이 채우는 이름을 테마 아틀라스에 없다고 거부하지 않는다");
        }

        // ---------------- 폴백 ----------------

        [Test]
        public void Rule_FallsBackToAtlasOnlyWhenListMissingOrHasNoPrefixFrame()
        {
            var enemy = Load().Enemy.Datas.First(e => e.id == Minion);
            Assert.That(EnemyFrameRule.Prefix("Stage1_Enemy"), Is.EqualTo("Stage1_Enemy_"));
            Assert.That(EnemyFrameRule.Prefix("Stage1_Enemy_"), Is.EqualTo("Stage1_Enemy_"));
            Assert.That(EnemyFrameRule.FrameName("World_Enemy_Mouse_", 0), Is.EqualTo("World_Enemy_Mouse_00"), "아틀라스 프레임 이름");

            Body(NewEnemyPrefab(true, null), out var noList);
            Assert.That(EnemyFrameRule.FromPrefab(noList, enemy), Is.Null, "목록 없음 → 아틀라스");
            Body(NewEnemyPrefab(true), out var empty);
            Assert.That(EnemyFrameRule.FromPrefab(empty, enemy), Is.Null, "빈 목록 → 아틀라스");
            Body(NewEnemyPrefab(true, NewSprite("Other_Enemy_00")), out var other);
            Assert.That(EnemyFrameRule.FromPrefab(other, enemy), Is.Null, "접두가 다른 그림만 → 아틀라스");

            var listed = NewEnemyPrefab(true, NewSprite("Stage1_Enemy_01"), null, NewSprite("Stage1_Enemy_02"), NewSprite("Stage1_Enemy_00"));
            Body(listed, out var b);
            Assert.That(EnemyFrameRule.FromPrefab(b, enemy).Select(f => f.name), Is.EqualTo(new[] { "Stage1_Enemy_00", "Stage1_Enemy_01" }), "이름순, frameCount까지, 빈 칸은 건너뜀");
            Assert.That(listed.GetComponent<SpriteFrameList>().MissingCount, Is.EqualTo(1));
        }

        static void Body(GameObject prefab, out Transform body) => body = EnemyFrameRule.FindBody(prefab.transform);

        [Test]
        public void Checker_PrefabWithoutPrefixFrames_StillRequiresThemeAtlas()
        {
            var t = Load();
            int themes = MvArtCatalog.FromProject().ThemeAtlases.Count;
            var noMatch = NewEnemyPrefab(true, NewSprite("Other_Enemy_00"));
            var report = Check(t, a => a == EnemyPrefab ? noMatch : null);
            var issues = RowIssues(report, Minion).ToList();
            Assert.That(issues, Is.Not.Empty, "목록이 채우지 못하면 게임이 아틀라스로 넘어가므로 아틀라스 확인을 되살린다");
            Assert.That(issues.All(i => i.Value == "Stage1_Enemy" && i.Atlas.StartsWith("Atlas_World_Theme_", StringComparison.Ordinal)), Is.True, string.Join("\n", issues));

            var unreachable = Check(t, a => null);
            Assert.That(RowIssues(unreachable, Minion), Is.Not.Empty, "프리팹을 볼 수 없으면 기존 아틀라스 확인");
            Assume.That(themes, Is.GreaterThan(0));
        }

        [Test]
        public void Checker_EnemyNotDrawnByPrefab_KeepsAtlasRule_EvenIfSomePrefabListHasTheName()
        {
            var text = ReadTsv(EnemyData.Table).Replace("\tWorld_Enemy_Mouse\t", "\tWorld_Enemy_Nope\t");
            Assume.That(text, Does.Contain("\tWorld_Enemy_Nope\t"));
            var t = Load(new MvMemoryTableSource(new MvTsvDirectorySource(MvTableLoader.TsvDirectory)).Set(EnemyData.Table, text));
            Assume.That(t.ObjectKind.Datas.Any(k => EnemyFrameRule.UsesEnemyFrames(k) && k.enemyId == DefenceMouse), Is.False, "수비전 쥐는 105에 연결된 종류가 없다");
            var everyList = NewEnemyPrefab(true, NewSprite("Stage1_Enemy_00"), NewSprite("Stage1_Enemy_01"), NewSprite("World_Enemy_Nope_00"));
            var report = Check(t, a => everyList);
            Assert.That(RowIssues(report, DefenceMouse).Any(i => i.Value == "World_Enemy_Nope" && i.Kind == MvResIssueKind.Unextracted), Is.True,
                "프리팹으로 그리지 않는 적은 기존 아틀라스 규칙(다른 프리팹 목록으로 통과시키지 않음)");
            Assert.That(RowIssues(report, Minion), Is.Empty, "같은 목록이 하수인은 채운다");
        }

        // ---------------- 실제 결함 ----------------

        [Test]
        public void Checker_BrokenListReferenceOrMissingBody_IsAnError()
        {
            var t = Load();
            var broken = NewEnemyPrefab(true, NewSprite("Stage1_Enemy_00"), null, NewSprite("Stage1_Enemy_01"));
            var r1 = Check(t, a => a == EnemyPrefab ? broken : null);
            var e1 = RowIssues(r1, Minion).Where(i => i.Kind == MvResIssueKind.Error).ToList();
            Assert.That(e1.Count, Is.EqualTo(1), string.Join("\n", RowIssues(r1, Minion)));
            Assert.That(e1[0].Atlas, Is.EqualTo(EnemyPrefab), "어느 프리팹인지");
            Assert.That(e1[0].Message, Does.Contain("깨진"));

            var noBody = NewEnemyPrefab(false, NewSprite("Stage1_Enemy_00"), NewSprite("Stage1_Enemy_01"));
            var r2 = Check(t, a => a == EnemyPrefab ? noBody : null);
            Assert.That(RowIssues(r2, Minion).Any(i => i.Kind == MvResIssueKind.Error && i.Message.Contains(EnemyFrameRule.BodyName)), Is.True,
                "몸 그림이 없으면 게임이 그릴 곳이 없다");
        }
    }
}
