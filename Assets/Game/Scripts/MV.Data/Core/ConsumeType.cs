namespace MV.Data
{
    /// <summary>
    /// 보상·비용 종류(기획서 07 §7.8.12 `07_consumeType`). 값 = 표의 typeId, 이름 = 표의 codeName.
    /// 검증기(<see cref="MvTableValidator"/>)가 표와 1:1인지 확인한다. 새 종류 = 표 행 + 여기 값 + 지급 처리 1개.
    /// 생성 연결은 MV.Editor `MvConsumeTypeCodegen`(Framework ConsumeTypes 생성기, 출력 `Core/Generated/`). 이 손 enum이 컴파일되어 있으면
    /// 첫 생성이 막히고(ConsumeTypeCollision), 지우면 컴파일 오류로 막혀 고정본 0.1.0-pre.7에는 이관 경로가 없다(TaskBoard MV-DATA). None(0)은 표 typeId 0 행.
    /// <see cref="RewardValueRule"/>는 표에서 만들지 않으므로 이관 뒤에도 이 파일에 남긴다.
    /// </summary>
    public enum ConsumeType
    {
        None = 0,
        Wealth = 1,
        PlayResource = 3,
        Item = 5,
        RandomBox = 8,
        Drop = 50,
        AD = 100,
        Inapp = 101,
        DailyQuestPoint = 300,
        WeeklyQuestPoint = 301,
        Pass = 500,
        StairLv = 600,
        Card = 601,
        RunGold = 602,
        RunSouls = 603,
        RunDia = 604,
        RunStairs = 605,
        RunWeapon = 606,
        GaugeFill = 607,
        RbPending = 608,
        RebirthCooldownSec = 609,
        Gear = 610,
        PetCard = 620,
        FriendCoin = 621,
        GodShard = 622,
        Blessing = 630,
    }

    /// <summary>런 보상 값 규칙(07 §7.1.7 `{p}ValueRule`).</summary>
    public enum RewardValueRule
    {
        Flat,
        DiaCap,
        HalfDiaCapCeil,
    }
}
