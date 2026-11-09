using System;
using System.Collections.Generic;
using System.Linq;
using MV.Data;
using MV.Run;
using NUnit.Framework;

namespace MV.Sound.Tests
{
    /// <summary>RunEvent → 소리 키가 정본 SND 호출 지점(L1622~1699 밖 SND.play)과 1:1인지.</summary>
    public sealed class MvRunSoundRouterTests
    {
        sealed class Site
        {
            public string Line;
            public Func<MvTables, RunEvent[]> Events;
            public string[] Expected;
        }

        static int Kind(MvTables t, string key) => t.ObjectKind.ByKey(key)?.id ?? throw new ArgumentException(key);

        static RunEvent Ev(RunEventType type, int objId = 0, int kindId = 0, int a = 0, int b = 0, int c = 0, bool flag = false, bool quiet = false) =>
            new RunEvent(type, objId: objId, kindId: kindId, a: a, b: b, c: c, flag: flag, quiet: quiet);

        static Site S(string line, Func<MvTables, RunEvent[]> events, params string[] expected) =>
            new Site { Line = line, Events = events, Expected = expected };

        /// <summary>정본 런 쪽 SND.play 호출 지점 목록. 각 줄의 조건이 참인 경우와 거짓인 경우를 함께 둔다.</summary>
        static IEnumerable<Site> RunSites()
        {
            yield return S("L1890 계단 게이지 Lv 상승", t => new[] { Ev(RunEventType.GaugeLevelUp, a: 2) }, "lvup");
            yield return S("L1912 tower 피격", t => new[] { Ev(RunEventType.Hit, 1, Kind(t, "tower")) }, "clank");
            yield return S("L1912 tower 피격(quiet도 울림)", t => new[] { Ev(RunEventType.Hit, 1, Kind(t, "tower"), quiet: true) }, "clank");
            yield return S("L1919 stair 피격(잔재: hittable 아님)", t => new[] { Ev(RunEventType.Hit, 1, Kind(t, "stair")) });
            yield return S("L1921 enemy 피격(소리 없음)", t => new[] { Ev(RunEventType.Hit, 1, Kind(t, "enemy")) });
            yield return S("L1930 wall 피격(소리 없음)", t => new[] { Ev(RunEventType.Hit, 1, Kind(t, "wall")) });
            yield return S("L1945 sgate·cgate 피격(소리 없음)", t => new[] { Ev(RunEventType.Hit, 1, Kind(t, "sgate")), Ev(RunEventType.Hit, 2, Kind(t, "cgate")) });
            yield return S("L1925 enemy 처치 coin(8% 캣닢은 소리 없음)", t => new[]
            {
                Ev(RunEventType.Break, 5, Kind(t, "enemy"), a: -1), Ev(RunEventType.Gain, 5, a: (int)ConsumeType.RunGold),
                Ev(RunEventType.Gain, 5, a: (int)ConsumeType.RunDia),
            }, "coin");
            yield return S("L1936+L1937 wall 파괴 wood + 생선 soul", t => new[]
            {
                Ev(RunEventType.Break, 7, Kind(t, "wall"), a: (int)FieldReward.Soul), Ev(RunEventType.Gain, 7, a: (int)ConsumeType.RunSouls),
            }, "wood", "soul");
            yield return S("L1936+L1939 wall 파괴 wood + 캣닢 gem", t => new[]
            {
                Ev(RunEventType.Break, 7, Kind(t, "wall"), a: (int)FieldReward.Dia), Ev(RunEventType.Gain, 7, a: (int)ConsumeType.RunDia),
            }, "wood", "gem");
            yield return S("L1936+L1941 wall 파괴 wood + 츄르 coin", t => new[]
            {
                Ev(RunEventType.Break, 7, Kind(t, "wall"), a: (int)FieldReward.Gold), Ev(RunEventType.Gain, 7, a: (int)ConsumeType.RunGold),
            }, "wood", "coin");
            yield return S("L1964 gem 상자 파괴", t => new[]
            {
                Ev(RunEventType.Break, 9, Kind(t, "gem"), a: -1), Ev(RunEventType.Gain, 9, a: (int)ConsumeType.RunDia),
            }, "gem");
            yield return S("L1964 gemBonus 상자 파괴", t => new[] { Ev(RunEventType.Break, 9, Kind(t, "gemBonus"), a: -1) }, "gem");
            yield return S("L1966 coin 상자: 타격당 츄르는 소리 없음, 파괴 coin", t => new[]
            {
                Ev(RunEventType.Hit, 9, Kind(t, "coin")), Ev(RunEventType.Gain, 9, a: (int)ConsumeType.RunGold),
                Ev(RunEventType.Break, 9, Kind(t, "coin"), a: -1), Ev(RunEventType.Gain, 9, a: (int)ConsumeType.RunGold),
            }, "coin");
            yield return S("L1966 coinBonus 상자 파괴", t => new[] { Ev(RunEventType.Break, 9, Kind(t, "coinBonus"), a: -1) }, "coin");
            yield return S("L2028 골목 진입", t => new[] { Ev(RunEventType.FloorEnter, a: 2, flag: true) }, "floor");
            yield return S("L2046 sgate 통과 n>0", t => new[] { Ev(RunEventType.GateTake, 3, Kind(t, "sgate"), a: 4) }, "gate");
            yield return S("L2046 sgate 통과 n=0(소리 없음)", t => new[] { Ev(RunEventType.GateTake, 3, Kind(t, "sgate"), a: 0) });
            yield return S("L2051 cgate 통과 made>0", t => new[] { Ev(RunEventType.GateTake, 4, Kind(t, "cgate"), a: 2) }, "forge");
            yield return S("L2051 cgate 통과 made=0(소리 없음)", t => new[] { Ev(RunEventType.GateTake, 4, Kind(t, "cgate"), a: 0) });
            yield return S("L2049·L2054 게이트 놓침(소리 없음)", t => new[] { Ev(RunEventType.GateMiss, 3, Kind(t, "sgate"), a: 4) });
            yield return S("L2063 쥐 훔치기(0이어도)", t => new[] { Ev(RunEventType.Steal, 11, a: 0) }, "steal");
            yield return S("L2068 장애물 막힘", t => new[] { Ev(RunEventType.Blocked, 12) }, "wood");
            yield return S("L2091 본체 발사", t => new[] { Ev(RunEventType.Fire, 1, a: 0, c: 0) }, "throw");
            yield return S("L2091 본체 2연발은 한 번", t => new[] { Ev(RunEventType.Fire, 1, a: 0, c: -1), Ev(RunEventType.Fire, 2, a: 0, c: 1) }, "throw");
            yield return S("L2091 친구 발사(소리 없음)", t => new[] { Ev(RunEventType.Fire, 1, a: 1), Ev(RunEventType.Fire, 2, a: 2) });
            yield return S("L2083 창(return, 소리 없음)", t => new[] { Ev(RunEventType.Fire, 1, a: 0, flag: true) });
            yield return S("L2107 명중", t => new[] { Ev(RunEventType.ProjImpact, 1) }, "hit");
            yield return S("L2107 치명 명중", t => new[] { Ev(RunEventType.ProjImpact, 1, flag: true) }, "crit");
            yield return S("L2260 계단 오르기 시작", t => new[] { Ev(RunEventType.ClimbStart, a: 30, b: 30) }, "gate");
            yield return S("L2270 칸 등장 i%4==0", t => Enumerable.Range(0, 9).Select(i => Ev(RunEventType.ClimbStepShown, a: i)).ToArray(), "tick", "tick", "tick");
            yield return S("L2277 랩 카드", t => new[] { Ev(RunEventType.ClimbLap, a: 1, b: 75) }, "card_4");
            yield return S("L2281 등급 상승 1~4", t => Enumerable.Range(1, 4).Select(i => Ev(RunEventType.ClimbTierUp, a: i)).ToArray(),
                "tierup_1", "tierup_2", "tierup_3", "tierup_4");
            yield return S("L2286 착지", t => new[] { Ev(RunEventType.ClimbEnd, a: 30, b: 2) }, "land");
            yield return S("L2368 룰렛 넘김은 첫 카드만", t => new[] { Ev(RunEventType.RouletteFlip, a: 0), Ev(RunEventType.RouletteFlip, a: 1), Ev(RunEventType.RouletteFlip, a: 2) }, "tick");
            yield return S("L2369 룰렛 확정 card(pick.t)", t => Enumerable.Range(0, 5).Select(i => Ev(RunEventType.RouletteChosen, a: 0, b: 1, c: i)).ToArray(),
                "card_0", "card_1", "card_2", "card_3", "card_4");
            yield return S("L2377 카드 적용", t => new[] { Ev(RunEventType.CardApplied, a: 1, b: 110001001) }, "buy");
            yield return S("L2408 게이트 도착 즉시 win", t => new[] { Ev(RunEventType.RunFinished, a: (int)FinishReason.Gate, flag: false) }, "win");
            yield return S("L2408 장애물 종료 350ms 뒤 fail", t => new[] { Ev(RunEventType.RunFinished, a: (int)FinishReason.Wall, flag: false) }, "fail@350ms");
            yield return S("L2408 pass·zoneEnd면 350ms 뒤 win", t => new[] { Ev(RunEventType.RunFinished, a: (int)FinishReason.Wall, flag: true) }, "win@350ms");
            yield return S("L3165 워프 단계 >0 tierup(min(4, st+1))", t => Enumerable.Range(0, 4).Select(i => Ev(RunEventType.ClimbWarpStage, a: i)).ToArray(),
                "tierup_2", "tierup_3", "tierup_4");
        }

