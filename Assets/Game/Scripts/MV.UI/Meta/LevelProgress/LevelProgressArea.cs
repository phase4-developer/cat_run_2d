using jjevol;
using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// 디자이너 프리팹 <c>Element_LevelProgress</c>(Area 하나)의 루트 부품(MV-UI3-LVPROG).
    /// 관문 칸(<see cref="LevelProgressItem"/>)과 Zone 배너(<see cref="LevelProgressZoneBanner"/>)에 데이터만 넘긴다.
    /// 관문 수는 표(<c>303_areaFloor</c> 행 수)가 정하고, 프리팹 칸보다 많으면 경고하고 남는 칸은 끈다.
    /// </summary>
    public sealed class LevelProgressArea : UIBase
    {
        /// <summary>관문 칸, 아래(첫 관문)부터. 형제 순서와 같다(세로 정렬 역순 배치).</summary>
        [SerializeField] LevelProgressItem[] items = new LevelProgressItem[0];
        [SerializeField] LevelProgressZoneBanner zoneBanner;

        LevelProgressAreaInfo _info;

        public int ItemCount => items != null ? items.Length : 0;
        public LevelProgressItem Item(int i) => items[i];
        public LevelProgressZoneBanner ZoneBanner => zoneBanner;
        public bool HasRefs
        {
            get
            {
                if (!zoneBanner || items == null || items.Length == 0) return false;
                foreach (var it in items) if (!it || !it.HasRefs) return false;
                return zoneBanner.HasRefs;
            }
        }

        public void ShowUI(LevelProgressAreaInfo info)
        {
            _info = info;
            ShowUI();
        }

        protected override void _DrawContents()
        {
            if (_info == null) return;
            if (zoneBanner)
            {
                if (_info.ZoneStart) zoneBanner.ShowUI(new LevelProgressZoneData(_info.ZoneNo, _info.ZoneName));
                else zoneBanner.HideUI();
            }

            int need = _info.Milestones.Length;
            if (need > ItemCount)
                Debug.LogWarning($"[LevelProgressArea] Area {_info.AreaNo} 관문 {need}개인데 Element_LevelProgress 프리팹 칸은 {ItemCount}개입니다. 프리팹에 Element_Progress_Item을 더하세요(Assets/Game/Art/UI/Prefab/LevelProgress/Element_LevelProgress.prefab).");
            var label = new AreaLabelData(_info.ZoneNo, _info.AreaName);
            for (int i = 0; i < ItemCount; i++)
            {
                var it = items[i];
                if (!it) continue;
                if (i < need) it.ShowUI(new LevelProgressItemData(_info.Milestones[i], i == 0, label));
                else it.HideUI();
            }
        }
    }
}
