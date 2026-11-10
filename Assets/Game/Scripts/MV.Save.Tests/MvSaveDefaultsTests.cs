using System.Collections.Generic;
using System.Linq;
using MV.Save;
using NUnit.Framework;

namespace MV.Save.Tests
{
    /// <summary>개발 빌드 공통: 열어 둔 저장소를 시험마다 닫는다(프로필당 저장소 1개 규칙).</summary>
    public abstract class MvSaveFixture
    {
        private readonly List<MvSaveStore> _stores = new List<MvSaveStore>();

        internal MvSaveStore Track(MvSaveOpenResult result)
        {
            if (result.Store != null) _stores.Add(result.Store);
            return result.Store;
        }

        [TearDown]
        public void CloseStores()
        {
            foreach (MvSaveStore store in _stores) store.Dispose();
            _stores.Clear();
        }
    }

    /// <summary>09 §9.10 #1 — 새 저장 = 정본 DEF() 대응.</summary>
    public sealed class MvSaveDefaultsTests : MvSaveFixture
    {
        [Test]
        public void NewSave_MatchesProtoDefFields()
        {
            var options = MvSaveTestKit.Options(new MemoryPrefs());
            MvSaveOpenResult result = MvSaveStore.Open(options);
            MvSaveStore save = Track(result);
            Assert.That(result.Status, Is.EqualTo(MvSaveOpenStatus.Created));
            Assert.That(result.Notice, Is.EqualTo(MvSaveNotice.None));

            // DEF: level:1, area:0, cp:0, bars:{}, runs:0, escaped:false, zoneClear:false (zone은 저장하지 않음)
            Assert.That(save.Progress.Level, Is.EqualTo(1));
            Assert.That(save.Progress.Area, Is.EqualTo(0));
            Assert.That(save.Progress.Cp, Is.EqualTo(0));
            Assert.That(save.Progress.BarAreaKeys, Is.Empty);
            Assert.That(save.Progress.Runs, Is.EqualTo(0));
            Assert.That(save.Progress.Escaped, Is.False);
            Assert.That(save.Progress.ZoneClear, Is.False);
            Assert.That(save.Progress.BestArea, Is.EqualTo(0));
            Assert.That(save.Progress.LastSettledRunSeq, Is.EqualTo(0));

            // DEF: gold·souls·dia 0, rb.tokens 0 → 재화. 08의 5~17도 0.
            foreach (int id in MvWealthIds.AllIds) Assert.That(save.GetWealth(id), Is.EqualTo(0), "wealth " + id);
            Assert.That(save.Wealth.Balances.Count, Is.EqualTo(18));

            // DEF: up:{stair,weapon,income}=0, deck:[0,1,0,0,0,0,0,0,0], god:'thunder'
            Assert.That(save.Lobby.Up.Keys, Is.EquivalentTo(new[] { "stair", "weapon", "income" }));
            Assert.That(save.Lobby.Up.Values, Is.All.EqualTo(0));
            Assert.That(save.Lobby.Deck, Is.EqualTo(new[] { 0, 1, 0, 0, 0, 0, 0, 0, 0 }));
            Assert.That(save.God.Selected, Is.EqualTo("thunder"));
            Assert.That(save.God.GodKeys, Is.Empty);

            // DEF: aup:{rate,range,clone,crit,dbl}=0
            Assert.That(save.AreaUp.Levels.Keys, Is.EquivalentTo(new[] { "rate", "range", "clone", "crit", "dbl" }));
            Assert.That(save.AreaUp.Levels.Values, Is.All.EqualTo(0));

            // DEF: rb:{lv:0, pending:0, perks:{power,crit,obst,dcap}=0, cdUntil:0}
            Assert.That(save.Rebirth.Lv, Is.EqualTo(0));
            Assert.That(save.Rebirth.Pending, Is.EqualTo(0));
            Assert.That(save.Rebirth.Perks.Keys, Is.EquivalentTo(new[] { "power", "crit", "obst", "dcap" }));
            Assert.That(save.Rebirth.Perks.Values, Is.All.EqualTo(0));
            Assert.That(save.Rebirth.CdUntil, Is.EqualTo(0));

            // DEF: ads:{day:''} → 사용 수 없음. rb.open 없음 → 해금 없음.
            Assert.That(save.Daily.Day, Is.EqualTo(""));
            Assert.That(save.Daily.Ads, Is.Empty);
            Assert.That(save.Daily.ModePlays, Is.Empty);
            Assert.That(save.Unlock.Opened, Is.Empty);
            Assert.That(save.Unlock.SeenUnlock, Is.Empty);
            Assert.That(save.Unlock.SeenNew, Is.Empty);

            // 정본 ce_mute 기본 false. 08·2차 시스템은 빈 상태.
            Assert.That(save.Settings.Muted, Is.False);
            Assert.That(save.Settings.ContentTab, Is.EqualTo(""));
            Assert.That(save.Shop.Guarantee, Is.Empty);
            Assert.That(save.Research.RunningKey, Is.Null);
            Assert.That(save.Pet.IsEmpty && save.Friend.IsEmpty && save.Gear.IsEmpty && save.Pass.IsEmpty && save.Blessing.IsEmpty, Is.True);

            Assert.That(save.Meta.CreatedAt, Is.EqualTo(MvSaveTestKit.Now));
            Assert.That(save.Meta.ImportSource, Is.EqualTo(""));
            Assert.That(save.Meta.TableVersion, Is.EqualTo(1));
        }

        [Test]
        public void ProtoDef_ConvertsToNewSaveDefaults()
        {
            var config = MvSaveConfig.CreateDefault();
            string converted = MvProtoSave.ToPayloadJson(MvSaveTestKit.ProtoDefJson, config, MvSaveTestKit.Now);
            string defaults = MvSaveDefaults.CreateJson(config, MvSaveTestKit.Now);
            Assert.That(MvSaveJson.DeepEquals(MvSaveJson.Without(converted, MvSaveKeys.Meta), MvSaveJson.Without(defaults, MvSaveKeys.Meta)), Is.True,
                "converted: " + converted + "\ndefaults: " + defaults);
        }

        [Test]
        public void NewSave_RegistersAndStoresEveryContainer()
        {
            var prefs = new MemoryPrefs();
            var options = MvSaveTestKit.Options(prefs);
            MvSaveStore save = Track(MvSaveStore.Open(options));
            Assert.That(MvSaveKeys.All.Count, Is.EqualTo(20));
            foreach (string key in MvSaveKeys.All) Assert.That(save.Repository.IsSavedLocally(key), Is.True, key);
            Assert.That(prefs.HasKey(MvSaveTestKit.Slot(options, 0)), Is.True, "첫 실행 저장이 칸 0에 써져야 한다");

            string first = save.ExportPayloadJson();
            save.Dispose();
            MvSaveOpenResult again = MvSaveStore.Open(options);
            Track(again);
            Assert.That(again.Status, Is.EqualTo(MvSaveOpenStatus.Opened));
            Assert.That(again.LoadedSchema, Is.EqualTo(MvSaveSchema.Current));
            Assert.That(MvSaveJson.DeepEquals(first, again.Store.ExportPayloadJson()), Is.True);
            Assert.That(MvSaveKeys.All.All(key => MvSaveJson.Pick(first, key) != "{}"), Is.True);
        }
    }
}
