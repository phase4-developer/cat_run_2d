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
    /// 설정은 바꾸지 않고 사실만 확인한다.
    /// </summary>
    public sealed class MvAtlasPackTests
    {
        const string AtlasDir = "Assets/Game/Addressables/Local/UIAtlas";

        [Test]
        public void UiAtlases_PackedSprites_DrawAreaDoesNotOverlapOtherSprites()
        {
            var report = new List<string>();
            var problems = new List<string>();
            bool anyPacked = false;
            foreach (var path in Directory.GetFiles(AtlasDir, "*.spriteatlasv2"))
            {
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path.Replace('\\', '/'));
                Assert.That(atlas, Is.Not.Null, path);
                SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget, false);
                var sprites = new Sprite[atlas.spriteCount];
                atlas.GetSprites(sprites);
                var items = new List<(string name, Rect tex, Rect draw, int texId)>();
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
                }
                foreach (var a in items)
                    foreach (var b in items)
                    {
                        if (a.name == b.name || a.texId != b.texId) continue;
                        if (a.draw.Overlaps(b.tex))
                            problems.Add($"{Path.GetFileName(path)}: {a.name} 그리는 영역 {a.draw}에 {b.name}({b.tex})이 들어옴");
                    }
            }
            foreach (var line in report) Debug.Log("[MvAtlasPackTests] " + line);
            foreach (var line in problems) Debug.Log("[MvAtlasPackTests] 겹침 " + line);
            if (!anyPacked) Assert.Inconclusive("패킹된 그림이 없습니다(이 환경에서 아틀라스 패킹이 돌지 않음).\n" + string.Join("\n", report));
            Assert.That(problems, Is.Empty, string.Join("\n", problems) + "\n--\n" + string.Join("\n", report));
        }
    }
}
