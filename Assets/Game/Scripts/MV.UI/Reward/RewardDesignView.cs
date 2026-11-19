using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 디자이너 <c>Popup_Reward</c>(Art/UI/Prefab/UIPopup)를 공용 보상 팝업 겉으로 쓰는 연결(MV-UI3-REWARD, 노션 [팝업] 보상획득 팝업).
    /// <c>UIPopup_Reward</c> 래퍼 루트에 붙고, 참조는 래퍼 안 중첩 인스턴스를 직렬화로 가리킨다(결과 팝업과 같은 구조). 디자이너 원본은 바꾸지 않는다.
    /// <list type="bullet">
    /// <item>Backboard(래퍼가 디자이너 팝업 뒤에 둔 전체 막) 터치 = 닫기(노션). 디자이너 장식은 터치를 막지 않게 raycast를 끈다.</item>
    /// <item>보상 칸: <c>Reward Group/Holder_Reward</c>를 원형으로 보상 수만큼 풀에서 빌린다(노션 「Holder_Reward 단위로 보상 수가 생성」, 칸 수는 코드에 없음).</item>
    /// <item>순차 등장: 칸이 <see cref="revealInterval"/> 간격으로 하나씩 나온다(노션 「하나씩 딜레이 … 0.2s 내외」). 자리(Holder)는 처음부터 잡아 두고
    /// 안의 <c>Slot_Item</c>만 켜서 가로 배치가 흔들리지 않게 한다. Slot_Item 트윈은 노션 「추후」라 아직 없다.</item>
    /// <item>닫기 잠금: 등장이 끝날 때까지(최대 <see cref="maxCloseLock"/>) 닫기를 막는다(정크버스터즈 RewardPopup, 사용자 2026-11-19 결정 카드 「정크버스터즈처럼 막기」).</item>
    /// </list>
    /// 시간은 실제 시간(<see cref="Time.unscaledDeltaTime"/>)으로 센다. 시험은 <see cref="Advance"/>로 시간을 넣는다.
    /// </summary>
    public sealed class RewardDesignView : MonoBehaviour
    {
        /// <summary>제목 「Reward!」 언어팩 키. 키가 없으면 시안 글자를 그대로 둔다.</summary>
        public const string TitleKey = "ui.reward.title";

        [Tooltip("래퍼가 디자이너 팝업 뒤에 둔 전체 막(터치 = 닫기)")]
        [SerializeField] Button backboard;
        [Tooltip("중첩 디자이너 Popup_Reward 루트(장식 raycast 끄기 범위)")]
        [SerializeField] RectTransform designerRoot;
        [Tooltip("Panel/Title/Text (TMP) 「Reward!」")]
        [SerializeField] TMP_Text title;
        [Tooltip("Reward Group 안 첫 Holder_Reward(원형이자 첫 칸, 자식 Slot_Item에 ItemSlot)")]
        [SerializeField] RectTransform holderTemplate;

        [Header("연출(노션 값, Slot_Item 트윈은 노션 「추후」)")]
        [Tooltip("칸이 하나씩 나오는 간격(초, 실제 시간). 노션 「0.2s 내외」")]
        [SerializeField] float revealInterval = 0.2f;
        [Tooltip("등장 중 닫기를 막는 최대 시간(초, 실제 시간). 정크버스터즈 maxBackEnableDelay 1.4 — 임시 값")]
        [SerializeField] float maxCloseLock = 1.4f;

        public event Action ClosePressed;

        struct Cell
        {
            public RectTransform Holder;
            public ItemSlot Slot;
            public ItemSlotData Data;
        }

        readonly List<Cell> _cells = new List<Cell>();
        ObjectPool<RectTransform> _pool;
        bool _templateTaken;
        bool _ready;
        int _revealed;
        float _elapsed;

        public Button Backboard => backboard;
        public string TitleText => title ? title.text : null;
        public float RevealInterval => revealInterval;
        public float MaxCloseLock => maxCloseLock;
        public int Count => _cells.Count;
        public int Revealed => _revealed;
        public bool Revealing => _revealed < _cells.Count;
        /// <summary>등장이 끝났거나 잠금 상한이 지났으면 닫을 수 있다.</summary>
        public bool CanClose => !Revealing || _elapsed >= maxCloseLock;

        /// <summary>지금 보이는 칸(켜진 Slot_Item) 순서대로.</summary>
        public IEnumerable<ItemSlot> ShownSlots
        {
            get
            {
                for (int i = 0; i < _revealed && i < _cells.Count; i++) yield return _cells[i].Slot;
            }
        }

        /// <summary>자리를 잡은 칸(Holder) 순서대로.</summary>
        public IEnumerable<RectTransform> Holders
        {
            get { foreach (var c in _cells) yield return c.Holder; }
        }

        /// <summary>프리팹 연결 확인(시험·검사용): 빠진 참조 이름 목록.</summary>
        public string MissingRefs()
        {
            var miss = new List<string>();
            if (!backboard) miss.Add(nameof(backboard));
            if (!designerRoot) miss.Add(nameof(designerRoot));
            if (!title) miss.Add(nameof(title));
            if (!holderTemplate) miss.Add(nameof(holderTemplate));
            else
            {
                var slot = holderTemplate.GetComponentInChildren<ItemSlot>(true);
                if (!slot) miss.Add("holderTemplate(ItemSlot)");
                else
                {
                    string e = slot.MissingRefs();
                    if (e.Length > 0) miss.Add("holderTemplate(" + e + ")");
                }
            }
            return string.Join(", ", miss);
        }

        void Awake() => Init();

        void Init()
        {
            if (_ready) return;
            _ready = true;
            if (backboard) backboard.onClick.AddListener(() => ClosePressed?.Invoke());
            BlockDecorRaycasts();
            _pool = new ObjectPool<RectTransform>(CreateHolder, h => h.gameObject.SetActive(true), h => h.gameObject.SetActive(false),
                h => { if (h && h != holderTemplate) Destroy(h.gameObject); });
            if (holderTemplate) holderTemplate.gameObject.SetActive(false);
        }

        /// <summary>디자이너 장식(그림·글자)이 Backboard 터치를 가로채지 않게 한다(패널 위를 눌러도 닫힘).</summary>
        void BlockDecorRaycasts()
        {
            if (!designerRoot) return;
            foreach (var g in designerRoot.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        }

        /// <summary>첫 칸은 디자이너 원본을 그대로 쓰고, 그 뒤는 복제한다.</summary>
        RectTransform CreateHolder()
        {
            if (!_templateTaken && holderTemplate) { _templateTaken = true; return holderTemplate; }
            return Instantiate(holderTemplate, holderTemplate.parent, false);
        }

        /// <summary>칸을 다시 채우고 첫 칸부터 순차 등장을 시작한다.</summary>
        public void Show(IReadOnlyList<ItemSlotData> items)
        {
            Init();
            if (title) title.text = MvUiText.Get("ui", TitleKey, title.text);

            int n = 0;
            if (items != null && holderTemplate)
                foreach (var it in items)
                {
                    Cell c;
                    if (n < _cells.Count) c = _cells[n];
                    else
                    {
                        var h = _pool.Get();
                        c = new Cell { Holder = h, Slot = h.GetComponentInChildren<ItemSlot>(true) };
                        if (!c.Slot) Debug.LogWarning("[RewardPopup] Holder_Reward 안에 ItemSlot(Slot_Item)이 없습니다: " + h.name);
                    }
                    c.Holder.SetSiblingIndex(n);
                    c.Data = it;
                    if (c.Slot) c.Slot.gameObject.SetActive(false); // 자리만 잡고 숨김
                    if (n < _cells.Count) _cells[n] = c; else _cells.Add(c);
                    n++;
                }
            for (int i = _cells.Count - 1; i >= n; i--)
            {
                _pool.Release(_cells[i].Holder);
                _cells.RemoveAt(i);
            }

            _revealed = 0;
            _elapsed = 0f;
            Advance(0f); // 첫 칸은 바로
        }

        /// <summary>시간을 흘린다(실제 시간 초). i번째 칸은 i × 간격에 나온다.</summary>
        public void Advance(float dt)
        {
            if (dt > 0f) _elapsed += dt;
            while (_revealed < _cells.Count && (revealInterval <= 0f || _elapsed + 1e-5f >= _revealed * revealInterval))
                RevealNext();
        }

        /// <summary>남은 칸을 모두 바로 보인다.</summary>
        public void RevealAll()
        {
            while (_revealed < _cells.Count) RevealNext();
        }

        void RevealNext()
        {
            var c = _cells[_revealed++];
            if (c.Slot) c.Slot.ShowUI(c.Data);
        }

        void Update()
        {
            if (Revealing) Advance(Time.unscaledDeltaTime);
        }
    }
}
