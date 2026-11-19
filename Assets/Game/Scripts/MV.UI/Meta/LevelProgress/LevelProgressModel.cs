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
        /// <summary>그 Zone의 첫 Area면 Zone 배너를 켠다.</summary>
        public bool ZoneStart;
        /// <summary><c>302_area.mapColor</c>. 비었거나 읽지 못하면 null(디자이너 색 유지).</summary>
        public string Color;
        /// <summary>관문(<c>303_areaFloor</c> floor 순)마다 Milestone 영혼 수. 마지막 = 이 Area의 <c>needSouls</c>.</summary>
        public long[] Milestones;
        public bool Locked;
        public bool Current;
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

        /// <summary>아래(첫 Area)부터 위로.</summary>
        public IReadOnlyList<LevelProgressAreaInfo> Areas => _areas;

        public static LevelProgressModel Build(MvTables tables, int currentAreaNo)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            var m = new LevelProgressModel();
            long prevNeed = 0;
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
                        AreaName = MvUiText.Name(a.id, ""),
                        ZoneNo = z.sort,
                        ZoneName = MvUiText.Name(z.id, ""),
                        ZoneStart = first,
                        Color = string.IsNullOrEmpty(a.mapColor) ? null : a.mapColor,
                        Milestones = ms,
                        Locked = a.areaNo > currentAreaNo,
                        Current = a.areaNo == currentAreaNo,
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
