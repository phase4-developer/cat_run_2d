using System;
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
    /// 플레이 가능한 빌드(MV-PLAY-INT): Boot → Game → 로비 → 출발 → 스크립트 입력으로 런 → 결과 받기(정산 1번) → 로비.
    /// 로그 오류가 나면 실패한다. 저장은 메모리 저장소로 격리한다(실제 PlayerPrefs를 쓰지 않는다).
    /// </summary>
    public sealed class GameLoopTests
    {
        const float TimeoutSeconds = 120f;
        const int MaxRunFrames = 4000;

        MvMemorySavePrefs _prefs;

        [SetUp]
        public void IsolateSave()
        {
            _prefs = MvSave.UseMemoryPreferences();
        }

        [TearDown]
        public void ReleaseSave()
        {
            var loop = MvGameLoop.Current;
            if (loop != null)
            {
                loop.ScriptedDrag = null;
                loop.FixedDt = null;
                loop.TicksPerFrame = 1;
            }
            MvBootMeta.Detach();
            MvSave.Shutdown();
            MvSave.PreferencesOverride = null;
        }

        [UnityTest]
        public IEnumerator Loop_LobbyRunResultLobby_SettlesOnce()
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
            Assert.IsTrue(GameBootstrap.ServicesReady);

            // 로비: Game 씬 초기화가 월드·로비 무대를 만들고 로비 화면을 열었다.
            var loop = MvGameLoop.Current;
            Assert.IsNotNull(loop, "Game 씬에 게임 루프가 없다");
            Assert.AreEqual(MvLoopPhase.Lobby, loop.Phase);
            var store = MvSave.Current;
            Assert.IsNotNull(store);
            long settledBefore = store.Progress.LastSettledRunSeq;
            Assert.IsTrue(loop.FxMaterialsLoaded, "이펙트 머티리얼(Fx/Mat_Normal·Fx/Mat_Add)을 읽지 못했다");

            // 로비 시설 단계: 진입 때 넣고, 강화 반짝임(OnFacilityBump)이 다시 넣는다(로그 오류는 시험 실패).
            Assert.Greater(loop.FacilityApplyCount, 0, "로비 진입이 시설 단계를 넣지 않았다");
            int applied = loop.FacilityApplyCount;
            loop.BumpFacility("tower");
            Assert.AreEqual(applied + 1, loop.FacilityApplyCount, "강화 반짝임이 시설 단계를 다시 넣지 않았다");

            // 출발: 같은 프레임에 런이 시작된다. 스크립트 입력(좌우 드래그)으로 빠르게 돈다.
            loop.FixedDt = 0.05;
            loop.TicksPerFrame = 40;
            loop.ScriptedDrag = f => 2.0 * Math.Sin(f * 0.37);
            Assert.IsTrue(loop.PressStart(), "출발이 런을 시작하지 않았다");
            Assert.AreEqual(MvLoopPhase.Run, loop.Phase);
            Assert.AreEqual(settledBefore + 1, loop.CurrentRunSeq, "런 순번은 마지막 정산 순번 + 1");
            Assert.AreEqual(MvRunSnapshot.SeedFor(loop.CurrentRunSeq), loop.CurrentSeed, "시드는 런 순번에서 결정적으로 만든다");

            elapsed = 0f;
            while (loop.Phase == MvLoopPhase.Run && loop.RunFrames < MaxRunFrames && elapsed < TimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(MvLoopPhase.Result, loop.Phase, $"런이 끝나지 않았다(프레임 {loop.RunFrames})");
            Assert.Greater(loop.RunFrames, 0);
            Assert.AreEqual(1, loop.RunsFinished);

            elapsed = 0f;
            while (!loop.ResultOpen && elapsed < TimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.IsTrue(loop.ResultOpen, "결과 팝업이 열리지 않았다");
            Assert.AreEqual(0, loop.SettleCount, "받기 전에 정산했다");

            // 받기: 정산 1번 → 로비.
            Assert.IsTrue(loop.PressCollect());
            Assert.IsFalse(loop.PressCollect(), "받기가 두 번 눌렸다");
            Assert.AreEqual(1, loop.SettleCount, "정산은 받기 1번에 1번");
            Assert.IsTrue(loop.LastSettleOk, "정산이 실패했다");
            Assert.AreEqual(loop.CurrentRunSeq, MvSave.Current.Progress.LastSettledRunSeq, "정산이 이 런 순번을 기록하지 않았다");

            elapsed = 0f;
            while (loop.Phase != MvLoopPhase.Lobby && elapsed < TimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(MvLoopPhase.Lobby, loop.Phase, "결과 뒤 로비로 돌아오지 않았다");
            Assert.AreEqual(1, loop.SettleCount);
            Assert.Greater(_prefs.Count, 0, "정산이 시험 메모리 저장소에 써지지 않았다");

            // 로비에 있는 동안 1초마다 시설 단계를 다시 넣는다(메타 팝업을 닫은 뒤 반영).
            applied = loop.FacilityApplyCount;
            elapsed = 0f;
            while (loop.FacilityApplyCount == applied && elapsed < 3f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Greater(loop.FacilityApplyCount, applied, "로비에서 시설 단계를 주기적으로 다시 넣지 않았다");
        }

        /// <summary>저장의 벽 기록 JSON → Floor별 칸 HP. 빈 칸(null)·없는 Floor·다른 Area는 비운다. cells = 읽은 칸 합(−1 = 기록 없음).</summary>
        [TestCase(0, 3, 3)]
        [TestCase(1, 3, -1)]
        [TestCase(0, 2, 2)]
        public void ParseBars_ReadsAreaFloors(int area, int floorCount, int cells)
        {
            const string json = "{\"level\":1,\"area\":0,\"bars\":{\"A0\":[[120,null,0],[],[7.5]],\"A2\":[[1]]}}";
            var bars = MvRunSnapshot.ParseBars(json, area, floorCount);
            if (cells < 0)
            {
                Assert.IsNull(bars, "기록이 없는 Area는 null(정본 save.bars[barKey()] 없음)");
                return;
            }
            Assert.AreEqual(floorCount, bars.Length);
            int total = 0;
            foreach (var floor in bars) total += floor.Count;
            Assert.AreEqual(cells, total);
            Assert.AreEqual(120, bars[0][0]);
            Assert.IsFalse(bars[0].ContainsKey(1), "빈 칸은 기록이 없다");
            Assert.AreEqual(0, bars[0][2]);
            Assert.AreEqual(0, bars[1].Count);
            if (floorCount > 2) Assert.AreEqual(7.5, bars[2][0]);
        }

        /// <summary>시드는 런 순번에서 결정적이고, 순번이 다르면 다르다.</summary>
        [Test]
        public void SeedFor_IsDeterministicPerRunSeq()
        {
            Assert.AreEqual(MvRunSnapshot.SeedFor(1), MvRunSnapshot.SeedFor(1));
            Assert.AreNotEqual(MvRunSnapshot.SeedFor(1), MvRunSnapshot.SeedFor(2));
            Assert.AreNotEqual(0UL, MvRunSnapshot.SeedFor(0));
        }
    }
}
