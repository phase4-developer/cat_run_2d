using jjevol;
using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// 디자이너 프리팹 <c>Element_LevelProgress</c>(Area 하나)의 루트 부품(MV-UI3-LVPROG).
    /// 관문 칸(<see cref="LevelProgressItem"/>)에 데이터만 넘기고, Zone 첫 Area면 배너 높이만큼 빈 자리(<c>Space_Banner_Zone</c>)를 켠다.
    /// 배너 그림은 게이지 뒤 배경 층(<see cref="LevelProgressAreaBg"/>)이 그린다.
    /// 관문 수는 표(<c>303_areaFloor</c> 행 수)가 정하고, 프리팹 칸보다 많으면 경고하고 남는 칸은 끈다.
    /// </summary>
    public sealed class LevelProgressArea : UIBase
    {
        /// <summary>관문 칸, 아래(첫 관문)부터. 형제 순서와 같다(세로 정렬 역순 배치).</summary>
        [SerializeField] LevelProgressItem[] items = new LevelProgressItem[0];
        /// <summary><c>Space_Banner_Zone</c>: 배경 층 Zone 배너와 같은 높이의 빈 자리(관문 눈금이 배경 칸과 맞게).</summary>
        [SerializeField] GameObject bannerSpace;

        LevelProgressAreaInfo _info;

        public int ItemCount => items != null ? items.Length : 0;
        public LevelProgressItem Item(int i) => items[i];
        public GameObject BannerSpace => bannerSpace;
        public bool HasRefs
        {
            get
            {
                if (!bannerSpace || items == null || items.Length == 0) return false;
                foreach (var it in items) if (!it || !it.HasRefs) return false;
                return true;
            }
        }

        public void ShowUI(LevelProgressAreaInfo info)
        {
            _info = info;
            ShowUI();
        }

        /// <summary>보이는 영혼 수를 관문 칸에 넘긴다(Area Lock). <paramref name="animate"/>면 풀릴 때 Lock 연출.</summary>
        public void SetSouls(long souls, bool animate)
        {
            for (int i = 0; i < ItemCount; i++)
                if (items[i] && items[i].gameObject.activeSelf) items[i].SetSouls(souls, animate);
        }

        protected override void _DrawContents()
        {
            if (_info == null) return;
            if (bannerSpace) bannerSpace.SetActive(_info.ZoneStart);

            int need = _info.Milestones.Length;
            if (need > ItemCount)
                Debug.LogWarning($"[LevelProgressArea] Area {_info.AreaNo} 관문 {need}개인데 Element_LevelProgress 프리팹 칸은 {ItemCount}개입니다. 프리팹에 Element_Progress_Item을 더하세요(Assets/Game/Art/UI/Prefab/LevelProgress/Element_LevelProgress.prefab).");
            var label = new AreaLabelData(_info.ZoneNo, _info.AreaName, _info.AreaIcon);
            for (int i = 0; i < ItemCount; i++)
            {
                var it = items[i];
                if (!it) continue;
                if (i < need) it.ShowUI(new LevelProgressItemData(_info.Milestones[i], i == 0, label, _info.SoulsAtlas, _info.SoulsIcon, _info.UnlockSouls));
                else it.HideUI();
            }
        }
    }
}
