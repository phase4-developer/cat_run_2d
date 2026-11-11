using System.Collections.Generic;
using MV.Data;
using MV.Save;

namespace MV.UI
{
    /// <summary>
    /// 지도 화면 코드(05 §5.10). <c>301_zone</c>(sort) × <c>302_area</c>(zone, sort)를 돌며 아래에서 위로 쌓는다(행 추가 = 지도 확장).
    /// 구역마다: 구역 머리 → [그 Area 기능 상자(<c>05_contentOpen</c> Area 조건 행)] → Area 배너 → 골목(<c>303_areaFloor</c>) → … → 맨 위 끝 글.
    /// 버튼은 닫기뿐(진행 변경 없음).
    /// </summary>
    public sealed class MapPresenter : MetaPanelPresenter
    {
        public const string AreaPrefix = "area:";

        protected override void Build(MetaPanelModel m, MvTables tables)
        {
            m.Title = "달동네 지도"; // ui.map.title
            m.BottomUp = true;
            int cur = Port.AreaIndex + 1;
            int cp = Port.Cp;
            long souls = Port.Wealth(MvWealthIds.Souls);
            string soulsName = MvUiText.WealthName(tables, MvWealthIds.TypeOf(MvWealthIds.Souls)); // 01_wealth 구출 고양이

            foreach (var z in tables.Zone.Sorted)
            {
                m.Add(new MetaRow("zone:" + z.id, MetaRowKind.Banner,
                    "ZONE " + z.sort + " · " + NameOf(z.id, "") + (z.active ? "" : " · 업데이트 예정")) // ui.map.zoneTag / soonSuffix
                {
                    Icon = z.mapIcon,
                    Locked = z.areaFrom > cur,
                });

                foreach (var a in AreasOf(tables, z.id))
                {
                    foreach (var f in tables.ContentOpen.Sorted)
                    {
                        if (MV.Meta.MvUnlockRules.MapArea(f) != a.areaNo) continue;
                        bool open = Port.IsOpen(f.key);
                        m.Add(new MetaRow("feat:" + f.key, MetaRowKind.Row, NameOf(f.id, f.key))
                        {
                            Icon = f.icon,
                            // ui.map.featOn / featSoon / featAt
                            Value = open ? "해금됨" : !f.active ? "New Feature · 업데이트 예정" : "Area " + a.areaNo + " 개방 시 해금",
                            Desc = DescOf(f.id),
                            Highlight = open,
                        });
                    }

                    bool locked = a.areaNo > cur;
                    bool here = a.areaNo == cur;
                    int need = AreaByNo(tables, a.areaNo - 1)?.needSouls ?? 0; // 앞 Area의 needSouls, 첫 Area는 「시작」
                    m.Add(new MetaRow(AreaPrefix + a.areaNo, MetaRowKind.Banner, "AREA " + a.areaNo + " · " + NameOf(a.id, ""))
                    {
                        Icon = a.mapIcon,
                        Value = (need <= 0 ? "시작" : soulsName + " " + need) + (here ? " · 현재 위치" : ""), // ui.map.start / need / here
                        Highlight = here,
                        Locked = locked,
                    });
                    if (here) m.FocusRowId = AreaPrefix + a.areaNo;

                    var floors = new List<AreaFloorData>(tables.AreaFloor.GetGroup(a.floorGroup));
                    floors.Sort((x, y) => x.floor.CompareTo(y.floor));
                    for (int i = 0; i < floors.Count; i++)
                    {
                        string state = !a.active ? "업데이트 예정"                                  // ui.map.soon
                            : a.areaNo < cur || (here && i < cp) ? "완료"                         // ui.map.cpDone
                            : here ? soulsName + " " + MvUiFormat.Fmt(souls) + "/" + a.needSouls          // ui.map.cpSouls
                            : "체크포인트";                                                         // ui.map.cpBase
                        m.Add(new MetaRow("cp:" + a.areaNo + ":" + floors[i].floor, MetaRowKind.Grid, "골목 " + (i + 1)) // ui.map.cp
                        {
                            Value = state,
                            Locked = locked,
                        });
                    }
                }
            }
            m.Add(new MetaRow("end", MetaRowKind.Text, "다음 구역 — 업데이트 예정")); // ui.map.end

            var curArea = AreaByNo(tables, cur);
            string zoneName = curArea != null ? NameOf(curArea.zoneId, "") : "";
            m.Sub = zoneName + " · Area " + cur + " · " + soulsName + " " + MvUiFormat.Fmt(souls) + "/" + (curArea?.needSouls ?? 0); // ui.map.cur
        }

        static List<AreaData> AreasOf(MvTables tables, int zoneId)
        {
            var list = new List<AreaData>();
            foreach (var a in tables.Area.Sorted) if (a.zoneId == zoneId) list.Add(a);
            return list;
        }

        static AreaData AreaByNo(MvTables tables, int no)
        {
            foreach (var a in tables.Area.Sorted) if (a.areaNo == no) return a;
            return null;
        }

        protected override bool OnAction(string actionId) => false;
    }
}
