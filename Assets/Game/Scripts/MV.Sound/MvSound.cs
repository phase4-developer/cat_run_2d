using System.Collections.Generic;
using MV.Data;
using MV.Run;

namespace MV.Sound
{
    /// <summary>
    /// 게임 전역 소리 입구. Boot가 <see cref="InstallDefault"/>로 한 번 세우고, MV.UI는 <see cref="PlayUi(MvUiSound)"/>,
    /// 런 화면은 <c>RunSim.DrainEvents()</c> 결과를 <see cref="OnRunEvents"/>로 넘긴다. 세우기 전 호출은 무시한다.
    /// </summary>
    public static class MvSound
    {
        public static MvSoundPlayer Player { get; private set; }

        /// <summary>시험·교체용. null이면 끈다.</summary>
        public static void Install(MvSoundPlayer player) => Player = player;

        /// <summary>AudioManager 출력 + unscaled 시계 + MvTables.Current + 지연 재생 드라이버.</summary>
        public static MvSoundPlayer InstallDefault()
        {
            var p = new MvSoundPlayer(new MvAudioManagerOut(), new MvUnscaledClock(), () => MvTables.Current, new System.Random());
            Install(p);
            MvSoundDriver.Ensure();
            return p;
        }

        /// <summary>UI 정본 호출 지점 소리(지연 포함).</summary>
        public static void PlayUi(MvUiSound sound) => Player?.Play(MvUiSounds.Cue(sound));

        /// <summary>03_sound key 직접(새 UI용). 울렸으면 true.</summary>
        public static bool PlayUi(string key) => Player != null && Player.Play(key);

        public static bool Muted
        {
            get => Player != null && Player.Muted;
            set { if (Player != null) Player.Muted = value; }
        }

        /// <summary>로비 BGM 레이어(정본 toLobby L2473 intensity 0). 부팅 후 첫 로비에서도 부른다.</summary>
        public static void EnterLobby() => Player?.EnterLobby();

        /// <summary>런 BGM 레이어(정본 startRun L2661 intensity 1). RunStarted 이벤트가 오면 자동.</summary>
        public static void EnterRun() => Player?.EnterRun();

        public static void OnRunEvents(IEnumerable<RunEvent> events) => Player?.OnRunEvents(events);

        public static void Tick() => Player?.Tick();
    }
}
