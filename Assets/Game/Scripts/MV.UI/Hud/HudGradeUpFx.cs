using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 등급 업 연출(MV-UI3-STAIRFX 노션 「Stair 업그레이드 연출」): 크기 튐(Tween.Scale) + 흰 덮개(Flash) + 반짝임(Effect_Sparkle).
    /// 무기 칸 머지 결과(<see cref="HudWeaponSlot.PlayMerged"/>)와 같은 모양이다. 디자이너가 칸 안에 둔 <c>Flash</c>·<c>Effect_Sparkle</c>을 연결해 쓰고,
    /// 계단 칸(인게임 <c>Mid/Stair/Slot_Stair</c>, 로비 <c>Mid/Stair/Slot_Stair</c>)에 붙는다. 언제 부를지는 화면(계단 섹션·로비)이 정한다.
    /// 연출 값은 이 필드가 정본(노션에 수치가 없어 무기 머지 값을 임시로 따름, 원장 MV-UI3-STAIRFX).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudGradeUpFx : MonoBehaviour
    {
        [Tooltip("크기가 튀는 대상(비면 이 오브젝트)")]
        [SerializeField] Transform scaleTarget;
        [Tooltip("시작 크기(임시, 무기 머지 mergedScale과 같음)")]
        [SerializeField] float fromScale = 1.35f;
        [Tooltip("제모습으로 돌아오는 초(임시)")]
        [SerializeField] float sec = 0.3f;
        [SerializeField] Ease ease = Ease.OutQuad;
        [Tooltip("하얗게 밝아졌다 사라지는 덮개(디자이너 Flash). 쉴 때 색·켜짐은 프리팹 그대로 둔다.")]
        [SerializeField] Graphic flash;
        [Tooltip("덮개 최고 알파(임시)")]
        [SerializeField] float flashPeak = 1f;
        [Tooltip("덮개 모양을 따를 등급 틀 그림(등급마다 그림이 바뀐다). 비면 덮개 그림 그대로.")]
        [SerializeField] Image flashShapeFrom;
        [Tooltip("켤 때마다 처음부터 터지는 반짝임(디자이너 Effect_Sparkle). 쉴 때는 꺼 둔다.")]
        [SerializeField] GameObject sparkle;

        Color _flashRest;
        bool _flashActiveRest, _captured;
        Vector3 _scaleRest;

        Transform Target => scaleTarget ? scaleTarget : transform;

        public bool HasFlash => flash;
        public bool HasSparkle => sparkle;
        /// <summary>연출 중인가(시험·QA).</summary>
        public bool Playing { get; private set; }
        /// <summary>재생 횟수(시험·QA).</summary>
        public int PlayCount { get; private set; }

        void Capture()
        {
            if (_captured) return;
            _captured = true;
            _scaleRest = Target.localScale;
            if (flash)
            {
                _flashRest = flash.color;
                _flashActiveRest = flash.gameObject.activeSelf;
            }
        }

        /// <summary>크기 fromScale → 제모습, 덮개 flashPeak → 0, 반짝임 다시 켜기.</summary>
        public Tween Play()
        {
            Capture();
            Stop();
            Playing = true;
            PlayCount++;
            var t = Target;
            t.localScale = _scaleRest * fromScale;
            if (sparkle) { sparkle.SetActive(false); sparkle.SetActive(true); }
            var seq = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            seq.Join(t.DOScale(_scaleRest, sec).SetEase(ease));
            if (flash)
            {
                if (flash is Image f && flashShapeFrom && flashShapeFrom.sprite)
                {
                    f.sprite = flashShapeFrom.sprite;
                    f.type = flashShapeFrom.type;
                    f.pixelsPerUnitMultiplier = flashShapeFrom.pixelsPerUnitMultiplier;
                    f.preserveAspect = flashShapeFrom.preserveAspect;
                }
                flash.gameObject.SetActive(true);
                SetFlashAlpha(flashPeak);
                float a = flashPeak;
                seq.Join(DOTween.To(() => a, v => { a = v; SetFlashAlpha(v); }, 0f, sec).SetEase(ease));
            }
            seq.OnComplete(Stop);
            return seq;
        }

        void SetFlashAlpha(float a)
        {
            var c = _flashRest;
            c.a = a;
            flash.color = c;
        }

        /// <summary>연출을 멈추고 쉬는 모습으로(반짝임은 끝까지 두지 않고 끈다).</summary>
        public void Stop()
        {
            DOTween.Kill(this);
            if (!_captured) return;
            Playing = false;
            Target.localScale = _scaleRest;
            if (flash)
            {
                flash.color = _flashRest;
                flash.gameObject.SetActive(_flashActiveRest);
            }
        }

        /// <summary>반짝임까지 끈다(화면을 다시 열 때).</summary>
        public void ResetLook()
        {
            Stop();
            if (sparkle) sparkle.SetActive(false);
        }

        void OnDisable() => ResetLook();
    }
}
