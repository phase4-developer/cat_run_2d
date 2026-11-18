using System;
using System.Collections.Generic;
using MV.Data;
using MV.Run;
using MV.View;

namespace MV.Boot
{
    /// <summary>
    /// 로비 트랙(MV-SPEC-02 01 §3 [원작]): 다음 런과 같은 순번·시드로 만든 런 상태(Tick하지 않음)에서 시작점부터
    /// `lobby.objZMax` 안 오브젝트만 깔고, 걷는 냥법사와 함께 매 프레임 월드에 맞춘다.
    /// </summary>
    sealed class MvLobbyTrack
    {
        readonly List<RunObject> _objs = new List<RunObject>(64);
        RunSim _sim;

        /// <summary>깐 오브젝트 수(시험·QA용).</summary>
        public int ObjectCount => _objs.Count;
        /// <summary>미리보기 런의 프레임 수(Tick하지 않으니 0, 없으면 −1. 시험·QA용).</summary>
        public long Frame => _sim != null ? _sim.State.Frame : -1;

        /// <summary>미리보기 런으로 트랙을 깔고 소품 줄을 다시 만든다.</summary>
        public void Build(RunSim preview, MvTables tables, WorldView world)
        {
            _sim = preview;
            var s = preview.State;
            LobbyTrack.Pick(s.Objs, s.Base, tables.RunConfig.D(RunConfigKeys.Lobby_ObjZMax), _objs);
            world.RebuildProps(s.Base, Math.Max(0, s.Gz - s.Base), false, s.WorldY);
            Sync(world);
        }

        /// <summary>로비 한 프레임: 깐 오브젝트와 제자리 걷기 냥법사(런 단계 Run → Walk)를 맞춘다.</summary>
        public void Sync(WorldView world)
        {
            if (_sim == null) return;
            var s = _sim.State;
            world.SyncObjects(_objs, s.P.Z, s.WorldY);
            world.SyncCats(s);
        }

        /// <summary>출발하면 트랙을 비운다(런이 같은 자리에 새로 깐다).</summary>
        public void Clear()
        {
            _sim = null;
            _objs.Clear();
        }
    }
}
