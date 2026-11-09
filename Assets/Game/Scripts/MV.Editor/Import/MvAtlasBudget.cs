using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MV.Editor.Import
{
    /// <summary>아틀라스 1개의 크기·메모리 추정(기획서 03 §3.8.1, §3.8.11, §3.8.12).</summary>
    public sealed class MvAtlasEstimate
    {
        public MvAtlasSpec Spec;
        public int SpriteCount;
        /// <summary>스프라이트(타이트면 알파 경계로 자른 사각) + 패딩 면적 합.</summary>
        public long PaddedArea;
        /// <summary>PaddedArea / 최대 크기². 사각(알파 경계) 기준이라 타이트 패킹은 이보다 덜 쓸 수 있다 — 1.0 초과는 「실측 필요 위험」.</summary>
        public double AreaRatio;
        /// <summary>
        /// 확실한 하한 / 최대 크기². 타이트면 불투명 화소 수(패딩 제외), 아니면 사각+패딩(Full Rect는 사각 그대로 쓴다).
        /// 1.0을 넘으면 어떤 패킹으로도 1페이지에 안 들어간다(`_2` 분할 필요).
        /// </summary>
        public double LowerBoundRatio;
        /// <summary>사각 MaxRects로 들어간 가장 작은 2의 거듭제곱 페이지. 못 넣으면 0.</summary>
        public int PageWidth, PageHeight;
        public bool RectFit => PageWidth > 0;
        /// <summary>압축 뒤 바이트(밉 포함). 사각으로 못 넣으면 최대 크기 1페이지로 센다.</summary>
        public long Bytes;

        public override string ToString() =>
            $"{Spec.Name}\t{SpriteCount}장\t하한 {LowerBoundRatio:P1}\t사각 {AreaRatio:P1}\t추정 {(RectFit ? PageWidth + "x" + PageHeight : "사각추정 초과(타이트 실측 필요)")}" +
            $"\t예상 {Spec.ExpectedWidth}x{Spec.ExpectedHeight}\t{Spec.Format}{(Spec.Mipmap ? "+mip" : "")}\t{Bytes / 1048576.0:F2} MB";
    }

    /// <summary>
    /// 아틀라스 예산 추정. 실제 패킹(SpriteAtlas v2)은 EditorSettings의 Sprite Packer 모드가 켜져야 하므로, 여기서는
    /// PNG(타이트면 알파 경계)로 면적 하한과 MaxRects 사각 추정을 낸다. 실측 대조는 §3.8.12(Memory Profiler, Atlas 미리보기).
    /// </summary>
    public static class MvAtlasBudget
    {
        /// <summary>§3.8.11 목표·상한(렌더 타깃 제외).</summary>
        public const double MemoryTargetMb = 48;
        public const double MemoryLimitMb = 64;
        /// <summary>§3.8.11 표의 TMP 글꼴 4개 합(이 계약 밖, 상수로 더한다).</summary>
        public const double FontMb = 7.0;

        public static List<MvAtlasEstimate> EstimateProject()
        {
            var list = new List<MvAtlasEstimate>();
            foreach (var pair in MvArtSetup.FindAtlasFolders())
            {
                var files = pair.Value.SelectMany(f => Directory.GetFiles(f, "*.png", SearchOption.AllDirectories)).ToList();
                list.Add(Estimate(MvArtRules.SpecFor(pair.Key), files));
            }
            return list;
        }

        public static MvAtlasEstimate Estimate(MvAtlasSpec spec, IReadOnlyList<string> pngFiles)
        {
            var rects = new List<Vector2Int>(pngFiles.Count);
            long opaque = 0;
            foreach (var file in pngFiles)
            {
                Vector2Int size;
                if (spec.Tight)
                {
                    size = TrimmedSize(file, out var count);
                    opaque += count;
                }
                else size = PngSize(file);
                rects.Add(new Vector2Int(size.x + spec.Padding, size.y + spec.Padding));
            }
            var e = Estimate(spec, rects);
            if (spec.Tight) e.LowerBoundRatio = opaque / ((double)spec.MaxSize * spec.MaxSize);
            return e;
        }

        public static MvAtlasEstimate Estimate(MvAtlasSpec spec, List<Vector2Int> paddedRects)
        {
            var e = new MvAtlasEstimate { Spec = spec, SpriteCount = paddedRects.Count };
            e.PaddedArea = paddedRects.Sum(r => (long)r.x * r.y);
            e.AreaRatio = e.PaddedArea / ((double)spec.MaxSize * spec.MaxSize);
            e.LowerBoundRatio = e.AreaRatio;

            foreach (var (w, h) in PageSizes(spec.MaxSize))
            {
                if ((long)w * h < e.PaddedArea) continue;
                if (!MaxRects.Fits(paddedRects, w, h)) continue;
                e.PageWidth = w;
                e.PageHeight = h;
                break;
            }
            int pw = e.RectFit ? e.PageWidth : spec.MaxSize, ph = e.RectFit ? e.PageHeight : spec.MaxSize;
            e.Bytes = CompressedBytes(pw, ph, spec.Format, spec.Mipmap);
            return e;
        }

        /// <summary>작은 면적부터, 같은 면적이면 가로가 긴 것부터(2:1까지).</summary>
        static IEnumerable<(int, int)> PageSizes(int max)
        {
            var sizes = new List<(int w, int h)>();
            for (int w = 32; w <= max; w *= 2)
            for (int h = 32; h <= max; h *= 2)
                if (h <= w && w <= h * 2) sizes.Add((w, h));
            return sizes.OrderBy(s => (long)s.w * s.h).ThenByDescending(s => s.w);
        }

        /// <summary>ASTC 블록 크기로 센 바이트(밉 포함 시 1x1까지).</summary>
        public static long CompressedBytes(int width, int height, TextureImporterFormat format, bool mipmap)
        {
            int block;
            switch (format)
            {
                case TextureImporterFormat.ASTC_4x4: block = 4; break;
                case TextureImporterFormat.ASTC_5x5: block = 5; break;
                case TextureImporterFormat.ASTC_6x6: block = 6; break;
                case TextureImporterFormat.ASTC_8x8: block = 8; break;
                default: return (long)width * height * 4 * (mipmap ? 4 : 3) / 3;
            }
            long total = 0;
            int w = width, h = height;
            while (true)
            {
                total += (long)((w + block - 1) / block) * ((h + block - 1) / block) * 16;
                if (!mipmap || (w == 1 && h == 1)) break;
                w = Math.Max(1, w / 2);
                h = Math.Max(1, h / 2);
            }
            return total;
        }

        /// <summary>
        /// 런 화면 텍스처 총량(§3.8.11 표 구성): 테마 2(전환 중) + 플레이어 고양이 3 + 친구 3 + Fx + UI 상주 + UI 로비·카드 + 로비 아틀라스 + 방 텍스처 + 글꼴.
        /// 각 묶음은 가장 큰 추정값을 쓴다.
        /// </summary>
        public static double RunScreenMegabytes(IReadOnlyList<MvAtlasEstimate> estimates, out string breakdown)
        {
            long Max(Func<string, bool> pick) => estimates.Where(e => pick(e.Spec.Name)).Select(e => e.Bytes).DefaultIfEmpty(0).Max();
            long Sum(Func<string, bool> pick) => estimates.Where(e => pick(e.Spec.Name)).Sum(e => e.Bytes);

            var parts = new List<(string, long)>
            {
                ("테마 ×2", 2 * Max(n => MvArtRules.ThemeKeyOf(n) != null)),
                ("플레이어 고양이 ×3", 3 * Max(n => n.StartsWith(MvArtRules.CatAtlasPrefix, StringComparison.Ordinal))),
                ("친구 고양이 ×3", 3 * Max(n => n.StartsWith(MvArtRules.FriendAtlasPrefix, StringComparison.Ordinal))),
                ("Fx", Sum(n => n.StartsWith("Atlas_Fx_", StringComparison.Ordinal))),
                ("UI 전부", Sum(MvArtRules.IsUiAtlas)),
                ("로비 아틀라스", Sum(n => n == MvArtRules.LobbyAtlas)),
                ("방 텍스처(1080x1920 6x6)", CompressedBytes(1080, 1920, TextureImporterFormat.ASTC_6x6, false)),
            };
            var sb = new StringBuilder();
            long total = 0;
            foreach (var (name, bytes) in parts)
            {
                total += bytes;
                sb.Append($"{name} {bytes / 1048576.0:F2} MB; ");
            }
            double mb = total / 1048576.0 + FontMb;
            sb.Append($"글꼴 {FontMb:F1} MB; 합 {mb:F2} MB");
            breakdown = sb.ToString();
            return mb;
        }

        /// <summary>PNG 머리의 가로·세로(IHDR).</summary>
        public static Vector2Int PngSize(string file)
        {
            using var stream = File.OpenRead(file);
            var head = new byte[24];
            if (stream.Read(head, 0, 24) < 24) throw new InvalidDataException("PNG too short: " + file);
            int w = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
            int h = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
            return new Vector2Int(w, h);
        }

        /// <summary>알파 &gt; 0 화소의 경계 사각 크기(타이트 패킹의 사각 근사). 완전 투명이면 1x1.</summary>
        public static Vector2Int TrimmedSize(string file) => TrimmedSize(file, out _);

        /// <param name="opaque">알파 &gt; 0 화소 수.</param>
        public static Vector2Int TrimmedSize(string file, out long opaque)
        {
            opaque = 0;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(file), false))
                {
                    var full = PngSize(file);
                    opaque = (long)full.x * full.y;
                    return full;
                }
                var px = tex.GetPixels32();
                int w = tex.width, h = tex.height, minX = w, minY = h, maxX = -1, maxY = -1;
                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        if (px[row + x].a == 0) continue;
                        opaque++;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
                return maxX < 0 ? new Vector2Int(1, 1) : new Vector2Int(maxX - minX + 1, maxY - minY + 1);
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        /// <summary>MaxRects(Best Short Side Fit), 회전 없음(§3.8.1 회전 끔).</summary>
        static class MaxRects
        {
            public static bool Fits(List<Vector2Int> rects, int width, int height)
            {
                var free = new List<RectInt> { new RectInt(0, 0, width, height) };
                foreach (var r in rects.OrderByDescending(r => Math.Max(r.x, r.y)).ThenByDescending(r => Math.Min(r.x, r.y)))
                {
                    int best = -1, bestShort = int.MaxValue, bestLong = int.MaxValue;
                    for (int i = 0; i < free.Count; i++)
                    {
                        var f = free[i];
                        if (r.x > f.width || r.y > f.height) continue;
                        int dx = f.width - r.x, dy = f.height - r.y;
                        int s = Math.Min(dx, dy), l = Math.Max(dx, dy);
                        if (s < bestShort || (s == bestShort && l < bestLong))
                        {
                            best = i;
                            bestShort = s;
                            bestLong = l;
                        }
                    }
                    if (best < 0) return false;
                    var placed = new RectInt(free[best].x, free[best].y, r.x, r.y);
                    var next = new List<RectInt>(free.Count + 4);
                    foreach (var f in free)
                    {
                        if (!placed.Overlaps(f))
                        {
                            next.Add(f);
                            continue;
                        }
                        if (placed.xMin > f.xMin) next.Add(new RectInt(f.xMin, f.yMin, placed.xMin - f.xMin, f.height));
                        if (placed.xMax < f.xMax) next.Add(new RectInt(placed.xMax, f.yMin, f.xMax - placed.xMax, f.height));
                        if (placed.yMin > f.yMin) next.Add(new RectInt(f.xMin, f.yMin, f.width, placed.yMin - f.yMin));
                        if (placed.yMax < f.yMax) next.Add(new RectInt(f.xMin, placed.yMax, f.width, f.yMax - placed.yMax));
                    }
                    free = Prune(next);
                }
                return true;
            }

            static List<RectInt> Prune(List<RectInt> rects)
            {
                var keep = new List<RectInt>(rects.Count);
                for (int i = 0; i < rects.Count; i++)
                {
                    bool contained = false;
                    for (int j = 0; j < rects.Count && !contained; j++)
                    {
                        if (i == j) continue;
                        var a = rects[i];
                        var b = rects[j];
                        bool inside = a.xMin >= b.xMin && a.yMin >= b.yMin && a.xMax <= b.xMax && a.yMax <= b.yMax;
                        if (inside && (!a.Equals(b) || j < i)) contained = true;
                    }
                    if (!contained) keep.Add(rects[i]);
                }
                return keep;
            }
        }
    }
}
