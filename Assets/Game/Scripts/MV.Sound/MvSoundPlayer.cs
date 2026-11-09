using System;
using System.Collections.Generic;
using MV.Data;
using MV.Run;

namespace MV.Sound
{
    /// <summary>BGM 레이어(정본 SND.intensity 0/1, D-57 두 믹스 파일).</summary>
    public enum MvBgmLayer { None, Lobby, Run }

    /// <summary>
    /// 소리 재생 본체(03 §3.9.5). key → 03_sound 행 → 음소거·GAP·피치 → <see cref="IMvAudioOut"/>.
    /// 시계·출력·피치 난수는 주입한다. 피치 난수는 런 결정성을 깨지 않게 RunRng가 아닌 별도 <see cref="Random"/>를 쓴다.
    /// // Framework 대체: GAP·피치 무작위는 com.jjevol.audio에 없어 게임이 한다(Framework debounce는 끄고 부른다).
    /// </summary>
    public sealed class MvSoundPlayer
    {
        /// <summary>레이어 전환 크로스페이드(03 §3.9.4 규칙 2).</summary>
        public const float LayerCrossfadeSeconds = 0.03f;

        readonly IMvAudioOut _out;
        readonly IMvSoundClock _clock;
        readonly Func<MvTables> _tables;
        readonly Random _pitchRng;
        readonly MvRunSoundRouter _router;
        readonly Dictionary<string, double> _last = new Dictionary<string, double>(StringComparer.Ordinal);
        readonly List<Pending> _pending = new List<Pending>();
        readonly List<Pending> _due = new List<Pending>();
        readonly List<MvSoundCue> _cues = new List<MvSoundCue>();
        readonly HashSet<string> _warned = new HashSet<string>(StringComparer.Ordinal);
        long _seq;
        bool _muted;

        struct Pending
        {
            public double At;
            public long Seq;
            public string Key;
        }

        public MvSoundPlayer(IMvAudioOut output, IMvSoundClock clock, Func<MvTables> tables = null, Random pitchRng = null)
        {
            _out = output ?? throw new ArgumentNullException(nameof(output));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _tables = tables ?? (() => MvTables.Current);
            _pitchRng = pitchRng ?? new Random();
            _router = new MvRunSoundRouter(_tables);
        }

        /// <summary>표에 없는 key 등 경고(key마다 1번). 기본은 Unity 콘솔.</summary>
        public Action<string> Warn { get; set; } = m => UnityEngine.Debug.LogWarning(m);

        public MvBgmLayer BgmLayer { get; private set; } = MvBgmLayer.None;

        /// <summary>로비 레이어 key(03 §3.9.3 bgm_lobby).</summary>
        public string LobbyBgmKey { get; set; } = MvSoundKeys.BgmLobby;

        /// <summary>런 레이어 key. 표 bgm 열(301·302·304·400, 빈칸이면 bgm_run)을 고른 쪽이 런 시작 전에 넣는다.</summary>
        public string RunBgmKey { get; set; } = MvSoundKeys.BgmRun;

        /// <summary>대기 중인 지연 재생 수.</summary>
        public int PendingCount => _pending.Count;

        /// <summary>
        /// 음소거(정본 ce_mute L1624 → 저장 설정). 켜면 효과음은 호출 자체를 막고(L1678, GAP 기록도 안 함),
        /// 음악은 트랙만 끄고 계속 흐른다(L1684).
        /// </summary>
        public bool Muted
        {
            get => _muted;
            set
            {
                _muted = value;
                _out.SetMusicOn(!value);
            }
        }

        /// <summary>
        /// 효과음 1회. 정본 <c>SND.play(k)</c>와 같은 순서: 음소거 → 표에 있음 → GAP(묶음 키, gapMs 안이면 버림) → 기록 → 재생.
        /// 울렸으면 true.
        /// </summary>
        public bool Play(string key)
        {
            if (_muted || string.IsNullOrEmpty(key)) return false;
            var t = _tables();
            if (t == null) return false;
            var row = t.Sound.ByKey(key);
            if (row == null || row.soundType != SoundType.Sfx)
            {
                WarnOnce(key, row == null ? $"[MV.Sound] 03_sound에 key `{key}`가 없습니다" : $"[MV.Sound] `{key}`는 효과음이 아닙니다");
                return false;
            }
            double now = _clock.Now;
            string group = MvSoundKeys.GapGroup(key);
            if (_last.TryGetValue(group, out double last) && (now - last) * 1000.0 < row.gapMs) return false;
            _last[group] = now;
            _out.PlaySfx(row.soundName, (float)row.volume, Pitch(row.pitchRand));
            return true;
        }