        static List<string> Route(MvTables t, IEnumerable<RunEvent> events)
        {
            var router = new MvRunSoundRouter(() => t);
            var cues = new List<MvSoundCue>();
            foreach (var e in events) router.Route(e, cues);
            return cues.Select(c => c.ToString()).ToList();
        }

        [Test]
        public void RunEvents_MatchCanonicalSndCallSites()
        {
            var t = SoundTestTables.Load();
            var fails = new List<string>();
            foreach (var s in RunSites())
            {
                var got = Route(t, s.Events(t));
                if (!got.SequenceEqual(s.Expected))
                    fails.Add($"{s.Line}: 기대 [{string.Join(",", s.Expected)}] 실제 [{string.Join(",", got)}]");
                foreach (var k in s.Expected)
                {
                    var key = k.Split('@')[0];
                    var row = t.Sound.ByKey(key);
                    if (row == null || row.soundType != SoundType.Sfx) fails.Add($"{s.Line}: 03_sound에 효과음 `{key}` 없음");
                }
            }
            Assert.That(fails, Is.Empty, string.Join("\n", fails));
        }

        /// <summary>정본 L2281은 C.tier = −1에서 시작해 tierFor = 0(COMMON)이어도 tierup(0)을 부른다. 키는 정본 그대로 낸다.</summary>
        [Test]
        public void ClimbTierUp0_FollowsCanonicalKey()
        {
            var t = SoundTestTables.Load();
            Assert.That(Route(t, new[] { Ev(RunEventType.ClimbTierUp, a: 0) }), Is.EqualTo(new[] { "tierup_0" }));
        }

