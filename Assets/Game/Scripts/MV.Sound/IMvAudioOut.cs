using UnityEngine;

namespace MV.Sound
{
    /// <summary>
    /// 재생 출력(03 §3.9.5). 실제 구현은 <see cref="MvAudioManagerOut"/>(Framework AudioManager), 시험은 요청을 기록하는 대역.
    /// 주소는 <c>03_sound.soundName</c>(Addressables 주소)이다.
    /// </summary>
    public interface IMvAudioOut
    {
        /// <summary>효과음 1회(트랙 Sfx, Framework debounce 끔).</summary>
        void PlaySfx(string address, float volume, float pitch);

        /// <summary>
        /// 음악 교체(반복 재생). keepPosition이면 지금 음악의 재생 위치(timeSamples)를 새 음악에 이어 붙인다(D-57 두 믹스 같은 위치 전환).
        /// </summary>
        void PlayMusic(string address, float volume, float crossfadeSeconds, bool keepPosition);

        /// <summary>음악 정지.</summary>
        void StopMusic();

        /// <summary>음악 트랙 켜기/끄기. 음소거 중에도 음악은 계속 흐른다(정본 L1684).</summary>
        void SetMusicOn(bool on);
    }

    /// <summary>GAP·지연 재생 시계(초). 실제는 <see cref="Time.unscaledTimeAsDouble"/>, 시험은 주입.</summary>
    public interface IMvSoundClock
    {
        double Now { get; }
    }

    /// <summary>정본 GAP은 오디오 컨텍스트 실시간(L1678) → 시간 배율·히트스톱과 무관한 unscaled 시간.</summary>
    public sealed class MvUnscaledClock : IMvSoundClock
    {
        public double Now => Time.unscaledTimeAsDouble;
    }
}
