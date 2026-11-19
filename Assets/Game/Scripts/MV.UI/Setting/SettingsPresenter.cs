using System;
using MV.Meta;
using MV.Save;
using MV.Sound;

namespace MV.UI
{
    /// <summary>설정 팝업 값(볼륨 0~1). 저장은 0~100 정수(<c>mvSettings</c>).</summary>
    public struct SettingsValues
    {
        public float BgmVolume;
        public float SfxVolume;
        public bool BgmMute;
        public bool SfxMute;
        public bool Haptic;
    }

    /// <summary>설정 읽기·저장 입구. 기본은 <see cref="MvSettingsPort"/>(저장 = 메타 요청), 시험은 가짜.</summary>
    public interface ISettingsPort
    {
        /// <summary>저장된 값. 저장을 못 열었으면 기본값.</summary>
        SettingsValues Read();
        /// <summary>소리 설정 저장. 성공이면 true.</summary>
        bool SaveSound(SettingsValues v);
        /// <summary>진동 저장. 성공이면 true.</summary>
        bool SaveHaptic(bool on);
        /// <summary>소리·진동 모듈에 바로 적용(저장과 별개).</summary>
        void Apply(SettingsValues v, bool bgmMuted, bool sfxMuted);
    }

    /// <summary>저장 <c>mvSettings</c>를 읽고 메타 요청(<c>meta.setSound</c>·<c>meta.setHaptic</c>)으로 쓴다. 적용은 MvSound·MvHaptics.</summary>
    public sealed class MvSettingsPort : ISettingsPort
    {
        public static readonly MvSettingsPort Instance = new MvSettingsPort();

        /// <summary>저장을 못 열었을 때(정크버스터즈 기본과 같음): 배경음 0.9, 효과음 1, Mute 꺼짐, 진동 켜짐.</summary>
        public static SettingsValues Defaults => new SettingsValues { BgmVolume = 0.9f, SfxVolume = 1f, Haptic = true };

        public SettingsValues Read()
        {
            var store = MvSave.Current;
            if (store == null || store.IsDisposed) return Defaults;
            var s = store.Settings;
            return new SettingsValues
            {
                BgmVolume = s.BgmVolume / 100f,
                SfxVolume = s.SfxVolume / 100f,
                BgmMute = s.BgmMute,
                SfxMute = s.SfxMute,
                Haptic = s.Haptic,
            };
        }

        public bool SaveSound(SettingsValues v)
        {
            var meta = MvMeta.Current;
            if (meta == null || !meta.IsReady) return false;
            return meta.SetSound(SettingsPresenter.ToPercent(v.BgmVolume), SettingsPresenter.ToPercent(v.SfxVolume), v.BgmMute, v.SfxMute).Ok;
        }

        public bool SaveHaptic(bool on)
        {
            var meta = MvMeta.Current;
            return meta != null && meta.IsReady && meta.SetHaptic(on).Ok;
        }

        public void Apply(SettingsValues v, bool bgmMuted, bool sfxMuted)
        {
            MvSound.ApplySettings(v.BgmVolume, v.SfxVolume, bgmMuted, sfxMuted);
            MvHaptics.IsOn = v.Haptic;
        }
    }

    /// <summary>
    /// 설정 팝업 판단(노션 [팝업] 설정 = 정크버스터즈 설정과 같은 기능, 계정·버전·UID 제외, MV-UI2-SETTING).
    /// 슬라이더는 바로 적용하고 저장은 화면이 잠시 뒤 <see cref="FlushSound"/>로(정크버스터즈 1초 뒤 저장), Mute·진동은 바로 저장한다.
    /// 볼륨이 0이면 음소거로 보고, 0에서 Mute를 풀면 0.5로 되돌린다(정크버스터즈와 같음).
    /// </summary>
    public sealed class SettingsPresenter
    {
        /// <summary>이 값 이하 볼륨은 음소거로 본다(정크버스터즈 0.0001).</summary>
        public const float SilentVolume = 0.0001f;
        /// <summary>0 볼륨에서 Mute를 풀 때 되돌릴 볼륨(정크버스터즈 0.5).</summary>
        public const float UnmuteVolume = 0.5f;

        readonly ISettingsPort _port;
        SettingsValues _v;

        public SettingsPresenter(ISettingsPort port) => _port = port ?? throw new ArgumentNullException(nameof(port));

        public SettingsValues Values => _v;
        /// <summary>슬라이더가 바꿨고 아직 저장하지 않은 소리 값이 있다.</summary>
        public bool SoundDirty { get; private set; }

        /// <summary>화면에 보일 음소거(Mute를 켰거나 볼륨이 0).</summary>
        public bool BgmMuted => _v.BgmMute || _v.BgmVolume <= SilentVolume;
        public bool SfxMuted => _v.SfxMute || _v.SfxVolume <= SilentVolume;

        /// <summary>값이 바뀌면 화면을 다시 그린다.</summary>
        public event Action Changed;

        public void Open()
        {
            _v = _port.Read();
            SoundDirty = false;
            Changed?.Invoke();
        }

        public void SetBgmVolume(float v)
        {
            _v.BgmVolume = Clamp01(v);
            SoundDirty = true;
            ApplyAndNotify();
        }

        public void SetSfxVolume(float v)
        {
            _v.SfxVolume = Clamp01(v);
            SoundDirty = true;
            ApplyAndNotify();
        }

        public void SetBgmMute(bool mute)
        {
            _v.BgmMute = mute;
            if (!mute && _v.BgmVolume <= SilentVolume) _v.BgmVolume = UnmuteVolume;
            ApplyAndNotify();
            FlushSound(true);
        }

        public void SetSfxMute(bool mute)
        {
            _v.SfxMute = mute;
            if (!mute && _v.SfxVolume <= SilentVolume) _v.SfxVolume = UnmuteVolume;
            ApplyAndNotify();
            FlushSound(true);
        }

        public void SetHaptic(bool on)
        {
            if (_v.Haptic == on) return;
            _v.Haptic = on;
            ApplyAndNotify();
            _port.SaveHaptic(on);
        }

        /// <summary>바뀐 소리 값을 저장한다. force면 슬라이더가 안 바꿨어도 저장. 저장했으면 true.</summary>
        public bool FlushSound(bool force = false)
        {
            if (!force && !SoundDirty) return false;
            SoundDirty = false;
            return _port.SaveSound(_v);
        }

        void ApplyAndNotify()
        {
            _port.Apply(_v, BgmMuted, SfxMuted);
            Changed?.Invoke();
        }

        /// <summary>0~1 → 저장 정수 0~100(반올림).</summary>
        public static int ToPercent(float v01) => (int)Math.Round(Clamp01(v01) * 100f, MidpointRounding.AwayFromZero);

        static float Clamp01(float v) => float.IsNaN(v) ? 0f : v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
