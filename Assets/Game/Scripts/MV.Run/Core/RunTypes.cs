using System.Collections.Generic;

namespace MV.Run
{
    /// <summary>프로토 mode(04 §4.13.1). 룰렛은 겹침 상태라 <see cref="RunState.Roulette"/>로 따로 둔다.</summary>
    public enum RunPhase { Lobby, Run, Climb, Result }

    /// <summary>종료 사유(04 §4.11.1, §4.20.2).</summary>
    public enum FinishReason { None, Gate, Wall, ModeClear, ModeFail }

    /// <summary>모드 종료 판정(04 §4.20.2 ④).</summary>
    public enum RunEnd { None, Clear, Fail }

    /// <summary>골목 장애물 칸 보상(fieldSpec rew).</summary>
    public enum FieldReward { Gold, Soul, Dia }

    /// <summary>피해 숫자 색 키(04 §4.18 Hit.colorKey).</summary>
    public enum HitColor { Normal, Crit, GodBolt, GodFire, Burn, Beam, Meteor, Blast }

    /// <summary>피해 출처(04 §4.20.2 HitInfo.source).</summary>
    public enum HitSource { Projectile, Spear, GodProc, Chain, Storm, Blast, Burn, Orb, Spin, Beam, Meteor, Companion, Mode }

    /// <summary>
    /// 한 Tick의 입력(04 §4.1.2). 화면 px → m 변환은 MV.UI 몫이다.
    /// <see cref="DragTargetX"/>: 드래그 중이면 목표 tx(= 누른 순간 tx + 이동 m, clamp 전). null이면 드래그 없음.
    /// <see cref="KeyAxis"/>: −1/0/+1 (← →). 실시간 dt로 tx에 더한다.
    /// </summary>
    public struct RunInput
    {
        public double? DragTargetX;
        public int KeyAxis;

        public static RunInput None => default;
        public static RunInput Drag(double tx) => new RunInput { DragTargetX = tx };
        public static RunInput Key(int axis) => new RunInput { KeyAxis = axis };
    }

    /// <summary>동행 자동 사격 발동 방식(04 §4.20.5, 08 skillTrigger 번호).</summary>
    public enum CompanionTrigger { Hits = 1, Chance = 2, Seconds = 3 }

    /// <summary>동행 1개(04 §4.20.5). 순서 = 처리 순서.</summary>
    public readonly struct CompanionSpec
    {
        public readonly int SourceId;
        public readonly CompanionTrigger Trigger;
        public readonly double Every;
        public readonly double DmgPct;
        public readonly int Targets;

        public CompanionSpec(int sourceId, CompanionTrigger trigger, double every, double dmgPct, int targets)
        {
            SourceId = sourceId; Trigger = trigger; Every = every; DmgPct = dmgPct; Targets = targets;
        }
    }

    /// <summary>메타 능력치 7필드, % 단위(04 §4.20.4). 0이면 정본과 비트 단위로 같다.</summary>
    public struct RunModifiers
    {
        public double Dmg, FireRate, Range, CritDmg, Gold, CritChance, DoubleChance;
        public static RunModifiers Zero => default;

        /// <summary>검증기(§4.20.4): FireRate > −100, 나머지 ≥ 0(NaN 금지). 문제가 없으면 null, 있으면 첫 문제.</summary>
        public string Validate()
        {
            if (!(FireRate > -100)) return $"RunModifiers.FireRate {FireRate}: −100보다 커야 합니다";
            if (!(Dmg >= 0)) return Bad(nameof(Dmg), Dmg);
            if (!(Range >= 0)) return Bad(nameof(Range), Range);
            if (!(CritDmg >= 0)) return Bad(nameof(CritDmg), CritDmg);
            if (!(Gold >= 0)) return Bad(nameof(Gold), Gold);
            if (!(CritChance >= 0)) return Bad(nameof(CritChance), CritChance);
            if (!(DoubleChance >= 0)) return Bad(nameof(DoubleChance), DoubleChance);
            return null;
        }

        static string Bad(string name, double v) => $"RunModifiers.{name} {v}: 0 이상이어야 합니다(감소 효과는 아직 없다)";
    }

    /// <summary>
    /// 장애물 HP 기록 `save.bars['A'+area]`(04 §4.7.4). Floors[f][idx] = 남은 HP(0 이상). f = 0..2.
    /// </summary>
    public sealed class BarRecord
    {
        public readonly Dictionary<int, double>[] Floors;

        public BarRecord(int floorCount = 3)
        {
            Floors = new Dictionary<int, double>[floorCount];
            for (int i = 0; i < floorCount; i++) Floors[i] = new Dictionary<int, double>();
        }

        public bool TryGet(int floor, int idx, out double hp)
        {
            hp = 0;
            return floor >= 0 && floor < Floors.Length && Floors[floor].TryGetValue(idx, out hp);
        }

        public void Set(int floor, int idx, double hp) => Floors[floor][idx] = hp;

        public BarRecord Clone()
        {
            var c = new BarRecord(Floors.Length);
            for (int f = 0; f < Floors.Length; f++)
                foreach (var kv in Floors[f]) c.Floors[f][kv.Key] = kv.Value;
            return c;
        }
    }

