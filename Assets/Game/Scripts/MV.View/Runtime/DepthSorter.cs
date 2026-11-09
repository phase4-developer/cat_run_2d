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

        public void Init(ViewConfig cfg, Camera cam)
        {
            _cfg = cfg;
            _camera = cam;
        }

        void LateUpdate()
        {
            if (_cfg == null || _camera == null) return;
            var ct = _camera.transform;
            Billboard.SyncAll(ct.rotation);
            SortAll(ct.position, ct.forward, _cfg);
        }

        /// <summary>등록된 모든 그림을 한 번 정렬한다(시험·QA 하네스가 직접 부를 수 있게 분리).</summary>
        public static void SortAll(Vector3 camPos, Vector3 camForward, ViewConfig cfg) => SortAll(DepthSortable.All, camPos, camForward, cfg);

        public static void SortAll(System.Collections.Generic.IReadOnlyList<DepthSortable> all, Vector3 camPos, Vector3 camForward, ViewConfig cfg)
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
                s.ApplyFogAlpha((float)DepthMath.FogAlpha(dAnchor, cfg.FogStart, cfg.FogRange));
            }
        }
    }
}
