using System.Collections.Generic;
using MV.Data;

namespace MV.Run
{
    /// <summary>
    /// 런 규칙 확장 지점(04 §4.20.2). 정본은 <see cref="MainRunRule"/>, 모드는 MV.Content의 IModeRule이 구현한다(MV.Run은 MV.Content를 모른다).
    /// 훅 호출 자체는 RunRng를 뽑거나 이벤트를 만들지 않는다. 모드 난수는 <see cref="RunState.ModeRng"/>만 쓴다.
    /// </summary>
    public interface IRunRule
    {
        /// <summary>① 시작: <see cref="RunSim.Create(RunContext, IRunRule, IRng, IRunLevelBuilder)"/> 첫 단계. save 스냅숏 → RunSetup(§4.14.5).</summary>
        RunSetup Build(RunContext ctx);

        /// <summary>② 코스 생성: §4.14.5 6단계(BuildLevel) 자리, 런마다 1회. Floor 3단을 이 한 번에 만든다. 오브젝트는 <see cref="RunState.AddObj"/>로 끝에 붙인다.</summary>
        void BuildCourse(RunState s, CourseRequest req);

        /// <summary>③ 피격: hit() 한 번이 가호(proc·chain·storm·blast)까지 끝난 뒤. 중첩 hit도 각각(안쪽부터). hit 재호출·RunRng 금지.</summary>
        void OnHit(RunState s, in HitInfo h);

        /// <summary>모드 진행: updRun 15단계 뒤(동행 사격 다음). Run 상태에서만.</summary>
        void Tick(RunState s, double dt);

        /// <summary>④ 종료 판정: Tick 바로 뒤. 정본 종료(gate·wall)가 먼저다. Clear → finish(ModeClear), Fail → finish(ModeFail).</summary>
        RunEnd CheckEnd(RunState s);

        /// <summary>⑤ 정산(계산만): finish(reason) 안. 계정 재화 지급은 MV.Meta만 한다.</summary>
        RunResult Settle(RunState s, FinishReason reason);

        /// <summary>이벤트 관찰: <see cref="RunState.Emit"/>마다(FloorEnter·Break·Hit …). 쌓인 뒤 부른다.</summary>
        void OnEvent(RunState s, in RunEvent e);
    }

    /// <summary>
    /// <see cref="IRunRule.Build"/>의 입력. save를 읽어 스냅숏을 만드는 것은 호출자(MV.Meta/Boot) 몫이다(MV.Run은 MV.Save를 모른다).
    /// </summary>
    public sealed class RunContext
    {
        public MvTables Tables;
        /// <summary>save 스냅숏으로 만든 정본 RunSetup(§4.14.5). 모드 필드는 기본값.</summary>
        public RunSetup Snapshot;
        /// <summary>MV.Meta가 합산한 메타 능력치(§4.20.4).</summary>
        public RunModifiers MetaModifiers;
        /// <summary>장착 펫·친구 고양이 동행(§4.20.5). 펫 슬롯 → 친구 슬롯 순서.</summary>
        public List<CompanionSpec> Companions = new List<CompanionSpec>();
    }

    /// <summary><see cref="IRunRule.BuildCourse"/> 요청 = 정본 `buildLevel(baseZ, level, saved)` 인자.</summary>
    public readonly struct CourseRequest
    {
        public readonly double BaseZ;
        public readonly int Level;
        /// <summary>장애물 기록(`keepBars` = 0이면 null).</summary>
        public readonly BarRecord Saved;

        public CourseRequest(double baseZ, int level, BarRecord saved) { BaseZ = baseZ; Level = level; Saved = saved; }
    }

    /// <summary>피격 1회 정보(04 §4.20.2). Dmg는 §4.3.2 반올림·상하한 뒤 정수.</summary>
    public readonly struct HitInfo
    {
        public readonly int ObjId;
        /// <summary>105_objectKind.id.</summary>
        public readonly int Kind;
        public readonly int Dmg;
        public readonly bool Crit;
        /// <summary>이 hit의 반응으로 오브젝트가 죽었다(가호 전 판정).</summary>
        public readonly bool Killed;
        public readonly HitSource Source;

        public HitInfo(int objId, int kind, int dmg, bool crit, bool killed, HitSource source)
        {
            ObjId = objId; Kind = kind; Dmg = dmg; Crit = crit; Killed = killed; Source = source;
        }
    }
}
