using System;
using System.Collections;
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
    /// 팝업 백판(MV-FIX-POPUPBACKBOARD): 로비에서 팝업을 열면 UIManager Popup/Backboard가 켜지고,
    /// 팝업 밖 화면 어디를 눌러도 UI 레이캐스트의 맨 위가 백판 또는 팝업이다(뒤 화면·HUD가 입력을 받지 않는다).
    /// 이 시험 어셈블리는 MV.UI·uGUI를 참조하지 않아(asmdef는 통합 Writer) 리플렉션으로 읽는다.
    /// </summary>
    public sealed class PopupBackboardTests
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
        public IEnumerator Popup_Backboard_BlocksEverythingBehind()
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
            for (int i = 0; i < 10; i++) yield return null;

            var nav = Type.GetType("MV.UI.MvUiNav, MV.UI");
            Assert.IsNotNull(nav, "MV.UI.MvUiNav 없음");
            nav.GetMethod("OpenAreaUpgradeAsync").Invoke(null, new object[] { null });
            elapsed = 0f;
            while (elapsed < 2f) { elapsed += Time.unscaledDeltaTime; yield return null; }

            var backboard = FindByPath("Popup/Backboard");
            Assert.IsNotNull(backboard, "UIManager Popup/Backboard 없음");
            Debug.Log($"[PopupBackboard] active={backboard.activeInHierarchy} sibling={backboard.transform.GetSiblingIndex()}/{backboard.transform.parent.childCount}");
            Assert.IsTrue(backboard.activeInHierarchy, "팝업이 열렸는데 백판이 꺼져 있다");
            var popupRoot = backboard.transform.parent;

            // 화면 격자 점마다 UI 레이캐스트 맨 위를 본다.
            var report = new StringBuilder();
            int leaks = 0;
            for (int y = 1; y <= 9; y++)
            for (int x = 1; x <= 4; x++)
            {
                var pos = new Vector2(Screen.width * x / 5f, Screen.height * y / 10f);
                var top = TopHit(pos, out string all);
                bool ok = top != null && top.transform.IsChildOf(popupRoot);
                if (!ok) leaks++;
                report.AppendLine($"{(ok ? "ok  " : "LEAK")} ({x},{y}) {pos} top={Path(top)} | {all}");
            }
            Debug.Log("[PopupBackboard] raycast\n" + report);
            LogCanvases();
            Assert.AreEqual(0, leaks, "팝업 뒤로 입력이 새는 점이 있다:\n" + report);
        }

        static GameObject FindByPath(string suffix)
        {
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "Backboard" && t.parent != null && t.parent.name == "Popup") return t.gameObject;
            return null;
        }

        static string Path(GameObject go)
        {
            if (go == null) return "(none)";
            var sb = new StringBuilder(go.name);
            for (var t = go.transform.parent; t != null; t = t.parent) sb.Insert(0, t.name + "/");
            return sb.ToString();
        }

        /// <summary>EventSystem.RaycastAll을 리플렉션으로 부른다. 첫 결과(정렬된 맨 위)를 돌려준다.</summary>
        static GameObject TopHit(Vector2 pos, out string all)
        {
            all = "";
            var esType = Type.GetType("UnityEngine.EventSystems.EventSystem, UnityEngine.UI");
            var pedType = Type.GetType("UnityEngine.EventSystems.PointerEventData, UnityEngine.UI");
            var rrType = Type.GetType("UnityEngine.EventSystems.RaycastResult, UnityEngine.UI");
            Assert.IsNotNull(esType, "uGUI EventSystem 형식 없음");
            var es = esType.GetProperty("current", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            Assert.IsNotNull(es, "EventSystem.current 없음");
            var ped = Activator.CreateInstance(pedType, es);
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
                sb.AppendLine($"{Path(c.gameObject)} root={c.isRootCanvas} override={c.overrideSorting} order={c.sortingOrder} layer={c.sortingLayerName} mode={c.renderMode}");
            Debug.Log("[PopupBackboard] canvases\n" + sb);
        }
    }
}
