using NUnit.Framework;

namespace MV.UI.Tests
{
    /// <summary>
    /// MV-UI3-RESULTCLOSE: 결과 받기 뒤 로비 이동은 결과 팝업 닫기 전환이 끝난 뒤에만, 어떤 경우에도 팝업이 남지 않게.
    /// 가짜 팝업은 Framework <c>UIPopup.ClosePopup</c>과 같이 전환 중이면 닫기를 무시한다(사용자 2026-11-23 재현: 열림 연출 중 미리 터치).
    /// 실제 화면 확인은 사람 확인(NOT_RUN).
    /// </summary>
    public sealed class ResultCloseSequenceTests
    {
        sealed class FakePopup : ResultCloseSequence.IPort
        {
            public bool IsOpen { get; set; } = true;
            public bool IsTransitioning { get; set; }
            public bool RefuseClose;
            public int Closes, Forces;
            public ResultCloseSequence Seq;

            /// <summary>Framework ClosePopup: 열려 있지 않거나 전환 중이면 무시, 아니면 닫기 전환 시작.</summary>
            public void Close()
            {
                Closes++;
                if (RefuseClose || !IsOpen || IsTransitioning) return;
                IsOpen = false;
                IsTransitioning = true;
            }

            /// <summary>Framework ForceClosePopup: 전환 없이 onHide → 파괴.</summary>
            public void ForceClose()
            {
                Forces++;
                IsOpen = false;
                IsTransitioning = false;
                Seq.Closed();
            }

            public void EndShow() => IsTransitioning = false;

            /// <summary>닫기 전환 끝(onHide).</summary>
            public void EndHide()
            {
                IsTransitioning = false;
                Seq.Closed();
            }
        }

        static (FakePopup popup, ResultCloseSequence seq) New(bool showing)
        {
            var p = new FakePopup { IsTransitioning = showing };
            p.Seq = new ResultCloseSequence(p);
            return (p, p.Seq);
        }

        [Test]
        public void CollectDuringShowTransition_WaitsShow_ThenClose_ThenLobby()
        {
            var (p, seq) = New(showing: true);
            int lobby = 0;

            Assert.That(seq.Begin(() => lobby++), Is.True);
            Assert.That(lobby, Is.Zero, "열림 전환 중 미리 터치: 바로 로비로 가지 않는다");
            Assert.That(p.Closes, Is.Zero, "전환 중 닫기는 Framework가 무시하므로 아직 걸지 않는다");
            Assert.That(seq.WaitingShow, Is.True);

            seq.Tick();
            Assert.That(p.Closes, Is.Zero, "열림 전환이 끝나기 전에는 계속 기다린다");

            p.EndShow();
            seq.Tick();
            Assert.That(p.Closes, Is.EqualTo(1), "열림이 끝나면 닫기 전환을 건다");
            Assert.That(p.IsOpen, Is.False);
            Assert.That(lobby, Is.Zero, "닫기 전환 중에는 로비로 가지 않는다");

            p.EndHide();
            Assert.That(lobby, Is.EqualTo(1), "닫힘 완료 뒤 로비 1번");
            Assert.That(seq.Done, Is.True);
        }

        [Test]
        public void Collect_Idle_ClosesNow_LobbyOnlyAfterHide()
        {
            var (p, seq) = New(showing: false);
            int lobby = 0;

            seq.Begin(() => lobby++);
            Assert.That(p.Closes, Is.EqualTo(1));
            Assert.That(p.IsOpen, Is.False);
            Assert.That(lobby, Is.Zero, "닫기 연출이 끝나기 전에는 로비로 가지 않는다");

            seq.Tick();
            Assert.That(p.Closes, Is.EqualTo(1), "닫기는 한 번만 건다");

            p.EndHide();
            Assert.That(lobby, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedInput_IgnoredAfterFirst()
        {
            var (p, seq) = New(showing: false);
            int a = 0, b = 0;

            Assert.That(seq.Begin(() => a++), Is.True);
            Assert.That(seq.Begin(() => b++), Is.False, "중복 입력은 무시");
            p.EndHide();
            seq.Closed();

            Assert.That(a, Is.EqualTo(1));
            Assert.That(b, Is.Zero);
            Assert.That(p.Closes, Is.EqualTo(1));
        }

        [Test]
        public void CloseRefused_ForcesClose_NoPopupLeft()
        {
            var (p, seq) = New(showing: false);
            p.RefuseClose = true;
            int lobby = 0;

            seq.Begin(() => lobby++);

            Assert.That(p.Forces, Is.EqualTo(1), "닫기가 거절되면 강제로 닫는다");
            Assert.That(p.IsOpen, Is.False, "결과 팝업이 남지 않는다");
            Assert.That(lobby, Is.EqualTo(1));
        }

        [Test]
        public void DestroyedMidClose_StillGoesToLobbyOnce()
        {
            var (p, seq) = New(showing: false);
            int lobby = 0;

            seq.Begin(() => lobby++);
            seq.Closed(); // 닫기 전환 중 파괴(OnDestroy)
            p.EndHide();  // 늦게 온 onHide

            Assert.That(lobby, Is.EqualTo(1));
        }

        [Test]
        public void HiddenBeforeCollect_DoesNotGoToLobby()
        {
            var (p, seq) = New(showing: false);
            seq.Closed(); // 받기 전 닫힘(씬 정리 등)
            Assert.That(seq.Done, Is.False);

            int lobby = 0;
            p.IsOpen = false;
            seq.Begin(() => lobby++);
            Assert.That(lobby, Is.EqualTo(1), "이미 닫혔으면(전환 없음) 바로 다음으로 간다");
            Assert.That(p.Closes, Is.Zero);
        }

        [Test]
        public void AlreadyHiding_WaitsHideComplete()
        {
            var (p, seq) = New(showing: false);
            p.IsOpen = false;
            p.IsTransitioning = true; // 다른 경로가 이미 닫는 중
            int lobby = 0;

            seq.Begin(() => lobby++);
            seq.Tick();
            Assert.That(lobby, Is.Zero);
            Assert.That(p.Closes, Is.Zero);

            p.EndHide();
            Assert.That(lobby, Is.EqualTo(1));
        }
    }
}
