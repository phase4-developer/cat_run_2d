using TMPro;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 글자판 프리팹 `Obj_Text_Count`(디자이너 리소스, 노션 「게이트 카운터 프리팹 설명」)를 채운다.
    /// 이 컴포넌트는 글자판 프리팹 뿌리에 붙어 있고, 제목·숫자·막대·막대 피벗은 Inspector 참조다(이름으로 찾지 않는다).
    /// 오브젝트 프리팹은 글자판 프리팹을 중첩해 품는다. 실행 중에 이 컴포넌트를 붙이거나 만들지 않는다.
    /// 위치·크기·색·정렬 순서(그룹 안 맨 위 = 100대)는 프리팹 값 그대로 두고 글자·보임·막대 길이만 바꾼다. 안개 알파 = opacity × (1 − fogK)(<see cref="IFogAlphaTarget"/>).
    /// </summary>
    public sealed class CountLabel : MonoBehaviour, IFogAlphaTarget
    {
        public const string PrefabPath = "Assets/Game/Addressables/Local/BG/Prefab/Object/Obj_Text_Count.prefab";
        /// <summary>그룹 안 맨 위(정본 글자판 편향 −0.05와 같은 결과). 글자판 프리팹의 정렬 순서가 이 값대에 있고, 한 줄 글자(<see cref="RunObjectView"/>)도 같은 층을 쓴다.</summary>
        public const int SortBase = 100;

        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _number;
        [SerializeField] GameObject _gauge;
        [SerializeField] Transform _pivot;
        Vector3 _pivotScale = Vector3.one;
        bool _bound;
        string _titleText, _numberText;
        float _fill = -2f;

        public TMP_Text Title => _title;
        public TMP_Text Number => _number;
        public GameObject GaugeRoot => _gauge;
        public Transform Pivot => _pivot;

        /// <summary><paramref name="root"/> 아래 글자판 프리팹의 <see cref="CountLabel"/>. 글자판을 품지 않은 오브젝트면 null.</summary>
        public static CountLabel Find(Transform root)
        {
            var label = root.GetComponentInChildren<CountLabel>(true);
            if (label != null) label.Bind();
            return label;
        }

        void Bind()
        {
            if (_bound) return;
            _bound = true;
            if (_title == null || _number == null || _gauge == null || _pivot == null)
                Debug.LogError($"[CountLabel] {name}: 글자판 참조가 비었습니다(제목 {_title != null}·숫자 {_number != null}·막대 {_gauge != null}·피벗 {_pivot != null}). " +
                    $"{PrefabPath}의 CountLabel Inspector에서 연결하세요. 빈 칸은 그리지 않습니다.", this);
            if (_pivot != null) _pivotScale = _pivot.localScale;
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
    }
}
