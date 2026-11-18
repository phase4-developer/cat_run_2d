using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MV.View.Tests
{
    /// <summary>
    /// 티켓 03 나머지 순수 계산: 화면 눈(02 §2.5.5), 로비 투영(02 §2.9).
    /// 골든 값은 정본 `drawMoonProp`·`drawClothes`·`drawSnow`·`iso` 식을 Node(V8)로 돌린 결과다.
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

        [Test]
        public void LobbyIso_MatchesProto()
        {
            LobbyIso.IsoPx(-0.92, -0.92, 360, 640, out double x, out double y);
            Assert.That(x, Is.EqualTo(180).Within(1e-6));
            Assert.That(y, Is.EqualTo(212.9408).Within(1e-6));
            LobbyIso.IsoPx(1, -1, 360, 640, out x, out y);
            Assert.That(x, Is.EqualTo(468).Within(1e-6));
            Assert.That(y, Is.EqualTo(350.72).Within(1e-6));
            LobbyIso.IsoUnits(-0.78, -0.3, out x, out y);
            Assert.That(x, Is.EqualTo(110.88 / 40 - 4.5).Within(1e-9));
            Assert.That(y, Is.EqualTo(8 - 269.8496 / 40).Within(1e-9));
            Assert.That(LobbyIso.Bump(1), Is.EqualTo(1).Within(1e-9));
            Assert.That(LobbyIso.Bump(0.97), Is.EqualTo(1.016431312).Within(1e-8));
            Assert.That(LobbyIso.SortingOrder(1), Is.LessThan(LobbyIso.SortingOrder(-1)), "화면 위(뒤)가 먼저");
        }

        [Test]
        public void LobbyFacSprite_NamesFollowStageAndFrames()
        {
            Assert.That(LobbyIso.FacSprite("tower", 0, 0), Is.Null);
            Assert.That(LobbyIso.FacSprite("tower", 1, 5), Is.EqualTo("Lobby_Fac_Tower_1"));
            Assert.That(LobbyIso.FacSprite("tower", 2, 1.0), Is.EqualTo("Lobby_Fac_Tower_2_04"), "4.2 fps");
            Assert.That(LobbyIso.FacSprite("stove", 0, 0), Is.EqualTo("Lobby_Fac_Stove_0"));
            Assert.That(LobbyIso.FacSprite("stove", 3, 1.0), Is.EqualTo("Lobby_Fac_Stove_3_02"), "10.2 fps, 8프레임");
            Assert.That(LobbyIso.FacSprite("rack", 2, 0, 9), Is.EqualTo("Lobby_Fac_Rack_2_F4"));
            Assert.That(LobbyIso.FacSprite("scratch", 3, 0), Is.EqualTo("Lobby_Fac_Scratch_3"));
            Assert.That(LobbyIso.FacSprite("basket", 1, 0), Is.EqualTo("Lobby_Fac_Basket_1"));
        }
    }
}
