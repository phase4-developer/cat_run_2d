using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MV.Save;
using NUnit.Framework;
using UnityEngine;

namespace MV.Save.Tests
{
    /// <summary>09 §9.10 #6 — 표가 바뀐 뒤 저장 정리(09 §9.6). 표 값은 손으로 만든 목록(실제 표 대조는 Boot PlayMode 시험).</summary>
    public sealed class MvSaveReferenceFixTests : MvSaveFixture
    {
        private static MvSaveReferenceCatalog Catalog() => new MvSaveReferenceCatalog
        {
            GodKeys = new HashSet<string> { "thunder", "flame" },
            MaxWeaponTier = 8,
            MaxAreaIndex = 29,
            BarCellLimits = new Dictionary<int, int[]> { [0] = new[] { 24, 30, 36 }, [1] = new[] { 24, 30, 36 } },
            LobbyUpMax = new Dictionary<string, int> { ["stair"] = 30, ["weapon"] = 149, ["income"] = 50 },
            AreaUpMax = new Dictionary<string, int> { ["rate"] = 10, ["range"] = 10, ["clone"] = 4, ["crit"] = 10, ["dbl"] = 10 },
            PerkMax = new Dictionary<string, int> { ["power"] = 20, ["crit"] = 20, ["obst"] = 10, ["dcap"] = 20 },
            AdPlacementKeys = new HashSet<string> { "dia", "skip" },
            ModeContentKeys = null,
            ContentOpenKeys = new HashSet<string> { "rebirth", "quest" },
        };

        [Test]
        public void UnknownGod_DeckLength_AreaOverflow_FixedOnOpenAndSavedOnce()
        {
            var prefs = new MemoryPrefs();
            var options = MvSaveTestKit.Options(prefs);
            MvSaveStore save = Track(MvSaveStore.Open(options));
            save.ApplyAndSaveJson(
                "{\"mvGod\":{\"sel\":\"ghost\"},\"mvLobby\":{\"deck\":[0,1,2,0,0,0,0,0,0,3,4]}," +
                "\"mvProgress\":{\"area\":40,\"cp\":2,\"bestArea\":40,\"bars\":{\"A40\":[[1]]}}}",
                false, MvSaveKeys.God, MvSaveKeys.Lobby, MvSaveKeys.Progress);
            save.Dispose();

            options.References = Catalog();
            MvSaveOpenResult result = MvSaveStore.Open(options);
            save = Track(result);
            Assert.That(result.Status, Is.EqualTo(MvSaveOpenStatus.Opened));
            Assert.That(result.ReferenceFixes, Is.EquivalentTo(new[]
            {
                "mvGod.sel", "mvLobby.deck", "mvProgress.area", "mvProgress.cp", "mvProgress.bars", "mvProgress.bestArea",
            }));
            Assert.That(save.God.Selected, Is.EqualTo("thunder"));
            // 길이 = 최대 tier 8 + 1. 넘친 등급(9·10번 칸 3+4)은 최대 등급 칸에 더한다.
            Assert.That(save.Lobby.Deck, Is.EqualTo(new[] { 0, 1, 2, 0, 0, 0, 0, 0, 7 }));
            Assert.That(save.Progress.Area, Is.EqualTo(29));
            Assert.That(save.Progress.Cp, Is.EqualTo(0));
            Assert.That(save.Progress.BarAreaKeys, Is.Empty);
            Assert.That(save.Progress.BestArea, Is.EqualTo(29));
            string fixedPayload = save.ExportPayloadJson();
            save.Dispose();

            // 정리 결과가 저장됐다: 다시 열면 할 일이 없고 값이 같다.
            MvSaveOpenResult again = MvSaveStore.Open(options);
            Track(again);
            Assert.That(again.ReferenceFixes, Is.Empty);
            Assert.That(MvSaveJson.DeepEquals(fixedPayload, again.Store.ExportPayloadJson()), Is.True);
        }

