using System;
using MV.Data;
using MV.Fx;
using MV.Run;
using MV.Sound;
using MV.UI;
using MV.View;
using UnityEngine;

namespace MV.Boot
{
    /// <summary>
    /// 런 한 프레임: 입력 → <see cref="RunSim.Tick"/>(×틱 수) → <see cref="RunSim.DrainEvents"/> 한 번 → 같은 목록을 소리·이펙트·월드 뷰·HUD(<see cref="MvHudEvents"/>)에 넘긴다.
    /// 계단 경험치 오브 도착(<see cref="StairOrbFlight"/>)은 Tick 앞에 시뮬로 넘겨 실제 게이지를 그 순간 올린다(MV-UI3-STAIRFX3).
    /// HUD의 계속 보이는 값은 상태 스냅샷(<see cref="IRunHudSource"/>)으로 읽는다.
    /// </summary>
    sealed class MvRunFrame : IDisposable
    {
        readonly MvRunInputReader _input;

        public MvRunFrame(MvTables tables)
        {
            _input = new MvRunInputReader(tables.RunConfig.D(RunConfigKeys.DRAG_SCREEN_M));
        }

        /// <summary>새 런: 드래그 상태를 지우고, 지난 런 오브 도착을 버린다.</summary>
        public void Reset()
        {
            _input.Reset();
            StairOrbFlight.BeginRun();
        }

        /// <summary>로비 출발 키를 이번 프레임에 눌렀는가(Input System 액션).</summary>
        public bool StartPressedThisFrame() => _input.StartPressedThisFrame();

        public void Dispose() => _input.Dispose();

        /// <param name="playing">런 단계(결과 단계에서는 입력 없이 시뮬만 끝까지 돈다).</param>
        /// <param name="scriptedDrag">시험 훅: 런 프레임 → 목표 tx(null = 드래그 없음). null이면 포인터를 읽는다.</param>
        /// <param name="frame">이번 런의 프레임 번호(<paramref name="scriptedDrag"/>에 넘긴다).</param>
        /// <param name="fixedDt">시험 훅: 실시간 dt 대신 쓸 dt.</param>
        public void Step(RunSim sim, WorldView world, bool playing, Func<int, double?> scriptedDrag, int frame, double? fixedDt, int ticks)
        {
            var s = sim.State;
            RunInput input;
            if (!playing) input = RunInput.None;
            else if (scriptedDrag != null)
            {
                var tx = scriptedDrag(frame);
                input = tx.HasValue ? RunInput.Drag(tx.Value) : RunInput.None;
            }
            else input = _input.Read(s);

            double dt = fixedDt ?? Time.unscaledDeltaTime;
            int n = Math.Max(1, ticks);
            // 오브가 도착한(또는 도착 못 하고 끝난) 수령 몫을 이번 Tick 처음에 받는다(MV-UI3-STAIRFX3, 시간을 재지 않는다).
            StairOrbFlight.DrainArrivals(sim.ReceiveArrived);
            for (int i = 0; i < n; i++)
            {
                sim.Tick(dt, input);
                if (s.Phase == RunPhase.Result) break;
            }

            var events = sim.DrainEvents();
            MvSound.OnRunEvents(events);
            MvFx.OnRunEvents(events);
            world.OnRunEvents(events);   // 오브젝트 맞음·우물 벽돌 빠짐(06 §6.13)
            SettleUnclaimedWellGauge(sim, events);
            MvHudEvents.OnRunEvents(events);   // HUD 섹션(무기 지급·계단 등급·룰렛·카드 적용 연출, MV-UI3-HUD1)
            foreach (var e in events)
                if (e.Type == RunEventType.Toast) MvUiToast.Show(RunToastText.Of(e));   // 모은 마나 없음·5 미만 냥파워 없음 등(런 HUD가 띄움)
            world.SyncObjects(s.Objs, s.P.Z, s.WorldY);
            world.SyncCats(s);   // 플레이어·친구 고양이(02 §2.1.4)
        }

        /// <summary>
        /// 오브로 맡지 못한 우물 게이지 수령(탑 뷰 없음·재생기 없음)은 바로 도착으로 친다. 게이지가 멈추지 않게(MV-UI3-STAIRFX3).
        /// 맡은 수령은 오브마다 도착을 알린다(<see cref="StairOrbEmitter"/>).
        /// </summary>
        static void SettleUnclaimedWellGauge(RunSim sim, System.Collections.Generic.List<RunEvent> events)
        {
            foreach (var e in events)
            {
                if (e.Type != RunEventType.WellFlush || e.B <= 0 || StairOrbFlight.TakeClaim(e.B)) continue;
                for (int i = 0, n = Math.Max(1, e.A); i < n; i++) sim.ReceiveArrived(e.B);
            }
        }
    }
}
