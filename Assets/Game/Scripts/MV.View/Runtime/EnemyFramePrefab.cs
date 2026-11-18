using UnityEngine;
using MV.Data;

namespace MV.View
{
    /// <summary>
    /// 적 프리팹 구조(뷰): 몸 그림 자식과 그 위 <see cref="SpriteFrameList"/>. 이름 고르기는 <see cref="EnemyFrameRule"/>(MV.Data).
    /// 게임(<see cref="ObjectViewFactory"/>)은 로드한 프리팹 인스턴스로, 리소스 검사기(MV.Editor)는 에디터로 연 프리팹 자산으로 같은 함수를 부른다(MV-FIX-RESCHECK).
    /// </summary>
    public static class EnemyFramePrefab
    {
        /// <summary>적 프리팹에서 몸 그림(SpriteRenderer)이 붙은 자식 이름.</summary>
        public const string BodyName = "Image";

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

        /// <summary>프리팹 목록에서 고른 프레임. null이면 목록이 없거나 접두 그림이 없어 테마 아틀라스로 넘어간다(정상 폴백).</summary>
        public static Sprite[] FromPrefab(Transform body, EnemyData e)
        {
            string prefix = EnemyFrameRule.Prefix(e?.sprite);
            if (prefix == null) return null;
            var list = FrameList(body);
            return list != null ? list.Pick(prefix, EnemyFrameRule.Count(e)) : null;
        }
    }
}
