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
        /// <summary>
        /// 투사체 공간 충돌 검출(판정 형상 = 오브젝트 프리팹 Collider). 런을 돌리는 쪽이 넣는다.
        /// 투사체가 있는데 비어 있으면 <see cref="Tick"/>이 예외를 낸다(다른 판정으로 대신하지 않는다).
        /// </summary>
        public IProjectileHitQuery HitQuery { get; set; }

        /// <summary>
        /// 우물 게이지 수령(108 wellGauge)을 화면의 오브 도착으로 받는다(MV-UI3-STAIRFX3, 사용자 2026-11-20 「이펙트가 도착해서 종료되는 시점에 처리해」).
        /// 켜면 WellFlush가 시간 없이 수령 번호(이벤트 B)를 내고, 오브 하나가 도착하거나 도착하지 못하고 끝날 때마다 <see cref="ReceiveArrived"/>로 한 몫씩 받는다.
        /// 끄면(기본: 화면 없이 시뮬만 도는 시험 — 도착을 알릴 오브가 없다) 108 행 지연(preDelayMs·delayMs) 그대로다. 실제 게임 런은 Boot가 켠다.
        /// </summary>
        public bool WellGaugeOnArrival { get; set; }

        /// <summary>
        /// 화면이 알리는 오브 하나 도착(<paramref name="receiveId"/> = WellFlush 이벤트 B). 다음 <see cref="Tick"/> 처음에 그 수령의 한 몫을 게이지에 더한다.
        /// 모르는 번호·끝난 수령·0 이하는 무시한다. 런이 끝난 뒤(R.fin)의 도착은 버린다.
        /// </summary>
        public void ReceiveArrived(int receiveId)
        {
            if (receiveId > 0) State.ReceiveArrivals.Add(receiveId);
        }

        /// <summary>
        /// 룰렛 카드 적용을 화면의 카드 비행 도착으로 받는다(MV-UI3-CLIMBFX3, 사용자 2026-11-23 「비행 시간도 프리팹 인스펙터로 조절하고 싶다고 하니 표가 아니라 프리팹값으로 교체」).
        /// 켜면 비행을 시작한 카드는 <see cref="CardArrived"/>를 받을 때 적용한다(비행 시간 = HUD Card Pos 프리팹 값).
        /// 끄면(기본: 화면 없이 시뮬만 도는 시험) 비행을 시작하는 순간 적용한다. 실제 게임 런은 Boot가 켜고, 화면이 맡지 않은 비행은 Boot가 바로 도착으로 친다.
        /// </summary>
        public bool CardApplyOnArrival { get; set; }

        /// <summary>
        /// 화면이 알리는 룰렛 카드 도착(<paramref name="cardIndex"/> = RouletteFly 이벤트 A, 지금 룰렛의 카드 번호). 다음 <see cref="Tick"/> 처음에 그 카드를 적용한다.
        /// 룰렛이 없거나 날지 않는 카드·이미 적용한 카드·범위 밖 번호는 무시한다.
        /// </summary>
        public void CardArrived(int cardIndex)
        {
            if (cardIndex >= 0) State.CardArrivals.Add(cardIndex);
        }

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

        /// <summary>
        /// 한 프레임. <paramref name="frameDt"/> = 이 프레임의 게임 시간(초, 배속 포함 — Unity <c>Time.deltaTime</c>, 배속·일시정지 = <c>Time.timeScale</c>).
        /// 안에서 한 걸음 dt = min(dtCap, frameDt / 배속)으로 나눠 배속 수만큼 돌고(§4.13.2 gdt·sdt), 「실시간」 타이머도 같은 게임 시간을 따른다(MV-CLOCK-1).
        /// 배속 1이면 예전(dt = min(dtCap, realDt))과 같다. 화면 없는 시험은 dt를 직접 넣는다.
        /// </summary>
        public void Tick(double frameDt, RunInput input) => TickCore(frameDt, input);

        partial void OnCreated();
        partial void TickCore(double frameDt, RunInput input);
    }
}
