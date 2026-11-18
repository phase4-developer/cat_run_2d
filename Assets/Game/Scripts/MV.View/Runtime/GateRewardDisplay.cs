using System;
using System.Collections.Generic;
using MV.Run;
using UnityEngine;
using UnityEngine.Pool;

namespace MV.View
{
    /// <summary>문 보상 묶음 종류. HUD가 받을 곳을 고른다(무기 = 오른쪽 무기 스택). 새 문 연출은 여기 종류를 더한다(예: 계단문 = 왼쪽 계단).</summary>
    public enum GateRewardKind { Weapon }

    /// <summary>통과 때 HUD로 넘기는 카드 한 장의 화면 자리(px, 왼아래 원점, 가운데·크기).</summary>
    public readonly struct GateCardRelease
    {
        public readonly Vector2 Screen, Size;

        public GateCardRelease(Vector2 screen, Vector2 size)
        {
            Screen = screen;
            Size = size;
        }
    }

    /// <summary>
    /// 문 위 보상 카드 묶음의 부모(MV-UI3-HUD9, 사용자 2026-11-19 「부모 클래스 만들어서 추후 다른 타입 … 추가 대비」).
    /// 장 수만큼 카드 원본(<see cref="GateRewardCard"/>)을 풀에서 꺼내 두 기둥 사이(왼쪽 끝 = <see cref="_leftX"/>, 오른쪽 끝 = <see cref="_rightX"/>)에
    /// 한 줄 <see cref="_columns"/>장씩 놓고, 넘치면 위로 줄을 늘린다. 통과(<c>Done</c>)하는 순간 카드마다 화면 자리를 <see cref="Released"/>로 넘기고 숨긴다
    /// (HUD가 같은 자리·같은 크기 UI 카드로 바꿔 날린다, 알파 변화 없음).
    /// 자식 클래스는 종류·장 수·그림만 정한다. 배치·크기 값은 프리팹 직렬화 필드(연출 값)다.
    /// </summary>
    public abstract class GateRewardDisplay : RunObjectModule
    {
        [Tooltip("카드 원본(꺼 둔 카드). 장 수만큼 복제해 풀에서 꺼낸다.")]
        [SerializeField] GateRewardCard _template;
        [Tooltip("한 줄 장 수. 넘치면 위 줄로 쌓는다.")]
        [SerializeField] int _columns = 4;
        [Tooltip("줄의 왼쪽 끝 카드 가운데 x(m, 묶음 기준) = 왼쪽 기둥.")]
        [SerializeField] float _leftX = -1.25f;
        [Tooltip("줄의 오른쪽 끝 카드 가운데 x(m, 묶음 기준) = 오른쪽 기둥.")]
        [SerializeField] float _rightX = 1.25f;
        [Tooltip("위 줄 간격(m).")]
        [SerializeField] float _rowStep = 0.5f;
        [Tooltip("카드 크기(m). HUD 스택 칸(170×150)과 같은 비율.")]
        [SerializeField] Vector2 _cardSize = new Vector2(0.85f, 0.75f);

        /// <summary>통과 때 카드 화면 자리(종류, 카드들). Boot 다리가 HUD로 넘긴다. 받는 쪽은 목록을 그 자리에서 읽고 보관하지 않는다.</summary>
        public static event Action<GateRewardKind, IReadOnlyList<GateCardRelease>> Released;

        readonly List<GateRewardCard> _shown = new List<GateRewardCard>(16);
        static readonly List<GateCardRelease> s_release = new List<GateCardRelease>(16);
        ObjectPool<GateRewardCard> _pool;
        Sprite _frame, _icon;
        Camera _cam;
        Transform _camT;
        bool _grew;

        /// <summary>지금 보이는 카드 수(시험·QA).</summary>
        public int Shown => _shown.Count;
        public GateRewardCard Template => _template;
        public IReadOnlyList<GateRewardCard> Cards => _shown;
        public Vector2 CardSize => _cardSize;

        public abstract GateRewardKind Kind { get; }

        /// <summary>문 위에 보일 장 수(통과 전).</summary>
        protected abstract int Count(RunObject o);

        /// <summary>카드 틀·그림. 없으면 null(원본 그림 그대로).</summary>
        protected abstract void Look(RunObjectView view, out Sprite frame, out Sprite icon);

        void Init()
        {
            if (_pool != null) return;
            if (_template != null) _template.gameObject.SetActive(false);
            _pool = new ObjectPool<GateRewardCard>(
                () =>
                {
                    var c = Instantiate(_template, _template.transform.parent, false);
                    c.name = _template.name;
                    _grew = true;
                    return c;
                },
                c => c.gameObject.SetActive(true),
                c => c.gameObject.SetActive(false),
                c => { if (c != null) Destroy(c.gameObject); });
        }

