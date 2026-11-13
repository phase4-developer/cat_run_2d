using NUnit.Framework;

namespace MV.Data.Tests
{
    /// <summary>MV-DATA-CHARLOOK: 116_charLook 외형 고르기와 검증(MV-SPEC-02 ArtResourcePlan §2).</summary>
    public sealed class MvCharLookTests
    {
        const string T = CharLookData.Table;

        static CharLookData Resolve(MvTables t, string catKey, int slot, int tier) =>
            CharLookResolver.Resolve(t.CharLook.Datas, t.Cat.ByKey(catKey).id, slot, tier);

        [Test]
        public void Resolve_PlayerTierPicksLargestFromTierAtOrBelow()
        {
            var t = TsvFixture.LoadTables();
            var rows = t.CharLook.Datas;
            int player = t.Cat.ByKey("player").id;
            int maxTier = 0;
            foreach (var row in rows)
                if (row.catId == player && row.slot == 0 && row.fromTier > maxTier) maxTier = row.fromTier;
            Assert.That(maxTier, Is.GreaterThan(1));
            for (int tier = 1; tier <= maxTier; tier++)
                Assert.That(Resolve(t, "player", 0, tier).fromTier, Is.EqualTo(tier), $"등급 {tier}");
            Assert.That(Resolve(t, "player", 0, maxTier + 5).fromTier, Is.EqualTo(maxTier), "마지막 세트보다 높은 등급");
            Assert.That(Resolve(t, "player", 0, 0).fromTier, Is.EqualTo(1), "등급 0");
        }

        [Test]
        public void Resolve_SlotBeyondLastUsesLastSlot()
        {
            var t = TsvFixture.LoadTables();
            var first = Resolve(t, "friend", 0, 1);
            Assert.That(first, Is.Not.Null);
            Assert.That(Resolve(t, "friend", first.slot + 99, 1).id, Is.EqualTo(Resolve(t, "friend", LastSlot(t, first.catId), 1).id));
        }

        [Test]
        public void Resolve_UnknownCat_ReturnsNull()
        {
            var t = TsvFixture.LoadTables();
            Assert.That(CharLookResolver.Resolve(t.CharLook.Datas, -1, 0, 1), Is.Null);
        }

        [Test]
        public void EveryCat_HasLookRow()
        {
            var t = TsvFixture.LoadTables();
            foreach (var cat in t.Cat.Datas)
                Assert.That(CharLookResolver.Resolve(t.CharLook.Datas, cat.id, 0, 1), Is.Not.Null, cat.key);
        }

        [TestCase("116001001", "fromTier", "99", "fromTier 1 행이 없습니다")]
        [TestCase("116001002", "fromTier", "1", "겹칩니다")]
        [TestCase("116002001", "slot", "1", "0부터 연속")]
        [TestCase("116001001", "scale", "0", "scale은 0보다")]
        [TestCase("116001001", "wheelAtlas", "", "wheel과 wheelAtlas")]
        public void Validator_RejectsBrokenRows(string id, string column, string value, string fragment)
        {
            var report = TsvFixture.Validate((T, TsvFixture.Set(TsvFixture.Read(T), id, column, value)));
            Assert.That(report.HasError, Is.True, report.Summary(60));
            Assert.That(report.Summary(200), Does.Contain(fragment));
        }

        static int LastSlot(MvTables t, int catId)
        {
            int last = 0;
            foreach (var row in t.CharLook.Datas)
                if (row.catId == catId && row.slot > last) last = row.slot;
            return last;
        }
    }
}
