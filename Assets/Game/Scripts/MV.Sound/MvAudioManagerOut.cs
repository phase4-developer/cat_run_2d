using System;
using System.Threading.Tasks;
using jjevol.Audio;
using UnityEngine;

namespace MV.Sound
{
    /// <summary>
    /// <see cref="IMvAudioOut"/>의 Framework 구현(03 §3.9.5): 효과음 <c>AudioManager.PlayAsync(soundName, AudioTrack.Sfx, volume, pitch, false, debounce: 0f)</c>,
    /// 음악 <c>PlayMusicAsync</c>. 주소 로드는 audio.addressables 공급자(Boot가 설정).
    /// </summary>
    public sealed class MvAudioManagerOut : IMvAudioOut
    {
        static bool Ready => !AudioManager.applicationIsQuitting;

        public void PlaySfx(string address, float volume, float pitch)
        {
            if (!Ready) return;
            Observe(AudioManager.Instance.PlayAsync(address, AudioTrack.Sfx, volume, pitch, false, 0f));
        }

        public void PlayMusic(string address, float volume, float crossfadeSeconds, bool keepPosition)
        {
            if (!Ready) return;
            PlayMusicCore(address, volume, crossfadeSeconds, keepPosition);
        }

        /// <summary>
        /// 03 §3.9.4 규칙 2: 요청 순간의 timeSamples와 시각을 잡아 두고, 새 음악이 시작되면 로드 동안 흐른 샘플만큼 더해 넣는다
        /// (두 믹스는 길이·샘플레이트가 같다).
        /// </summary>
        async void PlayMusicCore(string address, float volume, float crossfadeSeconds, bool keepPosition)
        {
            try
            {
                var am = AudioManager.Instance;
                var prev = am.CurrentMusic;
                bool keep = keepPosition && prev != null && prev.clip != null;
                int pos = keep ? prev.timeSamples : 0;
                double t0 = Time.unscaledTimeAsDouble;
                var src = await am.PlayMusicAsync(address, volume, 1f, crossfadeSeconds);
                if (!keep || src == null || src.clip == null || src.clip.samples <= 0) return;
                long elapsed = (long)Math.Round((Time.unscaledTimeAsDouble - t0) * src.clip.frequency);
                src.timeSamples = (int)((pos + elapsed) % src.clip.samples);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        public void StopMusic()
        {
            if (!Ready) return;
            AudioManager.Instance.StopAllOnTrack(AudioTrack.Music);
        }

        public void SetMusicOn(bool on)
        {
            if (!Ready) return;
            AudioManager.Instance.SetTrackOn(AudioTrack.Music, on);
        }

        static async void Observe(Task<AudioSource> task)
        {
            try { await task; }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Debug.LogException(ex); }
        }
    }
}