        static readonly RunEventType[] Sounding =
        {
            RunEventType.FloorEnter, RunEventType.Fire, RunEventType.ProjImpact, RunEventType.Hit, RunEventType.Break, RunEventType.Gain,
            RunEventType.GateTake, RunEventType.GaugeLevelUp, RunEventType.Steal, RunEventType.Blocked, RunEventType.ClimbStart,
            RunEventType.ClimbStepShown, RunEventType.ClimbLap, RunEventType.ClimbTierUp, RunEventType.ClimbWarpStage, RunEventType.ClimbEnd,
            RunEventType.RouletteFlip, RunEventType.RouletteChosen, RunEventType.CardApplied, RunEventType.RunFinished,
        };

        /// <summary>
        /// 새 RunEventType이 생기면 소리 여부를 정하게 한다(04 §4.18.1). 소리 없는 이벤트는 어떤 필드 값이어도 조용하다.
        /// RunStarted는 BGM 레이어만 바꾼다(효과음 없음).
        /// </summary>
        [Test]
        public void SilentEvents_ProduceNoSound()
        {
            var t = SoundTestTables.Load();
            var loud = new List<string>();
            foreach (RunEventType type in Enum.GetValues(typeof(RunEventType)))
            {
                if (Array.IndexOf(Sounding, type) >= 0) continue;
                var got = Route(t, new[]
                {
                    Ev(type, 1, Kind(t, "tower"), a: 0, b: 1, c: 0, flag: false),
                    Ev(type, 2, Kind(t, "wall"), a: 1, b: 0, c: 4, flag: true),
                });
                if (got.Count > 0) loud.Add($"{type}: {string.Join(",", got)}");
            }
            Assert.That(loud, Is.Empty, string.Join("\n", loud));
        }

        [Test]
        public void GapGroup_StripsVariantNumber()
        {
            Assert.That(MvSoundKeys.GapGroup("tierup_3"), Is.EqualTo("tierup"));
            Assert.That(MvSoundKeys.GapGroup("card_0"), Is.EqualTo("card"));
            Assert.That(MvSoundKeys.GapGroup("bgm_lobby"), Is.EqualTo("bgm_lobby"));
            Assert.That(MvSoundKeys.GapGroup("hit"), Is.EqualTo("hit"));
            Assert.That(MvSoundKeys.GapGroup("_1"), Is.EqualTo("_1"));
        }
    }
}
