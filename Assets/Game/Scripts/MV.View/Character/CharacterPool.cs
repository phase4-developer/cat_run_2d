using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using jjevol;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace MV.View
{
    /// <summary>
    /// 캐릭터 프리팹 풀(MV-SPEC-02 ArtResourcePlan §5): 주소(`116_charLook.prefab`)마다 Addressables로 1번 읽고 `ObjectPool`로 인스턴스를 돌려 쓴다.
    /// 읽는 중이면 <see cref="Get"/>이 null(그 캐릭터는 그리지 않는다). 실패한 주소는 다시 읽지 않는다(오류 1번).
    /// <see cref="Dispose"/>에서 인스턴스를 지우고 lease를 반환한다.
    /// </summary>
    public sealed class CharacterPool : IDisposable
    {
        sealed class Entry
        {
            public AddressableLease<GameObject> Lease;
            public ObjectPool<CharacterRig> Pool;
            public readonly List<CharacterRig> All = new List<CharacterRig>();
            public bool Loading, Failed;
        }

        readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        readonly Transform _parent;
        readonly Action<CharacterRig> _onCreate;
        readonly CancellationTokenSource _cts = new CancellationTokenSource();
        bool _disposed;

        /// <param name="parent">인스턴스 부모.</param>
        /// <param name="onCreate">새 인스턴스를 만든 직후 1번(정렬·빌보드 부품 붙이기).</param>
        public CharacterPool(Transform parent, Action<CharacterRig> onCreate)
        {
            _parent = parent;
            _onCreate = onCreate;
        }

        public bool IsLoaded(string address) => address != null && _entries.TryGetValue(address, out var e) && e.Pool != null;

        /// <summary>주소의 인스턴스 1개. 아직 읽지 않았으면 읽기를 시작하고 null.</summary>
        public CharacterRig Get(string address)
        {
            if (_disposed || string.IsNullOrEmpty(address)) return null;
            if (!_entries.TryGetValue(address, out var e)) _entries[address] = e = new Entry();
            if (e.Pool != null) return e.Pool.Get();
            if (!e.Loading && !e.Failed)
            {
                e.Loading = true;
                LoadAsync(address, e, _cts.Token).Forget();
            }
            return null;
        }

        public void Release(CharacterRig rig)
        {
            if (rig == null) return;
            if (!_disposed && rig.Address != null && _entries.TryGetValue(rig.Address, out var e) && e.Pool != null) e.Pool.Release(rig);
            else Object.Destroy(rig.gameObject);
        }

        async UniTaskVoid LoadAsync(string address, Entry e, CancellationToken ct)
        {
            AddressableLease<GameObject> lease = null;
            try
            {
                lease = await AddressableScope.GetPrefab(address, ct);
            }
            catch (OperationCanceledException)
            {
            }
            e.Loading = false;
            if (_disposed) { lease?.Dispose(); return; }
            if (lease == null || lease.Asset == null || lease.Asset.GetComponent<CharacterRig>() == null)
            {
                Debug.LogError(lease == null || lease.Asset == null
                    ? $"CharacterPool: 프리팹 로드 실패 {address}"
                    : $"CharacterPool: {address}에 {nameof(CharacterRig)}가 없습니다");
                lease?.Dispose();
                e.Failed = true;
                return;
            }
            e.Lease = lease;
            var prefab = lease.Asset;
            e.Pool = new ObjectPool<CharacterRig>(
                () =>
                {
                    var go = Object.Instantiate(prefab, _parent, false);
                    go.name = address;
                    var rig = go.GetComponent<CharacterRig>();
                    rig.Address = address;
                    e.All.Add(rig);
                    _onCreate?.Invoke(rig);
                    return rig;
                },
                rig => rig.gameObject.SetActive(true),
                rig =>
                {
                    rig.Clear();
                    rig.gameObject.SetActive(false);
                },
                rig =>
                {
                    e.All.Remove(rig);
                    if (rig != null) Object.Destroy(rig.gameObject);
                },
                collectionCheck: false);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            _cts.Dispose();
            foreach (var e in _entries.Values)
            {
                foreach (var rig in e.All)
                    if (rig != null) Object.Destroy(rig.gameObject);
                e.All.Clear();
                e.Pool?.Dispose();
                e.Lease?.Dispose();
            }
            _entries.Clear();
        }
    }
}
