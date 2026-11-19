using System;
using System.Globalization;
using MV.Data;
using MV.Run;

namespace MV.View
{
    /// <summary>
    /// 오브젝트 글자판 한 장의 내용(<see cref="ObjectLabel.Of"/> 결과). 글자판 프리팹 `Obj_Text_Count`의 세 칸
    /// (`Title`·`Count_Number`·`Count_Gauge`)에 그대로 들어간다. 프리팹이 없는 오브젝트는 숫자(없으면 제목)만 TMP 라벨로 쓴다.
    /// </summary>
    public readonly struct LabelContent
    {
        /// <summary>글자판 전체를 보일지.</summary>
        public readonly bool Visible;
        /// <summary>true면 프리팹에 디자이너가 넣은 제목 글자를 그대로 둔다(게이트 「계단」·「무기」).</summary>
        public readonly bool KeepTitle;
        /// <summary>제목 글자. null이면 제목 칸을 숨긴다(<see cref="KeepTitle"/>이면 무시).</summary>
        public readonly string Title;
        /// <summary>큰 숫자 칸. null이면 숨긴다.</summary>
        public readonly string Number;
        /// <summary>막대 채움 0~1. 0보다 작으면 막대를 숨긴다.</summary>
        public readonly float Gauge;

        public LabelContent(bool visible, bool keepTitle, string title, string number, float gauge)
        {
            Visible = visible;
            KeepTitle = keepTitle;
            Title = title;
            Number = number;
            Gauge = gauge;
        }

        public static readonly LabelContent Hidden = new LabelContent(false, false, null, null, NoGauge);
        public const float NoGauge = -1f;

    }

    /// <summary>
    /// 오브젝트 글자판 내용(정본 `updObjMesh` L1474~1534의 `drawLabel`, 문구 07 §7.12). 종류는 `105_objectKind.labelKind`가 정한다. 순수 계산.
    /// 그림(위치·크기·색)은 글자판 프리팹이 정하고(디자이너 리소스 = 시각 정본), 여기는 무엇을 언제 보일지만 정한다(새 HTML = 연출 정본).
    /// </summary>
    public static class ObjectLabel
    {
        /// <summary>마법서 탑 글자판: 플레이어가 탑 z − 1 m를 지나면 숨김(L1474 `o.z &gt; R.p.z − 1`).</summary>
        public const double TowerHideBack = 1;
        /// <summary>결계 블록 글자판: 같은 골목·같은 줄에서 앞에 남은 블록이 이 수보다 적을 때만(L1488 「각 줄 앞쪽 2칸만」).</summary>
        public const int WallShownRows = 2;
        /// <summary>계단문 숫자 앞 기호(노션 Obj_StairGate UI 「텍스트 포맷 : x{value}」). 언어와 무관한 기호라 언어팩 키를 두지 않는다.</summary>
        public const string StairCountPrefix = "x";

