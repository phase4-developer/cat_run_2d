using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace MV.Editor.Tests
{
    /// <summary>
    /// UI 아틀라스를 실제로 패킹해, 그림마다 화면에 그려지는 영역(원본 크기 그대로, FullRect·9-slice)이 아틀라스 안에서
    /// 다른 그림과 겹치지 않는지 본다(MV-UI3-LVGHOST: 지도 게이지 옆 잔상 = 이웃 그림이 비쳐 늘어난 모습으로 의심, 사용자 2026-11-23
    /// 「아틀라스 패킹할때 그림 간격 너무 타이트하게 한거아니야?」「여백을 제대로 확보 안하면 저렇게 될거같은데」).
    /// 확인된 원인: 아틀라스 패킹은 Mesh Type이 Tight인 그림의 투명 여백을 잘라 낸다. 9-slice(Sliced·Tiled)로 그리면 원래 크기 영역을 통째로 읽어
    /// 잘린 자리에 붙은 이웃 그림이 늘어나 보인다(Progress_Level_Fill 왼쪽 30px → Slot_Dia_Solid). Simple 그림은 잘린 만큼 덜 그려 괜찮다.
    /// 그래서 border가 있는 그림(9-slice로 쓰는 그림)은 잘리면 안 된다. 잘리는 까닭 = 그림 설정 Mesh Type이 Tight(spriteMeshType 1). 9-slice로 쓰는 그림은 FullRect(0)로 둔다.
    /// </summary>
    public sealed class MvAtlasPackTests
    {
        const string AtlasDir = "Assets/Game/Addressables/Local/UIAtlas";

        [Test]
        public void UiAtlases_SlicedSprites_AreNotTrimmedByPacking()
        {
            var report = new List<string>();
            var problems = new List<string>();
            bool anyPacked = false;
            var sliced = new List<(string name, Rect draw, int texId, string atlas)>();
            foreach (var path in Directory.GetFiles(AtlasDir, "*.spriteatlasv2"))
            {
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path.Replace('\\', '/'));
                Assert.That(atlas, Is.Not.Null, path);
                SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget, false);
                var sprites = new Sprite[atlas.spriteCount];
                atlas.GetSprites(sprites);
                var items = new List<(string name, Rect tex, Rect draw, int texId)>();
                sliced.Clear();
                foreach (var s in sprites)
                {
                    if (!s) continue;
                    string name = s.name.Replace("(Clone)", "");
                    Rect tex = s.textureRect;
                    Vector2 off = s.textureRectOffset;
                    var draw = new Rect(tex.x - off.x, tex.y - off.y, s.rect.width, s.rect.height);
                    int texId = s.texture ? s.texture.GetInstanceID() : 0;
                    if (s.packed) anyPacked = true;
                    bool trimmed = Mathf.Abs(tex.width - s.rect.width) > 0.5f || Mathf.Abs(tex.height - s.rect.height) > 0.5f;
                    if (name.StartsWith("Progress_Level") || trimmed)
                        report.Add($"{Path.GetFileName(path)} {name} packed={s.packed} atlasTex={(s.texture ? s.texture.width + "x" + s.texture.height : "-")} rect={s.rect.width}x{s.rect.height} texRect={tex} offset={off}{(trimmed ? " 잘림(TRIM)" : "")}");
                    items.Add((name, tex, draw, texId));
                    if (trimmed && s.border != Vector4.zero) sliced.Add((name, draw, texId, Path.GetFileName(path)));
                }
                foreach (var a in sliced)
                {
                    var hits = new List<string>();
                    foreach (var b in items)
                        if (b.name != a.name && b.texId == a.texId && a.draw.Overlaps(b.tex)) hits.Add(b.name);
                    problems.Add($"{a.atlas}: 9-slice 그림 {a.name}의 투명 여백이 패킹에서 잘림 → 그리는 영역에 {(hits.Count > 0 ? string.Join(", ", hits) : "빈 자리")}이(가) 비침. Mesh Type을 FullRect로 둔다");
                }
            }
            foreach (var line in report) Debug.Log("[MvAtlasPackTests] " + line);
            foreach (var line in problems) Debug.Log("[MvAtlasPackTests] 겹침 " + line);
            if (!anyPacked) Assert.Inconclusive("패킹된 그림이 없습니다(이 환경에서 아틀라스 패킹이 돌지 않음).\n" + string.Join("\n", report));
            Assert.That(problems, Is.Empty, string.Join("\n", problems) + "\n--\n" + string.Join("\n", report));
        }
    }
}
