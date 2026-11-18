using TMPro;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 오브젝트 글자판(03 §3.7.1): TMP 월드 텍스트, 카메라를 보는 빌보드. 안개 알파 = opacity × (1 − fogK)(<see cref="IFogAlphaTarget"/>).
    /// 외곽선 `rgba(0,0,0,.8)`·굵기는 공유 TMP 머티리얼에서 정한다(라벨마다 머티리얼 인스턴스 금지, 03 §3.8.9).
    /// 프리팹 안 TMP에 붙여 둔다(계단 칸 Obj_ClimbStep의 Number). 실행 중에 만들지 않는다. 글꼴·크기·정렬 순서는 프리팹 값.
    /// </summary>
    [RequireComponent(typeof(TextMeshPro))]
    public sealed class WorldLabel : MonoBehaviour, IFogAlphaTarget
    {
        TextMeshPro _tmp;
        float _opacity = 1f, _fog = 1f;
        string _text;

        public TextMeshPro Text => _tmp != null ? _tmp : (_tmp = GetComponent<TextMeshPro>());
        public string Current => _text;

        /// <summary>글자가 바뀔 때만 TMP를 다시 만든다(매 프레임 같은 값이면 할당 없음).</summary>
        public void SetText(string text)
        {
            if (text == _text) return;
            _text = text;
            Text.text = text ?? string.Empty;
        }

        public void SetOpacity(float a)
        {
            _opacity = a;
            Apply();
        }

        public void SetFogAlpha(float alpha)
        {
            _fog = alpha;
            Apply();
        }

        void Apply() => Text.alpha = _opacity * _fog;
    }
}
