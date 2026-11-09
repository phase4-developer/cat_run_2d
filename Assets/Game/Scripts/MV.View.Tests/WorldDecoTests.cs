using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MV.View.Tests
{
    /// <summary>
    /// 티켓 03 나머지 순수 계산: 담장 장식(02 §2.6.5), 빨랫줄·전구줄(02 §2.5.4), 화면 눈(02 §2.5.5), 로비 투영(02 §2.9).
    /// 골든 값은 정본 `drawMoonProp`·`drawClothes`·`drawSnow`·`iso` 식을 Node(V8)로 돌린 결과다.
    /// </summary>
    public sealed class WorldDecoTests
    {
        static Dictionary<string, int> FenceKinds(string deco)
        {
            var specs = new List<PropSpec>();
            PropLayout.Build(0, 200, false, specs);
            return specs.Where(s => s.Kind == PropKind.Fence)
                .Select(s => FenceDeco.Pick(deco, s.Hh, null))
                .GroupBy(d => d.Kind == FenceDecoKind.Awning ? (d.AwningColor == FenceDeco.AwningRed ? "AwnRed" : "AwnBlue") : d.Kind.ToString())
                .ToDictionary(g => g.Key, g => g.Count());
        }

        [Test]
        public void FenceDeco_CountsOverBuildEnv_MatchProto()
        {
            var market = FenceKinds("market");
            Assert.That(market.Values.Sum(), Is.EqualTo(111));
            Assert.That(market["Flower"], Is.EqualTo(24));
            Assert.That(market["AwnRed"], Is.EqualTo(46));
            Assert.That(market["AwnBlue"], Is.EqualTo(33));
            Assert.That(market["None"], Is.EqualTo(8));
            var stairs = FenceKinds("stairs");
            Assert.That(stairs["Flower"], Is.EqualTo(24));
            Assert.That(stairs["Gate"], Is.EqualTo(47));
            Assert.That(stairs["None"], Is.EqualTo(40));
            Assert.That(FenceDeco.Pick("snow", 0.5, null).SnowCap, Is.True);
            Assert.That(FenceDeco.Pick("stairs", 0.9, null).GateColor, Is.EqualTo(FenceDeco.DefaultDoor));
        }

        [Test]
        public void FenceDeco_AwningCellGeometry()
        {
            FenceDeco.AwningCell(0, 1, -0.2, out var p1, out var p2, out var p3, out var p4);
            Assert.That(p1.y, Is.EqualTo(1.35).Within(1e-12));
            Assert.That(p3.y, Is.EqualTo(0.95).Within(1e-12));
            Assert.That(p3.x, Is.EqualTo(-1.0).Within(1e-12), "길 쪽으로 0.8 m");
            Assert.That(p2.z - p1.z, Is.EqualTo(3.16 / 6).Within(1e-12));
            Assert.That(p4.z, Is.EqualTo(-1.58).Within(1e-12));
        }

        [Test]
        public void ClothLine_ItemsSagAndBulbs_MatchProto()
        {
            Assert.That(ClothLineLayout.PointSag(0), Is.EqualTo(0).Within(1e-12));
            Assert.That(ClothLineLayout.PointSag(1), Is.EqualTo(0.144).Within(1e-12));
            Assert.That(ClothLineLayout.PointSag(5), Is.EqualTo(0.4).Within(1e-12));
            string Items(double z) => string.Join(" ", new[] { 1, 3, 5, 7, 9 }.Select(i =>
                ClothLineLayout.TryCloth(z, i, out var c) ? i + ":" + c.ColorIndex + "," + (int)c.Kind : i + ":skip"));
            Assert.That(Items(13), Is.EqualTo("1:2,2 3:1,0 5:1,2 7:6,2 9:2,0"));
            Assert.That(Items(26), Is.EqualTo("1:skip 3:5,0 5:skip 7:2,2 9:skip"));
            Assert.That(Items(52), Is.EqualTo("1:5,0 3:3,0 5:1,2 7:3,1 9:2,2"));
            var on = Enumerable.Range(1, 9).Select(i => ClothLineLayout.BulbOn(i, 1000) ? 1 : 0);
            Assert.That(on, Is.EqualTo(new[] { 0, 1, 1, 0, 1, 1, 0, 1, 1 }));
            Assert.That(ClothLineLayout.BulbSprite(5, false), Is.EqualTo("World_Line_Bulb_1_Off"));
            Assert.That(ClothLineLayout.UsesBulbs("roof") && ClothLineLayout.UsesBulbs("snow") && !ClothLineLayout.UsesBulbs("market"), Is.True);
            var zs = new List<double>();
            PropLayout.ClothLines(10, ViewTestTables.Config(), zs);
            Assert.That(zs, Is.EqualTo(new double[] { 13, 26, 52, 65, 78 }));
        }

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

        [Test]
        public void LobbyStagePrefab_HasOrthoCameraAndMaterial()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Addressables/Remote/World/LobbyStage.prefab");
            Assert.That(prefab, Is.Not.Null);
            var so = new SerializedObject(prefab.GetComponent<LobbyStage>());
            var cam = so.FindProperty("_camera").objectReferenceValue as Camera;
            Assert.That(cam, Is.Not.Null);
            Assert.That(cam.orthographic, Is.True);
            Assert.That(cam.orthographicSize, Is.EqualTo(LobbyIso.OrthoSize));
            Assert.That(so.FindProperty("_spriteMaterial").objectReferenceValue, Is.Not.Null);
        }
    }
}
