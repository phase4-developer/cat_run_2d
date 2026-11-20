using System;
using System.Collections.Generic;
using jjevol;
using MV.Data;
using MV.Run;
using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// 인게임 무기 섹션의 런 연결(<c>IngameScreen/Mid/Weapon</c>, MV-UI3-HUD4, HUD_구조_설계안 3.3 WeaponSection·7.4).
    /// 스택·줄 서기·머지 연출은 같은 오브젝트의 공용 부품 <see cref="WeaponStackView"/>가 맡고(로비도 같은 부품, MV-UI3-LOBBY-WEAPONFX),
    /// 이 컴포넌트는 런만의 일을 한다: 런 손패(<see cref="RunState.W"/>)를 넘기고, 시뮬 <see cref="RunEventType.WeaponDeliver"/>를 받을 장으로,
    /// 무기문 카드(<see cref="MvHudFly.Launched"/>)를 줄 카드로, 비행 층 Hand 도착을 줄 없는 도착으로 넘긴다. 장착 칸 등급은 <see cref="ShownTier"/>로 게임 루프에 알린다.
    /// 시뮬 값은 읽기만 한다.
    /// </summary>
    public sealed class WeaponSection : UIBase, IHudFlyHandler
    {
        [Tooltip("스택 공용 부품(같은 오브젝트의 WeaponStackView).")]
        [SerializeField] WeaponStackView view;
        [Tooltip("비행 층에서 날아오는 카드를 둘 부모(보통 Weapon).")]
        [SerializeField] RectTransform flyParent;

        Func<RunState> _state;
        RunState _run;
        MvTables T => _run?.Tables;
        readonly Dictionary<int, HudWeaponSlot> _flies = new Dictionary<int, HudWeaponSlot>();   // 비행 id → 칸
        bool _ready;

        /// <summary>
        /// 지금 장착 칸(손패 맨 위)에 보이는 무기 등급(없으면 0). 캐릭터 무기 외형이 이 값을 따른다
        /// (사용자 2026-11-19 「캐릭터 무기가 바뀌는건 제일위에 무기가 바뀌는 순간이야 … 머지 연출때문에 먹었을때하고 실제 적용이 다르니까」). Boot가 매 프레임 RunState.ShownHandTier로 넣어 피해·투사체·외형이 같은 순간에 바뀐다.
        /// </summary>
        public static int ShownTier { get; private set; }

        // ---- 시험·QA ----
        public WeaponStackView View { get { Init(); return view; } }
        public int ActiveFlies => _flies.Count;

        void Awake() => Init();

        void Init()
        {
            if (_ready) return;
            _ready = true;
            if (!view)
            {
                Debug.LogWarning("[WeaponSection] view(같은 오브젝트의 WeaponStackView)가 비어 있습니다: Assets/Game/Art/UI/Prefab/UIScreen/IngameScreen.prefab Mid/Weapon", this);
                return;
            }
            view.Init();
            view.DmgMul = DmgMul;
            view.ExternalInFlight = () => MvHudFly.CountInFlight(HudFlyTarget.Hand);
            view.TopTierShown += OnTopTierShown;
        }

        static void OnTopTierShown(int tier) => ShownTier = tier;

        protected override bool SetSubscribe(bool subscribe)
        {
            if (!base.SetSubscribe(subscribe)) return false;
            if (subscribe)
            {
                MvHudEvents.OnEvent += OnRunEvent;
                MvHudFly.Arrived += OnFlyArrived;
                MvHudFly.Launched += OnLaunched;
                MvHudFly.Register(HudFlyTarget.Hand, this);
            }
            else
            {
                MvHudEvents.OnEvent -= OnRunEvent;
                MvHudFly.Arrived -= OnFlyArrived;
                MvHudFly.Launched -= OnLaunched;
                MvHudFly.Unregister(HudFlyTarget.Hand, this);
            }
            return true;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (view) view.TopTierShown -= OnTopTierShown;
            ShownTier = 0;
        }

        // ---------------- 런 연결(화면이 런을 넘긴다) ----------------

        public void BindRun(Func<RunState> state)
        {
            Init();
            _state = state;
            _run = null;
        }

        /// <summary>실시간 dt(배속과 무관, 05 §5.3.2).</summary>
        public void Tick(float realDt)
        {
            Init();
            if (!view) return;
            var s = _state?.Invoke();
            if (s != _run) { _run = s; ResetHand(s?.W); }
            if (s == null) return;
            // 비행 층 카드가 남아 있으면 손패를 시뮬 값으로 다시 맞추지 않는다(도착하면 들어간다).
            view.Step(realDt, _flies.Count > 0 ? null : s.W);
        }

        void ResetHand(int[] w)
        {
            foreach (var kv in _flies) view.ReleaseLoose(kv.Value);
            _flies.Clear();
            view.ResetHand(_run?.Tables, w);
        }

        double DmgMul() => _run?.Tables != null ? RunFormulas.CardMul(_run, CardEffectType.DmgMul) : 1;

        int FallbackTier => _run != null ? _run.Bt : 1;

        // ---------------- 이벤트 ----------------

        /// <summary>런 이벤트 1개(구독은 SetSubscribe, 시험은 직접 부른다).</summary>
        public void OnRunEvent(RunEvent e)
        {
            if (!view || !view.HasHand) return;
            switch (e.Type)
            {
                case RunEventType.WeaponHandReset:
                    if (e.Ids != null) ResetHand(e.Ids);
                    break;
                case RunEventType.WeaponDeliver:
                    view.Deliver(e.A, e.B);
                    break;
            }
        }

        /// <summary>비행 도착(구독은 SetSubscribe, 시험은 직접 부른다).</summary>
        public void OnFlyArrived(HudFlyTarget t, int amount)
        {
            if (t == HudFlyTarget.Hand && view) view.AddArrival();
        }

        /// <summary>
        /// 문 위 카드가 HUD 카드로 바뀐다(구독은 SetSubscribe, 시험은 직접 부른다). 공용 부품이 같은 화면 자리·같은 크기 스택 칸으로 세워 줄 자리로 날린다.
        /// 손패 지급은 시뮬 WeaponDeliver가 오면 공용 부품이 한다.
        /// </summary>
        public void OnLaunched(HudFlyTarget target, IReadOnlyList<Rect> cards)
        {
            Init();
            if (target != HudFlyTarget.Hand || !view) return;
            view.LaunchFromScreen(cards, view.NextTier(FallbackTier));
        }

        // ---------------- 날아오는 카드(IHudFlyHandler, 비행 층 Hand) ----------------

        public bool TryGetRect(out Vector2 center, out Vector2 size)
        {
            center = size = default;
            return view && view.TryGetNextSlotScreenRect(out center, out size);
        }

        public void Show(int id, int amount)
        {
            if (!flyParent || !view || !view.HasHand || _flies.ContainsKey(id)) return;
            var s = view.RentLoose(view.NextTier(FallbackTier));
            s.transform.SetParent(flyParent, false);
            s.transform.SetAsLastSibling();
            var rt = s.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            _flies[id] = s;
        }

        public void Place(int id, Vector2 screen, float scale, float alpha)
        {
            if (!_flies.TryGetValue(id, out var s) || !flyParent) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(flyParent, screen, view.Cam(), out var local))
                s.rectTransform.localPosition = new Vector3(local.x, local.y, 0);
            s.transform.localScale = Vector3.one * scale;
            s.SetAlpha(alpha);
        }

        public void Hide(int id)
        {
            if (!_flies.TryGetValue(id, out var s)) return;
            _flies.Remove(id);
            view.ReleaseLoose(s);
        }
    }
}
