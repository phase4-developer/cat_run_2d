using System;
using System.Collections.Generic;
using MV.Data;

namespace MV.Run
{
    /// <summary>
    /// 런 시뮬레이션 진입점(04 §4.13, D-22). 시간·입력·난수는 주입한다: <see cref="Tick"/>(실시간 dt, 입력), RunRng는 <see cref="Create"/>.
    /// </summary>
    public sealed partial class RunSim
    {
        public RunState State { get; }
        readonly IRunLevelBuilder _builder;

        RunSim(RunState s, IRunLevelBuilder builder)
        {
            State = s;
            _builder = builder;
        }

        /// <summary>RunSim.Create(§4.14.5): GEN의 NewRun으로 상태를 만들고 시작 이벤트를 낸다.</summary>
        public static RunSim Create(RunSetup setup, MvTables tables, IRng rng, IRunLevelBuilder builder)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (builder == null) throw new ArgumentNullException(nameof(builder));
            var s = builder.NewRun(setup, tables, rng);
            var sim = new RunSim(s, builder);
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
