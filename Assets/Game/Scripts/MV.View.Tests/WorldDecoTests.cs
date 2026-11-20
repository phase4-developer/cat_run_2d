using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MV.View.Tests
{
    /// <summary>
    /// 티켓 03 나머지 순수 계산: 화면 눈(02 §2.5.5). 로비 아지트 투영(LobbyIso)은 2026-11-19 정리(MV-CLEAN-AZIT).
    /// 골든 값은 정본 `drawMoonProp`·`drawClothes`·`drawSnow` 식을 Node(V8)로 돌린 결과다.
    /// </summary>
    public sealed class WorldDecoTests
    {
        static readonly Dictionary<int, double[]> s_snow = new Dictionary<int, double[]>
        {
            { 0, new[] { 287.335927, 507.566993, 2.802486 } },
            { 1, new[] { 256.663584, 250.828319, 1.305231 } },
            { 37, new[] { 261.858975, 521.500061, 3.148106 } },
            { 69, new[] { 17.175235, 285.889263, 2.086867 } },
        };

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(37)]
        [TestCase(69)]
        public void Snow_ParticleMatchesProto(int i)
        {
            double x = s_snow[i][0], y = s_snow[i][1], r = s_snow[i][2];
            SnowField.Particle(i, 12.5, 360, 640, 1, out double gx, out double gy, out double gr);
            Assert.That(gx, Is.EqualTo(x).Within(1e-5));
            Assert.That(gy, Is.EqualTo(y).Within(1e-5));
            Assert.That(gr, Is.EqualTo(r).Within(1e-5));
        }
    }
}
