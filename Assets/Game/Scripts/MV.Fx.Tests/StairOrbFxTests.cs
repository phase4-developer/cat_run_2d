using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using jjevol.Effect;
using MV.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MV.Fx.Tests
{
    /// <summary>
    /// MV-UI3-STAIRFX2: 계단 경험치 오브를 EffectManager로 UI Effect 루트에 재생(HUD 앞), 도착 신호는 요청이 끝날 때 한 번,
    /// 오브 부품은 UI 판 위에서 HUD 도착 자리에 정확히 닿는다. 화면 모양(크기·곡선·앞뒤)은 사람 확인(NOT_RUN 별도).
    /// </summary>
    public sealed class StairOrbFxTests
    {
        const string OrbPath = "Assets/Game/Addressables/Local/BG/Prefab/Object/Obj_Stair_Orb.prefab";

        sealed class FakePlayer : IEffectPlayer
        {
            public readonly List<EffectRequest> Requests = new List<EffectRequest>();
            public EffectHandle Play(EffectRequest request) { Requests.Add(request); return null; }
            public UniTask<EffectHandle> PlayAsync(EffectRequest request) { Requests.Add(request); return UniTask.FromResult<EffectHandle>(null); }
            public void Despawn(EffectHandle handle) { }
        }

        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
            StairOrbFx.ClearForTests();
            StairOrbFlight.ClearForTests();
        }

        RectTransform NewUiPlane(int sortingOrder)
        {
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            _made.Add(go);
            go.layer = 5; // UI
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var root = new GameObject("Effect", typeof(RectTransform));
            root.layer = 5;
            root.transform.SetParent(go.transform, false);
            return (RectTransform)root.transform;
        }

        [Test]
        public void OrbPrefab_HasStairOrbBehaviourAndParticles()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OrbPath);
            Assert.That(prefab, Is.Not.Null, OrbPath);
            var orb = prefab.GetComponent<StairOrb>();
            Assert.That(orb, Is.Not.Null, "뿌리 StairOrb(EffectManager 동작으로 수집)");
            Assert.That(orb is IEffectBehaviour);
            Assert.That(prefab.GetComponentInChildren<ParticleSystem>(true), Is.Not.Null, "EffectManager가 ParticleEffectRenderer를 고른다");
            Assert.That(new SerializedObject(orb).FindProperty("particle").objectReferenceValue, Is.Not.Null, "반환 때 잔상 지울 파티클");
        }

        [Test]
        public void Launch_PlaysOnUiRootAboveItsCanvas_AndArrivesOnceWhenEnded()
        {
            var plane = NewUiPlane(150);
            var fake = new FakePlayer();
            StairOrbFx.Install(fake);
            StairOrbFlight.UiRoot = () => plane;
            int arrived = 0;
            StairOrbFlight.Arrived += () => arrived++;
            StairOrbFlight.RaiseLaunched(1);

            var launch = new StairOrbFlight.OrbLaunch { From = Vector3.one, Spread = Vector3.up, Key = "Obj_Stair_Orb", SortingOffset = 1 };
            Assert.That(StairOrbFlight.TryLaunch(launch), Is.True);
            Assert.AreEqual(1, fake.Requests.Count);
            var req = fake.Requests[0];
            Assert.AreEqual("Obj_Stair_Orb", req.effectType);
            Assert.AreSame(plane, req.parent, "UI Effect 루트 아래(UI 카메라가 그린다)");
            Assert.AreEqual(151, req.sortingOrder, "판 캔버스 순서 + offset → HUD 위");
            Assert.That(req.loop, Is.True, "파티클 반복, 끝은 오브 부품");
            Assert.That(req.unscaledTime, Is.True);
            Assert.That(StairOrbFx.TryGet(req.skinIndex, out var got), Is.True, "요청 표식으로 출발 정보");
            Assert.AreEqual(launch.Spread, got.Spread);

            req.onEnded(EffectEndReason.Despawned);
            req.onEnded(EffectEndReason.Despawned);
            Assert.AreEqual(1, arrived, "끝날 때 도착 신호 한 번");
            Assert.AreEqual(0, StairOrbFlight.InFlight);
            Assert.AreEqual(0, StairOrbFx.Pending);
        }

        [Test]
        public void Launch_WithoutUiRoot_IsRefused()
        {
            StairOrbFx.Install(new FakePlayer());
            Assert.That(StairOrbFlight.TryLaunch(new StairOrbFlight.OrbLaunch { Key = "Obj_Stair_Orb" }), Is.False, "UI 판이 없으면 거절(방출기가 바로 도착으로 친다)");
        }

        [Test]
        public void Orb_FliesOnUiPlane_AndEndsExactlyOnHudTarget()
        {
            var plane = NewUiPlane(150);
            var camGo = new GameObject("WorldCam");
            _made.Add(camGo);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5;
            camGo.transform.position = new Vector3(0, 0, -10);

            var orb = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(OrbPath)).GetComponent<StairOrb>();
            _made.Add(orb.gameObject);
            var handleGo = new GameObject("Handle");
            handleGo.transform.SetParent(plane, false);
            orb.transform.SetParent(handleGo.transform, false);

            var target = new Vector2(30, 40);
            StairOrbFlight.Target = (out Vector2 s) => { s = target; return true; };
            orb.Launch(new Vector3(0, 1, 0), new Vector3(1, 2, 0), cam, plane);
            Assert.That(orb.Busy, Is.True);
            Assert.AreEqual(5, orb.gameObject.layer, "UI 카메라가 그리도록 UI 레이어");
            foreach (var p in orb.GetComponentsInChildren<ParticleSystem>(true)) Assert.AreEqual(5, p.gameObject.layer);
            Assert.That(orb.Fit, Is.GreaterThan(0f).And.Not.NaN);

            orb.Step(orb.TotalSec + 0.01f);
            Assert.That(orb.IsFinished, Is.True, "날기 끝 = 반환");
            Assert.That(RectTransformUtility.ScreenPointToWorldPointInRectangle(plane, target, null, out var expect));
            Assert.That(Vector3.Distance(expect, orb.transform.position), Is.LessThan(1e-3f), "HUD 도착 자리에 정확히");

            orb.End();
            Assert.That(orb.Busy, Is.False);
        }
    }
}