        // 문구(07 §7.12). 언어팩이 들어오기 전까지 코드 문구 + 키 주석(MV.UI와 같은 방식). 이름은 MV-SPEC-02 용어(TermIdMap 「이름만 다름」).
        public const string TowerText = "마법서 탑 · 맞히면 계단 Lv UP";          // ui.run.label.tower
        public const string StairText = "우다다 계단 · 모은 계단만큼 돌파";       // ui.run.label.stair
        public const string FloorNKey = "ui.run.floorN", AreaGateKey = "ui.run.areaGate";

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// 이 프레임의 글자판 내용. <paramref name="playerZ"/> = 플레이어 z, <paramref name="archHide"/> = `ARCH_LABEL_HIDE`,
        /// <paramref name="wallAhead"/> = 같은 골목·줄에서 이 블록 앞에 살아 있는 블록 수, <paramref name="goldMul"/> = 골드 배율(L1492 `goldMul()`).
        /// </summary>
        public static LabelContent Of(RunObject o, double playerZ, double archHide, int wallAhead, double goldMul)
        {
            var lk = o?.Kind != null ? o.Kind.labelKind : LabelKind.None;
            switch (lk)
            {
                case LabelKind.StairGate:
                    // L1509: 모은 계단 수 + 충전 막대. 제목은 프리팹 글자. 숫자 형식 「x{value}」(노션 Obj_StairGate UI, 등급별 「+N」 표기 없음).
                    return new LabelContent(true, true, null, StairCountPrefix + o.N.ToString(Inv), Gauge(o.Prog));
                case LabelKind.CraftGate:
                    // 노션 Obj_CraftGate UI: 문 안은 무기 아이콘(CraftGateBundle)과 다음 장까지 막대뿐, 숫자 글자판 없음. 장 수는 문 위 카드 묶음이 보인다.
                    return new LabelContent(true, true, null, null, Gauge(o.Prog));
                case LabelKind.WellText:
                    return new LabelContent(o.Z > playerZ - TowerHideBack, false, TowerText, null, LabelContent.NoGauge);
                case LabelKind.StairText:
                    return new LabelContent(!o.Used, false, StairText, null, LabelContent.NoGauge);
                case LabelKind.Arch:
                    // L1479: 관문 z − 3 m를 지나면 현판을 숨긴다(카메라를 가리지 않게).
                    return new LabelContent(!(playerZ > o.Z - archHide), false, ArchText(o), null, LabelContent.NoGauge);
                case LabelKind.WallCell:
                {
                    // L1487~1498: 앞쪽 2칸만, 보상 「+양」·체력·체력 막대.
                    if (o.Dead || wallAhead >= WallShownRows || !o.HasHp) return LabelContent.Hidden;
                    string rew = "+" + (o.Rew == FieldReward.Gold ? Fmt(o.RewardAmount * goldMul) : Fmt(o.RewardAmount));
                    return new LabelContent(true, false, rew, Fmt(Math.Max(0, o.Hp)), HpRatio(o));
                }
                case LabelKind.EnemyHp:
                    // L1521: 다치면 체력 숫자 + 체력 막대.
                    if (o.Dead || !o.HasHp || o.Hp >= o.Max) return LabelContent.Hidden;
                    return new LabelContent(true, false, null, Fmt(Math.Max(0, o.Hp)), HpRatio(o));
                case LabelKind.BoxHp:
                    // L1525~1526: 다치면 체력 숫자만.
                    if (o.Dead || !o.HasHp || o.Hp >= o.Max) return LabelContent.Hidden;
                    return new LabelContent(true, false, null, Fmt(Math.Max(0, o.Hp)), LabelContent.NoGauge);
                default:
                    return LabelContent.Hidden;
            }
        }

        /// <summary>결계문 현판 문구(L1791 `FLOOR_N`, L1801): 키는 생성기가 넣은 <see cref="RunObject.LabelKey"/>. 모르는 키는 키를 그대로 보여 드러낸다.</summary>
        public static string ArchText(RunObject o)
        {
            string key = !string.IsNullOrEmpty(o.LabelKey) ? o.LabelKey : o.Kind?.labelKey;
            if (key == FloorNKey) return "골목 " + o.LabelArg.ToString(Inv);              // ui.run.floorN 「골목 {0}」
            if (key == AreaGateKey) return "Area " + o.LabelArg.ToString(Inv) + " 돌파";  // ui.run.areaGate 「Area {0} 돌파」
            return key;
        }

        static float Gauge(double v) => (float)Math.Max(0, Math.Min(1, v));

        static float HpRatio(RunObject o) => o.Max > 0 ? Gauge(o.Hp / o.Max) : 0f;

        /// <summary>정본 `fmt(n)`(L855): floor 뒤 1,000,000 이상 x.xM, 10,000 이상 x.xK, 그 밖 천 단위 쉼표(MV.UI `MvUiFormat.Fmt`와 같은 규칙).</summary>
        public static string Fmt(double n)
        {
            if (double.IsNaN(n) || double.IsInfinity(n)) n = 0;
            n = Math.Floor(n);
            if (n >= 1000000) return Fixed1(n / 1000000) + "M";
            if (n >= 10000) return Fixed1(n / 1000) + "K";
            return n.ToString("#,0", Inv);
        }

        static string Fixed1(double v) => (Math.Floor(v * 10 + 0.5) / 10).ToString("0.0", Inv);
    }
}
