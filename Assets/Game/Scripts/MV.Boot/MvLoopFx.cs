using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using jjevol;
using MV.Fx;
using MV.Run;
using MV.View;
using UnityEngine;

namespace MV.Boot
{
    /// <summary>
    /// 런 연출 재생기 설치: 파티클 머티리얼 2개(<see cref="MvFx.MatNormalAddress"/>·<see cref="MvFx.MatAddAddress"/>)를 읽고
    /// 루프 아래 `FxRoot`에 <see cref="MvFx.InstallDefault"/>를 세운다. 루프가 사라지면 함께 내린다.
    /// </summary>
    sealed class MvLoopFx : IDisposable
    {
        readonly List<IDisposable> _leases = new List<IDisposable>();
        MvFxPlayer _player;

        /// <summary>머티리얼 2개를 모두 읽었는지.</summary>
        public bool MaterialsLoaded { get; private set; }

        public async UniTask InstallAsync(Transform parent, WorldView world, Func<RunState> state, Action alive, CancellationToken ct)
        {
            var fxRoot = new GameObject("FxRoot").transform;
            fxRoot.SetParent(parent, false);
            var normal = await LoadMaterialAsync(MvFx.MatNormalAddress, ct);
            alive();
            var add = await LoadMaterialAsync(MvFx.MatAddAddress, ct);
            alive();
            MaterialsLoaded = normal != null && add != null;
            if (!MaterialsLoaded) Debug.LogWarning("[MvLoopFx] 우다다 머티리얼을 찾지 못했습니다(" + MvFx.MatNormalAddress + ", " + MvFx.MatAddAddress + ").");
            _player = MvFx.InstallDefault(fxRoot, world.Rig, state, normal, add);
        }

        async UniTask<Material> LoadMaterialAsync(string address, CancellationToken ct)
        {
            var lease = await AddressableScope.Get<Material>(address, ct);
            if (lease == null) return null;
            _leases.Add(lease);
            return lease.Asset;
        }

        public void Dispose()
        {
            if (_player != null && MvFx.Player == _player) MvFx.Install(null);
            _player = null;
            foreach (var d in _leases) d?.Dispose();
            _leases.Clear();
        }
    }
}
