using System.Linq;
using MV.Meta;
using NUnit.Framework;

namespace MV.UI.Tests
{
    /// <summary>
    /// 에디터 전용 재화 치트(MV-DEV-CHEAT-wealth). 지급이 테스트 패널과 같은 메타 요청(<see cref="MvDebugRequests.Grant"/>)으로만 나가는지 본다.
    /// 보장하지 않는 범위: 실제 저장 반영·로비 재화 칸 갱신(메타 백엔드 DebugGrant와 LobbyScreen.Tick 몫)과 창 조작은 Editor 확인(NOT_RUN).
    /// </summary>
    public sealed class MvWealthCheatTests
    {
        // 01_wealth id: gold 1001001, dia 1001002
        [TestCase("gold", "1001001")]
        [TestCase("dia", "1001002")]
        public void Grant_SendsDebugGrantWithWealthId(string type, string id)
        {
            var port = new FakeMetaPort(null);
            var r = MvWealthCheat.Grant(port, type, 12345);
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(1, port.Debug.Count);
            Assert.AreEqual((MvDebugRequests.Grant, id, 12345L), port.Debug[0]);
        }

        [Test]
        public void Types_AreGoldAndDia()
        {
            CollectionAssert.AreEqual(new[] { "gold", "dia" }, MvWealthCheat.Types.ToArray());
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void Grant_RejectsNonPositiveAmount_WithoutSending(long amount)
        {
            var port = new FakeMetaPort(null);
            var r = MvWealthCheat.Grant(port, "gold", amount);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(MvMetaErrors.BadParams, r.Code);
            Assert.IsEmpty(port.Debug);
        }

        [Test]
        public void Grant_RejectsUnknownType_WithoutSending()
        {
            var port = new FakeMetaPort(null);
            var r = MvWealthCheat.Grant(port, "notWealth", 10);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(MvMetaErrors.UnknownKey, r.Code);
            Assert.IsEmpty(port.Debug);
        }

        [Test]
        public void Grant_NotReady_FailsWithoutSending()
        {
            var port = new FakeMetaPort(null) { Ready = false };
            var r = MvWealthCheat.Grant(port, "dia", 10);
            Assert.IsFalse(r.Ok);
            Assert.AreEqual(MvMetaErrors.NoStore, r.Code);
            Assert.IsEmpty(port.Debug);
            Assert.IsFalse(MvWealthCheat.Grant(null, "dia", 10).Ok);
        }
    }
}
