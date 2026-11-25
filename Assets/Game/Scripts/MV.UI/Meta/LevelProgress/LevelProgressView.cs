using System;
using System.Collections;
using System.Collections.Generic;
using jjevol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>LevelProgress 팝업 한 번 열기에 넘기는 데이터.</summary>
    public sealed class LevelProgressViewData
    {
        public LevelProgressModel Model;
        /// <summary>막대·인디케이터·보유 영혼 글의 시작 값.</summary>
        public long FromSouls;
        /// <summary>끝 값. <see cref="FromSouls"/>와 다르면 오름 연출을 한다.</summary>
        public long ToSouls;
        /// <summary>런 끝 자동 연출: 닫기 버튼을 숨기고, 연출이 끝나면 스스로 닫기를 요청한다(노션 Close 설명).</summary>
        public bool AutoClose;
    }

    /// <summary>
    /// 디자이너 프리팹 <c>Popup_LevelProgress</c>의 루트 부품(MV-UI3-LVPROG, 노션 「[팝업] LevelProgress」).
    /// 맡는 일: Area 칸(<see cref="LevelProgressArea"/>)·배경 칸(<see cref="LevelProgressAreaBg"/>) 수를 표 행 수에 맞추고 데이터 넘기기,
    /// 진척 막대(<c>ProgressBar</c>)·인디케이터 위치, 보유 영혼 글, 오름 연출, 스크롤 위치.
    /// 막대 눈금은 실제 배치된 각 관문 <c>Milestone</c> 위치를 읽어 구간마다 선형으로 채운다(관문 간격은 디자이너 배치가 정한다).
    /// 디자이너가 Content에 둔 예시 칸은 첫 데이터로 덮어 쓰고, 모자라면 같은 프리팹 원본으로 더 만든다(표 행 수는 고정이라 풀 없이 한 번만 만든다).
    /// </summary>
    public sealed class LevelProgressView : UIBase
    {
        const string PrefabPath = "Assets/Game/Art/UI/Prefab/UIPopup/Popup_LevelProgress.prefab";

        [Header("배치")]
        [SerializeField] ScrollRect scrollRect;
        /// <summary><c>Content</c>(세로 정렬 역순: 형제 순서 앞 = 아래). Area 칸의 부모.</summary>
        [SerializeField] RectTransform content;
        /// <summary><c>Area Background Group</c>. 배경 칸의 부모.</summary>
        [SerializeField] RectTransform backgroundGroup;
        /// <summary>Area 칸 원본(<c>Element_LevelProgress</c> 프리팹 에셋).</summary>
        [SerializeField] LevelProgressArea areaPrefab;
        /// <summary>배경 칸 원본(<c>Element_Progress_AreaBG</c> 프리팹 에셋).</summary>
        [SerializeField] LevelProgressAreaBg backgroundPrefab;

        [Header("진척")]
        [SerializeField] Slider progressBar;
        /// <summary><c>Fill/Holder_Indicator</c>. 인디케이터가 따라가는 점.</summary>
        [SerializeField] RectTransform indicatorHolder;
        /// <summary><c>Indicator</c>(최상단 층, 레이아웃 무시).</summary>
        [SerializeField] RectTransform indicator;
        /// <summary><c>Indicator/Tip_Level Count/Value Text (TMP)</c>: 보유 영혼 수.</summary>
        [SerializeField] TMP_Text soulsText;
        /// <summary>영혼 수 옆 재화 아이콘(<c>Icon (2)</c>, AddressableImage, 프리팹에 그림 없음). 그림은 모델의 영혼 아이콘.</summary>
        [SerializeField] AddressableImage soulsIcon;

        /// <summary><c>Close</c> 묶음. 자동 연출 동안 끈다.</summary>
        [Header("닫기")]
        [SerializeField] GameObject closeRoot;
        [SerializeField] Button closeButton;

        [Header("연출(임시값, 영상 기준 확정 전 — 사용자 2026-11-19 「임시값으로 먼저」)")]
        [SerializeField, Min(0f)] float riseDelay = 0.3f;
        [SerializeField, Min(0f)] float riseDuration = 1.0f;
        [SerializeField] AnimationCurve riseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        /// <summary>자동 연출에서 오름이 끝난 뒤 닫기까지.</summary>
        [SerializeField, Min(0f)] float autoCloseHold = 0.8f;

        /// <summary>닫기 버튼 또는 자동 연출 끝.</summary>
        public event Action CloseRequested;

        readonly List<LevelProgressArea> _areas = new List<LevelProgressArea>();
        readonly List<LevelProgressAreaBg> _backgrounds = new List<LevelProgressAreaBg>();
        readonly List<float> _stepValues = new List<float>();
        readonly FloatTween _riseProgress = new FloatTween();
        LevelProgressViewData _data;
        Coroutine _rise;
        bool _samplesTaken;
        long _shownSouls;

        public IReadOnlyList<LevelProgressArea> AreaViews => _areas;
        public IReadOnlyList<LevelProgressAreaBg> BackgroundViews => _backgrounds;
        public long ShownSouls => _shownSouls;
        public bool Rising => _rise != null;
        public float BarValue => progressBar ? progressBar.value : 0f;
        public bool CloseVisible => closeRoot && closeRoot.activeSelf;

        public bool HasRefs => scrollRect && content && backgroundGroup && areaPrefab && backgroundPrefab && progressBar
            && indicatorHolder && indicator && soulsText && closeRoot && closeButton;

        void Awake()
        {
            if (closeButton) closeButton.onClick.AddListener(OnClosePressed);
            if (!HasRefs) Debug.LogWarning($"[LevelProgressView] 참조가 빠졌습니다. {PrefabPath}의 LevelProgressView 필드를 채우세요.");
        }

        public void ShowUI(LevelProgressViewData data)
        {
            _data = data;
            ShowUI();
        }

        public override void HideUI()
        {
            StopRise();
            base.HideUI();
        }

        protected override void _DrawContents()
        {
            StopRise();
            if (_data?.Model == null || !HasRefs) return;
            var model = _data.Model;
            EnsureCount(model.Areas.Count);
            for (int i = 0; i < _areas.Count; i++)
            {
                bool on = i < model.Areas.Count;
                if (!on) { _areas[i].HideUI(); _backgrounds[i].HideUI(); continue; }
                var info = model.Areas[i];
                _areas[i].ShowUI(info);
                _backgrounds[i].ShowUI(new LevelProgressAreaBgData(ParseColor(info), info.ZoneStart,
                    new LevelProgressZoneData(info.ZoneNo, info.ZoneName, info.ZoneIcon)));
            }
            if (indicator) indicator.SetAsLastSibling();
            closeRoot.SetActive(!_data.AutoClose);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            BuildStepValues(model);
            SetSouls(_data.FromSouls, false);

            if (_data.ToSouls != _data.FromSouls || _data.AutoClose)
                _rise = StartCoroutine(Rise(_data.FromSouls, _data.ToSouls, _data.AutoClose));
        }

        /// <summary>오름 연출을 건너뛰고 끝 값으로 둔다(시험·닫기 직전).</summary>
        public void Complete()
        {
            if (_data == null) return;
            StopRise();
            SetSouls(_data.ToSouls, false);
        }

        IEnumerator Rise(long from, long to, bool autoClose)
        {
            if (riseDelay > 0f) yield return new WaitForSecondsRealtime(riseDelay);
            _riseProgress.Play(0f, 1f, from == to ? 0f : riseDuration, Ease.Linear);
            while (_riseProgress.IsPlaying)
            {
                _riseProgress.Tick(Time.unscaledDeltaTime);
                float progress01 = _riseProgress.Value;
                float k = riseCurve != null ? riseCurve.Evaluate(progress01) : progress01;
                SetSouls(from + (long)Math.Round((to - from) * (double)k), true);
                yield return null;
            }
            SetSouls(to, true);
            if (autoClose)
            {
                if (autoCloseHold > 0f) yield return new WaitForSecondsRealtime(autoCloseHold);
                _rise = null;
                CloseRequested?.Invoke();
                yield break;
            }
            _rise = null;
        }

        void StopRise()
        {
            if (_rise == null) return;
            StopCoroutine(_rise);
            _rise = null;
        }

        void OnClosePressed() => CloseRequested?.Invoke();

        void SetSouls(long souls, bool animate)
        {
            _shownSouls = souls;
            for (int i = 0; i < _areas.Count; i++)
                if (_areas[i].gameObject.activeSelf) _areas[i].SetSouls(souls, animate);
            soulsText.text = MvUiFormat.Fmt(souls);
            HudImage.SetOrHide(soulsIcon, _data.Model.SoulsAtlas, _data.Model.SoulsIcon);
            progressBar.SetValueWithoutNotify(ValueAt(_data.Model.StepPosition(souls)));
            indicator.position = indicatorHolder.position;
            FocusIndicator();
        }

        /// <summary>막대 값: 0 = 막대 아래, k번째 값 = k번째 관문 Milestone 높이.</summary>
        void BuildStepValues(LevelProgressModel model)
        {
            _stepValues.Clear();
            _stepValues.Add(0f);
            var area = progressBar.fillRect ? progressBar.fillRect.parent as RectTransform : null;
            if (!area) area = progressBar.transform as RectTransform;
            var r = area.rect;
            for (int i = 0; i < model.Areas.Count && i < _areas.Count; i++)
            {
                var view = _areas[i];
                int n = Mathf.Min(model.Areas[i].Milestones.Length, view.ItemCount);
                for (int k = 0; k < n; k++)
                {
                    var anchor = view.Item(k).MilestoneAnchor;
                    float y = area.InverseTransformPoint(anchor.position).y;
                    _stepValues.Add(r.height <= 0f ? 0f : Mathf.Clamp01((y - r.yMin) / r.height));
                }
            }
        }

        float ValueAt(float step)
        {
            if (_stepValues.Count == 0) return 0f;
            int i = Mathf.FloorToInt(step);
            if (i >= _stepValues.Count - 1) return _stepValues[_stepValues.Count - 1];
            if (i < 0) return _stepValues[0];
            return Mathf.Lerp(_stepValues[i], _stepValues[i + 1], step - i);
        }

        /// <summary>인디케이터가 화면 세로 가운데에 오게 스크롤한다(끝에서는 멈춘다).</summary>
        void FocusIndicator()
        {
            var viewport = scrollRect.viewport ? scrollRect.viewport : scrollRect.transform as RectTransform;
            float contentH = content.rect.height, viewH = viewport.rect.height;
            if (contentH <= viewH) return;
            float y = content.InverseTransformPoint(indicator.position).y - content.rect.yMin;
            scrollRect.verticalNormalizedPosition = Mathf.Clamp01((y - viewH * 0.5f) / (contentH - viewH));
        }

        /// <summary>Area 칸·배경 칸 수를 맞춘다. 처음에는 디자이너 예시 칸을 순서대로 가져다 쓴다.</summary>
        void EnsureCount(int count)
        {
            if (!_samplesTaken)
            {
                _samplesTaken = true;
                for (int i = 0; i < content.childCount; i++)
                    if (content.GetChild(i).TryGetComponent(out LevelProgressArea a)) _areas.Add(a);
                for (int i = 0; i < backgroundGroup.childCount; i++)
                    if (backgroundGroup.GetChild(i).TryGetComponent(out LevelProgressAreaBg b)) _backgrounds.Add(b);
                // 예시 칸 수가 둘이 다르면 짧은 쪽에 맞춰 남는 예시는 끈다.
                while (_areas.Count > _backgrounds.Count) { _areas[^1].gameObject.SetActive(false); _areas.RemoveAt(_areas.Count - 1); }
                while (_backgrounds.Count > _areas.Count) { _backgrounds[^1].gameObject.SetActive(false); _backgrounds.RemoveAt(_backgrounds.Count - 1); }
            }
            while (_areas.Count < count)
            {
                var a = Instantiate(areaPrefab, content, false);
                a.name = areaPrefab.name + " (" + _areas.Count + ")";
                if (_areas.Count > 0) a.transform.SetSiblingIndex(_areas[^1].transform.GetSiblingIndex() + 1);
                _areas.Add(a);
                var b = Instantiate(backgroundPrefab, backgroundGroup, false);
                b.name = backgroundPrefab.name + " (" + _backgrounds.Count + ")";
                if (_backgrounds.Count > 0) b.transform.SetSiblingIndex(_backgrounds[^1].transform.GetSiblingIndex() + 1);
                _backgrounds.Add(b);
            }
        }

        static Color? ParseColor(LevelProgressAreaInfo info)
        {
            if (string.IsNullOrEmpty(info.Color)) return null;
            if (ColorUtility.TryParseHtmlString(info.Color, out var c)) return c;
            Debug.LogWarning($"[LevelProgressView] Area {info.AreaNo}의 302_area.mapColor 「{info.Color}」를 색으로 읽지 못했습니다. 디자이너 색을 그대로 둡니다.");
            return null;
        }

        protected override void OnDestroy()
        {
            if (closeButton) closeButton.onClick.RemoveListener(OnClosePressed);
            base.OnDestroy();
        }
    }
}
