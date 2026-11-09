using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MV.Data
{
    // 08 콘텐츠 자료(야옹신·카드·메타 성장 비용) 의미 규칙.
    public static partial class MvTableValidator
    {
        /// <summary>08 §8.5.0 메타 능력치 7종(표에 문자열로 적는다).</summary>
        static readonly HashSet<string> s_contentStatKeys = new HashSet<string>(StringComparer.Ordinal)
            { "Dmg", "FireRate", "Range", "CritDmg", "Gold", "CritChance", "DoubleChance" };

        static void CheckContentData(MvTables t, MvValidationReport r)
        {
            CheckContentGods(t, r);
            CheckContentCardIds(t, r);
            CheckContentLevelPriceIds(t, r);
            CheckContentLevelPrices(t, r);
        }

        // ---------------- 113 ↔ 110 (07 §7.7.3, §7.7.6, 08 §8.3.2) ----------------

        static void CheckContentGods(MvTables t, MvValidationReport r)
        {
            const string name = GodData.Table;
            var groups = new Dictionary<int, int>();
            foreach (var g in t.God.Datas.OrderBy(x => x.id))
            {
                if (g.cardGroup < 1) r.Error(name, g.id, "cardGroup", "cardGroup은 1 이상이어야 합니다(0은 공용 카드)");
                else if (groups.TryGetValue(g.cardGroup, out int other))
                    r.Error(name, g.id, "cardGroup", $"cardGroup {g.cardGroup}를 113 행 {other}도 씁니다");
                else groups.Add(g.cardGroup, g.id);

                var cards = t.Card.GetGroup(g.cardGroup);
                if (g.cardGroup >= 1 && cards.Count == 0)
                    r.Error(name, g.id, "cardGroup", $"110_card에 group {g.cardGroup} 카드가 없습니다");
                else if (g.active && !cards.Any(c => c.active))
                    r.Error(name, g.id, "active", $"active 신인데 110_card group {g.cardGroup}에 active 카드가 없습니다");

                bool hasPrice = g.unlockPrice.Count > 0;
                switch (g.unlockType)
                {
                    case 0:
                        if (g.openId != 0 || hasPrice) r.Error(name, g.id, "unlockType", "unlockType 0(무료)은 openId·unlockPrice를 비워야 합니다");
                        break;
                    case 1:
                        if (g.openId == 0) r.Error(name, g.id, "openId", "unlockType 1(contentOpen)은 openId가 있어야 합니다");
                        if (hasPrice) r.Error(name, g.id, "unlockPrice", "unlockType 1은 unlockPrice를 비워야 합니다");
                        break;
                    case 2:
                        if (!hasPrice) r.Error(name, g.id, "unlockPrice", "unlockType 2(구매)는 unlockPrice가 있어야 합니다");
                        if (g.openId != 0) r.Error(name, g.id, "openId", "unlockType 2는 openId를 비워야 합니다");
                        break;
                    default:
                        r.Error(name, g.id, "unlockType", $"unlockType {g.unlockType}는 0(무료)·1(contentOpen)·2(구매)가 아닙니다");
                        break;
                }

                if (!string.IsNullOrEmpty(g.passiveStat) && !s_contentStatKeys.Contains(g.passiveStat))
                    r.Error(name, g.id, "passiveStat", $"`{g.passiveStat}`는 08 §8.5.0 statKey가 아닙니다");
            }

            foreach (var c in t.Card.Datas)
                if (c.group != 0 && !groups.ContainsKey(c.group))
                    r.Error(CardData.Table, c.id, "group", $"group {c.group}을 cardGroup으로 쓰는 113_god 행이 없습니다");
        }

        /// <summary>110 id = `110{group 3}{순번 3}`(07 §7.7.3).</summary>
        static void CheckContentCardIds(MvTables t, MvValidationReport r)
        {
            foreach (var c in t.Card.Datas)
                if (c.id / 1000000 != 110 || c.id / 1000 % 1000 != c.group || c.id % 1000 == 0)
                    r.Error(CardData.Table, c.id, "id", $"id가 110{{group 3}}{{순번 3}} 형식이 아닙니다(group {c.group})");
        }

        // ---------------- 16 (07 §7.8.6, 08 §8.3.5·8.5.2·8.5.4·8.5.8) ----------------

        static void CheckContentLevelPriceIds(MvTables t, MvValidationReport r)
        {
            foreach (var p in t.LevelPrice.Datas)
                if (p.id != MvLevelPriceGenerator.RowId(p.group, p.targetLevel))
                    r.Error(LevelPriceData.Table, p.id, "id", $"id가 16{{group 3}}{{targetLevel 3}} = {MvLevelPriceGenerator.RowId(p.group, p.targetLevel)}이 아닙니다");
        }

        /// <summary>
        /// 08 메타 묶음: 표에 그 묶음이 있으면 생성 결과의 targetLevel이 모두 있어야 한다(오류).
        /// 값·needPiece가 생성 결과와 다르거나 생성 범위 밖 행이 있으면 경고(손으로 조정한 값 허용, 07 §7.13.3과 같은 등급).
        /// </summary>
        static void CheckContentLevelPrices(MvTables t, MvValidationReport r)
        {
            const string name = LevelPriceData.Table;
            var needPiece = new Dictionary<int, string>();
            foreach (var raw in t.LevelPrice.RawRows)
                if (int.TryParse(raw.Get("id"), NumberStyles.Integer, C, out int id)) needPiece[id] = raw.Get("needPiece") ?? string.Empty;

            var generated = MvLevelPriceGenerator.GenerateContent();
            var expected = new HashSet<int>(generated.Select(g => g.Id));
            foreach (var group in generated.Select(g => g.Group).Distinct())
            {
                if (!t.LevelPrice.HasGroup(group)) continue; // 묶음이 아직 없으면(2차 콘텐츠 전) 건너뜀.
                foreach (var g in generated.Where(x => x.Group == group))
                {
                    var row = t.LevelPrice.Get(g.Group, g.TargetLevel);
                    if (row == null)
                    {
                        r.Error(name, g.Id, "targetLevel", $"16_levelPrice 묶음 {g.Group}에 targetLevel {g.TargetLevel}이 없습니다(08 생성 결과)");
                        continue;
                    }
                    if (!ContentSamePrice(row.price, g.Price))
                        r.Warn(name, row.id, "price", $"생성 도구 결과({ContentPriceText(g.Price)})와 다릅니다");
                    needPiece.TryGetValue(row.id, out var pieceRaw);
                    int piece = 0;
                    if (!string.IsNullOrEmpty(pieceRaw) && !int.TryParse(pieceRaw, NumberStyles.Integer, C, out piece))
                        r.Error(name, row.id, "needPiece", $"`{pieceRaw}`는 정수가 아닙니다");
                    else if (piece != g.NeedPiece)
                        r.Warn(name, row.id, "needPiece", $"needPiece {piece}가 생성 도구 결과 {g.NeedPiece}와 다릅니다");
                }
                foreach (var row in t.LevelPrice.GetGroup(group))
                    if (!expected.Contains(MvLevelPriceGenerator.RowId(row.group, row.targetLevel)))
                        r.Warn(name, row.id, "targetLevel", $"targetLevel {row.targetLevel}은 08 생성 범위 밖입니다");
            }
        }

        static bool ContentSamePrice(IReadOnlyList<ConsumeData> a, IReadOnlyList<ConsumeData> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i].Type != b[i].Type || a[i].Id != b[i].Id || Math.Abs(a[i].Value - b[i].Value) > 1e-9) return false;
            return true;
        }

        static string ContentPriceText(IReadOnlyList<ConsumeData> list) => string.Join(";", list.Select(x => x.ToString()));
    }
}
