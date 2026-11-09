using System.Collections;
using MV.Boot;
using MV.Data;
using MV.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MV.Tests
{
    /// <summary>
    /// Boot 씬이 10 §10.7의 1~6단계와 10단계를 돌고 Game 씬을 연다. 로그 오류가 나면 실패한다.
    /// 런 코어 연결(MV-G3-INT): 표 검증이 MV.Run 컴포넌트 목록을 쓰고, 저장이 열리면 MvMeta.Current가 생긴다.
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
            MvBootMeta.Detach();
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

            // 4단계: 표 검증이 MV.Run 컴포넌트 구현 목록(RunComponents.IsImplemented)으로 돌았고 오류가 없다.
            Assert.IsNotNull(MvBootMeta.TableOptions?.IsComponentImplemented, "Boot가 표 검증에 컴포넌트 구현 확인을 넘기지 않았다");
            Assert.IsTrue(MvBootMeta.TableOptions.IsComponentImplemented("hittable", "Always"));
            Assert.IsFalse(MvBootMeta.TableOptions.IsComponentImplemented("hittable", "NotImplementedType"));
            Assert.IsNotNull(MvTableLoader.LastReport);
            Assert.IsFalse(MvTableLoader.LastReport.HasError, MvTableLoader.LastReport.Summary());

            // 6단계 뒤: 영속 변경 진입점이 열린 저장소·재화 서비스·표를 본다.
            Assert.IsTrue(MvBootMeta.MetaReady, "Boot가 MvMeta.Current를 만들지 않았거나 저장·재화 서비스가 없다");
            Assert.IsTrue(MvBootMeta.MetaUsesCurrent, "MvMeta.Current가 지금 저장소·표를 보지 않는다");
        }

        /// <summary>저장을 못 열면 MvMeta.Current는 null로 기다리다가, 저장이 열리면 생기고 다시 열어도 이어 쓴다.</summary>
        [UnityTest]
        public IEnumerator Meta_WaitsForSave_WhenBootCouldNotOpen()
        {
            if (MvTables.Current == null)
            {
                var load = MvTableLoader.LoadAsync(null, MvBootMeta.CreateTableOptions());
                while (!load.IsCompleted) yield return null;
                Assert.IsNull(load.Exception, load.Exception?.ToString());
            }
            var tables = MvTables.Current;
            Assert.IsNotNull(tables);

            MvSave.Shutdown();
            MvBootMeta.Attach(tables);
            Assert.IsFalse(MvBootMeta.HasMeta, "저장이 없는데 MvMeta.Current가 생겼다");

            var opened = MvBootSave.Open(tables);
            Assert.IsTrue(opened.IsOpen, opened.Error);
            Assert.IsTrue(MvBootMeta.MetaReady, "저장이 열렸는데 MvMeta.Current가 준비되지 않았다");
            Assert.IsTrue(MvBootMeta.MetaUsesCurrent);

            MvSave.Shutdown();
            Assert.IsTrue(MvBootMeta.HasMeta);
            Assert.IsFalse(MvBootMeta.MetaReady, "닫힌 저장으로 명령을 받으면 안 된다");

            var reopened = MvSave.Reopen();
            Assert.IsTrue(reopened.IsOpen, reopened.Error);
            Assert.IsTrue(MvBootMeta.MetaReady);
            Assert.IsTrue(MvBootMeta.MetaUsesCurrent, "다시 연 저장소를 보지 않는다");
        }
    }
}
