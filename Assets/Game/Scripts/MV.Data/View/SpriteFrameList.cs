using System;
using System.Collections.Generic;
using UnityEngine;

namespace MV.Data
{
    /// <summary>
    /// 프리팹에 직접 넣어 두는 프레임 그림 목록(스테이지 전용 리소스처럼 테마 아틀라스에 없는 그림용).
    /// 적은 `102_enemy.sprite` 접두(예: Stage1_Enemy → Stage1_Enemy_00, _01 …)로 이 목록에서 먼저 찾고, 없으면 테마 아틀라스에서 찾는다(<see cref="EnemyFrameRule"/>).
    /// 넘기는 속도는 `102_enemy.fps`, 장수는 `frameCount`(목록에 있는 만큼까지).
    /// 게임(MV.View)과 리소스 검사기(MV.Editor)가 같은 규칙을 쓰도록 둘 다 참조하는 MV.Data에 둔다(MV-FIX-RESCHECK).
    /// </summary>
    public sealed class SpriteFrameList : MonoBehaviour
    {
        [Tooltip("프레임 그림들. 이름 접두로 골라 이름순으로 넘긴다(여러 적의 프레임을 한 목록에 넣어도 된다)")]
        public Sprite[] Frames = new Sprite[0];

        /// <summary><paramref name="prefix"/>(끝 _ 포함)로 시작하는 그림을 이름순으로 최대 <paramref name="count"/>장. 없으면 null.</summary>
        public Sprite[] Pick(string prefix, int count)
        {
            if (Frames == null || string.IsNullOrEmpty(prefix)) return null;
            var list = new List<Sprite>();
            foreach (var s in Frames)
                if (s != null && s.name.StartsWith(prefix, StringComparison.Ordinal)) list.Add(s);
            if (list.Count == 0) return null;
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            if (count > 0 && list.Count > count) list.RemoveRange(count, list.Count - count);
            return list.ToArray();
        }

        /// <summary>목록 칸 중 비어 있거나 깨진 참조(그림이 지워짐) 수.</summary>
        public int MissingCount
        {
            get
            {
                int n = 0;
                if (Frames != null) foreach (var s in Frames) if (s == null) n++;
                return n;
            }
        }
    }
}
