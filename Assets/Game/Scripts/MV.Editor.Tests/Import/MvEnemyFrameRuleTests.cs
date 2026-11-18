using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MV.Data;
using MV.Editor.Import;
using MV.View;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MV.Editor.Tests
{
    /// <summary>
    /// 리소스 검사기의 `102_enemy.sprite`(MV-FIX-RESCHECK). 게임과 같은 순서: 105(viewShape Mouse·enemyId) → 맵마다 305 → 105.prefab 프리팹,
    /// 몸 그림 `Image` 위 SpriteFrameList의 접두 그림, 없으면 테마 아틀라스. 정상 폴백(아틀라스 확인)과 자산 오류(오류)를 나눠 본다.
    /// 기대값은 대표 자산 이름으로 직접 적는다(공용 함수로 계산하지 않음).
    /// </summary>
    public sealed class MvEnemyFrameRuleTests
    {
        /// <summary>마녀의 하수인. 디자이너 b6ab1254: sprite = 접두 `Stage1_Enemy`, frameCount 2, 그림은 Obj_Enemy 프리팹 목록.</summary>
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

        static MvTables Load(string enemyTsv = null)
        {
            var source = new MvMemoryTableSource(new MvTsvDirectorySource(MvTableLoader.TsvDirectory));
            if (enemyTsv != null) source.Set(EnemyData.Table, enemyTsv);
            var t = new MvTables();
            t.LoadAll(source);
            return t;
        }

        static MvResourceCheckReport Check(MvTables t, Func<string, GameObject> prefabs) =>
            MvResourceColumnChecker.Check(t, MvArtCatalog.FromProject(), MvArtManifest.ReadTempList(), null, MvResourceColumnChecker.ProjectArtFileNames(), prefabs);

        static List<MvResIssue> RowIssues(MvResourceCheckReport r, int id) =>
            r.Issues.Where(i => i.Table == EnemyData.Table && i.RowId == id.ToString() && i.Column == "sprite").ToList();

        static string Show(IEnumerable<MvResIssue> issues) => string.Join("\n", issues);

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
            if (body) new GameObject("Image", typeof(SpriteRenderer)).transform.SetParent(board.transform, false);
            if (list != null) root.AddComponent<SpriteFrameList>().Frames = list;
            return root;
        }

        // ---------------- 프리팹 우선 ----------------

        [Test]
        public void Minion_ObjEnemyListSuppliesFrames_ThemeAtlasNotRequired()
        {
            var prefab = MvResourceColumnChecker.ProjectPrefabs()(EnemyPrefab);
            Assert.That(prefab, Is.Not.Null, "Addressables 주소 " + EnemyPrefab);
            var list = prefab.GetComponentsInChildren<SpriteFrameList>(true).SingleOrDefault();
            Assert.That(list, Is.Not.Null, "Obj_Enemy의 프레임 목록");
            Assert.That(list.Frames.Select(f => f ? f.name : null), Is.EquivalentTo(new[] { "Stage1_Enemy_00", "Stage1_Enemy_01" }), "디자이너가 넣은 그림");

            var t = Load();
            Assert.That(t.ObjectKind.Datas.Where(k => k.enemyId == Minion && k.viewShape == ObjectViewShape.Mouse).Select(k => k.prefab), Is.EqualTo(new[] { EnemyPrefab }),
                "105 적 모양 종류의 프리팹(305에 적 행 없음)");
            var report = Check(t, MvResourceColumnChecker.ProjectPrefabs());
            Assert.That(RowIssues(report, Minion), Is.Empty, "목록이 채우는 이름을 테마 아틀라스에 없다고 거부하지 않는다:\n" + Show(RowIssues(report, Minion)));
        }

        // ---------------- 정상 폴백 ----------------

        [Test]
        public void NormalFallback_NoListOrNoPrefixFrame_ChecksThemeAtlas_WithoutAssetError()
        {
            var t = Load();
            foreach (var prefab in new[] { NewEnemyPrefab(true, null), NewEnemyPrefab(true), NewEnemyPrefab(true, NewSprite("Other_Enemy_00")) })
            {
                var issues = RowIssues(Check(t, a => a == EnemyPrefab ? prefab : null), Minion);
                Assert.That(issues, Is.Not.Empty, "게임이 아틀라스로 넘어가므로 아틀라스 확인을 한다");
                Assert.That(issues.All(i => i.Kind == MvResIssueKind.Unextracted && i.Value == "Stage1_Enemy" && i.Atlas.StartsWith("Atlas_World_Theme_", StringComparison.Ordinal)),
                    Is.True, "정상 폴백은 자산 오류가 아니다:\n" + Show(issues));
            }
        }

        [Test]
        public void EnemyNotDrawnByPrefab_KeepsAtlasRule_EvenIfAPrefabListHasTheName()
        {
            var text = ReadTsv(EnemyData.Table).Replace("\tWorld_Enemy_Mouse\t", "\tWorld_Enemy_Nope\t");
            Assume.That(text, Does.Contain("\tWorld_Enemy_Nope\t"));
            var t = Load(text);
            Assume.That(t.ObjectKind.Datas.Any(k => k.enemyId == DefenceMouse), Is.False, "수비전 쥐는 105에 연결된 종류가 없다");
            var everything = NewEnemyPrefab(true, NewSprite("Stage1_Enemy_00"), NewSprite("Stage1_Enemy_01"), NewSprite("World_Enemy_Nope_00"));
            var report = Check(t, a => everything);
            Assert.That(RowIssues(report, DefenceMouse).Any(i => i.Value == "World_Enemy_Nope" && i.Kind == MvResIssueKind.Unextracted), Is.True,
                "프리팹으로 그리지 않는 적은 기존 아틀라스 규칙");
            Assert.That(RowIssues(report, Minion), Is.Empty, "같은 목록이 하수인은 채운다");
        }

        // ---------------- 자산 오류(폴백으로 숨기지 않음) ----------------

        [Test]
        public void PrefabCannotBeOpened_IsAnError_NotAtlasFallback()
        {
            var issues = RowIssues(Check(Load(), a => null), Minion);
            Assert.That(issues.Count, Is.EqualTo(1), Show(issues));
            Assert.That(issues[0].Kind, Is.EqualTo(MvResIssueKind.Error));
            Assert.That(issues[0].Atlas, Is.EqualTo(EnemyPrefab), "어느 프리팹인지");
            Assert.That(issues[0].Message, Does.Contain("열 수 없음"));
        }

        [Test]
        public void MissingBody_IsAnError_NotAtlasFallback()
        {
            var noBody = NewEnemyPrefab(false, NewSprite("Stage1_Enemy_00"), NewSprite("Stage1_Enemy_01"));
            var issues = RowIssues(Check(Load(), a => a == EnemyPrefab ? noBody : null), Minion);
            Assert.That(issues.Count, Is.EqualTo(1), Show(issues));
            Assert.That(issues[0].Kind, Is.EqualTo(MvResIssueKind.Error));
            Assert.That(issues[0].Message, Does.Contain("Image"));
        }

        [Test]
        public void BrokenListReference_IsAnError_EvenWhenOtherFramesSuffice()
        {
            var broken = NewEnemyPrefab(true, NewSprite("Stage1_Enemy_00"), null, NewSprite("Stage1_Enemy_01"));
            var issues = RowIssues(Check(Load(), a => a == EnemyPrefab ? broken : null), Minion);
            Assert.That(issues.Count, Is.EqualTo(1), Show(issues));
            Assert.That(issues[0].Kind, Is.EqualTo(MvResIssueKind.Error));
            Assert.That(issues[0].Atlas, Is.EqualTo(EnemyPrefab));
            Assert.That(issues[0].Message, Does.Contain("깨진"));

            var onlyBroken = NewEnemyPrefab(true, (Sprite)null);
            var both = RowIssues(Check(Load(), a => a == EnemyPrefab ? onlyBroken : null), Minion);
            Assert.That(both.Any(i => i.Kind == MvResIssueKind.Error && i.Message.Contains("깨진")), Is.True, "깨진 참조는 오류로 남기고");
            Assert.That(both.Any(i => i.Kind == MvResIssueKind.Unextracted), Is.True, "게임이 넘어가는 아틀라스도 확인한다:\n" + Show(both));
        }
    }
}
