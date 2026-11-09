using System.Collections;
using MV.Boot;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MV.Tests
{
    /// <summary>MV-00 완료 조건: Boot 씬이 10 §10.7의 1~3단계와 10단계를 돌고 Game 씬을 연다. 로그 오류가 나면 실패한다.</summary>
    public sealed class BootFlowTests
    {
        const float TimeoutSeconds = 60f;

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
        }
    }
}
