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
    /// 런 한 프레임: 입력 → <see cref="RunSim.Tick"/>(×틱 수) → <see cref="RunSim.DrainEvents"/> 한 번 → 같은 목록을 소리·이펙트·월드 뷰에 넘긴다.
    /// HUD는 상태를 직접 읽는다.
    /// </summary>
    sealed class MvRunFrame
    {
        readonly MvRunInputReader _input;

        public MvRunFrame(MvTables tables)
        {
            _input = new MvRunInputReader(tables.RunConfig.D(RunConfigKeys.DRAG_SCREEN_M));
        }

        /// <summary>새 런: 드래그 상태를 지운다.</summary>
        public void Reset() => _input.Reset();

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
            for (int i = 0; i < n; i++)
            {
                sim.Tick(dt, input);
                if (s.Phase == RunPhase.Result) break;
            }

            var events = sim.DrainEvents();
            MvSound.OnRunEvents(events);
            MvFx.OnRunEvents(events);
            world.OnRunEvents(events);   // 오브젝트 맞음·우물 벽돌 빠짐(06 §6.13)
            foreach (var e in events)
                if (e.Type == RunEventType.ClimbStart)   // 계단 위 새 지형(정본 buildEnv(len, from, cliff))
                    world.RebuildProps(e.Z, Math.Max(0, s.Gz - e.Z), true, s.WorldY + e.Amount2);
                else if (e.Type == RunEventType.Toast) MvUiToast.Show(RunToastText.Of(e));   // 모은 마나 없음·5 미만 냥파워 없음 등(런 HUD가 띄움)
            world.SyncObjects(s.Objs, s.P.Z, s.WorldY);
            world.SyncCats(s);   // 플레이어·친구 고양이(02 §2.1.4)
        }
    }
}
