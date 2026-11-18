using System.Collections.Generic;
using MV.Data;
using UnityEngine;
using UnityEngine.Pool;

namespace MV.View
{
    /// <summary>
    /// 무기문(cgate) 위 무기 카드 묶음(MV-UI3-HUD9, 사용자 결정 2026-11-19 「둘로 나눔」·「만든 장 수 그대로」·「임시 묶음 만들기」).
    /// 장 수 = 글자판 숫자와 같은 Made + Extra(상한 없음), 그림 = 지금 런 무기 단계(Bt)의 103_item.icon(Atlas_UI_Icon).
    /// 통과(<c>Done</c>)하면 숨긴다. 통과 뒤 스택까지 나는 카드는 HUD(<c>WeaponSection</c>)가 그린다.
    /// 임시 리소스: 카드 원본은 프리팹 자식 <see cref="_template"/>(꺼 둔 SpriteRenderer)이고 배치 값은 이 컴포넌트 직렬화 필드다.
    /// 디자이너 묶음 리소스가 오면 프리팹만 바꾼다(코드로 그림을 그리지 않는다).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CraftGateBundle : RunObjectModule
    {
        [Tooltip("카드 원본(꺼 둔 SpriteRenderer). 장 수만큼 이 원본을 복제해 풀에서 꺼낸다.")]
        [SerializeField] SpriteRenderer _template;
        [Tooltip("한 줄 장 수. 줄이 차면 위 줄로 쌓는다.")]
        [SerializeField] int _columns = 5;
        [Tooltip("카드 사이 간격(m): x = 옆, y = 위 줄")]
        [SerializeField] Vector2 _step = new Vector2(0.3f, 0.22f);
        [Tooltip("카드 높이(m). 스프라이트 크기와 상관없이 이 높이로 맞춘다.")]
        [SerializeField] float _cardHeight = 0.55f;

        readonly List<SpriteRenderer> _shown = new List<SpriteRenderer>(16);
        ObjectPool<SpriteRenderer> _pool;
        Sprite _sprite;
        bool _grew;

        /// <summary>지금 보이는 카드 수(시험·QA).</summary>
        public int Shown => _shown.Count;
        public SpriteRenderer Template => _template;

        void Init()
        {
            if (_pool != null) return;
            if (_template != null) _template.gameObject.SetActive(false);
            _pool = new ObjectPool<SpriteRenderer>(
                () =>
                {
                    var sr = Instantiate(_template, _template.transform.parent, false);
                    sr.name = _template.name;
                    _grew = true;
                    return sr;
                },
                sr => sr.gameObject.SetActive(true),
                sr => sr.gameObject.SetActive(false),
                sr => { if (sr != null) Destroy(sr.gameObject); });
        }

        public override void OnBind(RunObjectView view) => Show(view, 0, null);

        public override void OnUnbind(RunObjectView view) => Show(view, 0, null);

        public override void OnTick(RunObjectView view, in ObjectFrame f)
        {
            var o = view.Obj;
            int n = o == null || o.Done ? 0 : o.Made + o.Extra;
            Sprite sprite = null;
            if (n > 0 && view.Ctx?.Sprites != null && !string.IsNullOrEmpty(view.Ctx.GateWeaponIcon))
                sprite = view.Ctx.Sprites.Get(MvAtlas.UiIcon, view.Ctx.GateWeaponIcon);
            Show(view, n, sprite);
        }

        /// <summary>n장을 보인다(모자라면 풀에서 꺼내고 남으면 돌려준다). 그림이 없으면 카드 원본 그림 그대로.</summary>
        public void Show(RunObjectView view, int n, Sprite sprite)
        {
            if (_template == null) return;
            Init();
            if (n < 0) n = 0;
            while (_shown.Count > n)
            {
                int last = _shown.Count - 1;
                _pool.Release(_shown[last]);
                _shown.RemoveAt(last);
            }
            bool spriteChanged = sprite != null && sprite != _sprite;
            if (sprite != null) _sprite = sprite;
            while (_shown.Count < n)
            {
                var sr = _pool.Get();
                Place(sr, _shown.Count);
                if (_sprite != null) Fit(sr, _sprite);
                _shown.Add(sr);
            }
            if (spriteChanged) foreach (var sr in _shown) Fit(sr, _sprite);
            if (_grew && view != null && view.Ds != null)
            {
                _grew = false;
                view.Ds.Refresh();   // 새로 만든 카드를 깊이 정렬·안개 알파 대상에 넣는다
            }
        }

        /// <summary>i번째 카드 자리: 아래 줄부터 가운데 정렬로 채우고 위로 쌓는다. 뒤 카드가 앞 카드 위에 그려진다.</summary>
        void Place(SpriteRenderer sr, int i)
        {
            int cols = Mathf.Max(1, _columns);
            int row = i / cols, col = i % cols;
            float x = (col - (cols - 1) * 0.5f) * _step.x;
            var t = sr.transform;
            var p = _template.transform.localPosition;
            t.localPosition = new Vector3(p.x + x, p.y + row * _step.y, p.z - i * 0.001f);
            sr.sortingOrder = _template.sortingOrder + i;
        }

        void Fit(SpriteRenderer sr, Sprite sprite)
        {
            sr.sprite = sprite;
            float h = sprite.bounds.size.y;
            float s = h > 0 ? _cardHeight / h : 1f;
            sr.transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
