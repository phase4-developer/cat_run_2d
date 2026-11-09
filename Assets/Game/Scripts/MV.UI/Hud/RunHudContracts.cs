using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// 런 HUD가 한 프레임에 읽는 값(05 §5.2.2·5.2.3·5.2.5·5.2.6). 숫자는 원값, 이름은 이미 찾은 문자열.
    /// </summary>
    public struct RunHudState
    {
        /// <summary><c>R.level</c>.</summary>
        public int Level;
        /// <summary>1기반 Area 번호(<c>save.area + 1</c>).</summary>
        public int AreaNo;
        /// <summary><c>name:&lt;302_area id&gt;</c>. 없으면 빈 문자열.</summary>
        public string AreaName;
        /// <summary><c>R.floor</c>. 0이면 플레이 구간.</summary>
        public int Floor;

        /// <summary>런 츄르 <c>R.gold</c>.</summary>
        public double Gold;
        /// <summary><c>save.souls + R.souls</c>.</summary>
        public int Souls;
        /// <summary>현재 Area <c>302_area.needSouls</c>.</summary>
        public int NeedSouls;
        /// <summary>런 캣닢 <c>R.dia</c>.</summary>
        public int Dia;

        /// <summary>계단 상자 숫자(평소 <c>R.stairs</c>, 오르기 중 남은 칸).</summary>
        public int Stairs;
        /// <summary>계단 오르기 날기 중(상자 <c>fly</c> 모양).</summary>
        public bool Flying;

        /// <summary><c>R.gauge.lv</c>.</summary>
        public int GaugeLv;
        /// <summary>0~1 채움(lv ≥ 8이면 1).</summary>
        public float GaugeFill;

        /// <summary>장착 카드 등급(<c>103_item.tier</c>).</summary>
        public int WeaponTier;
        public string WeaponName;
        /// <summary><c>103_item.icon</c>(Atlas_UI_Icon 스프라이트 이름).</summary>
        public string WeaponIcon;
        /// <summary><c>103_item.gradeCol</c>.</summary>
        public string WeaponGradeColor;
        /// <summary><c>103_item.dmg × (1 + 0.2 × 공격력 카드)</c>.</summary>
        public double WeaponDmg;
        /// <summary>고양이 모습 짧은 이름(<c>103_item.lookName</c>). 없으면 빈 문자열.</summary>
        public string LookName;
    }

    /// <summary>
    /// 런 HUD가 값을 읽는 곳. 게임 루프(MV-PLAY-INT)가 런 시뮬에 맞춰 넘긴다. 기본 구현은 <see cref="RunStateHudSource"/>.
    /// </summary>
    public interface IRunHudSource
    {
        /// <summary>런이 없으면 false(HUD는 마지막 값을 둔다).</summary>
        bool TryRead(out RunHudState state);
    }

    /// <summary>
    /// 런 HUD의 겉(D-92). 컨트롤러 <see cref="RunHud"/>는 값이 바뀐 항목만 이 인터페이스로 넘긴다.
    /// 겉을 갈아 끼울 때 이 인터페이스를 구현한 컴포넌트를 프리팹에 두면 된다.
    /// </summary>
    public interface IRunHudView
    {
        void SetLevel(string text);
        void SetFloor(string text);
        void SetGold(string text);
        void SetSouls(string text);
        void SetDia(string text);
        void SetStairs(string text, bool flying);
        void SetGauge(string levelText, string perText, float fill01);
        void SetWeapon(int tier, string iconSprite, string name, string damageText, Color gradeColor);
    }
}
