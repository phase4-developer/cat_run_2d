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
    /// MV-UI3-STAIRFX2·3: 계단 경험치 오브를 Framework 비행(PlayFlight)으로 UI Effect 루트에 재생(HUD 앞), 도착(또는 도착 못 하고 끝남) 때
    /// 수령 번호를 정확히 한 번 알린다. 비행 자체(경로·도착 이벤트)는 Framework 시험(effects pre.5 PlayMode)이 맡는다. 화면 모양은 사람 확인(NOT_RUN 별도).
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
        public void OrbPrefab_HasFlightProfile_SpreadHoldThenFly()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OrbPath);
            Assert.That(prefab, Is.Not.Null, OrbPath);
            var profile = prefab.GetComponent<EffectFlightProfile>();
            Assert.That(profile, Is.Not.Null, "뿌리 EffectFlightProfile(Framework 비행 연출 값 정본)");
            Assert.AreEqual(2, profile.legs.Count, "퍼짐 이동 → HUD 비행");
            Assert.AreEqual(EffectFlightLegKind.Move, profile.legs[0].kind);
            Assert.AreEqual(0.2f, profile.legs[0].holdAfter, 1e-6f, "퍼진 뒤 멈춤 0.2초(노션)");
            Assert.AreEqual(EffectFlightLegKind.Move, profile.legs[1].kind);
            Assert.That(profile.matchUILayer, Is.True, "UI 판 아래에서 UI 카메라가 그리게");
            Assert.That(prefab.GetComponentInChildren<ParticleSystem>(true), Is.Not.Null, "EffectManager가 ParticleEffectRenderer를 고른다");
        }

        [Test]
        public void Launch_PlaysFlightOnUiRootAboveItsCanvas_AndReportsOnceWhenRefused()
        {
            var plane = NewUiPlane(150);
            var fake = new FakePlayer(); // Play가 null = 재생 거절 → Framework가 onEnded(false)를 부른다
            StairOrbFx.Install(fake);
            StairOrbFlight.UiRoot = () => plane;
            var arrived = new List<int>();
            StairOrbFlight.Arrived += id => arrived.Add(id);

            var launch = new StairOrbFlight.OrbLaunch { From = Vector3.one, Spread = Vector3.up, Key = "Obj_Stair_Orb", SortingOffset = 1, ReceiveId = 9, Run = StairOrbFlight.Run };
            Assert.That(StairOrbFlight.TryLaunch(launch), Is.True);
            Assert.AreEqual(1, fake.Requests.Count);
            var req = fake.Requests[0];
            Assert.AreEqual("Obj_Stair_Orb", req.effectType);
            Assert.AreSame(plane, req.parent, "UI Effect 루트 아래(UI 카메라가 그린다)");
            Assert.AreEqual(151, req.sortingOrder, "판 캔버스 순서 + offset → HUD 위");
            Assert.That(req.loop, Is.True, "비행이 도착에서 끝낸다(Framework 권장 loop)");
            Assert.That(req.unscaledTime, Is.True);
            Assert.AreEqual(new[] { 9 }, arrived.ToArray(), "재생하지 못해도 수령 번호를 한 번 알린다(게이지가 멈추지 않게)");

            req.onEnded?.Invoke(EffectEndReason.Despawned);
            Assert.AreEqual(1, arrived.Count, "두 번 알리지 않는다");
        }

        [Test]
        public void Launch_WithoutUiRoot_IsRefused()
        {
            StairOrbFx.Install(new FakePlayer());
            Assert.That(StairOrbFlight.TryLaunch(new StairOrbFlight.OrbLaunch { Key = "Obj_Stair_Orb" }), Is.False, "UI 판이 없으면 거절(방출기가 바로 도착으로 친다)");
        }

        [Test]
        public void FitScale_IsOneWithoutCamera_AndPositiveWithOne()
        {
            var plane = NewUiPlane(0);
            Assert.AreEqual(1f, StairOrbFx.FitScale(null, Vector3.zero, plane, null));
            var camGo = new GameObject("WorldCam");
            _made.Add(camGo);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5;
            float fit = StairOrbFx.FitScale(cam, Vector3.zero, plane, null);
            Assert.That(fit, Is.GreaterThan(0f).And.Not.NaN);
        }
    }
}
