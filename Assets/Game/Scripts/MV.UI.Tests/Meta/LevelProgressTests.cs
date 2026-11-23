using System.Collections.Generic;
using System.Linq;
using MV.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MV.UI.Tests
{
    /// <summary>
    /// LevelProgress(지도) 팝업(MV-UI3-LVPROG).
    /// 모델: 표 정본(TSV)으로 Area·관문·Milestone을 계산한다.
    /// 프리팹: 주소 프리팹 <c>UIPopup_Map</c>(디자이너 <c>Popup_LevelProgress</c> 변형)을 <c>Object.Instantiate</c>로 만들어 참조와 칸 수를 본다.
    /// 보장하지 않는 범위: EditMode라 Awake·코루틴(오름 연출)·실제 Canvas 배치·스크롤 위치는 돌지 않는다(막대 눈금 위치 값은 NOT_RUN, 화면 확인 필요).
    /// </summary>
    public sealed class LevelProgressTests
    {
        const string MapPath = "Assets/Game/Addressables/Local/UI/Meta/UIPopup_Map.prefab";
        const string ArtDir = "Assets/Game/Art/UI/Prefab/LevelProgress/";

        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go) Object.DestroyImmediate(go);
            _made.Clear();
        }

        static LevelProgressModel Model(MvTables t, int cur = 1) => LevelProgressModel.Build(t, cur);

        [Test]
        public void Model_OneEntryPerAreaRow_ZoneBannerOnFirstAreaOfEachZone()
        {
            var t = MetaUiTestKit.Base();
            var m = Model(t);
            Assert.That(m.Areas.Count, Is.EqualTo(t.Area.Sorted.Count()));
            Assert.That(m.Areas.Count(a => a.ZoneStart), Is.EqualTo(t.Zone.Sorted.Count(z => t.Area.Sorted.Any(a => a.zoneId == z.id))));
            Assert.That(m.Areas[0].ZoneStart, Is.True);
            Assert.That(m.Areas.Select(a => a.AreaNo), Is.Ordered);
        }

        [Test]
        public void Model_TableRowAdded_OneMoreArea()
        {
            int before = Model(MetaUiTestKit.Base()).Areas.Count;
            int after = Model(MetaUiTestKit.WithExtraRow("302_area")).Areas.Count;
            Assert.That(after, Is.EqualTo(before + 1));
        }

        [Test]
        public void Model_Milestones_FloorSoulsAccumulate_LastIsNeedSouls()
        {
            var t = MetaUiTestKit.Base();
            var m = Model(t);
            long prevNeed = 0;
            foreach (var info in m.Areas)
            {
                var area = t.Area.Sorted.First(a => a.areaNo == info.AreaNo);
                var floors = t.AreaFloor.GetGroup(area.floorGroup).OrderBy(f => f.floor).ToList();
                Assert.That(info.Milestones.Length, Is.EqualTo(floors.Count), $"Area {info.AreaNo} 관문 수 = 303 행 수");
                long acc = prevNeed;
                for (int i = 0; i < floors.Count; i++)
                {
                    acc += floors[i].souls;
                    long expect = i == floors.Count - 1 ? area.needSouls : System.Math.Min(acc, area.needSouls);
                    Assert.That(info.Milestones[i], Is.EqualTo(expect), $"Area {info.AreaNo} 관문 {i + 1}");
                }
                prevNeed = area.needSouls;
            }
            // 사용자 결정 예시: Area 1 = 10, 27, 50.
            Assert.That(m.Areas[0].Milestones, Is.EqualTo(new long[] { 10, 27, 50 }));
        }

        [Test]
        public void Model_StepPosition_ZeroAtStart_EndAtLastNeed_Monotonic()
        {
            var m = Model(MetaUiTestKit.Base());
            Assert.That(m.StepPosition(0), Is.EqualTo(0f));
            Assert.That(m.StepPosition(10), Is.EqualTo(1f).Within(1e-5), "첫 관문(10)에 닿으면 1칸");
            Assert.That(m.StepPosition(5), Is.EqualTo(0.5f).Within(1e-5));
            long last = m.Areas[m.Areas.Count - 1].Milestones.Last();
            Assert.That(m.StepPosition(last), Is.EqualTo((float)m.StepCount));
            Assert.That(m.StepPosition(last * 10), Is.EqualTo((float)m.StepCount));
            float prev = -1f;
            for (long s = 0; s <= 300; s += 7)
            {
                float p = m.StepPosition(s);
                Assert.That(p, Is.GreaterThanOrEqualTo(prev));
                prev = p;
            }
        }

        [Test]
        public void Model_CurrentAndLocked_FollowCurrentArea()
        {
            var m = Model(MetaUiTestKit.Base(), 3);
            Assert.That(m.CurrentIndex, Is.EqualTo(2));
            Assert.That(m.Areas[1].Locked, Is.False);
            Assert.That(m.Areas[3].Locked, Is.True);
        }

        [Test]
        public void Model_AreaColor_FromTable()
        {
            var m = Model(MetaUiTestKit.Base());
            Assert.That(m.Areas[0].Color, Is.EqualTo("#F19292"), "노션 견본 Area 1");
            Assert.That(m.Areas[5].Color, Is.EqualTo("#9B92F1"), "노션 견본 Area 6(Zone 2)");
            foreach (var a in m.Areas) Assert.That(ColorUtility.TryParseHtmlString(a.Color, out _), Is.True, $"Area {a.AreaNo} 색 {a.Color}");
        }

        [Test]
        public void DesignerParts_HaveRootComponents_WithAllReferences()
        {
            var item = AssetDatabase.LoadAssetAtPath<GameObject>(ArtDir + "Element_Progress_Item.prefab").GetComponent<LevelProgressItem>();
            Assert.That(item, Is.Not.Null, "Element_Progress_Item 루트 LevelProgressItem");
            Assert.That(item.HasRefs, Is.True, "LevelProgressItem 참조(Milestone·글·Area 이름표)");

            var banner = AssetDatabase.LoadAssetAtPath<GameObject>(ArtDir + "Element_Progress_Banner_Zone.prefab").GetComponent<LevelProgressZoneBanner>();
            Assert.That(banner, Is.Not.Null);
            Assert.That(banner.HasRefs, Is.True);

            var bg = AssetDatabase.LoadAssetAtPath<GameObject>(ArtDir + "Element_Progress_AreaBG.prefab").GetComponent<LevelProgressAreaBg>();
            Assert.That(bg, Is.Not.Null);
            Assert.That(bg.HasRefs, Is.True);
            Assert.That(bg.SlotCount, Is.EqualTo(3), "관문 칸 3 + 배너 칸 1(노션)");

            var area = AssetDatabase.LoadAssetAtPath<GameObject>(ArtDir + "Element_LevelProgress.prefab").GetComponent<LevelProgressArea>();
            Assert.That(area, Is.Not.Null);
            Assert.That(area.HasRefs, Is.True, "관문 칸 3개·Zone 배너");
            Assert.That(area.ItemCount, Is.EqualTo(3));
            // 아래(형제 순서 앞)부터: 관문 칸 순서가 배치 순서와 같다.
            for (int i = 1; i < area.ItemCount; i++)
                Assert.That(area.Item(i).transform.GetSiblingIndex(), Is.GreaterThan(area.Item(i - 1).transform.GetSiblingIndex()));
        }

        /// <summary>301_zone·302_area mapIcon은 모든 행이 채워져 있고, 디자이너 Zone·Area 텍스처 파일 이름이다(모자란 그림은 이전 그림, MV-UI3-ZONETEX).</summary>
        [Test]
        public void MapIcons_AllRowsPointToZoneAreaTextures()
        {
            const string dir = "Assets/Game/Art/UI/Texture/Background_ZoneArea";
            var names = new HashSet<string>(AssetDatabase.FindAssets("t:Texture2D", new[] { dir })
                .Select(g => System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g))));
            Assert.That(names, Is.Not.Empty, dir);
            var t = MetaUiTestKit.Base();
            foreach (var z in t.Zone.Datas) Assert.That(names, Does.Contain(z.mapIcon), "301_zone " + z.id);
            foreach (var a in t.Area.Datas) Assert.That(names, Does.Contain(a.mapIcon), "302_area " + a.id);
        }

        // MapPopup은 UIPopup(JJEvol.UI.Addressables) 파생이라 이 시험 어셈블리에서 형식으로 쓰지 않고 이름·직렬화 필드로 본다(AreaUpgradeDesignViewTests와 같은 방식).
        (Component popup, LevelProgressView view) NewMap()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapPath);
            Assert.That(prefab, Is.Not.Null, MapPath);
            var go = Object.Instantiate(prefab);
            _made.Add(go);
            var popup = go.GetComponent("MapPopup");
            Assert.That(popup, Is.Not.Null, "UIPopup_Map 루트 MapPopup");
            var so = new SerializedObject(popup);
            Assert.That(so.FindProperty("PopupType").stringValue, Is.EqualTo(MvUiIds.Map));
            var view = so.FindProperty("view").objectReferenceValue as LevelProgressView;
            Assert.That(view, Is.Not.Null, "MapPopup.view");
            Assert.That(view.HasRefs, Is.True, "LevelProgressView 참조");
            return (popup, view);
        }

        [Test]
        public void MapPrefab_IsLevelProgressVariant_WithViewReferences()
        {
            var (popup, view) = NewMap();
            Assert.That(view.gameObject, Is.SameAs(popup.gameObject), "디자이너 팝업 루트에 MapPopup이 붙는다(변형)");
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(AssetDatabase.LoadAssetAtPath<GameObject>(MapPath)), Is.Not.Null, "Popup_LevelProgress 변형");
            // 왼쪽 진척 게이지는 표시 전용(사용자 2026-11-23 「표시 용도라 입력 안되야해」): 슬라이더 입력 끔, 게이지 그림은 입력을 받지 않아 드래그가 스크롤로 간다.
            var bar = view.GetComponentsInChildren<UnityEngine.UI.Slider>(true).FirstOrDefault(sl => sl.name == "ProgressBar");
            Assert.That(bar, Is.Not.Null, "ProgressBar");
            Assert.That(bar.interactable, Is.False, "게이지 입력 끔");
            Assert.That(bar.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Where(g => g.raycastTarget).Select(g => g.name), Is.Empty, "게이지 그림 raycast 끔");
        }

        [Test]
        public void View_MakesOneAreaAndBackgroundPerTableRow_AndSkipsToEnd()
        {
            var (_, view) = NewMap();
            var model = Model(MetaUiTestKit.Base(), 2);
            view.ShowUI(new LevelProgressViewData { Model = model, FromSouls = 40, ToSouls = 120, AutoClose = false });
            Assert.That(view.AreaViews.Count, Is.EqualTo(model.Areas.Count), "Area 칸 = 302 행 수(예시 칸 7개를 덮고 모자라면 더 만든다)");
            Assert.That(view.BackgroundViews.Count, Is.EqualTo(model.Areas.Count));
            Assert.That(view.CloseVisible, Is.True, "로비에서 열면 닫기 버튼이 보인다");
            Assert.That(view.ShownSouls, Is.EqualTo(40));

            // 첫 Area: Zone 배너 켬, 첫 관문만 Area 이름표.
            var first = view.AreaViews[0];
            Assert.That(first.ZoneBanner.gameObject.activeSelf, Is.True);
            Assert.That(first.ZoneBanner.ZoneNumberText, Is.EqualTo("Zone 1"));
            Assert.That(first.Item(0).AreaLabel.gameObject.activeSelf, Is.True);
            Assert.That(first.Item(1).AreaLabel.gameObject.activeSelf, Is.False);
            Assert.That(first.Item(2).MilestoneText, Is.EqualTo(MvUiFormat.Fmt(50)));
            Assert.That(view.AreaViews[1].ZoneBanner.gameObject.activeSelf, Is.False, "Zone 중간 Area는 배너 끔");
            // 그림은 표 주소(MV-UI3-ZONETEX): Zone 배너 = 301_zone.mapIcon, Area 이름표 = 302_area.mapIcon.
            Assert.That(model.Areas[0].ZoneIcon, Is.Not.Empty);
            Assert.That(first.ZoneBanner.ZoneTextureKey, Is.EqualTo(model.Areas[0].ZoneIcon));
            Assert.That(first.Item(0).AreaLabel.AreaTextureKey, Is.EqualTo(model.Areas[0].AreaIcon));
            Assert.That(view.BackgroundViews[0].BannerSlot.gameObject.activeSelf, Is.True);
            Assert.That(view.BackgroundViews[1].BannerSlot.gameObject.activeSelf, Is.False);

            view.Complete();
            Assert.That(view.ShownSouls, Is.EqualTo(120));
            Assert.That(view.Rising, Is.False);

            // 다시 열어도 칸 수가 늘지 않는다.
            view.ShowUI(new LevelProgressViewData { Model = model, FromSouls = 0, ToSouls = 0 });
            Assert.That(view.AreaViews.Count, Is.EqualTo(model.Areas.Count));
        }

        [Test]
        public void View_RunEnd_HidesClose()
        {
            var (_, view) = NewMap();
            view.ShowUI(new LevelProgressViewData { Model = Model(MetaUiTestKit.Base()), FromSouls = 0, ToSouls = 30, AutoClose = true });
            Assert.That(view.CloseVisible, Is.False, "런 끝 자동 연출 동안 Close 끔(노션)");
        }
    }
}
