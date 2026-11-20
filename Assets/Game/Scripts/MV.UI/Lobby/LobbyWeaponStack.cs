using System.Collections.Generic;
using DG.Tweening;
using MV.Data;
using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// 로비 무기 스택의 로비 연결(MV-UI3-LOBBY-WEAPONFX, 노션 「LobbyScreen : WeaponUpgrade시 무기 획득 및 Stack 연출」).
    /// 스택·줄 서기·머지 연출은 같은 오브젝트의 공용 부품 <see cref="WeaponStackView"/>가 맡고(인게임과 같은 부품), 이 컴포넌트는 로비만의 일을 한다:
    /// 시작 덱(<c>mvLobby.deck</c>)을 손패로 넘기고, 스크래쳐(무기 강화)를 사면 덱에 넣은 카드 1장을 <c>Weapon_GetAnchor/Spwan</c>에서 <c>Dst</c>까지 옮긴 뒤
    /// (여러 장이면 같은 Dst에 겹친다) 공용 부품의 줄(<c>Weapon_QueueAnchor</c>)에 세운다. 그 뒤 전달·같은 등급 3장 머지(Flash·Fx·Scale)는 인게임과 같다.
    /// 비행 시간·곡선은 공용 부품의 줄서기 값(launchSec·launchArc)을 같이 쓴다. 노션에 로비 값이 따로 없다.
    /// </summary>
    public sealed class LobbyWeaponStack : MonoBehaviour
    {
        [Tooltip("스택 공용 부품(같은 오브젝트의 WeaponStackView).")]
        [SerializeField] WeaponStackView view;
        [Tooltip("새 카드가 나타나는 자리: Element_MainUpgrade_Weapon/Weapon_GetAnchor/Spwan.")]
        [SerializeField] RectTransform spawn;
        [Tooltip("새 카드가 먼저 모이는 자리: Element_MainUpgrade_Weapon/Weapon_GetAnchor/Dst. 여기서 줄(Weapon_QueueAnchor)로 간다.")]
        [SerializeField] RectTransform dst;

        int[] _target;
        readonly List<HudWeaponSlot> _flying = new List<HudWeaponSlot>();   // Spwan → Dst 중인 칸
        bool _ready;

        // ---- 시험·QA ----
        public WeaponStackView View { get { Init(); return view; } }
        public RectTransform Spawn => spawn;
        public RectTransform Dst => dst;
        /// <summary>Spwan → Dst를 날고 있어 아직 줄에 서지 않은 카드 수.</summary>
        public int Flying => _flying.Count;
        public bool Busy => _flying.Count > 0 || view && view.Busy;

        /// <summary>빈 직렬화 참조 이름(시험·QA).</summary>
        public List<string> MissingRefs()
        {
            var miss = new List<string>();
            if (!view) miss.Add(nameof(view));
            else foreach (var m in view.MissingRefs()) miss.Add("view." + m);
            if (!spawn) miss.Add(nameof(spawn));
            if (!dst) miss.Add(nameof(dst));
            return miss;
        }

        void Awake() => Init();

        void Init()
        {
            if (_ready) return;
            _ready = true;
            if (!view)
            {
                Debug.LogWarning("[LobbyWeaponStack] view(같은 오브젝트의 WeaponStackView)가 비어 있습니다: Assets/Game/Art/UI/Prefab/UIScreen/LobbyScreen.prefab Mid/Weapon", this);
                return;
            }
            view.Init();
            view.DmgMul = null;   // 로비 카드 공격력 = 표 값 그대로(런 카드 효과 없음)
        }

        /// <summary>
        /// 시작 덱(등급별 장수)을 보인다. 처음이거나 표가 바뀌면 바로 놓고, 그 밖에는 쉬는 중일 때 장수가 다르면 바로 맞춘다
        /// (무기 Level이 올라 낮은 카드를 버린 덱 등). 날아오는 카드가 있으면 다 들어간 뒤 맞춘다.
        /// </summary>
        public void Show(MvTables t, int[] deck)
        {
            Init();
            if (!view) return;
            _target = deck != null ? (int[])deck.Clone() : System.Array.Empty<int>();
            if (!view.HasHand || !ReferenceEquals(view.Tables, t)) { CancelFlying(); view.ResetHand(t, _target); return; }
            if (_flying.Count == 0) view.Sync(_target);
        }

        /// <summary>
        /// 스크래쳐 구매로 덱에 <paramref name="tier"/> 등급 카드 1장이 들어갔다. Spwan에서 나타나 Dst로 옮긴 뒤 줄에 서고, 줄에 서는 순간 받을 장 1장을 넣는다
        /// (받을 장은 줄 카드가 도착해야 손패에 들어간다). 정지 중(편집기 시험)에는 바로 줄에 세운다.
        /// </summary>
        public void Add(int tier)
        {
            Init();
            if (!view || !view.HasHand || tier < 1) return;
            var s = view.RentLoose(tier);
            if (!Application.isPlaying || !spawn || !dst)
            {
                view.JoinQueue(s);
                view.Deliver(1, tier);
                return;
            }
            var rt = s.rectTransform;
            rt.SetParent(spawn.parent, false);   // Bottom 강화 칸 위로 보이게 Weapon_GetAnchor 아래에서 출발
            rt.SetAsLastSibling();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.localScale = Vector3.one;
            rt.position = spawn.position;
            _flying.Add(s);
            view.FlyTo(rt, dst.position).OnComplete(() =>
            {
                if (!_flying.Remove(s)) return;
                view.JoinQueue(s);
                view.Deliver(1, tier);
            });
        }

        void Update()
        {
            if (!view) return;
            // 날아오는 카드가 남아 있으면 덱 장수로 다시 맞추지 않는다(다 들어간 뒤 맞춘다).
            view.Step(Time.unscaledDeltaTime, _flying.Count > 0 ? null : _target);
        }

        void OnDisable() => CancelFlying();

        /// <summary>Spwan → Dst 중인 칸을 거둔다(화면이 꺼짐·덱 다시 놓음). 덱은 이미 저장돼 있어 다음 맞춤에서 손패에 들어간다.</summary>
        void CancelFlying()
        {
            foreach (var s in _flying)
            {
                if (!s) continue;
                DOTween.Kill(s.rectTransform);
                if (view) view.ReleaseLoose(s);
            }
            _flying.Clear();
        }
    }
}
