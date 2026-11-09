using TMPro;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 오브젝트 글자판(03 §3.7.1): TMP 월드 텍스트, 카메라를 보는 빌보드. 안개 알파 = opacity × (1 − fogK)(<see cref="IFogAlphaTarget"/>).
    /// 외곽선 `rgba(0,0,0,.8)`·굵기는 공유 TMP 머티리얼에서 정한다(라벨마다 머티리얼 인스턴스 금지, 03 §3.8.9).
    /// </summary>
    [RequireComponent(typeof(TextMeshPro))]
    public sealed class WorldLabel : MonoBehaviour, IFogAlphaTarget
    {
        TextMeshPro _tmp;
        float _opacity = 1f, _fog = 1f;
        string _text;

        public TextMeshPro Text => _tmp != null ? _tmp : (_tmp = GetComponent<TextMeshPro>());
        public string Current => _text;

        /// <summary>새 라벨: 가운데 정렬, 월드 높이 <paramref name="sizeM"/> m 글자, 공유 머티리얼(있으면).</summary>
        public static WorldLabel Create(Transform parent, string name, float sizeM, Material shared)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<Billboard>();
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.fontSize = sizeM * 10f;   // TMP 월드: fontSize 10 ≈ 1 m 줄 높이
            if (shared != null) tmp.fontSharedMaterial = shared;
            return go.AddComponent<WorldLabel>();
        }

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
