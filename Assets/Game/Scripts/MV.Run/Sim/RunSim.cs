using System;
using System.Collections.Generic;
using MV.Data;

namespace MV.Run
{
    /// <summary>
    /// 런 시뮬레이션 진입점(04 §4.13, D-22). 시간·입력·난수는 주입한다: <see cref="Tick"/>(실시간 dt, 입력), RunRng는 <see cref="Create(RunSetup, MvTables, IRng, IRunLevelBuilder)"/>.
    /// 규칙 확장 지점은 <see cref="IRunRule"/>(04 §4.20). 규칙을 주지 않으면 정본 <see cref="MainRunRule"/>이다.
    /// </summary>
    public sealed partial class RunSim
    {
        public RunState State { get; }
        /// <summary>이 런의 규칙(정본이면 <see cref="MainRunRule"/>).</summary>
        public IRunRule Rule { get; }

        RunSim(RunState s, IRunRule rule)
        {
            State = s;
            Rule = rule;
        }

        /// <summary>정본 런: <c>Create(setup, tables, rng, builder, new MainRunRule(builder))</c>. setup은 ① Build를 마친 값으로 본다.</summary>
        public static RunSim Create(RunSetup setup, MvTables tables, IRng rng, IRunLevelBuilder builder) =>
            Create(setup, tables, rng, builder, null);

        /// <summary>
        /// ① <see cref="IRunRule.Build"/> → 아래 오버로드. ctx.Tables를 쓴다.
        /// </summary>
        public static RunSim Create(RunContext ctx, IRunRule rule, IRng rng, IRunLevelBuilder builder)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            var setup = rule.Build(ctx) ?? throw new InvalidOperationException($"{rule.GetType().Name}.Build가 RunSetup을 내지 않았습니다");
            return Create(setup, ctx.Tables, rng, builder, rule);
        }

        /// <summary>
        /// RunSim.Create(§4.14.5, §4.20.2): 상태(1~5단계) → ② <see cref="IRunRule.BuildCourse"/>(6단계) → 시작 이벤트.
        /// builder가 <see cref="IRunStateFactory"/>가 아니면(시험 대역) <see cref="IRunLevelBuilder.NewRun"/>이 코스까지 만들고 BuildCourse는 건너뛴다.
        /// rule이 null이면 <see cref="MainRunRule"/>. 모디파이어·동행이 검증에 걸리면 <see cref="ArgumentException"/>.
        /// </summary>
        public static RunSim Create(RunSetup setup, MvTables tables, IRng rng, IRunLevelBuilder builder, IRunRule rule)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            var err = setup.Validate(tables);
            if (err != null) throw new ArgumentException(err, nameof(setup));
            rule ??= new MainRunRule(builder);

            RunState s;
            if (builder is IRunStateFactory factory)
            {
                s = factory.NewState(setup, tables, rng);
                s.Rule = rule;
                rule.BuildCourse(s, new CourseRequest(0, setup.Level, setup.KeepBars ? setup.Bars : null));
            }
            else
            {
                s = builder.NewRun(setup, tables, rng);
                s.Rule = rule;
            }
            if (setup.Companions != null)
                foreach (var c in setup.Companions) s.Companions.Add(new CompanionRuntime { Spec = c, T = c.Every });

            var sim = new RunSim(s, rule);
            sim.OnCreated();
            return sim;
        }

        /// <summary>쌓인 이벤트를 넘기고 비운다.</summary>
        public List<RunEvent> DrainEvents()
        {
            var list = new List<RunEvent>(State.Events);
            State.Events.Clear();
            return list;
        }

        /// <summary>한 프레임. realDt = 기기 프레임 간격(초). 안에서 dt = min(dtCap, realDt), gdt, sdt를 만든다(§4.13.2).</summary>
        public void Tick(double realDt, RunInput input) => TickCore(realDt, input);

        partial void OnCreated();
        partial void TickCore(double realDt, RunInput input);
    }
}
