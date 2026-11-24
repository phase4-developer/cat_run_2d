using System;
using System.Collections.Generic;
using MV.Data;

namespace MV.UI
{
    /// <summary>LevelProgress Area 한 칸(디자이너 <c>Element_LevelProgress</c>)의 표시 데이터.</summary>
    public sealed class LevelProgressAreaInfo
    {
        public int AreaNo;
        public string AreaName;
        /// <summary>Zone 번호(<c>301_zone.sort</c>).</summary>
        public int ZoneNo;
        public string ZoneName;
        /// <summary><c>302_area.mapIcon</c>(Area 이름표 그림, Local_Texture 주소). 비면 null.</summary>
        public string AreaIcon;
        /// <summary><c>301_zone.mapIcon</c>(Zone 배너 그림, Local_Texture 주소). 비면 null.</summary>
        public string ZoneIcon;
        /// <summary>그 Zone의 첫 Area면 Zone 배너를 켠다.</summary>
        public bool ZoneStart;
        /// <summary><c>302_area.mapColor</c>. 비었거나 읽지 못하면 null(디자이너 색 유지).</summary>
        public string Color;
        /// <summary>관문(<c>303_areaFloor</c> floor 순)마다 Milestone 영혼 수. 마지막 = 이 Area의 <c>needSouls</c>.</summary>
        public long[] Milestones;
        /// <summary>
        /// 이 Area가 풀리는 영혼 수 = 앞 Area <c>needSouls</c>(첫 Area는 0). 보이는 영혼이 이보다 적으면 Area 이름표에 Lock을 씌운다
        /// (노션 「Popup_LevelProgress : Area Lock 표기」: 「Soul이 해당 Area 값보다 같거나 커지면 Lock의 Animation 연출 후 비활성화」).
        /// </summary>
        public long UnlockSouls;
        public bool Locked;
        public bool Current;
        /// <summary>Milestone 재화(영혼 = <c>rescueCat</c>) 아이콘(07_consumeType, MV-UI-ICONSRC). 없으면 null.</summary>
        public string SoulsAtlas, SoulsIcon;
    }

    /// <summary>
    /// LevelProgress 팝업 모델(MV-UI3-LVPROG, 05 §5.10 지도의 디자이너 판, 노션 「[팝업] LevelProgress」).
    /// <c>301_zone</c>(sort) × <c>302_area</c>(sort)를 돌고, Area마다 <c>303_areaFloor</c>(floorGroup) 행 수만큼 관문을 둔다(행 추가 = 지도 확장).
    /// Milestone(사용자 결정 2026-11-19 「골목 누적, 끝칸=needSouls」): 앞 Area needSouls(첫 Area는 0)에 골목 souls를 차례로 더하되
    /// 그 Area needSouls를 넘지 않게 자르고, 마지막 관문은 그 Area needSouls.
    /// </summary>
    public sealed class LevelProgressModel
    {
        readonly List<LevelProgressAreaInfo> _areas = new List<LevelProgressAreaInfo>();

        /// <summary>Milestone·팝업 머리 영혼 아이콘(07_consumeType 재화 rescueCat). 없으면 null.</summary>
        public string SoulsAtlas { get; private set; }
        public string SoulsIcon { get; private set; }

        /// <summary>아래(첫 Area)부터 위로.</summary>
        public IReadOnlyList<LevelProgressAreaInfo> Areas => _areas;

        public static LevelProgressModel Build(MvTables tables, int currentAreaNo)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            var m = new LevelProgressModel();
            long prevNeed = 0;
            MvConsumeIcon.TryWealth(tables, MV.Save.MvWealthIds.TypeOf(MV.Save.MvWealthIds.Souls), out var soulsAtlas, out var soulsIcon);
            m.SoulsAtlas = soulsAtlas; m.SoulsIcon = soulsIcon;
            foreach (var z in tables.Zone.Sorted)
            {
                bool first = true;
                foreach (var a in tables.Area.Sorted)
                {
                    if (a.zoneId != z.id) continue;
                    var floors = new List<AreaFloorData>(tables.AreaFloor.GetGroup(a.floorGroup));
                    floors.Sort((x, y) => x.floor.CompareTo(y.floor));
                    var ms = new long[floors.Count];
                    long acc = prevNeed;
                    for (int i = 0; i < floors.Count; i++)
                    {
                        acc += floors[i].souls;
                        ms[i] = i == floors.Count - 1 ? a.needSouls : Math.Min(acc, a.needSouls);
                    }
                    m._areas.Add(new LevelProgressAreaInfo
                    {
                        AreaNo = a.areaNo,
                        AreaName = a.name,
                        ZoneNo = z.sort,
                        ZoneName = MvUiText.Name(z.id, ""),
                        AreaIcon = string.IsNullOrEmpty(a.mapIcon) ? null : a.mapIcon,
                        ZoneIcon = string.IsNullOrEmpty(z.mapIcon) ? null : z.mapIcon,
                        ZoneStart = first,
                        Color = string.IsNullOrEmpty(a.mapColor) ? null : a.mapColor,
                        Milestones = ms,
                        UnlockSouls = prevNeed,
                        Locked = a.areaNo > currentAreaNo,
                        Current = a.areaNo == currentAreaNo,
                        SoulsAtlas = soulsAtlas,
                        SoulsIcon = soulsIcon,
                    });
                    first = false;
                    prevNeed = a.needSouls;
                }
            }
            return m;
        }

        /// <summary>전체 관문 수(막대 구간 수).</summary>
        public int StepCount
        {
            get { int n = 0; foreach (var a in _areas) n += a.Milestones.Length; return n; }
        }

        /// <summary>
        /// 영혼 수가 막대의 어디인지: 정수부 = 넘은 관문 수(0 = 첫 관문 아래), 소수부 = 다음 관문까지 비율.
        /// 첫 관문 아래 구간은 0에서 첫 Milestone까지. 마지막 관문을 넘으면 <see cref="StepCount"/>.
        /// </summary>
        public float StepPosition(long souls)
        {
            long lo = 0;
            int step = 0;
            foreach (var a in _areas)
            {
                foreach (var hi in a.Milestones)
                {
                    if (souls < hi)
                    {
                        long span = hi - lo;
                        float f = span <= 0 ? 0f : (float)(souls - lo) / span;
                        return step + Math.Max(0f, f);
                    }
                    lo = hi;
                    step++;
                }
            }
            return step;
        }

        /// <summary>현재 Area 순서(0기반). 없으면 -1.</summary>
        public int CurrentIndex
        {
            get { for (int i = 0; i < _areas.Count; i++) if (_areas[i].Current) return i; return -1; }
        }
    }
}
