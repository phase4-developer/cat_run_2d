using System;
using System.Collections.Generic;
using MV.Data;

namespace MV.UI
{
    /// <summary>
    /// 디자이너 HUD(MV-UI2-HUD)의 등급 그림·이름. 등급 정본은 표(103_item tier·115_cardTier tierIndex)이고, 그림만 표에서 찾는다.
    /// </summary>
    public static class HudGrade
    {
        /// <summary>09_uiResource 「등급 틀」 묶음(<see cref="MvUiGrade.FrameGroup"/>).</summary>
        public const int FrameGroup = MvUiGrade.FrameGroup;

        /// <summary>등급 번호 grade(1부터)의 틀 그림 행(<see cref="MvUiGrade.Frame"/>, 무기문 위 카드 묶음과 같은 규칙).</summary>
        public static UiResourceData Frame(MvTables t, int grade) => MvUiGrade.Frame(t, grade);

        /// <summary>115 행(tierIndex). 없으면 null.</summary>
        public static CardTierData CardTier(MvTables t, int tierIndex)
        {
            if (t == null) return null;
            foreach (var r in t.CardTier.Datas) if (r.tierIndex == tierIndex) return r;
            return null;
        }

        /// <summary>가장 높은 카드 등급(115 tierIndex 최대). 행이 없으면 −1.</summary>
        public static int CardTierMax(MvTables t)
        {
            int max = -1;
            if (t != null) foreach (var r in t.CardTier.Datas) if (r.tierIndex > max) max = r.tierIndex;
            return max;
        }

        /// <summary>등급 이름 `name:&lt;115 id&gt;`. 번역이 없으면 표 이름 끝 낱말 대문자(1차 가호 카드 화면과 같은 규칙).</summary>
        public static string CardTierName(MvTables t, CardTierData r)
        {
            if (t == null || r == null) return "";
            string raw = MvUiText.RawName(t.CardTier.RawRows, r.id) ?? "";
            int sp = raw.LastIndexOf(' ');
            return MvUiText.Name(r.id, (sp >= 0 ? raw.Substring(sp + 1) : raw).ToUpperInvariant());
        }

        /// <summary>카드 이름 `name:&lt;110 id&gt;`(1차 가호 카드 화면과 같은 규칙).</summary>
        public static string CardName(MvTables t, CardData c) =>
            c == null ? "" : MvUiText.Name(c.id, (t != null ? MvUiText.RawName(t.Card.RawRows, c.id) : null) ?? c.key);

        /// <summary>
        /// 계단 등급 카드 표시(디자이너 [HUD] 인게임 ClimbStair): 가운데(Current)는 지금 채우는 등급 = 얻은 등급 + 1(최고 등급이면 최고),
        /// 채움 = 그 등급까지 남은 칸 비율(앞 등급 need부터 이 등급 need까지). 최고 등급을 얻었으면 1.
        /// </summary>
        public static int ClimbCurrent(MvTables t, int tier, int rem, out float fill)
        {
            int max = CardTierMax(t);
            if (max < 0) { fill = 0; return -1; }
            int cur = Math.Min(max, tier + 1);
            if (tier >= max) { fill = 1; return cur; }
            int from = tier >= 0 ? CardTier(t, tier)?.need ?? 0 : 0;
            int to = CardTier(t, cur)?.need ?? from;
            fill = to > from ? Math.Max(0f, Math.Min(1f, (rem - from) / (float)(to - from))) : 1f;
            return cur;
        }
    }

    /// <summary>지팡이 손패 연출 한 단계: 한 장 넣기 또는 같은 단계 MERGE_N장 → 윗단계 1장.</summary>
    public struct HudHandStep
    {
        public bool Merge;
        /// <summary>넣은 단계 또는 머지된(아래) 단계.</summary>
        public int Tier;
        public override string ToString() => (Merge ? "merge " : "add ") + Tier;
    }

    /// <summary>
    /// 지팡이 손패 표시(MV-UI2-HUD 머지 연출). 시뮬 손패(<c>RunState.W</c>, 단계별 장수)는 읽기만 하고, 표시용 사본에서 같은 규칙(같은 단계 MERGE_N장 → 윗단계)을 다시 밟아 과정을 만든다.
    /// </summary>
    public static class HudWeaponHand
    {
        /// <summary>
        /// 표시 장수 from에 한 단계 장을 몇 장 넣어 to가 되는지 찾아 그 과정을 돌려준다. hintTier(지금 공방 단계)를 먼저 본다. 같은 과정이 없으면 null(바로 맞춘다).
        /// </summary>
        public static List<HudHandStep> Steps(int[] from, int[] to, int mergeN, int hintTier)
        {
            if (from == null || to == null || from.Length != to.Length || mergeN < 2) return null;
            if (Same(from, to)) return new List<HudHandStep>();
            long diff = Value(to, mergeN) - Value(from, mergeN);
            if (diff <= 0) return null;
            var order = new List<int>();
            if (hintTier >= 1 && hintTier < from.Length) order.Add(hintTier);
            for (int t = 1; t < from.Length; t++) if (t != hintTier) order.Add(t);
            foreach (int t in order)
            {
                long unit = Pow(mergeN, t - 1);
                if (unit <= 0 || diff % unit != 0) continue;
                long k = diff / unit;
                if (k > 4096) continue;
                var steps = new List<HudHandStep>();
                var w = (int[])from.Clone();
                for (long i = 0; i < k; i++) Add(w, t, mergeN, steps);
                if (Same(w, to)) return steps;
            }
            return null;
        }

        /// <summary>시뮬 AddWeapon과 같은 규칙: 넣고 같은 단계 mergeN장이면 윗단계로(최고 단계는 쌓인다).</summary>
        public static void Add(int[] w, int t, int mergeN, List<HudHandStep> steps)
        {
            t = Math.Max(1, Math.Min(w.Length - 1, t));
            w[t]++;
            steps?.Add(new HudHandStep { Tier = t });
            while (t < w.Length - 1 && w[t] >= mergeN)
            {
                w[t] -= mergeN; t++; w[t]++;
                steps?.Add(new HudHandStep { Merge = true, Tier = t - 1 });
            }
        }

        /// <summary>한 번 머지를 표시 사본에 적용한다.</summary>
        public static void ApplyMerge(int[] w, int tier, int mergeN)
        {
            w[tier] -= mergeN;
            w[tier + 1]++;
        }

        /// <summary>장착 = 가장 높은 단계(<c>RunState.EqTier</c>와 같음). 손패가 비면 1.</summary>
        public static int Equipped(int[] w)
        {
            for (int t = w.Length - 1; t >= 1; t--) if (w[t] > 0) return t;
            return 1;
        }

        /// <summary>스택 칸(장착 1장을 뺀 나머지, 높은 단계부터).</summary>
        public static List<int> Stack(int[] w)
        {
            var list = new List<int>();
            int eq = Equipped(w);
            for (int t = w.Length - 1; t >= 1; t--)
                for (int k = t == eq ? 1 : 0; k < w[t]; k++) list.Add(t);
            return list;
        }

        static bool Same(int[] a, int[] b)
        {
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        static long Value(int[] w, int mergeN)
        {
            long v = 0;
            for (int t = 1; t < w.Length; t++) v += w[t] * Pow(mergeN, t - 1);
            return v;
        }

        static long Pow(int b, int e)
        {
            long v = 1;
            for (int i = 0; i < e; i++) { v *= b; if (v > 1L << 40) return -1; }
            return v;
        }
    }
}
