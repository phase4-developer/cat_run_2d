using System;
using UnityEngine;

namespace MV.Data
{
    /// <summary>
    /// 적 몸 그림 프레임을 고르는 규칙(`102_enemy.sprite`·`frameCount`). 게임(<c>ObjectViewFactory.EnemyFrames</c>)과
    /// 리소스 검사기(<c>MvResourceColumnChecker</c>)가 같이 부른다. 그림을 읽어 오는 방법(Addressables·AssetDatabase)은 각자 맡는다(MV-FIX-RESCHECK).
    /// <list type="number">
    /// <item>이 규칙으로 그리는 오브젝트: <c>105_objectKind</c>의 <c>viewShape = Mouse</c>이고 <c>enemyId</c>가 그 적인 종류(<see cref="UsesEnemyFrames"/>).</item>
    /// <item>프리팹: <c>305_stageObject</c>(맵·종류·보상) → 없으면 <c>105.prefab</c>(<see cref="StageObjectData.PrefabFor"/>).</item>
    /// <item>몸 그림: 프리팹 안 이름 <see cref="BodyName"/> 자식. 그 위(자기 포함 부모 쪽)의 <see cref="SpriteFrameList"/>에서 접두 그림을 고른다(<see cref="FromPrefab"/>).</item>
    /// <item>목록이 없거나 접두 그림이 없으면 지금 테마 아틀라스의 <see cref="FrameName"/>(접두 + 두 자리 번호)를 <see cref="Count"/>장.</item>
    /// </list>
    /// </summary>
    public static class EnemyFrameRule
    {
        /// <summary>적 프리팹에서 몸 그림(SpriteRenderer)이 붙은 자식 이름.</summary>
        public const string BodyName = "Image";

        /// <summary>프레임 접두(끝 `_` 포함). 값이 비면 null.</summary>
        public static string Prefix(string sprite)
        {
            if (string.IsNullOrEmpty(sprite)) return null;
            return sprite.EndsWith("_", StringComparison.Ordinal) ? sprite : sprite + "_";
        }

        /// <summary>아틀라스 프레임 이름: 접두 + 두 자리 번호(`World_Enemy_Mouse_00`).</summary>
        public static string FrameName(string prefix, int index) => prefix + index.ToString("00");

        /// <summary>넘길 장수(`frameCount`, 최소 1).</summary>
        public static int Count(EnemyData e) => e == null ? 0 : Math.Max(1, e.frameCount);

        /// <summary>이 종류가 적 프레임 규칙으로 그려지는가(적 모양 + 적 행 연결).</summary>
        public static bool UsesEnemyFrames(ObjectKindData k) => k != null && k.viewShape == ObjectViewShape.Mouse && k.enemyId != 0;

        /// <summary>프리팹 안 몸 그림 자식(<see cref="BodyName"/>, 뿌리 아래 깊이 우선). 없으면 null.</summary>
        public static Transform FindBody(Transform root)
        {
            if (root == null) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.name == BodyName) return c;
                var d = FindBody(c);
                if (d != null) return d;
            }
            return null;
        }

        /// <summary>몸 그림이 쓰는 프레임 목록(몸 그림과 그 부모 쪽, 꺼진 것 포함). 없으면 null.</summary>
        public static SpriteFrameList FrameList(Transform body) => body != null ? body.GetComponentInParent<SpriteFrameList>(true) : null;

        /// <summary>프리팹 목록에서 고른 프레임. null이면 목록이 없거나 접두 그림이 없어 테마 아틀라스로 넘어간다.</summary>
        public static Sprite[] FromPrefab(Transform body, EnemyData e)
        {
            string prefix = Prefix(e?.sprite);
            if (prefix == null) return null;
            var list = FrameList(body);
            return list != null ? list.Pick(prefix, Count(e)) : null;
        }
    }
}