        [Test]
        public void ShortDeck_PadsWithZero()
        {
            MvSaveStore save = Track(MvSaveStore.Open(MvSaveTestKit.Options(new MemoryPrefs())));
            save.ApplyAndSaveJson("{\"mvLobby\":{\"deck\":[0,1,2]}}", false, MvSaveKeys.Lobby);
            IReadOnlyList<string> fixes = MvSaveReferenceFix.Run(save, Catalog(), MvSaveConfig.CreateDefault());
            Assert.That(fixes, Is.EqualTo(new[] { "mvLobby.deck" }));
            Assert.That(save.Lobby.Deck, Is.EqualTo(new[] { 0, 1, 2, 0, 0, 0, 0, 0, 0 }));
        }

        [Test]
        public void UpgradeAndKeyReferences_ClampOrDropUnknown()
        {
            MvSaveStore save = Track(MvSaveStore.Open(MvSaveTestKit.Options(new MemoryPrefs())));
            string tooMany = "[" + string.Join(",", Enumerable.Repeat("5", 25)) + "]";
            save.ApplyAndSaveJson(
                "{\"mvLobby\":{\"up\":{\"stair\":99,\"ghost\":3}},\"mvAreaUp\":{\"lv\":{\"clone\":9}},\"mvRebirth\":{\"perks\":{\"obst\":50}}," +
                "\"mvDaily\":{\"day\":\"" + MvSaveTestKit.Today + "\",\"ads\":{\"dia\":1,\"ghost\":2},\"modePlays\":{\"mode.any\":1}}," +
                "\"mvUnlock\":{\"opened\":[\"rebirth\",\"ghost\"],\"seenNew\":[\"ghost\"]}," +
                "\"mvProgress\":{\"bars\":{\"A0\":[" + tooMany + "],\"A1\":[[1,2],[3]],\"A7\":[[1]]}}}",
                false, MvSaveKeys.Lobby, MvSaveKeys.AreaUp, MvSaveKeys.Rebirth, MvSaveKeys.Daily, MvSaveKeys.Unlock, MvSaveKeys.Progress);

            IReadOnlyList<string> fixes = MvSaveReferenceFix.Run(save, Catalog(), MvSaveConfig.CreateDefault());

            Assert.That(fixes, Is.EquivalentTo(new[]
            {
                "mvLobby.up", "mvAreaUp.lv", "mvRebirth.perks", "mvDaily.ads", "mvUnlock.opened", "mvUnlock.seenNew", "mvProgress.bars",
            }));
            Assert.That(save.Lobby.GetUp("stair"), Is.EqualTo(30));
            Assert.That(save.Lobby.Up.ContainsKey("ghost"), Is.False);
            Assert.That(save.AreaUp.GetLevel("clone"), Is.EqualTo(4));
            Assert.That(save.Rebirth.GetPerk("obst"), Is.EqualTo(10));
            Assert.That(save.Daily.Ads.Keys, Is.EquivalentTo(new[] { "dia" }));
            Assert.That(save.Daily.ModePlays.Keys, Is.EquivalentTo(new[] { "mode.any" }), "400_content 표가 없으면 modePlays는 그대로 둔다");
            Assert.That(save.Unlock.Opened, Is.EqualTo(new[] { "rebirth" }));
            Assert.That(save.Unlock.SeenNew, Is.Empty);
            // A0은 Floor 1 칸 수 25 > rows×3(24), A7은 표에 없는 Area → 버림. A1은 맞다.
            Assert.That(save.Progress.BarAreaKeys, Is.EqualTo(new[] { "A1" }));
        }

