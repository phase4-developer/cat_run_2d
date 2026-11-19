using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 오브 탑 단계 연출: 마법서 탑 게이지 단계(<see cref="ObjectViewContext.GaugeLv"/>, 1 ~ 8)만큼 단계 레이어를 앞에서부터 켠다(누적).
    /// [0] = 1단계(메인 오브), … 마지막까지 켜지면 최고 단계 모습. 단계가 바뀌면 바로 켜고 끈다(전환 연출 없음).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TowerOrbStages : RunObjectModule
    {
        [Tooltip("단계 레이어(순서 = 게이지 단계). 게이지 단계 n이면 [0] ~ [n-1]을 켠다")]
        [SerializeField] GameObject[] _stages = new GameObject[0];

        int _shown = -1;

        /// <summary>지금 켜 둔 단계 수(시험·QA).</summary>
        public int Shown => _shown;
        public int StageCount => _stages != null ? _stages.Length : 0;

        public override void OnBind(RunObjectView view)
        {
            _shown = -1;
            Apply(view.Ctx != null ? view.Ctx.GaugeLv : 1);
        }

        public override void OnTick(RunObjectView view, in ObjectFrame f)
        {
            if (view.Ctx != null) Apply(view.Ctx.GaugeLv);
        }

        /// <summary>단계 <paramref name="level"/>까지 켠다(에디터 미리보기에서도 부를 수 있다).</summary>
        public void Apply(int level)
        {
            if (_stages == null || level == _shown) return;
            _shown = level;
            for (int i = 0; i < _stages.Length; i++)
            {
                var g = _stages[i];
                if (g == null) continue;
                bool on = i < level;
                if (g.activeSelf != on) g.SetActive(on);
            }
        }
    }
}
