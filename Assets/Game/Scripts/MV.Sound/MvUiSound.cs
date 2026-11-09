namespace MV.Sound
{
    /// <summary>
    /// UI 쪽 정본 SND 호출 지점(MV-UI-*가 <see cref="MvSound.PlayUi(MvUiSound)"/>로 부른다). 런 이벤트가 아닌 것만 있다.
    /// 조건(덱 머지일 때만, 런 중일 때만, 경고가 처음 켜질 때만)은 부르는 쪽이 판단한다.
    /// </summary>
    public enum MvUiSound
    {
        /// <summary>버튼 기본(UIButtonSound), 결과 「다음」 L2444, 「×3」 L2445, 지도 L2610, 소리 버튼 L3856(음소거 전환 뒤).</summary>
        ButtonClick,
        /// <summary>로비 강화 구매 L2644.</summary>
        LobbyUpgradeBuy,
        /// <summary>로비 무기 강화로 덱이 머지됐을 때, 450ms 뒤 L2644.</summary>
        LobbyDeckMerge,
        /// <summary>Area 강화 구매 L2530.</summary>
        AreaUpgradeBuy,
        /// <summary>부활 특성 구매 L2553.</summary>
        RebirthPerkBuy,
        /// <summary>부활 완료 L2563.</summary>
        RebirthDone,
        /// <summary>신기능 해금 오버레이 L2613.</summary>
        UnlockOverlay,
        /// <summary>HAND 카드 머지 L1193.</summary>
        HandMerge,
        /// <summary>장착 등급 상승 「변신!」(런 중만) L1221.</summary>
        HandTierUp,
        /// <summary>전방 장애물 경고가 처음 켜질 때 L2739.</summary>
        ObstacleWarn,
    }

    public static class MvUiSounds
    {
        /// <summary>로비 덱 머지 소리 지연(정본 L2644 setTimeout 450). 표 열 없음.</summary>
        public const int LobbyDeckMergeDelayMs = 450;

        public static MvSoundCue Cue(MvUiSound s)
        {
            switch (s)
            {
                case MvUiSound.ButtonClick: return new MvSoundCue(MvSoundKeys.Click);
                case MvUiSound.LobbyUpgradeBuy: return new MvSoundCue(MvSoundKeys.Buy);
                case MvUiSound.LobbyDeckMerge: return new MvSoundCue(MvSoundKeys.Merge, LobbyDeckMergeDelayMs);
                case MvUiSound.AreaUpgradeBuy: return new MvSoundCue(MvSoundKeys.Buy);
                case MvUiSound.RebirthPerkBuy: return new MvSoundCue(MvSoundKeys.Lvup);
                case MvUiSound.RebirthDone: return new MvSoundCue(MvSoundKeys.Win);
                case MvUiSound.UnlockOverlay: return new MvSoundCue(MvSoundKeys.Lvup);
                case MvUiSound.HandMerge: return new MvSoundCue(MvSoundKeys.Merge);
                case MvUiSound.HandTierUp: return new MvSoundCue(MvSoundKeys.Lvup);
                case MvUiSound.ObstacleWarn: return new MvSoundCue(MvSoundKeys.Warn);
                default: return default;
            }
        }
    }
}
