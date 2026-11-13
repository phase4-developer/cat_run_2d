using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 오브젝트 프리팹(`105_objectKind.prefab`, `305_stageObject.prefab`)의 줄어드는 단계 그림(노션 「신작 배경 리소스 관리」:
    /// 츄르 더미 Pile_1~3, 블루 마법석 Crystal_L·C·R). 남은 체력 비율만큼 앞에서부터 켠다. 단계 수 = 배열 길이(프리팹이 정한다).
    /// 이 컴포넌트가 없는 프리팹은 단계 없이 통째로 그린다.
    /// </summary>
    public sealed class ObjectPrefabRig : MonoBehaviour
    {
        [Tooltip("남은 체력이 줄면 뒤에서부터 끄는 그림(앞 = 마지막까지 남는 것)")]
        [SerializeField] GameObject[] _stages;

        public int StageCount => _stages != null ? _stages.Length : 0;

        /// <summary>켜 둘 단계 수 = ceil(n × hp / max). 다 먹기 전(hp &gt; 0)에는 1개 이상 남긴다.</summary>
        public static int ShownStages(int n, double hp, double max)
        {
            if (n <= 0) return 0;
            if (max <= 0 || hp >= max) return n;
            if (hp <= 0) return 0;
            int k = (int)System.Math.Ceiling(n * hp / max);
            return System.Math.Max(1, System.Math.Min(n, k));
        }

        /// <summary>체력 비율로 단계 그림을 켜고 끈다.</summary>
        public void SetFill(double hp, double max)
        {
            int n = StageCount, k = ShownStages(n, hp, max);
            for (int i = 0; i < n; i++)
            {
                var s = _stages[i];
                if (s != null && s.activeSelf != i < k) s.SetActive(i < k);
            }
        }
    }
}
