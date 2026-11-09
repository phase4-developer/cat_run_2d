using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI.Tests
{
    /// <summary>HUD 표시 값이 런 상태와 일치하는지(대역 상태·대역 겉, 05 §5.2.2~5.2.6).</summary>
    public sealed class RunHudTests
    {
        sealed class FakeSource : IRunHudSource
        {
            public RunHudState State;
            public bool Has = true;
            public bool TryRead(out RunHudState state) { state = State; return Has; }
        }

        sealed class FakeView : IRunHudView
        {
            public readonly Dictionary<string, string> Text = new Dictionary<string, string>();
            public readonly Dictionary<string, int> Calls = new Dictionary<string, int>();
            public float Fill;
            public bool Flying;
            public int Tier;
            public string Icon;
            public Color Grade;

            void Set(string key, string value)
            {
                Text[key] = value;
                Calls[key] = Count(key) + 1;
            }

            public int Count(string key) => Calls.TryGetValue(key, out var n) ? n : 0;

            public void SetLevel(string text) => Set("level", text);
            public void SetFloor(string text) => Set("floor", text);
            public void SetGold(string text) => Set("gold", text);
            public void SetSouls(string text) => Set("souls", text);
            public void SetDia(string text) => Set("dia", text);
            public void SetStairs(string text, bool flying) { Flying = flying; Set("stairs", text); }
            public void SetGauge(string levelText, string perText, float fill01) { Fill = fill01; Text["per"] = perText; Set("gauge", levelText); }
            public void SetWeapon(int tier, string iconSprite, string name, string damageText, Color gradeColor)
            {
                Tier = tier; Icon = iconSprite; Grade = gradeColor; Text["dmg"] = damageText; Set("weapon", name);
            }
        }

        static RunHudState Sample() => new RunHudState
        {
            Level = 3, AreaNo = 2, AreaName = "Alley", Floor = 1,
            Gold = 12345, Souls = 40, NeedSouls = 100, Dia = 7,
            Stairs = 15, Flying = false, GaugeLv = 2, GaugeFill = 0.5f,
            WeaponTier = 3, WeaponName = "W3", WeaponIcon = "UI_Icon_Weapon_3", WeaponGradeColor = "#3a8ff0", WeaponDmg = 32, LookName = "Cat",
        };

        static (RunHudPresenter p, FakeSource src, FakeView view) Make()
        {
            var p = new RunHudPresenter();
            var src = new FakeSource { State = Sample() };
            var view = new FakeView();
            p.SetView(view);
            p.Bind(src);
            return (p, src, view);
        }

        [Test]
        public void Tick_ShowsRunStateValues()
        {
            var (p, _, v) = Make();
            Assert.IsTrue(p.Tick());
            Assert.AreEqual("Level 3", v.Text["level"]);
            Assert.AreEqual("Alley · Area 2\n골목 1", v.Text["floor"]);
            Assert.AreEqual("12.3K", v.Text["gold"]);
            Assert.AreEqual("40/100", v.Text["souls"]);
            Assert.AreEqual("7", v.Text["dia"]);
            Assert.AreEqual("15", v.Text["stairs"]);
            Assert.IsFalse(v.Flying);
            Assert.AreEqual("Lv 2", v.Text["gauge"]);
            Assert.AreEqual("칸당 +2", v.Text["per"]);
            Assert.AreEqual(0.5f, v.Fill, 1e-6f);
            Assert.AreEqual(3, v.Tier);
            Assert.AreEqual("UI_Icon_Weapon_3", v.Icon);
            Assert.AreEqual("W3", v.Text["weapon"]);
            Assert.AreEqual("피해 32 · Cat", v.Text["dmg"]);
        }

        [Test]
        public void Tick_PlayFloorAndNoAreaName()
        {
            var (p, src, v) = Make();
            src.State.Floor = 0;
            src.State.AreaName = "";
            p.Tick();
            Assert.AreEqual("Area 2\n플레이 구간", v.Text["floor"]);
        }

        [Test]
        public void Tick_SameState_DoesNotRedraw()
        {
            var (p, _, v) = Make();
            p.Tick();
            p.Tick();
            p.Tick();
            foreach (var key in new[] { "level", "floor", "gold", "souls", "dia", "stairs", "gauge", "weapon" })
                Assert.AreEqual(1, v.Count(key), key);
        }

        [Test]
        public void Tick_ChangedValue_RedrawsOnlyThatItem()
        {
            var (p, src, v) = Make();
            p.Tick();
            src.State.Gold = 999;
            src.State.Stairs = 16;
            src.State.Flying = true;
            p.Tick();
            Assert.AreEqual("999", v.Text["gold"]);
            Assert.AreEqual("16", v.Text["stairs"]);
            Assert.IsTrue(v.Flying);
            Assert.AreEqual(2, v.Count("gold"));
            Assert.AreEqual(2, v.Count("stairs"));
            Assert.AreEqual(1, v.Count("level"));
            Assert.AreEqual(1, v.Count("souls"));
            Assert.AreEqual(1, v.Count("weapon"));
        }

        [Test]
        public void Tick_NoRun_KeepsLastValues()
        {
            var (p, src, v) = Make();
            p.Tick();
            src.Has = false;
            src.State.Gold = 1;
            Assert.IsFalse(p.Tick());
            Assert.AreEqual("12.3K", v.Text["gold"]);
        }

        [Test]
        public void Bind_NewSource_RedrawsAll()
        {
            var (p, _, v) = Make();
            p.Tick();
            p.Bind(new FakeSource { State = Sample() });
            p.Tick();
            Assert.AreEqual(2, v.Count("level"));
            Assert.AreEqual(2, v.Count("weapon"));
        }

        [Test]
        public void Fmt_FollowsCanonRule()
        {
            Assert.AreEqual("0", MvUiFormat.Fmt(0.9));
            Assert.AreEqual("9,999", MvUiFormat.Fmt(9999));
            Assert.AreEqual("10.0K", MvUiFormat.Fmt(10000));
            Assert.AreEqual("1.5M", MvUiFormat.Fmt(1500000));
        }

        [Test]
        public void DefaultView_BuildsAndShowsTexts()
        {
            var go = new GameObject("UIHud_Run", typeof(RectTransform));
            try
            {
                var view = go.AddComponent<RunHudView>();
                var p = new RunHudPresenter();
                p.SetView(view);
                p.Bind(new FakeSource { State = Sample() });
                p.Tick();
                var texts = new List<string>();
                foreach (var t in go.GetComponentsInChildren<Text>(true)) texts.Add(t.text);
                CollectionAssert.Contains(texts, "Level 3");
                CollectionAssert.Contains(texts, "12.3K");
                CollectionAssert.Contains(texts, "15");
                CollectionAssert.Contains(texts, "Lv 2");
                CollectionAssert.Contains(texts, "피해 32 · Cat");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
