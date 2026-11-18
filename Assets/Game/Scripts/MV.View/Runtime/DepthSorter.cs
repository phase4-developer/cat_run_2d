using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 카메라 갱신 뒤(02 §2.2.2 5단계) 빌보드 회전을 맞추고 등록된 <see cref="DepthSortable"/>마다
    /// d 계산 → 근평면 숨김 → sortingOrder → 안개 알파를 매긴다(02 §2.3.2, §2.4). 정본 화가 알고리즘(깊이 내림차순)을 대신한다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class DepthSorter : MonoBehaviour
    {
        [SerializeField] Camera _camera;
        ViewConfig _cfg;

        [Header("가림 페이드 (고양이를 지나친 큰 오브젝트를 흐리게)")]
        [Tooltip("켜면 카메라 기준으로 고양이보다 가까운 오브젝트를 흐리게 한다")] public bool OccludeFade = true;
        [Tooltip("가장 흐릴 때 알파(0~1)")] [Range(0f, 1f)] public float OccludeMinAlpha = 0.35f;
        [Tooltip("고양이 깊이에서 이 거리(m)만큼 더 가까워지면 최소 알파가 된다")] [Min(0.01f)] public float OccludeFadeLen = 1.5f;
        [Tooltip("고양이 깊이보다 이만큼(m) 앞에서부터 흐려지기 시작한다(+면 조금 더 지나친 뒤부터)")] public float OccludeStart = 0f;
        [Tooltip("켜면 길가 소품·장식(정렬 순위 0)도 흐린다. 끄면 길 위 오브젝트(순위 1)만")] public bool OccludeProps = true;

        /// <summary>가림 페이드 기준점(고양이 발밑) 공급자. <see cref="WorldView"/>가 넣는다.</summary>
        public delegate bool FocusProvider(out Vector3 p);
        FocusProvider _focus;

        public void SetFocus(FocusProvider focus) => _focus = focus;

        /// <summary>가림 페이드 설정 한 묶음(정적 정렬 함수에 넘긴다). Len ≤ 0이면 끈다.</summary>
        public struct Occlusion
        {
            public double FocusDepth, Start, Len;
            public float MinAlpha;
            public int MaxRank;
        }
        static readonly int s_fogColor = Shader.PropertyToID("_MvFogColor");
        static readonly int s_fogParams = Shader.PropertyToID("_MvFogParams");
        /// <summary>색 안개 그림이 먼 끝에서 나타나는 거리 m(안개 끝 앞 이만큼 동안 알파 0 → 1).</summary>
        public const double FogPopLen = 6;

        public void Init(ViewConfig cfg, Camera cam)
        {
            _cfg = cfg;
            _camera = cam;
        }

        void LateUpdate()
        {
            if (_cfg == null || _camera == null) return;
            var ct = _camera.transform;
            // 색 안개 전역값(MV_Fog.hlsl): 안개색 = 카메라 배경(테마 fog), 시작·범위 = view.fogStart·fogRange.
            DepthSortable.FogColor = _camera.backgroundColor;
            Shader.SetGlobalColor(s_fogColor, _camera.backgroundColor);
            Shader.SetGlobalVector(s_fogParams, new Vector4((float)_cfg.FogStart, (float)_cfg.FogRange, 0f, 0f));
            Billboard.SyncAll(ct.rotation);
            var occ = new Occlusion();
            if (OccludeFade && _focus != null && _focus(out var fp))
            {
                occ.FocusDepth = DepthMath.Depth(Vector3d.From(fp), Vector3d.From(ct.position), Vector3d.From(ct.forward));
                occ.Start = OccludeStart;
                occ.Len = OccludeFadeLen;
                occ.MinAlpha = OccludeMinAlpha;
                occ.MaxRank = OccludeProps ? DepthMath.RankObject : -DepthMath.RankObject;   // 음수 = 그 순위만(오브젝트만)
            }
            SortAll(DepthSortable.All, ct.position, ct.forward, _cfg, occ);
        }

        /// <summary>등록된 모든 그림을 한 번 정렬한다(시험·QA 하네스가 직접 부를 수 있게 분리).</summary>
        public static void SortAll(Vector3 camPos, Vector3 camForward, ViewConfig cfg) => SortAll(DepthSortable.All, camPos, camForward, cfg);

        public static void SortAll(System.Collections.Generic.IReadOnlyList<DepthSortable> all, Vector3 camPos, Vector3 camForward, ViewConfig cfg)
            => SortAll(all, camPos, camForward, cfg, default);

        /// <summary>
        /// 정렬 + 가림 페이드: 순위가 소품·오브젝트(고양이·투사체·이펙트 제외)이고 앵커 깊이가 고양이 깊이 − Start보다 가까우면
        /// 그 차이가 Len이 될 때까지 알파를 1 → MinAlpha로 내린다.
        /// </summary>
        public static void SortAll(System.Collections.Generic.IReadOnlyList<DepthSortable> all, Vector3 camPos, Vector3 camForward, ViewConfig cfg, Occlusion occ)
        {
            var cp = Vector3d.From(camPos);
            var f = Vector3d.From(camForward);
            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                var anchor = Vector3d.From(s.Anchor);
                double dAnchor = DepthMath.Depth(anchor, cp, f);
                double dProbe = s.SortOffsetY != 0f ? DepthMath.Depth(new Vector3d(anchor.x, anchor.y + s.SortOffsetY, anchor.z), cp, f) : dAnchor;
                // 정본: 정렬 탐침이 근평면 안이면 목록에 넣지 않는다(L3792 `if (!pp) continue`).
                bool shown = !DepthMath.IsNearCut(dProbe, cfg.NearCut) && !DepthMath.IsNearCut(dAnchor, cfg.NearCut);
                s.ApplyVisible(shown);
                if (!shown) continue;
                s.ApplyOrder(DepthMath.SortingOrder(dProbe, s.Bias, s.Rank, cfg.SortQuant));
                float oa = 1f;
                if (occ.Len > 0 && OccludeRank(s.Rank, occ.MaxRank))
                {
                    double k = (occ.FocusDepth - occ.Start - dAnchor) / occ.Len;
                    if (k > 0) oa = Mathf.Lerp(1f, occ.MinAlpha, (float)System.Math.Min(1.0, k));
                }
                s.SetOccludeAlpha(oa);
                s.ApplyFog((float)DepthMath.FogK(dAnchor, cfg.FogStart, cfg.FogRange), (float)DepthMath.FogPopAlpha(dAnchor, cfg.FogStart, cfg.FogRange, FogPopLen));
            }
        }
        /// <summary>maxRank ≥ 0: 순위 0..maxRank, 음수: 순위 −maxRank만.</summary>
        static bool OccludeRank(int rank, int maxRank) => maxRank >= 0 ? rank >= 0 && rank <= maxRank : rank == -maxRank;
    }
}
