using System.Collections.Generic;
using System.Linq;
using MV.Data;
using NUnit.Framework;
using UnityEngine;

namespace MV.View.Tests
{
    /// <summary>
    /// 적 프레임 선택(MV-FIX-RESCHECK): 이름 규칙 <see cref="EnemyFrameRule"/>(MV.Data) + 프리팹 구조 <see cref="EnemyFramePrefab"/>(MV.View).
    /// 승인된 순서 = 몸 그림 `Image` 위 SpriteFrameList의 접두 그림 → 없으면 테마 아틀라스 접두 + 두 자리 번호. 기대값은 이름으로 직접 적는다.
    /// </summary>
    public sealed class EnemyFramePrefabTests
    {
        readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        static EnemyData Minion() => ViewTestTables.Load().Enemy.Datas.First(e => e.id == 102001001);

        Sprite NewSprite(string name)
        {
            var s = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f));
            s.name = name;
            _made.Add(s);
            return s;
        }

        /// <summary>뿌리 / Billboard / Image(SpriteRenderer). list가 있으면 뿌리에 SpriteFrameList.</summary>
        Transform NewBody(params Sprite[] list)
        {
            var root = new GameObject("FakeEnemy");
            _made.Add(root);
            var board = new GameObject("Billboard");
            board.transform.SetParent(root.transform, false);
            var img = new GameObject("Image", typeof(SpriteRenderer));
            img.transform.SetParent(board.transform, false);
            if (list != null) root.AddComponent<SpriteFrameList>().Frames = list;
            return EnemyFramePrefab.FindBody(root.transform);
        }

        [Test]
        public void Names_PrefixAndAtlasFrameNames()
        {
            Assert.AreEqual("Stage1_Enemy_", EnemyFrameRule.Prefix("Stage1_Enemy"));
            Assert.AreEqual("Stage1_Enemy_", EnemyFrameRule.Prefix("Stage1_Enemy_"));
            Assert.IsNull(EnemyFrameRule.Prefix(""));
            Assert.AreEqual("World_Enemy_Mouse_00", EnemyFrameRule.FrameName("World_Enemy_Mouse_", 0));
            Assert.AreEqual("World_Enemy_Mouse_15", EnemyFrameRule.FrameName("World_Enemy_Mouse_", 15));
        }

        [Test]
        public void PrefabList_IsUsedFirst_InNameOrder_UpToFrameCount()
        {
            var e = Minion();
            Assume.That(e.sprite, Is.EqualTo("Stage1_Enemy"));
            Assume.That(e.frameCount, Is.EqualTo(2));
            var body = NewBody(NewSprite("Stage1_Enemy_01"), null, NewSprite("Stage1_Enemy_02"), NewSprite("Other_00"), NewSprite("Stage1_Enemy_00"));
            Assert.That(body, Is.Not.Null, "몸 그림 = Image");
            Assert.That(EnemyFramePrefab.FromPrefab(body, e).Select(s => s.name), Is.EqualTo(new[] { "Stage1_Enemy_00", "Stage1_Enemy_01" }));
            Assert.AreEqual(1, EnemyFramePrefab.FrameList(body).MissingCount, "빈 칸은 게임은 건너뛰고 검사기는 오류로 센다");
        }

        [Test]
        public void NoList_EmptyList_OrNoPrefixFrame_FallBackToAtlas()
        {
            var e = Minion();
            Assert.IsNull(EnemyFramePrefab.FromPrefab(NewBody(null), e), "목록 없음");
            Assert.IsNull(EnemyFramePrefab.FromPrefab(NewBody(), e), "빈 목록");
            Assert.IsNull(EnemyFramePrefab.FromPrefab(NewBody(NewSprite("Other_Enemy_00")), e), "접두 그림 없음");
            Assert.IsNull(EnemyFramePrefab.FromPrefab(null, e), "몸 그림 없음");
        }
    }
}