        /// <summary>delayMs 뒤 <see cref="Play"/>(정본 setTimeout). 음소거·GAP은 울릴 때 본다. 0 이하면 바로.</summary>
        public void PlayDelayed(string key, int delayMs)
        {
            if (delayMs <= 0) { Play(key); return; }
            _pending.Add(new Pending { At = _clock.Now + delayMs / 1000.0, Seq = _seq++, Key = key });
        }

        public void Play(MvSoundCue cue) => PlayDelayed(cue.Key, cue.DelayMs);

        /// <summary>지연 재생 중 때가 된 것을 울린다(매 프레임, <see cref="MvSoundDriver"/>).</summary>
        public void Tick()
        {
            if (_pending.Count == 0) return;
            double now = _clock.Now;
            _due.Clear();
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].At > now) continue;
                _due.Add(_pending[i]);
                _pending.RemoveAt(i);
            }
            if (_due.Count == 0) return;
            _due.Sort((a, b) => a.At != b.At ? a.At.CompareTo(b.At) : a.Seq.CompareTo(b.Seq));
            foreach (var p in _due) Play(p.Key);
            _due.Clear();
        }

        /// <summary>지연 재생을 버린다(런 강제 종료 등).</summary>
        public void ClearPending() => _pending.Clear();

        // ---------------- RunEvent ----------------

        /// <summary>런 이벤트 1개: RunStarted면 런 레이어, 그다음 정본 호출 지점의 소리.</summary>
        public void OnRunEvent(in RunEvent e)
        {
            if (e.Type == RunEventType.RunStarted) EnterRun();
            _cues.Clear();
            _router.Route(e, _cues);
            for (int i = 0; i < _cues.Count; i++) Play(_cues[i]);
        }

        /// <summary><c>RunSim.DrainEvents()</c> 결과를 순서대로.</summary>
        public void OnRunEvents(IEnumerable<RunEvent> events)
        {
            if (events == null) return;
            foreach (var e in events) OnRunEvent(e);
        }

        // ---------------- BGM ----------------

        public void EnterLobby() => SetBgmLayer(MvBgmLayer.Lobby);
        public void EnterRun() => SetBgmLayer(MvBgmLayer.Run);

        /// <summary>
        /// 03 §3.9.4: 처음은 그 레이어 음악을 처음부터, 레이어가 바뀌면 같은 재생 위치로 0.03초 크로스페이드. 같은 레이어면 아무것도 안 한다.
        /// 음소거와 상관없이 전환한다(정본 intensity는 음소거 중에도 바뀜).
        /// </summary>
        public void SetBgmLayer(MvBgmLayer layer)
        {
            if (layer == BgmLayer) return;
            var prev = BgmLayer;
            BgmLayer = layer;
            if (layer == MvBgmLayer.None) { _out.StopMusic(); return; }
            string key = layer == MvBgmLayer.Lobby ? LobbyBgmKey : RunBgmKey;
            var row = _tables()?.Sound.ByKey(key);
            if (row == null || row.soundType != SoundType.Music)
            {
                WarnOnce(key, $"[MV.Sound] 03_sound에 음악 key `{key}`가 없습니다");
                return;
            }
            bool keep = prev != MvBgmLayer.None;
            _out.PlayMusic(row.soundName, (float)row.volume, keep ? LayerCrossfadeSeconds : 0f, keep);
        }

        // ---------------- 내부 ----------------

        /// <summary>피치 = 1 ± pitchRand(균등). 0이면 난수를 뽑지 않고 1(정본 pitchRand 0).</summary>
        float Pitch(double rand)
        {
            if (!(rand > 0)) return 1f;
            return (float)(1.0 + (_pitchRng.NextDouble() * 2.0 - 1.0) * rand);
        }

        void WarnOnce(string key, string message)
        {
            if (_warned.Add(key ?? string.Empty)) Warn?.Invoke(message);
        }
    }
}
