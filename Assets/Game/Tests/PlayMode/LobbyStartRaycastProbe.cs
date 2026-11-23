using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using MV.Boot;
using MV.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MV.Tests
{
    /// <summary>
    /// 진단(MV-FIX-LOBBYSTART3): 로비에서 팝업 없이 화면 격자 점마다 UI 레이캐스트 맨 위를 기록한다.
    /// 출발 영역(StartArea)이 아니고 Selectable 아래도 아닌 그림이 맨 위면 그 점은 게임 시작 누름을 가로챈다.
    /// 이 시험 어셈블리는 uGUI를 참조하지 않아(asmdef는 통합 Writer) 리플렉션으로 읽는다.
    /// </summary>
    public sealed class LobbyStartRaycastProbe
    {
        const float TimeoutSeconds = 120f;

        [SetUp]
        public void IsolateSave() => MvSave.UseMemoryPreferences();

        [UnityTearDown]
        public IEnumerator EndGame() => PlayModeIsolation.EndGame();

        [TearDown]
        public void ReleaseSave()
        {
            MvBootMeta.Detach();
            MvSave.Shutdown();
            MvSave.PreferencesOverride = null;
        }

        [UnityTest]
        public IEnumerator Lobby_StartArea_ReceivesPresses()
        {
            SceneManager.LoadScene(GameBootstrap.BootSceneName, LoadSceneMode.Single);
            yield return null;
            float elapsed = 0f;
            while ((GameBootstrap.State == BootState.NotStarted || GameBootstrap.State == BootState.Running) && elapsed < TimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(BootState.Succeeded, GameBootstrap.State, GameBootstrap.LastError?.ToString());
            var loop = MvGameLoop.Current;
            Assert.IsNotNull(loop);
            Assert.AreEqual(MvLoopPhase.Lobby, loop.Phase);
            elapsed = 0f;
            while (elapsed < 3f) { elapsed += Time.unscaledDeltaTime; yield return null; }

            var esType = Type.GetType("UnityEngine.EventSystems.EventSystem, UnityEngine.UI");
            var es = esType.GetProperty("current", BindingFlags.Public | BindingFlags.Static).GetValue(null) as Behaviour;
            var module = es ? esType.GetProperty("currentInputModule").GetValue(es) as Behaviour : null;
            Debug.Log($"[LobbyStartProbe] eventSystem={Path(es ? es.gameObject : null)} enabled={(es ? es.isActiveAndEnabled : false)} module={(module ? module.GetType().FullName + " enabled=" + module.isActiveAndEnabled : "(none)")}");

            var selectable = Type.GetType("UnityEngine.UI.Selectable, UnityEngine.UI");
            var report = new StringBuilder();
            int blocked = 0;
            for (int y = 1; y <= 9; y++)
            for (int x = 1; x <= 4; x++)
            {
                var pos = new Vector2(Screen.width * x / 5f, Screen.height * y / 10f);
                var top = TopHit(es, pos, out string all);
                string tag;
                if (top == null) tag = "NONE";
                else if (top.name == "StartArea") tag = "ok  ";
                else if (top.GetComponentInParent(selectable, true) != null) tag = "btn ";
                else tag = "BLK ";
                if (tag == "BLK ") blocked++; // NONE는 배치 모드 가로 화면에서 세로 캔버스 밖
                report.AppendLine($"{tag} ({x},{y}) {pos} top={Path(top)} | {all}");
            }
            Debug.Log("[LobbyStartProbe] raycast\n" + report);
            LogCanvases();

            // 화면 가운데 맨 위 객체에 EventSystem 누름 이벤트(누름 → 드래그 준비)를 모듈과 같은 순서로 보내고 런으로 넘어가는지 본다.
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var hit = TopHit(es, center, out _);
            Debug.Log($"[LobbyStartProbe] center top={Path(hit)}");
            Assert.IsNotNull(hit, "화면 가운데에 UI가 없다");
            var pedType2 = Type.GetType("UnityEngine.EventSystems.PointerEventData, UnityEngine.UI");
            var ped2 = Activator.CreateInstance(pedType2, new object[] { es });
            pedType2.GetProperty("position").SetValue(ped2, center);
            var areaType = Type.GetType("MV.UI.LobbyStartArea, MV.UI");
            Assert.IsNotNull(areaType, "MV.UI.LobbyStartArea 형식 없음");
            var area = hit.GetComponent(areaType);
            Assert.IsNotNull(area, "가운데 맨 위 객체에 LobbyStartArea가 없다: " + Path(hit));
            areaType.GetMethod("OnInitializePotentialDrag").Invoke(area, new[] { ped2 });
            Debug.Log("[LobbyStartProbe] sent OnInitializePotentialDrag to " + Path(hit));
            elapsed = 0f;
            while (loop.Phase == MvLoopPhase.Lobby && elapsed < 3f) { elapsed += Time.unscaledDeltaTime; yield return null; }
            Debug.Log($"[LobbyStartProbe] phase after press={loop.Phase}");
            Assert.AreEqual(MvLoopPhase.Run, loop.Phase, "출발 누름 뒤 런으로 넘어가지 않았다");
            Assert.AreEqual(0, blocked, "출발 영역을 가리는 점이 있다:\n" + report);
        }

        static string Path(GameObject go)
        {
            if (go == null) return "(none)";
            var sb = new StringBuilder(go.name);
            for (var t = go.transform.parent; t != null; t = t.parent) sb.Insert(0, t.name + "/");
            return sb.ToString();
        }

        static GameObject TopHit(Behaviour es, Vector2 pos, out string all)
        {
            all = "";
            if (es == null) return null;
            var esType = es.GetType();
            var pedType = Type.GetType("UnityEngine.EventSystems.PointerEventData, UnityEngine.UI");
            var rrType = Type.GetType("UnityEngine.EventSystems.RaycastResult, UnityEngine.UI");
            var ped = Activator.CreateInstance(pedType, new object[] { es });
            pedType.GetProperty("position").SetValue(ped, pos);
            var list = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(rrType));
            esType.GetMethod("RaycastAll").Invoke(es, new[] { ped, list });
            var goField = rrType.GetProperty("gameObject");
            var sb = new StringBuilder();
            for (int i = 0; i < list.Count && i < 4; i++)
                sb.Append(Path((GameObject)goField.GetValue(list[i]))).Append(" ; ");
            all = sb.ToString();
            return list.Count > 0 ? (GameObject)goField.GetValue(list[0]) : null;
        }

        static void LogCanvases()
        {
            var sb = new StringBuilder();
            foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                sb.AppendLine($"{Path(c.gameObject)} root={c.isRootCanvas} override={c.overrideSorting} order={c.sortingOrder} mode={c.renderMode}");
            Debug.Log("[LobbyStartProbe] canvases\n" + sb);
        }
    }
}
