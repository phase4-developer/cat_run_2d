using System.Collections.Generic;
using MV.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MV.Fx.Tests
{
    /// <summary>이펙트 프리팹(13종) 직렬화 값이 04_effect 그룹 001 행·06 §6.3 고정 색과 같은지, 드라이버가 정본 개수·수명을 만드는지.</summary>
    public sealed class MvFxPrefabTests
    {
        const string Dir = "Assets/Game/Addressables/Local/Effect/Prefab/";

        /// <summary>06 §6.3 표가 정한 종류별 고정 색(호출 색이 없을 때).</summary>
        static readonly Dictionary<string, string> FixedColor = new Dictionary<string, string>
        {
            ["fx.dust"] = "#d8cbb4", ["fx.trail"] = "#bfefff", ["fx.impact"] = "#ffffff", ["fx.bolt"] = "#bfeaff", ["fx.fireball"] = "#ff6a2a",
            ["fx.paw"] = "#f6b65e", ["fx.hiss"] = "#c8a8ff", ["fx.brickChunk"] = "#c8643c", ["fx.holyBeam"] = "#fff6d0", ["fx.meteor"] = "#ffb36b",
        };

        static string PathOf(EffectData row) => Dir + row.prefab.Substring(row.prefab.LastIndexOf('/') + 1) + ".prefab";

        static IEnumerable<EffectData> BasicRows(MvTables t)
        {
            foreach (var r in t.Effect.Sorted)
                if (r.id / 1000 == 4001 && r.key != "fx.frost") yield return r; // fx.frost는 D-46 미구현(프리팹 없음)
        }

        [Test]
        public void PrefabsMatchEffectTable()
        {
            var t = FxTestTables.Load();
            var fails = new List<string>();
            int n = 0;
            foreach (var row in BasicRows(t))
            {
                n++;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(PathOf(row));
                if (go == null) { fails.Add($"{row.key}: 프리팹 없음 {PathOf(row)}"); continue; }
                var d = go.GetComponent<MvFxDriver>();
                if (d == null) { fails.Add($"{row.key}: MvFxDriver 없음"); continue; }
                void Eq(string col, double table, float prefab)
                {
                    if (table != 0 && Mathf.Abs((float)table - prefab) > 1e-4f) fails.Add($"{row.key}.{col}: 표 {table} 프리팹 {prefab}");
                }
                Eq("count", row.count, d.count);
                Eq("speed", row.speed, d.speed);
                Eq("size", row.size, d.size);
                Eq("life", row.life, d.life);
                Eq("grav", row.grav, d.grav);
                Eq("radius", row.radius, d.radius);
                if (Mathf.Abs((float)row.sortBias - d.sortBias) > 1e-4f) fails.Add($"{row.key}.sortBias: 표 {row.sortBias} 프리팹 {d.sortBias}");
                if (FixedColor.TryGetValue(row.key, out var hex) && !string.Equals(hex, d.color, System.StringComparison.OrdinalIgnoreCase))
                    fails.Add($"{row.key}.color: 06 {hex} 프리팹 {d.color}");
                string kindName = row.key.Substring(3);
                if (!string.Equals(d.kind.ToString(), kindName, System.StringComparison.OrdinalIgnoreCase)) fails.Add($"{row.key}: kind {d.kind}");
            }
            Assert.AreEqual(13, n, "기본 종류 13종(frost 제외)");
            Assert.IsEmpty(fails, string.Join("\n", fails));
        }

        static MvFxDriver Spawn(string file, in FxSpawn s)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + file + ".prefab");
            Assert.IsNotNull(prefab, file);
            var go = Object.Instantiate(prefab);
            var d = go.GetComponent<MvFxDriver>();
            d.SetSkin(MvFxParamBank.Put(s));
            d.Play(0, false, null);
            return d;
        }

        [Test]
        public void DriverSpawnsCanonCountsLivesAndColors()
        {
            var made = new List<GameObject>();
            try
            {
                // 표 1 sparks: n개, 수명 life·rnd(0.7, 1.2), 호출 색
                var s = FxSpawn.Of("fx.sparks", Vector3.zero).WithColor(new Color32(0xff, 0xd6, 0x5a, 255));
                s.Count = 12; s.Life = 0.6f;
                // Sparks 프리팹은 디자이너 파티클(드라이버 꺼짐)이라 정본 공식은 켜진 드라이버로 본다.
                var sg = new GameObject("SparksCanon"); made.Add(sg);
                var d = sg.AddComponent<MvFxDriver>();
                d.kind = FxKind.Sparks;
                d.SetSkin(MvFxParamBank.Put(s));
                d.Play(0, false, null);
                Assert.AreEqual(12, d.PartCount);
                for (int i = 0; i < d.PartCount; i++)
                {
                    Assert.That(d.PartLife(i), Is.InRange(0.6f * 0.7f - 1e-4f, 0.6f * 1.2f + 1e-4f));
                    Assert.AreEqual(new Color32(0xff, 0xd6, 0x5a, 255), d.PartColor(i));
                }

                // 표 2 debris 기본 6개, 수명 0.6~0.9
                d = Spawn("Debris", FxSpawn.Of("fx.debris", Vector3.zero)); made.Add(d.gameObject);
                Assert.AreEqual(6, d.PartCount);
                for (int i = 0; i < d.PartCount; i++) Assert.That(d.PartLife(i), Is.InRange(0.6f - 1e-4f, 0.9f + 1e-4f));

                // 표 7 dust 기본 6개, 색 #d8cbb4, 수명 0.45~0.7
                d = Spawn("Dust", FxSpawn.Of("fx.dust", Vector3.zero)); made.Add(d.gameObject);
                Assert.AreEqual(6, d.PartCount);
                Assert.AreEqual(new Color32(0xd8, 0xcb, 0xb4, 255), d.PartColor(0));
                for (int i = 0; i < d.PartCount; i++) Assert.That(d.PartLife(i), Is.InRange(0.45f - 1e-4f, 0.7f + 1e-4f));

                // 표 3 boom: 바깥 0.45 + 속 0.25 + 링 0.5 + 불꽃 14
                d = Spawn("Boom", FxSpawn.Of("fx.boom", Vector3.zero)); made.Add(d.gameObject);
                Assert.AreEqual(3 + 14, d.PartCount);
                Assert.AreEqual(0.45f, d.PartLife(0), 1e-5f); Assert.AreEqual(0.25f, d.PartLife(1), 1e-5f); Assert.AreEqual(0.5f, d.PartLife(2), 1e-5f);

                // 표 6 impact 치명: #ffb07a, 0.13 s
                var im = FxSpawn.Of("fx.impact", Vector3.zero); im.Crit = true;
                d = Spawn("Impact", im); made.Add(d.gameObject);
                Assert.AreEqual(1, d.PartCount);
                Assert.AreEqual(0.13f, d.PartLife(0), 1e-5f);
                Assert.AreEqual(new Color32(0xff, 0xb0, 0x7a, 255), d.PartColor(0));

                // 표 4 bolt: 구간 6 + 마디 빛 6 + 끝 빛 1, 0.22 s
                var bo = FxSpawn.Of("fx.bolt", new Vector3(0, 4.5f, 0)); bo.To = new Vector3(0, 1, 0); bo.Life = 0.22f; // 기본 수명은 프리팹(디자이너) 값이라 호출 값으로 본다
                // Bolt 프리팹도 디자이너 파티클(드라이버 꺼짐)이라 정본 공식은 켜진 드라이버로 본다.
                var bg = new GameObject("BoltCanon"); made.Add(bg);
                d = bg.AddComponent<MvFxDriver>();
                d.kind = FxKind.Bolt;
                d.SetSkin(MvFxParamBank.Put(bo));
                d.Play(0, false, null);
                Assert.AreEqual(13, d.PartCount);
                Assert.AreEqual(0.22f, d.PartLife(0), 1e-5f);

                // 수명이 지나면 끝난다(simDt 진행)
                bool done = false;
                d = Spawn("Impact", FxSpawn.Of("fx.impact", Vector3.zero)); made.Add(d.gameObject);
                d.Play(0, false, () => done = true);
                d.Step(0.05f);
                Assert.IsFalse(done);
                d.Step(0.05f);
                Assert.IsTrue(done, "impact 0.08 s 뒤 완료");
            }
            finally
            {
                foreach (var g in made) Object.DestroyImmediate(g);
            }
        }

        /// <summary>Sparks 프리팹: 정본 불꽃과 디자이너 ParticleSystem을 함께 1회 재생하고, 둘 다 끝나야 완료(긴 쪽 기준).</summary>
        [Test]
        public void SparksPrefab_PlaysCodeAndParticles_CompletesAfterLonger()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "Sparks.prefab");
            Assert.IsNotNull(prefab);
            Assert.IsTrue(prefab.GetComponent<MvFxDriver>().enabled, "Sparks 프리팹은 드라이버를 켠 상태(사용자 2026-11-16)");
            Assert.Greater(prefab.GetComponentsInChildren<ParticleSystem>(true).Length, 0, "디자이너 파티클");
            var go = Object.Instantiate(prefab);
            try
            {
                var d = go.GetComponent<MvFxDriver>();
                Assert.IsFalse(d.ParticleMode);
                bool done = false;
                d.SetSkin(MvFxParamBank.Put(FxSpawn.Of("fx.sparks", Vector3.zero)));
                d.Play(0, false, () => done = true);
                Assert.Greater(d.PartCount, 0, "정본 불꽃");
                Assert.IsTrue(d.ParticlesPlaying, "파티클도 재생");
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) Assert.IsFalse(ps.main.loop, ps.name + " 반복 끔");
                for (int i = 0; i < 100; i++) d.Step(0.1f);
                Assert.AreEqual(0, d.PartCount, "정본 불꽃은 끝남");
                Assert.IsFalse(done, "파티클이 남아 있으면 아직 완료 아님");
                for (int i = 0; i < 1000 && !done; i++) d.StepParticles(0.1f);
                Assert.IsTrue(done, "파티클 상한 시간 안에 완료");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>드라이버를 꺼 둔 프리팹은 파티클만 1회 재생하고 상한 시간 안에 완료를 알린다(끝나지 않고 남던 문제).</summary>
        [Test]
        public void DisabledDriver_PlaysParticlesOnlyAndCompletes()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "Sparks.prefab");
            Assert.IsNotNull(prefab);
            var go = Object.Instantiate(prefab);
            try
            {
                var d = go.GetComponent<MvFxDriver>();
                d.enabled = false; // 프리팹에서 꺼 둔 것과 같음(EditMode는 Awake 전이라 처음 쓸 때 판단)
                Assert.IsTrue(d.ParticleMode);
                bool done = false;
                d.SetSkin(MvFxParamBank.Put(FxSpawn.Of("fx.sparks", Vector3.zero)));
                d.Play(0, false, () => done = true);
                Assert.AreEqual(0, d.PartCount, "정본 부품을 만들지 않음");
                Assert.IsTrue(d.enabled, "재생 동안 켜져 끝을 본다");
                for (int i = 0; i < 1000 && !done; i++) d.StepParticles(0.1f);
                Assert.IsTrue(done, "상한 시간 안에 완료");
                Assert.IsFalse(d.enabled);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
