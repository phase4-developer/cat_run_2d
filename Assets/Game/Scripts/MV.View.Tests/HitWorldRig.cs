using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using MV.Data;
using MV.Run;
using UnityEditor;
using UnityEngine;

namespace MV.View.Tests
{
    /// <summary>
    /// 투사체 판정 시험 준비: 게임과 같은 경로(<see cref="ObjectViewFactory"/> → 뷰 연결 → 프리팹 <see cref="RunHitbox"/> 등록 → <see cref="RunHitWorld"/> 질의)를 만든다.
    /// 프리팹만 Addressables 대신 에셋 원본(또는 시험용 원본)을 바로 준다. 판정 형상을 시험이 따로 공급하지 않는다.
    /// </summary>
    public sealed class HitWorldRig : IDisposable
    {
        public const string StageMap = "Stage1";
        readonly List<GameObject> _made = new List<GameObject>();
        public readonly ObjectViewFactory Factory;
        public readonly RunHitWorld World;

        /// <param name="assetOf">프리팹 주소 → 원본. null이면 에셋(<see cref="LoadPrefab"/>).</param>
        public HitWorldRig(MvTables t, Func<string, GameObject> assetOf = null)
        {
            assetOf ??= LoadPrefab;
            var camGo = new GameObject("HitRigCam");
            _made.Add(camGo);
            camGo.AddComponent<Camera>();
            var go = new GameObject("HitRig");
            _made.Add(go);
            Factory = go.AddComponent<ObjectViewFactory>();
            Factory.Init(ViewTestTables.Config(), camGo.transform, new WorldSpriteCache(), WorldView.FxAtlas);
            Factory.SetTheme(ViewTestTables.Theme("market"));
            Factory.HideLabels = true;   // TMP 글꼴 없이
            // EditMode는 게임 시간(Time.time)이 흐르지 않는다: 기본은 실제 경과 시간(시험의 Sleep), 정확한 시간이 필요한 시험은 Clock을 바꾼다(MV-CLOCK-3).
            Factory.Clock = () => Time.realtimeSinceStartupAsDouble;
            Factory.PrefabLoader = (a, ct) => UniTask.FromResult(new PrefabHandle(assetOf(a), null));
            Factory.SetStage(t, StageMap);
            World = go.AddComponent<RunHitWorld>();
            Factory.ConnectHits(World);
            Frame(0);
        }

        /// <summary>화면 프레임 한 번(카메라 기준 z = <paramref name="pz"/>). 판정 범위(그리기 범위)는 이 z를 기준으로 한다.</summary>
        public void Frame(double pz, IReadOnlyList<RunObject> objs = null, double groundY = 0) =>
            Factory.Sync(objs ?? Array.Empty<RunObject>(), pz, pz, groundY);

        public static GameObject LoadPrefab(string address)
        {
            foreach (var guid in AssetDatabase.FindAssets(address + " t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == address) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }

        public void Dispose()
        {
            foreach (var go in _made) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _made.Clear();
        }
    }
}
