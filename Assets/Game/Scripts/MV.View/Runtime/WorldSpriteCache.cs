using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using jjevol;
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace MV.View
{
    /// <summary>
    /// 월드 `SpriteRenderer`용 아틀라스 로더(03 §3.8.8).
    /// Framework 대체: ui.addressables의 `AddressableImage`는 UGUI `Image` 전용이라 월드용을 게임에 둔다. 로드는 Framework `AddressableScope`.
    /// `SpriteAtlas.GetSprite`는 부를 때마다 복제본을 만들므로 이름당 1회만 부르고 캐시한다. 반환은 복제본 Destroy → lease Dispose 순서.
    /// </summary>
    public sealed class WorldSpriteCache : IDisposable
    {
        public const string MissingSprite = "World_Missing";

        sealed class Entry
        {
            public AddressableLease<SpriteAtlas> Lease;
            public readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        }

        readonly Dictionary<string, Entry> _atlases = new Dictionary<string, Entry>(StringComparer.Ordinal);

        public bool IsLoaded(string atlasKey) => atlasKey != null && _atlases.ContainsKey(atlasKey);

        public async UniTask<bool> LoadAtlasAsync(string atlasKey, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(atlasKey)) return false;
            if (_atlases.ContainsKey(atlasKey)) return true;
            var lease = await AddressableScope.Get<SpriteAtlas>(atlasKey, ct);
            if (lease == null || lease.Asset == null)
            {
                lease?.Dispose();
                Debug.LogError($"WorldSpriteCache: 아틀라스 로드 실패 {atlasKey}");
                return false;
            }
            if (_atlases.ContainsKey(atlasKey))
            {
                lease.Dispose();   // 기다리는 사이 같은 키가 먼저 들어왔다.
                return true;
            }
            _atlases[atlasKey] = new Entry { Lease = lease };
            return true;
        }

        /// <summary>캐시에 없으면 아틀라스에서 1회 꺼낸다. 없는 이름은 `World_Missing`(개발 중 누락을 화면에 보이게).</summary>
        public Sprite Get(string atlasKey, string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName) || atlasKey == null || !_atlases.TryGetValue(atlasKey, out var e)) return null;
            if (e.Sprites.TryGetValue(spriteName, out var s)) return s;
            s = e.Lease.Asset.GetSprite(spriteName);
            if (s == null && spriteName != MissingSprite)
            {
                Debug.LogWarning($"WorldSpriteCache: {atlasKey}에 {spriteName}이 없습니다");
                s = Get(atlasKey, MissingSprite);
            }
            e.Sprites[spriteName] = s;
            return s;
        }

        /// <summary>여러 아틀라스에서 차례로 찾는다(분할 아틀라스 `_2` 등). 없으면 null(대체 그림·경고 없음).</summary>
        public Sprite GetAny(IReadOnlyList<string> atlasKeys, string spriteName)
        {
            if (atlasKeys == null || string.IsNullOrEmpty(spriteName)) return null;
            for (int i = 0; i < atlasKeys.Count; i++)
            {
                if (atlasKeys[i] == null || !_atlases.TryGetValue(atlasKeys[i], out var e)) continue;
                if (e.Sprites.TryGetValue(spriteName, out var s) && s != null) return s;
                s = e.Lease.Asset.GetSprite(spriteName);
                if (s == null) continue;
                e.Sprites[spriteName] = s;
                return s;
            }
            return null;
        }

        /// <summary>프레임 묶음(`..._00`~`_NN`)을 한 번에 캐시해 배열로 돌려준다(런 중 할당 0).</summary>
        public Sprite[] GetFrames(string atlasKey, string prefix, int count)
        {
            var arr = new Sprite[count];
            for (int i = 0; i < count; i++) arr[i] = Get(atlasKey, prefix + i.ToString("00"));
            return arr;
        }

        public void ReleaseAtlas(string atlasKey)
        {
            if (atlasKey == null || !_atlases.TryGetValue(atlasKey, out var e)) return;
            _atlases.Remove(atlasKey);
            var destroyed = new HashSet<Sprite>();
            foreach (var s in e.Sprites.Values)
                if (s != null && destroyed.Add(s)) Object.Destroy(s);
            e.Sprites.Clear();
            e.Lease.Dispose();
        }

        public void Dispose()
        {
            var keys = new List<string>(_atlases.Keys);
            foreach (var k in keys) ReleaseAtlas(k);
        }
    }
}
