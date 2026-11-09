namespace MV.Data
{
    /// <summary>
    /// 보상·비용 종류(기획서 07 §7.8.12 `07_consumeType`). 값 = 표의 typeId, 이름 = 표의 codeName.
    /// 검증기(<see cref="MvTableValidator"/>)가 표와 1:1인지 확인한다. 새 종류 = 표 행 + 여기 값 + 지급 처리 1개.
    /// Framework 표 제작 창의 ConsumeType 코드 생성으로 옮기면 이 파일을 지우고 생성 파일을 쓴다(TaskBoard MV-DATA 남김).
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
