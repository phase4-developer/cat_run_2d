using System;
using System.Collections.Generic;

namespace MV.Data
{
    /// <summary>
    /// 적 몸 그림 프레임을 고르는 이름 규칙(`102_enemy.sprite`·`frameCount`). 표 값과 이름만 다룬다(Unity 오브젝트 없음).
    /// 게임(<c>MV.View.EnemyFramePrefab</c>·<c>ObjectViewFactory.EnemyFrames</c>)과 리소스 검사기(<c>MvResourceColumnChecker</c>)가 같이 부른다(MV-FIX-RESCHECK).
    /// <list type="number">
    /// <item>이 규칙으로 그리는 오브젝트: <c>105_objectKind</c>의 <c>viewShape = Mouse</c>이고 <c>enemyId</c>가 그 적인 종류(<see cref="UsesEnemyFrames"/>).</item>
    /// <item>프리팹 주소: <c>305_stageObject</c>(맵·종류·보상) → 없으면 <c>105.prefab</c>(<see cref="StageObjectData.PrefabFor"/>).</item>
    /// <item>프리팹 프레임 목록에서 접두 그림을 이름순으로 <see cref="Count"/>장(<see cref="PickNames"/>). 프리팹 구조(몸 그림·목록 컴포넌트)는 MV.View가 안다.</item>
    /// <item>목록이 없거나 접두 그림이 없으면 테마 아틀라스의 <see cref="FrameName"/>(접두 + 두 자리 번호).</item>
    /// </list>
    /// </summary>
    public static class EnemyFrameRule
    {
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

        /// <summary>
        /// 목록 이름들(<paramref name="names"/>, 비어 있는 칸은 null) 중 <paramref name="prefix"/>로 시작하는 것을 이름순(서수)으로 최대 <paramref name="count"/>개 고른 칸 번호.
        /// 하나도 없으면 null(아틀라스로 넘어간다).
        /// </summary>
        public static int[] PickNames(IReadOnlyList<string> names, string prefix, int count)
        {
            if (names == null || string.IsNullOrEmpty(prefix)) return null;
            var picked = new List<int>();
            for (int i = 0; i < names.Count; i++)
                if (names[i] != null && names[i].StartsWith(prefix, StringComparison.Ordinal)) picked.Add(i);
            if (picked.Count == 0) return null;
            picked.Sort((a, b) => string.CompareOrdinal(names[a], names[b]));
            if (count > 0 && picked.Count > count) picked.RemoveRange(count, picked.Count - count);
            return picked.ToArray();
        }
    }
}