        [Test]
        public void NothingToFix_DoesNotSave()
        {
            var prefs = new MemoryPrefs();
            var clock = new TestClock();
            MvSaveStore save = Track(MvSaveStore.Open(MvSaveTestKit.Options(prefs, clock)));
            Dictionary<string, string> before = prefs.Copy();
            long lastSaveAt = save.Meta.LastSaveAt;
            clock.Now += 1000;

            Assert.That(MvSaveReferenceFix.Run(save, Catalog(), MvSaveConfig.CreateDefault()), Is.Empty);
            Assert.That(prefs.Values, Is.EquivalentTo(before));
            Assert.That(save.Meta.LastSaveAt, Is.EqualTo(lastSaveAt));
        }

        [Test]
        public void TableVersionChange_IsRecorded()
        {
            MvSaveStore save = Track(MvSaveStore.Open(MvSaveTestKit.Options(new MemoryPrefs())));
            var config = MvSaveConfig.CreateDefault();
            config.TableVersion = 7;
            Assert.That(MvSaveReferenceFix.Run(save, Catalog(), config), Is.EqualTo(new[] { "mvMeta.tableVersion" }));
            Assert.That(save.Meta.TableVersion, Is.EqualTo(7));
        }
    }

    /// <summary>재화 id 상수와 <c>01_wealth</c> 표, Boot 저장 격리 지점.</summary>
    public sealed class MvSaveIntegrationTests
    {
        [TearDown]
        public void Reset()
        {
            MvSave.Shutdown();
            MvSave.PreferencesOverride = null;
        }

        [Test]
        public void WealthIds_MatchWealthTableTsv()
        {
            string path = Path.Combine(Application.dataPath, "Game", "GameData", "Table", "TSV", "01_wealth.txt");
            string[] lines = File.ReadAllLines(path);
            string[] header = lines[0].TrimStart('﻿').Split('\t');
            int idColumn = Array.IndexOf(header, "id");
            int typeColumn = Array.IndexOf(header, "type");
            Assert.That(idColumn, Is.GreaterThanOrEqualTo(0));
            Assert.That(typeColumn, Is.GreaterThanOrEqualTo(0));

            var rows = lines.Skip(1)
                .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#", StringComparison.Ordinal))
                .Select(line => line.Split('\t'))
                .Select(cells => new KeyValuePair<int, string>(int.Parse(cells[idColumn]), cells[typeColumn].Trim()))
                .ToList();
            Assert.That(rows.Count, Is.EqualTo(MvWealthIds.AllIds.Count));
            Assert.That(MvWealthIds.Mismatches(rows), Is.Empty);

            // 다르면 잡는다.
            rows[0] = new KeyValuePair<int, string>(rows[0].Key, "renamed");
            rows.Add(new KeyValuePair<int, string>(1001999, "extra"));
            Assert.That(MvWealthIds.Mismatches(rows).Count, Is.EqualTo(2));
        }

        [Test]
        public void Init_UsesPreferencesOverride_AndNotifiesCurrentChanged()
        {
            var optionPrefs = new MemoryPrefs();
            MvMemorySavePrefs isolated = MvSave.UseMemoryPreferences();
            var seen = new List<MvSaveStore>();
            Action<MvSaveStore> handler = store => seen.Add(store);
            MvSave.CurrentChanged += handler;
            try
            {
                MvSaveOpenResult result = MvSave.Init(MvSaveTestKit.Options(optionPrefs));
                Assert.That(result.Status, Is.EqualTo(MvSaveOpenStatus.Created));
                Assert.That(isolated.Count, Is.GreaterThan(0), "끼운 메모리 저장소에 써야 한다");
                Assert.That(optionPrefs.Values, Is.Empty, "옵션의 저장소는 쓰지 않는다");
                Assert.That(seen, Is.EqualTo(new[] { result.Store }));

                MvSave.Shutdown();
                Assert.That(seen.Count, Is.EqualTo(2));
                Assert.That(seen[1], Is.Null);
                Assert.That(MvSave.Current, Is.Null);
            }
            finally { MvSave.CurrentChanged -= handler; }
        }
    }
}
