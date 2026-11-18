using DG.Tweening;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 같은 오브젝트의 DOTweenAnimation(DOTween Pro)을 오브젝트마다 다르게 만든다: 시작 위치(위상)·속도·움직임 크기를 범위 안에서 무작위로.
    /// 템플릿을 복제해 쓰는 장식(StageMap 길가 장식)이나 같은 설정의 하늘 장식이 다 같이 똑같이 움직이지 않게 한다.
    /// DOTweenAnimation 설정(에셋)은 바꾸지 않고, 실행할 때 한 번(Start) 트윈만 다시 만든다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TweenRandomizer : MonoBehaviour
    {
        [Tooltip("켜면 주기 안의 아무 시점에서 시작한다(박자가 어긋나 보이는 데 가장 효과가 크다)")]
        public bool RandomPhase = true;
        [Tooltip("속도 배율 범위(1 = 원래 속도). 다르면 시간이 갈수록 박자가 더 어긋난다")]
        public Vector2 SpeedRange = new Vector2(0.8f, 1.2f);
        [Tooltip("움직임 크기 배율 범위(끝값에 곱함, 1 = 원래 크기)")]
        public Vector2 AmountRange = new Vector2(0.7f, 1.3f);

        bool _done;
        // 원래 끝값(처음 적용 때 한 번 기록). 직렬화해 두어서, 이미 값이 바뀐 템플릿을 복제한 오브젝트가 바뀐 값을 또 곱하지 않게 한다.
        [SerializeField, HideInInspector] bool _hasBase;
        [SerializeField, HideInInspector] Vector3[] _baseV3;
        [SerializeField, HideInInspector] Vector2[] _baseV2;
        [SerializeField, HideInInspector] float[] _baseF;

        void Start() => Apply();

        /// <summary>무작위 값을 적용한다(처음 한 번). 다시 섞으려면 <paramref name="force"/> = true.</summary>
        public void Apply(bool force = false)
        {
            if (_done && !force) return;
            _done = true;
            var anims = GetComponents<DOTweenAnimation>();
            if (!_hasBase || _baseV3 == null || _baseV3.Length != anims.Length)
            {
                _baseV3 = new Vector3[anims.Length];
                _baseV2 = new Vector2[anims.Length];
                _baseF = new float[anims.Length];
                for (int i = 0; i < anims.Length; i++)
                {
                    _baseV3[i] = anims[i].endValueV3;
                    _baseV2[i] = anims[i].endValueV2;
                    _baseF[i] = anims[i].endValueFloat;
                }
                _hasBase = true;
            }
            for (int i = 0; i < anims.Length; i++)
            {
                var anim = anims[i];
                if (anim == null || !anim.isActive) continue;
                float amount = Random.Range(AmountRange.x, AmountRange.y);
                anim.endValueV3 = _baseV3[i] * amount;
                anim.endValueV2 = _baseV2[i] * amount;
                anim.endValueFloat = _baseF[i] * amount;
                anim.RewindThenRecreateTweenAndPlay();   // 원래 자세로 되돌린 뒤 새 끝값으로 다시 만든다(상대값 트윈이 누적되지 않게)
                var t = anim.tween;
                if (t == null) continue;
                t.timeScale = Random.Range(SpeedRange.x, SpeedRange.y);
                if (RandomPhase)
                {
                    float cycle = anim.duration * (anim.loopType == LoopType.Yoyo ? 2f : 1f);
                    t.Goto(anim.delay + Random.value * cycle, true);
                }
            }
        }
    }
}