    /// <summary>골목 장애물 칸 1개(fieldSpec 결과, 04 §4.14.3). GEN이 만든다.</summary>
    public readonly struct FieldCell
    {
        public readonly int Idx, R, L;
        public readonly double Hp;
        public readonly FieldReward Rew;
        public readonly int Amt;

        public FieldCell(int idx, int r, int l, double hp, FieldReward rew, int amt)
        {
            Idx = idx; R = r; L = l; Hp = hp; Rew = rew; Amt = amt;
        }
    }

    /// <summary>
    /// 런 동안 바뀌지 않는 save 스냅숏 + 모드 필드(04 §4.14.5, §4.20.3). 런 중 save를 읽는 곳은 모두 이것을 쓴다.
    /// Area·Zone은 프로토처럼 0기반(`save.area`). 표의 areaNo = Area + 1.
    /// </summary>
    public sealed class RunSetup
    {
        public ulong Seed;
        public int Area;
        public int Zone;
        public int Level = 1;
        /// <summary>누적 구출 고양이(재화 rescueCat, MV-SPEC-02 Q-34). 개방 판정(§4.11.1)에 쓴다.</summary>
        public int Souls;
        public int Cp;
        public BarRecord Bars;

        // 로비 강화 up.{stair, weapon, income}
        public int UpStair, UpWeapon, UpIncome;
        /// <summary>시작 덱(등급별 장수, 0번 미사용). 기본 9칸은 빈 입력·시험용이고 MvRunStart가 저장 덱 길이로 바꾼다. 런 W 길이는 103_item에서 따로 잡는다.</summary>
        public int[] Deck = new int[9];
        // Area 강화 aup.{rate, range, clone, crit, dbl}
        public int AupRate, AupRange, AupClone, AupCrit, AupDbl;
        /// <summary>Area 강화 관통 확률 Lv(13_areaUpgrade statKey PierceChance, MV-SPEC-02 Q-28).</summary>
        public int AupPierce;
        // 부활 rb.perks.{power, crit, obst, dcap}
        public int PerkPower, PerkCrit, PerkObst, PerkDcap;
        /// <summary>냥환생 「지팡이 제작 2배」·「계단 2배」 Lv(14_rebirthPerk statKey CraftDouble·StairDouble, MV-SPEC-02 Q-27).</summary>
        public int PerkCraftDbl, PerkStairDbl;
        /// <summary>출전 신 `113_god.key`.</summary>
        public string God = "thunder";
        /// <summary>`06_balancePreset.key`(D-03 기본 easy).</summary>
        public string Preset = "easy";
        /// <summary>디버그 발사 속도(DBG.rate). null이면 프리셋 rate.</summary>
        public double? RateOverride;
        /// <summary>디버그 배속 DBG.ts(정수 1~3).</summary>
        public int TimeScale = 1;

        // ---- 모드 필드(04 §4.20.3) — 기본값 = 정본 ----
        /// <summary>null이면 `300_runConfig.RUN_SPEED`.</summary>
        public double? ForwardSpeed;
        public bool FailOnWall = true;
        public int FixedWeaponTier;
        public int CraftPerMade = 1;
        /// <summary>null이면 `302_area.hp`.</summary>
        public double? ZoneHp;
        public double WallHpMul = 1;
        public bool UseCheckpoint = true;
        public bool KeepBars = true;
        /// <summary>0이면 `111_cardPool[main]`.</summary>
        public int CardPoolId;
        public int Layout;
        public RunModifiers Modifiers;
        public List<CompanionSpec> Companions = new List<CompanionSpec>();

        /// <summary>동행 최대 수(펫 2 + 친구 3, §4.20.5).</summary>
        public const int MaxCompanions = 5;

        /// <summary>모디파이어·동행 검증. 문제가 없으면 null(<see cref="RunSim.Create(RunSetup, MV.Data.MvTables, IRng, IRunLevelBuilder, IRunRule)"/>가 부른다).</summary>
        public string Validate()
        {
            var err = Modifiers.Validate();
            if (err != null) return err;
            if (Companions == null) return null;
            if (Companions.Count > MaxCompanions) return $"동행 {Companions.Count}개: 최대 {MaxCompanions}개";
            for (int i = 0; i < Companions.Count; i++)
            {
                var c = Companions[i];
                if (c.Trigger != CompanionTrigger.Hits && c.Trigger != CompanionTrigger.Chance && c.Trigger != CompanionTrigger.Seconds)
                    return $"동행 {i}({c.SourceId}): Trigger {(int)c.Trigger}";
                if (!(c.Every > 0)) return $"동행 {i}({c.SourceId}): Every {c.Every}는 0보다 커야 합니다";
                if (!(c.DmgPct >= 0)) return $"동행 {i}({c.SourceId}): DmgPct {c.DmgPct}";
                if (c.Targets < 1) return $"동행 {i}({c.SourceId}): Targets {c.Targets}는 1 이상";
            }
            return null;
        }

        /// <summary>얕은 복사 + 덱·장애물 기록·동행 목록 복사(Build 훅이 스냅숏을 바꾸지 않게).</summary>
        public RunSetup Clone()
        {
            var c = (RunSetup)MemberwiseClone();
            c.Deck = Deck != null ? (int[])Deck.Clone() : null;
            c.Bars = Bars?.Clone();
            c.Companions = Companions != null ? new List<CompanionSpec>(Companions) : new List<CompanionSpec>();
            return c;
        }
    }
}
