using System;
using System.Collections.Generic;
using UnityEngine;

namespace MV.Data
{
    /// <summary>난이도 프리셋(`06_balancePreset.key`) 하나의 기본 연사(초당 발사 수).</summary>
    [Serializable]
    public sealed class PresetFireRate
    {
        [MvLabel("난이도 (key)"), Tooltip("06_balancePreset의 key(easy·mid·orig)")] public string key;
        [MvLabel("초당 발사 수 (rate)"), Tooltip("기본 연사. 발사 간격 = 1 ÷ (이 값 × 카드·강화·연구 배율). 키우면 더 빨리 쏜다")] public double rate;
    }

    /// <summary>
    /// 런 시뮬이 읽는 캐릭터 기본값(UnityEngine 타입 없음 — MV.Run이 바로 읽는다). 정본은 <see cref="MvCharacterSettings"/> 에셋이고
    /// 이 객체는 그 사본이다. 장비·카드·연구 등으로 바뀌는 최종 능력치는 담지 않는다(런 상태가 계산한다).
    /// </summary>
    public sealed class RunCharacterConfig
    {
        /// <summary>전진 속도(m/s).</summary>
        public double RunSpeed;
        /// <summary>템플릿 중간 구역 전진 속도(m/s, 310 midFromZ~midToZ).</summary>
        public double RunSpeedMid;
        /// <summary>키 이동 속도(m/s).</summary>
        public double KeyMoveSpeed;
        /// <summary>x 추종 계수(1/s, 지수 추종).</summary>
        public double MoveFollowK;
        /// <summary>프리셋 key → 기본 연사(초당 발사 수).</summary>
        public readonly Dictionary<string, double> PresetRates = new Dictionary<string, double>(StringComparer.Ordinal);

        public bool TryRate(string presetKey, out double rate)
        {
            rate = 0;
            return presetKey != null && PresetRates.TryGetValue(presetKey, out rate);
        }

        /// <summary>프리셋 기본 연사. 없으면 오류(대체 값으로 가리지 않는다).</summary>
        public double Rate(string presetKey) =>
            TryRate(presetKey, out double r) ? r : throw new InvalidOperationException($"MvCharacterSettings: 프리셋 '{presetKey}'의 기본 연사가 없습니다");

        public RunCharacterConfig Clone()
        {
            var c = new RunCharacterConfig { RunSpeed = RunSpeed, RunSpeedMid = RunSpeedMid, KeyMoveSpeed = KeyMoveSpeed, MoveFollowK = MoveFollowK };
            foreach (var kv in PresetRates) c.PresetRates[kv.Key] = kv.Value;
            return c;
        }
    }

    /// <summary>
    /// 캐릭터 이동·공격속도 기본값의 정본(2026-11-16 사용자 지시: 이동 4개 + 프리셋 연사를 표에서 옮김).
    /// `Resources`에 있어 편집 모드·EditMode 시험·Play Mode·빌드가 같은 에셋을 읽는다. 런타임은 <see cref="ToConfig"/> 사본만 쓴다.
    /// </summary>
    [CreateAssetMenu(fileName = "MvCharacterSettings", menuName = "MoonVillage/Character Settings")]
    public sealed class MvCharacterSettings : ScriptableObject
    {
        public const string ResourcePath = "MvCharacterSettings";
        public const string AssetPath = "Assets/Game/GameData/Settings/Resources/MvCharacterSettings.asset";

        [Header("이동 — Play 중에 바꾸면 달리는 중인 런에 바로 반영")]
        [MvLabel("전진 속도 (runSpeed)"), Tooltip("앞으로 달리는 속도(m/초)")] public double runSpeed;
        [MvLabel("중간 구역 전진 속도 (runSpeedMid)"), Tooltip("템플릿 중간 구역(310 midFromZ~midToZ)에서 달리는 속도(m/초)")] public double runSpeedMid;
        [MvLabel("키보드 좌우 이동 속도 (keyMoveSpeed)"), Tooltip("←/→ 키를 누를 때 목표 위치가 움직이는 속도(m/초). 개발용 입력")] public double keyMoveSpeed;
        [MvLabel("좌우 따라가기 빠르기 (moveFollowK)"), Tooltip("드래그한 위치로 고양이가 따라가는 빠르기(1/초). 10이면 약 0.1초에 63%. 키우면 더 민첩하다")] public double moveFollowK;

        [Header("공격속도 — 난이도별 기본 연사")]
        [Tooltip("난이도 프리셋(06_balancePreset.key)마다 기본 연사(초당 발사 수). 발사 간격 = 1 ÷ (연사 × 카드·강화·연구 배율)")]
        public List<PresetFireRate> presetFireRates = new List<PresetFireRate>();

        /// <summary>Inspector에서 값을 바꿨을 때(에디터 전용 알림). Play 중이면 <see cref="MvTables"/>가 사본을 새로 만든다.</summary>
        public event Action Changed;

        void OnValidate() => Changed?.Invoke();

        /// <summary>런타임 사본(에셋은 바꾸지 않는다).</summary>
        public RunCharacterConfig ToConfig()
        {
            var c = new RunCharacterConfig { RunSpeed = runSpeed, RunSpeedMid = runSpeedMid, KeyMoveSpeed = keyMoveSpeed, MoveFollowK = moveFollowK };
            foreach (var p in presetFireRates)
            {
                if (p == null || string.IsNullOrEmpty(p.key)) continue;
                if (c.PresetRates.ContainsKey(p.key)) throw new InvalidOperationException($"MvCharacterSettings: 프리셋 '{p.key}'가 두 번 있습니다");
                c.PresetRates[p.key] = p.rate;
            }
            return c;
        }

        /// <summary>Resources의 에셋. 없으면 오류.</summary>
        public static MvCharacterSettings Load()
        {
            var s = Resources.Load<MvCharacterSettings>(ResourcePath);
            if (s == null) throw new InvalidOperationException("MvCharacterSettings 에셋이 없습니다: " + AssetPath);
            return s;
        }

        /// <summary>Resources의 에셋을 읽어 사본을 만든다. 없으면 오류.</summary>
        public static RunCharacterConfig LoadConfig() => Load().ToConfig();
    }
}
