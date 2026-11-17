using TMPro;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 글자판 프리팹 `Obj_Text_Count`(디자이너 리소스, 노션 「게이트 카운터 프리팹 설명」)를 채운다.
    /// 자식 이름: `Title`(제목 TMP) · `Count_Number`(숫자 TMP) · `Count_Gauge`(막대 바탕) ▸ `Gauge_Pivot`(x 배율 0~1 = 채움) ▸ `Gauge_Color`.
    /// 오브젝트 프리팹이 이 프리팹을 품고 있으면 <see cref="Find"/>가 런타임에 붙인다(프리팹에 미리 붙여 둘 필요 없음).
    /// 위치·크기·색은 프리팹 값 그대로 두고 글자·보임·막대 길이만 바꾼다. 안개 알파 = opacity × (1 − fogK)(<see cref="IFogAlphaTarget"/>).
    /// </summary>
    public sealed class CountLabel : MonoBehaviour, IFogAlphaTarget
    {
        public const string NumberName = "Count_Number", TitleName = "Title", GaugeName = "Count_Gauge", PivotName = "Gauge_Pivot";
        /// <summary>그룹 안 맨 위(정본 글자판 편향 −0.05와 같은 결과, <see cref="ObjectViewFactory"/> TMP 라벨과 같은 값). 프리팹 안 순서는 이 위에 더한다.</summary>
        public const int SortBase = 100;

        TMP_Text _title, _number;
        GameObject _gauge;
        Transform _pivot;
        Vector3 _pivotScale = Vector3.one;
        bool _bound;
        string _titleText, _numberText;
        float _fill = -2f;

        public TMP_Text Title => _title;
        public TMP_Text Number => _number;
        public GameObject GaugeRoot => _gauge;
        public Transform Pivot => _pivot;

        /// <summary><paramref name="root"/> 아래에서 `Count_Number`를 가진 글자판을 찾아 붙인다. 없으면 null.</summary>
        public static CountLabel Find(Transform root)
        {
            var num = Deep(root, NumberName);
            if (num == null || num.parent == null) return null;
            var host = num.parent;
            if (!host.TryGetComponent(out CountLabel label)) label = host.gameObject.AddComponent<CountLabel>();
            label.Bind();
            return label;
        }

        void Bind()
        {
            if (_bound) return;
            _bound = true;
            var t = transform;
            _title = Child<TMP_Text>(t, TitleName);
            _number = Child<TMP_Text>(t, NumberName);
            var g = t.Find(GaugeName);
            _gauge = g != null ? g.gameObject : null;
            _pivot = g != null ? Deep(g, PivotName) : null;
            if (_pivot != null) _pivotScale = _pivot.localScale;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.sortingOrder += SortBase;
            if (_title != null) _titleText = _title.text;
        }

        /// <summary>내용을 입힌다(바뀐 칸만 다시 만든다).</summary>
        public void Apply(in LabelContent c)
        {
            if (gameObject.activeSelf != c.Visible) gameObject.SetActive(c.Visible);
            if (!c.Visible) return;
            if (_title != null)
            {
                bool on = c.KeepTitle || c.Title != null;
                if (_title.gameObject.activeSelf != on) _title.gameObject.SetActive(on);
                if (!c.KeepTitle && c.Title != null && c.Title != _titleText) { _titleText = c.Title; _title.text = c.Title; }
            }
            if (_number != null)
            {
                bool on = c.Number != null;
                if (_number.gameObject.activeSelf != on) _number.gameObject.SetActive(on);
                if (on && c.Number != _numberText) { _numberText = c.Number; _number.text = c.Number; }
            }
            if (_gauge != null)
            {
                bool on = c.Gauge >= 0;
                if (_gauge.activeSelf != on) _gauge.SetActive(on);
                if (on && _pivot != null && c.Gauge != _fill)
                {
                    _fill = c.Gauge;
                    _pivot.localScale = new Vector3(_pivotScale.x * c.Gauge, _pivotScale.y, _pivotScale.z);
                }
            }
        }

        public void SetFogAlpha(float alpha)
        {
            if (_title != null) _title.alpha = alpha;
            if (_number != null) _number.alpha = alpha;
        }

        static T Child<T>(Transform t, string name) where T : Component
        {
            var c = t.Find(name);
            return c != null ? c.GetComponent<T>() : null;
        }

        static Transform Deep(Transform t, string name)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                if (c.name == name) return c;
                var d = Deep(c, name);
                if (d != null) return d;
            }
            return null;
        }
    }
}
