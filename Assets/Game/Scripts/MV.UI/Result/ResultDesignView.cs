using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 디자이너 <c>Popup_LevelComplete</c>(Art/UI/Prefab/UIPopup)을 결과 팝업 겉으로 쓰는 연결(MV-UI3-RESULT, 노션 [팝업] 인게임 결과).
    /// <c>UIPopup_Result</c> 래퍼 프리팹 루트에 붙고, 참조는 래퍼 안 중첩 인스턴스를 직렬화로 가리킨다(로비·Area 업그레이드와 같은 구조). 디자이너 원본 팝업은 바꾸지 않는다.
    /// <list type="bullet">
    /// <item>Backboard(래퍼가 디자이너 팝업 뒤에 둔 전체 막) 터치 = ×1 받기(노션, 사용자 2026-11-19 「노션 따름」). 디자이너 장식은 터치를 막지 않게 raycast를 끈다(광고 버튼만 남김).</item>
    /// <item>Progress: 게이지가 정산 전 값 → 정산 후 값으로 보간해 오른다(노션 「Progress.Fill이 변화가 있을 시 이전 → 변화 값으로 보간이동」). 시간·곡선은 이 컴포넌트 직렬화 필드(임시 값, 원장 「임시」).</item>
    /// <item>Reward: 디자이너가 넣어 둔 첫 <c>Slot_Item</c>을 원형으로 보상 수만큼 풀에서 빌린다(칸 수는 코드에 없음).</item>
    /// <item>시안에 없는 기획서 요소(진행 글 「Area N 개방까지」, 통계 4줄)는 그리지 않는다(로직은 화면 코드에 남김).</item>
    /// </list>
    /// </summary>
    public sealed class ResultDesignView : MonoBehaviour, IResultView, IResultAdView
    {
        public const string AdRewardKey = "ui.result.adReward";
        public const string AdRewardFallback = "보상";
        public const string NextKey = "ui.result.next";
        public const string NextFallback = "괜찮아요";
        public const string ProgressionKey = "ui.result.progression";
        public const string RewardKey = "ui.result.reward";

        [Tooltip("래퍼가 디자이너 팝업 뒤에 둔 전체 막(터치 = ×1 받기)")]
        [SerializeField] Button backboard;
        [Tooltip("중첩 디자이너 Popup_LevelComplete 루트(장식 raycast 끄기 범위)")]
        [SerializeField] RectTransform designerRoot;
        [Tooltip("Obj_Complete/Label Text (TMP)")]
        [SerializeField] TMP_Text title;

        [Header("Progress")]
        [SerializeField] Slider progress;
        [Tooltip("Tip_Progress/Count Text (TMP)")]
        [SerializeField] TMP_Text progressCount;
        [SerializeField] HudAreaLabel areaLabel;

        [Header("Reward")]
        [Tooltip("Reward Group 안 첫 Slot_Item(원형이자 첫 칸)")]
        [SerializeField] ItemSlot rewardTemplate;

        [Header("Button_RewardAds")]
        [SerializeField] Button adButton;
        [SerializeField] TMP_Text adText;
        [Tooltip("CanvasGroup/Text (TMP) 「No Thanks」 안내 글")]
        [SerializeField] TMP_Text nextText;

        [Header("고정 제목 글자(언어팩 키로 채움)")]
        [Tooltip("Progression 칸 제목 「Progression」")]
        [SerializeField] TMP_Text progressionLabel;
        [Tooltip("Reward 칸 제목 「Reward」")]
        [SerializeField] TMP_Text rewardLabel;

        [Header("연출(임시 값, 노션 연출 확정 뒤 조정)")]
        [Tooltip("열린 뒤 게이지가 오르기 시작할 때까지(초, 실제 시간)")]
        [SerializeField] float fillDelay = 0.3f;
        [Tooltip("게이지가 이전 값에서 새 값까지 오르는 시간(초, 실제 시간)")]
        [SerializeField] float fillDuration = 0.6f;
        [SerializeField] AnimationCurve fillCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("글자 모양(시안)")]
        [Tooltip("{0} = 진척도 % 정수")]
        [SerializeField] string percentFormat = "{0}%";
        [Tooltip("{0} = 광고 배수, {1} = 「보상」 문구(언어팩)")]
        [SerializeField] string adTextFormat = "<size=75>{0}x</size> <color=\"white\">{1}";

        public event Action CollectPressed;
        public event Action X3Pressed;

        readonly List<ItemSlot> _slots = new List<ItemSlot>();
        ObjectPool<ItemSlot> _pool;
        bool _templateTaken;
        bool _ready;
        ResultProgress _progress;
        bool _fillPending;
        Coroutine _fill;

        public IReadOnlyList<ItemSlot> Slots => _slots;
        public HudAreaLabel AreaLabel => areaLabel;
        public Button Backboard => backboard;
        public Button AdButton => adButton;
        public string TitleText => title ? title.text : null;
        public string ProgressText => progressCount ? progressCount.text : null;
        public string AdText => adText ? adText.text : null;
        public float ProgressValue => progress ? progress.value : -1f;
        public float FillDuration => fillDuration;

        /// <summary>프리팹 연결 확인(시험·검사용): 빠진 참조 이름 목록.</summary>
        public string MissingRefs()
        {
            var miss = new List<string>();
            if (!backboard) miss.Add(nameof(backboard));
            if (!designerRoot) miss.Add(nameof(designerRoot));
            if (!title) miss.Add(nameof(title));
            if (!progress) miss.Add(nameof(progress));
            if (!progressCount) miss.Add(nameof(progressCount));
            if (!areaLabel) miss.Add(nameof(areaLabel));
            if (!rewardTemplate) miss.Add(nameof(rewardTemplate));
            else
            {
                string e = rewardTemplate.MissingRefs();
                if (e.Length > 0) miss.Add("rewardTemplate(" + e + ")");
            }
            if (!adButton) miss.Add(nameof(adButton));
            if (!adText) miss.Add(nameof(adText));
            if (!nextText) miss.Add(nameof(nextText));
            return string.Join(", ", miss);
        }

        void Awake() => Init();

        void Init()
        {
            if (_ready) return;
            _ready = true;
            if (backboard) backboard.onClick.AddListener(() => CollectPressed?.Invoke());
            if (adButton) adButton.onClick.AddListener(() => X3Pressed?.Invoke());
            BlockDecorRaycasts();
            _pool = new ObjectPool<ItemSlot>(CreateSlot, s => s.gameObject.SetActive(true), s => s.gameObject.SetActive(false),
                s => { if (s && s != rewardTemplate) Destroy(s.gameObject); });
            if (rewardTemplate) rewardTemplate.gameObject.SetActive(false);
            if (progress) progress.interactable = false;
        }

        /// <summary>디자이너 장식(그림·글자)이 Backboard 터치를 가로채지 않게 한다. 광고 버튼 아래만 남긴다.</summary>
        void BlockDecorRaycasts()
        {
            if (!designerRoot) return;
            var keep = adButton ? adButton.transform : null;
            foreach (var g in designerRoot.GetComponentsInChildren<Graphic>(true))
                if (keep == null || !g.transform.IsChildOf(keep)) g.raycastTarget = false;
        }

        /// <summary>첫 칸은 디자이너 원본을 그대로 쓰고, 그 뒤는 복제한다.</summary>
        ItemSlot CreateSlot()
        {
            if (!_templateTaken && rewardTemplate) { _templateTaken = true; return rewardTemplate; }
            return Instantiate(rewardTemplate, rewardTemplate.transform.parent, false);
        }

        public void SetTitle(string text, Color color)
        {
            Init();
            // 색은 디자이너 글자 스타일을 그대로 쓴다(시안이 모양 정본). 글자만 바꾼다.
            if (title) title.text = text ?? "";
            if (nextText) nextText.text = MvUiText.Get("ui", NextKey, NextFallback);
            // 디자이너 고정 글자: 키가 없으면 시안 글자를 그대로 둔다.
            if (progressionLabel) progressionLabel.text = MvUiText.Get("ui", ProgressionKey, progressionLabel.text);
            if (rewardLabel) rewardLabel.text = MvUiText.Get("ui", RewardKey, rewardLabel.text);
        }

        public void SetProgress(in ResultProgress p)
        {
            Init();
            _progress = p;
            if (areaLabel) areaLabel.ShowUI(new AreaLabelData(p.ZoneNo, p.AreaName, p.AreaIcon));
            if (_fill != null) { StopCoroutine(_fill); _fill = null; }
            if (p.To01 > p.From01 && fillDuration > 0f)
            {
                ShowFill(p.From01, PercentAt(p.From01));
                _fillPending = true;
                if (isActiveAndEnabled) StartFill();
            }
            else
            {
                _fillPending = false;
                ShowFill(p.To01, p.Percent);
            }
        }

        void OnEnable()
        {
            if (_fillPending && _fill == null) StartFill();
        }

        void OnDisable()
        {
            if (_fill == null) return;
            StopCoroutine(_fill);
            _fill = null;
            if (_fillPending) ShowFill(_progress.From01, PercentAt(_progress.From01));
        }

        void StartFill() => _fill = StartCoroutine(Fill(_progress));

        IEnumerator Fill(ResultProgress p)
        {
            if (fillDelay > 0f) yield return new WaitForSecondsRealtime(fillDelay);
            float t = 0f;
            while (t < fillDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = fillCurve != null ? fillCurve.Evaluate(Mathf.Clamp01(t / fillDuration)) : Mathf.Clamp01(t / fillDuration);
                float v = Mathf.LerpUnclamped(p.From01, p.To01, k);
                ShowFill(v, Math.Min(p.Percent, PercentAt(v)));
                yield return null;
            }
            ShowFill(p.To01, p.Percent);
            _fillPending = false;
            _fill = null;
        }

        /// <summary>게이지 끝까지 바로 보낸다(시험·건너뛰기용).</summary>
        public void CompleteFill()
        {
            if (_fill != null) { StopCoroutine(_fill); _fill = null; }
            _fillPending = false;
            ShowFill(_progress.To01, _progress.Percent);
        }

        static int PercentAt(float v01) => Mathf.Clamp((int)(v01 * 100f + 1e-4f), 0, 100);

        void ShowFill(float v01, int percent)
        {
            if (progress) progress.SetValueWithoutNotify(Mathf.Clamp01(v01));
            if (progressCount) progressCount.text = string.Format(percentFormat, percent);
        }

        public void SetRewards(IReadOnlyList<ResultRewardItem> items)
        {
            Init();
            int n = 0;
            if (items != null && rewardTemplate)
                foreach (var it in items)
                {
                    ItemSlot s;
                    if (n < _slots.Count) s = _slots[n];
                    else { s = _pool.Get(); _slots.Add(s); }
                    s.transform.SetSiblingIndex(n);
                    s.ShowUI(new ItemSlotData(it.Tables, it.WealthType, it.Count));
                    n++;
                }
            for (int i = _slots.Count - 1; i >= n; i--)
            {
                _pool.Release(_slots[i]);
                _slots.RemoveAt(i);
            }
        }

        /// <summary>통계 줄: 시안에 자리가 없어 그리지 않는다(05 §5.6 로직은 화면 코드에 남김, AGENTS.md 「UI 기준」).</summary>
        public void SetStat(string text) { }

        public void SetCollectInteractable(bool interactable)
        {
            Init();
            if (backboard) backboard.interactable = interactable;
        }

        public void SetX3(int mul, bool interactable)
        {
            Init();
            if (adText) adText.text = string.Format(adTextFormat, mul, MvUiText.Get("ui", AdRewardKey, AdRewardFallback));
            if (adButton) adButton.interactable = interactable;
        }
    }
}