        public override void OnBind(RunObjectView view) => Show(view, 0, null, null);

        public override void OnUnbind(RunObjectView view) => Show(view, 0, null, null);

        public override void OnTick(RunObjectView view, in ObjectFrame f)
        {
            var o = view.Obj;
            if (o == null) { Show(view, 0, null, null); return; }
            if (o.Done)
            {
                if (_shown.Count > 0) Release(view);
                return;
            }
            int n = Math.Max(0, Count(o));
            Sprite frame = null, icon = null;
            if (n > 0) Look(view, out frame, out icon);
            Show(view, n, frame, icon);
        }

        /// <summary>통과: 보이는 카드마다 화면 자리를 넘기고 모두 숨긴다(같은 프레임에 HUD 카드가 그 자리에 선다).</summary>
        void Release(RunObjectView view)
        {
            s_release.Clear();
            var cam = Cam(view);
            foreach (var c in _shown) s_release.Add(ScreenOf(c, cam));
            Show(view, 0, null, null);
            Released?.Invoke(Kind, s_release);
            s_release.Clear();
        }

        Camera Cam(RunObjectView view)
        {
            var t = view != null && view.Ctx != null ? view.Ctx.Cam : null;
            if (t != _camT) { _camT = t; _cam = t != null ? t.GetComponent<Camera>() : null; }
            return _cam;
        }

        /// <summary>카드 가운데·크기를 화면 px로(카메라가 없으면 0).</summary>
        GateCardRelease ScreenOf(GateRewardCard c, Camera cam)
        {
            if (cam == null) return default;
            var t = c.transform;
            var lossy = t.lossyScale;
            float h = _cardSize.y * Mathf.Abs(lossy.y), w = _cardSize.x * Mathf.Abs(lossy.x);
            Vector3 center = t.position;
            Vector2 sc = cam.WorldToScreenPoint(center);
            Vector2 st = cam.WorldToScreenPoint(center + cam.transform.up * (h * 0.5f));
            float sh = Vector2.Distance(sc, st) * 2f;
            return new GateCardRelease(sc, new Vector2(h > 0 ? sh * w / h : 0f, sh));
        }

        /// <summary>n장을 보인다(모자라면 풀에서 꺼내고 남으면 돌려준다). 장 수가 바뀌면 모두 다시 놓는다.</summary>
        public void Show(RunObjectView view, int n, Sprite frame, Sprite icon)
        {
            if (_template == null) return;
            Init();
            if (n < 0) n = 0;
            bool relayout = n != _shown.Count;
            while (_shown.Count > n)
            {
                int last = _shown.Count - 1;
                _pool.Release(_shown[last]);
                _shown.RemoveAt(last);
            }
            bool repaint = relayout || (frame != null && frame != _frame) || (icon != null && icon != _icon);
            if (frame != null) _frame = frame;
            if (icon != null) _icon = icon;
            while (_shown.Count < n) _shown.Add(_pool.Get());
            if (relayout || repaint)
                for (int i = 0; i < _shown.Count; i++) Place(_shown[i], i, n);
            if (_grew && view != null && view.Ds != null)
            {
                _grew = false;
                view.Ds.Refresh();   // 새로 만든 카드를 깊이 정렬·안개 알파 대상에 넣는다
            }
        }

        /// <summary>
        /// i번째 카드 자리: 아래 줄부터 한 줄 <see cref="_columns"/>장, 줄마다 왼쪽 끝 = 왼쪽 기둥·오른쪽 끝 = 오른쪽 기둥으로 고르게(한 장이면 가운데).
        /// 아래 줄 카드가 위 줄 카드 앞에 그려진다.
        /// </summary>
        void Place(GateRewardCard c, int i, int n)
        {
            int cols = Mathf.Max(1, _columns);
            int rows = (n + cols - 1) / cols;
            int row = i / cols, col = i % cols;
            int inRow = row < rows - 1 ? cols : n - row * cols;
            float x = inRow <= 1 ? (_leftX + _rightX) * 0.5f : Mathf.Lerp(_leftX, _rightX, col / (float)(inRow - 1));
            var p = _template.transform.localPosition;
            c.transform.localPosition = new Vector3(p.x + x, p.y + row * _rowStep, p.z + row * 0.001f - col * 0.0001f);
            int order = BaseOrder() + 2 * ((rows - 1 - row) * cols + col);
            c.Paint(_frame, _icon, _cardSize, order);
        }

        int BaseOrder() => _template.Frame != null ? _template.Frame.sortingOrder : 60;
    }
}
