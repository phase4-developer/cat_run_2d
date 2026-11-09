using System.Collections;
using MV.Boot;
using MV.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MV.Tests
{
    /// <summary>
    /// Boot 씬이 10 §10.7의 1~6단계와 10단계를 돌고 Game 씬을 연다. 로그 오류가 나면 실패한다.
    /// 저장은 메모리 저장소(<see cref="MvSave.PreferencesOverride"/>)로 격리한다 — 실제 PlayerPrefs를 쓰지 않는다.
    /// </summary>
    public sealed class BootFlowTests
    {
        const float TimeoutSeconds = 60f;

        MvMemorySavePrefs _prefs;

        [SetUp]
        public void IsolateSave()
        {
            _prefs = MvSave.UseMemoryPreferences();
        }

        [TearDown]
        public void ReleaseSave()
        {
            MvSave.Shutdown();
            MvSave.PreferencesOverride = null;
        }

        [UnityTest]
        public IEnumerator Boot_OpensGameScene_WithoutErrors()
        {
            var initializedBefore = GameInitializer.InitializeCount;
            SceneManager.LoadScene("Boot", LoadSceneMode.Single);
            yield return null;

            var elapsed = 0f;
            while ((GameBootstrap.State == BootState.NotStarted || GameBootstrap.State == BootState.Running) && elapsed < TimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.AreEqual(BootState.Succeeded, GameBootstrap.State, GameBootstrap.LastError?.ToString());
            Assert.AreEqual(GameBootstrap.GameSceneName, SceneManager.GetActiveScene().name);
            Assert.AreEqual(initializedBefore + 1, GameInitializer.InitializeCount);

            // 6단계: 표 값으로 새 저장을 만들었고, 표와 어긋난 값이 없어 참조 정리가 할 일이 없다.
            Assert.IsNotNull(MvBootSave.LastOpen, "Boot가 저장을 열지 않았다");
            Assert.AreEqual(MvSaveOpenStatus.Created, MvBootSave.LastOpen.Status, MvBootSave.LastOpen.Error);
            Assert.AreEqual(MvSaveNotice.None, MvBootSave.PendingNotice);
            CollectionAssert.IsEmpty(MvBootSave.LastOpen.ReferenceFixes, "새 저장 기본값이 표와 어긋난다");
            Assert.IsNotNull(MvSave.Current);
            Assert.Greater(_prefs.Count, 0, "새 저장이 시험 메모리 저장소에 써지지 않았다");

            // 일시정지 저장을 받을 오브젝트가 Game 씬 전환 뒤에도 1개 살아 있다.
            Assert.AreEqual(1, Object.FindObjectsByType<MvSaveLifecycle>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
        }
    }
}
