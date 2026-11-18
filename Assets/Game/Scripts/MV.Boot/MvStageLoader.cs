using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using jjevol;
using MV.Data;
using MV.View;
using UnityEngine;

namespace MV.Boot
{
    /// <summary>
    /// 지역별 월드 준비: `302_area.stageMap` 맵 프리팹과 `302_area.themeId` 테마 그림. 같은 지역·테마면 다시 읽지 않는다.
    /// 행·주소가 없거나 로드에 실패하면 오류로 드러낸다(대체 지면으로 가리지 않는다).
    /// </summary>
    sealed class MvStageLoader : IDisposable
    {
        readonly MvTables _tables;
        readonly WorldView _world;
        readonly MvLoopViewSource _view;
        readonly Action _alive;
        readonly List<IDisposable> _leases = new List<IDisposable>();
        int _themeId = int.MinValue;
        int _area = int.MinValue;

        /// <summary>지금 지역의 스테이지 맵(없으면 null).</summary>
        public StageMap Map { get; private set; }

        /// <param name="alive">기다린 뒤 루프가 살아 있는지 확인한다(사라졌으면 취소 예외).</param>
        public MvStageLoader(MvTables tables, WorldView world, MvLoopViewSource view, Action alive)
        {
            _tables = tables;
            _world = world;
            _view = view;
            _alive = alive;
        }

        /// <summary>지역 번호(0부터)의 맵과 테마를 맞춘다.</summary>
        public async UniTask ApplyAsync(int area, CancellationToken ct)
        {
            AreaData row = null;
            foreach (var a in _tables.Area.Datas) if (a.areaNo == area + 1) { row = a; break; }
            await LoadMapAsync(area, row, ct);
            if (row == null || row.themeId == _themeId) return;
            var theme = _tables.Theme.Get(row.themeId);
            if (theme == null) return;
            _themeId = row.themeId;
            if (!await _world.SetThemeAsync(theme, ct)) Debug.LogWarning("[MvStageLoader] 테마 그림 일부를 불러오지 못했습니다: " + theme.key);
        }

        /// <summary>지역이 바뀌면 그 지역 맵 프리팹을 월드 아래에 불러오고 이전 맵은 지운다.</summary>
        async UniTask LoadMapAsync(int area, AreaData row, CancellationToken ct)
        {
            if (area == _area || _world == null) return;
            _area = area;
            ReleaseMap();
            if (_world.Objects != null) _world.Objects.SetStage(_tables, row?.stageMap);   // 맵별 오브젝트 프리팹(305 → 105.prefab)
            if (row == null || string.IsNullOrEmpty(row.stageMap)) { Debug.LogError($"[MvStageLoader] 302_area 지역 {area + 1}의 stageMap이 없습니다"); return; }
            var r = await AddressableScope.InstantiateWithLease(row.stageMap, _world.transform, ct);
            _alive();
            if (r.instance == null) { Debug.LogError("[MvStageLoader] 스테이지 맵 로드 실패: " + row.stageMap); return; }
            if (r.destroyOnDispose != null) _leases.Add(r.destroyOnDispose);
            if (r.lease != null) _leases.Add(r.lease);
            Map = r.instance.GetComponent<StageMap>();
            if (Map != null) Map.Bind(_world.Rig.Cam, () => (float)_view.Camera.Wy, () => _world.Rig.Ready ? _world.Rig.Model.OrbitE : 0,
                () => _view.TryGetCliff(out var cliff) ? cliff : (CliffInfo?)null);
        }

        void ReleaseMap()
        {
            foreach (var d in _leases) d?.Dispose();
            _leases.Clear();
            Map = null;
        }

        public void Dispose() => ReleaseMap();
    }
}
