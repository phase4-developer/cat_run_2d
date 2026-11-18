using System.Collections.Generic;
using MV.Data;
using MV.Run;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 런 오브젝트 하나의 화면 뷰(뷰 뿌리 = SortingGroup + <see cref="DepthSortable"/>, 관리자가 만들고 풀에서 다시 쓴다).
    /// 그림과 동작은 뿌리 아래 프리팹 인스턴스의 <see cref="RunObjectModule"/>들이 정한다(프리팹이 없는 종류는 관리자가 만들지 않는다).
    /// 이 뷰가 하는 일: 모듈 호출 순서, 그림 묶음 자세 적용(<see cref="Pose"/>), 글자판, 판정 Hitbox 등록·해제(연결·회수와 같은 흐름).
    /// 생성·풀 대여/반환·전체 비활성화는 관리자(<see cref="ObjectViewFactory"/>)만 한다. 모듈은 <see cref="ReleaseReady"/>로 회수 가능 여부만 알린다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunObjectView : MonoBehaviour
    {
        readonly List<RunObjectModule> _modules = new List<RunObjectModule>(8);
        readonly List<Transform> _poseTargets = new List<Transform>(2);
        readonly List<Vector3> _posePos = new List<Vector3>(2), _poseScale = new List<Vector3>(2);
        readonly List<Billboard> _rollBoards = new List<Billboard>(16);
        readonly List<Collider> _hitCols = new List<Collider>(4);
        float _appliedRoll;
        bool _hitRegistered;
        static readonly HashSet<string> s_reported = new HashSet<string>();

        public RunObject Obj { get; private set; }
        public ObjectViewContext Ctx { get; private set; }
        public DepthSortable Ds { get; private set; }
        /// <summary>그림 인스턴스(프리팹 인스턴스, 코드 모양은 null).</summary>
        public GameObject Instance { get; private set; }
        /// <summary>프리팹 주소(코드 모양은 null).</summary>
        public string Prefab { get; private set; }
        /// <summary>이 프레임 그림 묶음 자세(모듈이 더하고 곱한다).</summary>
        public readonly ObjectPose Pose = new ObjectPose();
        /// <summary>쓰러지는 중(paperDie). <see cref="ObjectMotion"/>이 정한다. 쓰러지는 동안 글자판은 숨긴다.</summary>
        public bool Dying { get; set; }
        /// <summary>프리팹 글자판(`Obj_Text_Count`). 없으면 null(TMP 한 줄 글자판).</summary>
        public CountLabel Board { get; private set; }
        /// <summary>프리팹 안 투사체 판정 Collider(<see cref="RunHitbox"/>).</summary>
        public IReadOnlyList<Collider> HitColliders => _hitCols;
        /// <summary>프레임 표식(관리자가 이번 Sync에서 본 뷰인지).</summary>
        internal int Stamp;

        /// <summary>모든 모듈이 회수를 막지 않는가.</summary>
        public bool ReleaseReady
        {
            get
            {
                for (int i = 0; i < _modules.Count; i++) if (_modules[i].HoldsRelease) return false;
                return true;
            }
        }

        /// <summary>붙은 모듈 하나(시험·QA). 없으면 null.</summary>
        public T Module<T>() where T : RunObjectModule
        {
            for (int i = 0; i < _modules.Count; i++) if (_modules[i] is T m) return m;
            return null;
        }

        /// <summary>관리자: 뷰 뿌리를 만들 때 한 번.</summary>
        internal void Setup(ObjectViewContext ctx, string prefab)
        {
            Ctx = ctx;
            Prefab = prefab;
            Ds = GetComponent<DepthSortable>();
        }

        /// <summary>관리자: 프리팹 인스턴스가 준비되면 한 번(그 뒤 풀에서 다시 써도 그대로). 모듈·글자판·Hitbox·그림 묶음을 프리팹에서 읽는다.</summary>
        internal void AttachInstance(GameObject inst)
        {
            Instance = inst;
            inst.transform.localPosition = Vector3.zero;   // 프리팹 뿌리 = 뷰 뿌리 = 시뮬 위치
            if (inst.GetComponentInChildren<Billboard>(true) == null && s_reported.Add("billboard:" + Prefab))
                Debug.LogWarning("[RunObjectView] 오브젝트 프리팹에 Billboard가 없습니다(카메라를 볼 이미지 묶음에 붙여야 함): " + Prefab);
            // 연출은 빌보드 묶음(그림)만 움직인다. 프리팹 뿌리와 그 아래 Hitbox는 시뮬 위치에 그대로 둔다.
            foreach (var b in inst.GetComponentsInChildren<Billboard>(true)) AddPoseTarget(b.transform);
            _hitCols.Clear();
            foreach (var hb in inst.GetComponentsInChildren<RunHitbox>(true)) _hitCols.AddRange(hb.GetComponentsInChildren<Collider>(true));
            Board = CountLabel.Find(inst.transform);
            inst.GetComponentsInChildren(true, _modules);
            Ds.Refresh();
            if (Obj != null) BindModules();
        }

        void AddPoseTarget(Transform t)
        {
            _poseTargets.Add(t);
            _posePos.Add(t.localPosition);
            _poseScale.Add(t.localScale);
        }

        /// <summary>관리자: 오브젝트에 연결(풀에서 꺼낼 때). 모듈 상태를 처음으로 두고 판정 Hitbox를 등록한다.</summary>
        internal void Bind(RunObject o)
        {
            Obj = o;
            Dying = false;
            Ds.SortOffsetY = (float)Ctx.Cfg.ObjProbeY;
            Ds.Bias = o.Kind != null ? (float)o.Kind.sortBias : 0f;
            Ds.SetAlphaScale(1f);
            if (Instance != null) Instance.transform.localPosition = Vector3.zero;
            BindModules();
        }

        void BindModules()
        {
            Pose.Reset();
            ApplyPose();
            for (int i = 0; i < _modules.Count; i++) _modules[i].OnBind(this);
            RegisterHits();
        }

        void RegisterHits()
        {
            if (_hitRegistered || Obj?.Kind == null || Ctx.Hits == null) return;
            if (Obj.Kind.hittable == Hittable.Never) return;
            if (Instance == null) return;   // 프리팹 로드 전: 로드되면 AttachInstance가 다시 부른다
            if (_hitCols.Count == 0)
            {
                if (s_reported.Add("hitbox:" + Prefab))
                    Debug.LogWarning($"[RunObjectView] 피격 가능 종류 {Obj.Kind.key}의 프리팹 {Prefab}에 {nameof(RunHitbox)}(Collider)가 없습니다(맞지 않음).");
                return;
            }
            Ctx.Hits.Register(Obj, _hitCols);
            _hitRegistered = true;
        }

        /// <summary>관리자: 회수(풀로 돌려줄 때). 판정 등록을 풀고 모듈이 바꾼 그림을 프리팹 기본으로 되돌린다.</summary>
        internal void Unbind()
        {
            if (_hitRegistered && Ctx.Hits != null && Obj != null) Ctx.Hits.Unregister(Obj);
            _hitRegistered = false;
            for (int i = 0; i < _modules.Count; i++) _modules[i].OnUnbind(this);
            Pose.Reset();
            ApplyPose();
            Ds.SetAlphaScale(1f);
            if (Instance != null) Instance.transform.localPosition = Vector3.zero;
            Dying = false;
            Obj = null;
        }

        /// <summary>관리자: 매 프레임(그리기 범위 안이거나 쓰러지는 중). 모듈 → 자세 적용 → 글자판.</summary>
        internal void Tick(in ObjectFrame f)
        {
            if (Obj == null) return;
            Pose.Reset();
            for (int i = 0; i < _modules.Count; i++)
            {
                var m = _modules[i];
                if (!Dying || m.TicksWhileDying) m.OnTick(this, f);
            }
            ApplyPose();
            UpdateLabel(f.PlayerZ);
        }

        /// <summary>관리자: 이 오브젝트의 런 이벤트를 모듈에 돌린다.</summary>
        internal void OnRunEvent(in RunEvent e)
        {
            for (int i = 0; i < _modules.Count; i++) _modules[i].OnRunEvent(this, e);
            if (Dying) UpdateLabel(0);
        }

        void ApplyPose()
        {
            for (int i = 0; i < _poseTargets.Count; i++)
            {
                var t = _poseTargets[i];
                if (t == null) continue;
                t.localPosition = _posePos[i] + Pose.Offset;
                t.localScale = Vector3.Scale(_poseScale[i], Pose.Scale);
            }
            if (Pose.Roll != 0f || _appliedRoll != 0f)
            {
                // 코드 모양은 그림 조각마다 빌보드라 묶음 아래 빌보드 모두에 같은 기울기를 준다.
                _rollBoards.Clear();
                var tmp = ListPool.Boards;
                for (int i = 0; i < _poseTargets.Count; i++)
                {
                    if (_poseTargets[i] == null) continue;
                    _poseTargets[i].GetComponentsInChildren(true, tmp);
                    _rollBoards.AddRange(tmp);
                }
                for (int i = 0; i < _rollBoards.Count; i++) _rollBoards[i].Roll = Pose.Roll;
                _appliedRoll = Pose.Roll;
            }
            if (Ds != null) Ds.SetAlphaScale(Pose.Alpha);
        }

        /// <summary>
        /// 글자판(매 프레임). 내용은 <see cref="ObjectLabel.Of"/>(105.labelKind, 표가 정한다), 그림은 프리팹 글자판(<see cref="CountLabel"/>)만 쓴다.
        /// 글자판을 품지 않은 프리팹은 글자를 띄우지 않고, 표가 글자를 요구하면 프리팹 주소를 경고로 한 번 알린다(대체 글자를 만들지 않는다).
        /// </summary>
        void UpdateLabel(double playerZ)
        {
            var o = Obj;
            var c = Dying || o == null || Ctx.HideLabels ? LabelContent.Hidden : ObjectLabel.Of(o, playerZ, Ctx.Cfg.ArchLabelHide, Ctx.WallRows.Ahead(o), Ctx.GoldMul);
            if (Board != null) { Board.Apply(c); return; }
            if (c.Visible && Instance != null && o?.Kind != null && s_reported.Add("label:" + Prefab))
                Debug.LogWarning($"[RunObjectView] 오브젝트 프리팹 {Prefab}에 글자판 프리팹(Obj_Text_Count)이 없어 글자를 띄우지 않습니다(105 {o.Kind.key} labelKind {o.Kind.labelKind}). " +
                    $"글자판을 프리팹에 중첩하세요: {CountLabel.PrefabPath}", this);
        }

        static class ListPool
        {
            public static readonly List<Billboard> Boards = new List<Billboard>(16);
        }
    }
}
