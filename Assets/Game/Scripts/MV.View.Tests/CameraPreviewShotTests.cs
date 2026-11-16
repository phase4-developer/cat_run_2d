using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using MV.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace MV.View.Tests
{
    /// <summary>
    /// 시각 검수(MV-SCENE-VERIFY): `Game.unity`의 실제 `GameCamera`를 편집 모드로 열어 미리보기 4시점(Lobby·Run·Climb·ClimbSide)을
    /// 9:16로 찍어 `Logs/CameraShots`(git 제외)에 남기고, 설정 에셋 값을 바꾸면 편집 모드 구도가 따라오는지 본다.
    /// 에셋 값은 메모리에서만 바꾸고 끝에 되돌린다(저장하지 않음). 씬도 저장하지 않고 닫는다.
    /// </summary>
    public sealed class CameraPreviewShotTests
    {
        const string ScenePath = "Assets/Game/Scenes/Game.unity";
        const int ShotW = 540, ShotH = 960;

        internal static string ShotDir => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs", "CameraShots");

        [UnityTest]
        public IEnumerator EditPreview_FourViews_ShotAndFollowSettings()
        {
            var settings = ViewTestTables.Camera();
            string original = JsonUtility.ToJson(settings);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            RenderTexture rt = null;
            CameraRig rig = null;
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    rig = root.GetComponentInChildren<CameraRig>(true);
                    if (rig != null) break;
                }
                Assert.IsNotNull(rig, "Game 씬에 CameraRig(GameCamera)가 없다");
                Assert.AreSame(settings, rig.Settings, "씬 GameCamera가 설정 에셋을 참조하지 않는다");

                Directory.CreateDirectory(ShotDir);
                rt = new RenderTexture(ShotW, ShotH, 24);
                rig.Cam.targetTexture = rt; // 미리보기 FOV가 9:16 화면비로 계산되게 먼저 건다.

                var so = new SerializedObject(rig);
                var preview = so.FindProperty("_preview");
                var log = new StringBuilder("view\tpos\teuler\tfovV\n");
                foreach (CameraPreview v in Enum.GetValues(typeof(CameraPreview)))
                {
                    preview.enumValueIndex = (int)v;
                    so.ApplyModifiedPropertiesWithoutUndo(); // Inspector와 같은 길: OnValidate → 다음 에디터 틱에 구도
                    yield return Ticks();
                    var t = rig.transform;
                    if (v == CameraPreview.Lobby)
                    {
                        var l = settings.lobby;
                        AssertNear(new Vector3(0, (float)l.posY, (float)l.posZ), t.position, "Lobby 미리보기 위치 = lobby 행");
                    }
                    Shot(rig.Cam, rt, Path.Combine(ShotDir, $"edit_{(int)v}_{v}.png"));
                    log.Append(v).Append('\t').Append(t.position.ToString("F3")).Append('\t')
                        .Append(t.eulerAngles.ToString("F2")).Append('\t').Append(rig.Cam.fieldOfView.ToString("F2")).Append('\n');
                }

                // 설정 변경 반영: Lobby 시점에서 lobby.posY를 +1 → Inspector 수정과 같은 OnValidate → 구도가 따라온다.
                preview.enumValueIndex = (int)CameraPreview.Lobby;
                so.ApplyModifiedPropertiesWithoutUndo();
                yield return Ticks();
                float before = rig.transform.position.y;
                settings.lobby.posY += 1;
                RaiseValidate(settings);
                yield return Ticks();
                float after = rig.transform.position.y;
                log.Append("settingsChange\tlobby.posY+1\t").Append(before.ToString("F3")).Append(" -> ").Append(after.ToString("F3")).Append('\n');
                Assert.AreEqual(before + 1, after, 1e-3f, "설정 에셋 변경이 편집 모드 구도에 반영되지 않았다");

                JsonUtility.FromJsonOverwrite(original, settings);
                RaiseValidate(settings);
                yield return Ticks();
                Assert.AreEqual(before, rig.transform.position.y, 1e-3f, "되돌린 값으로 구도가 돌아오지 않았다");

                File.WriteAllText(Path.Combine(ShotDir, "edit_poses.tsv"), log.ToString(), new UTF8Encoding(false));
                Debug.Log("[CameraPreviewShot]\n" + log);
            }
            finally
            {
                JsonUtility.FromJsonOverwrite(original, settings);
                if (rig != null) rig.Cam.targetTexture = null;
                if (rt != null) UnityEngine.Object.DestroyImmediate(rt);
                EditorSceneManager.CloseScene(scene, true);
            }
            Assert.AreEqual(original, JsonUtility.ToJson(settings), "설정 에셋 값이 원래대로 돌아오지 않았다");
        }

        static IEnumerator Ticks()
        {
            for (int i = 0; i < 3; i++) yield return null;
        }

        /// <summary>Inspector가 값을 바꿀 때 Unity가 부르는 것과 같은 `OnValidate`.</summary>
        internal static void RaiseValidate(ScriptableObject so) =>
            so.GetType().GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(so, null);

        static void AssertNear(Vector3 expected, Vector3 actual, string msg) =>
            Assert.Less(Vector3.Distance(expected, actual), 1e-3f, $"{msg}: 기대 {expected:F3}, 실제 {actual:F3}");

        /// <summary>카메라 하나를 RenderTexture에 그려 PNG로 쓴다(URP: 렌더 요청).</summary>
        internal static void Shot(Camera cam, RenderTexture rt, string path)
        {
            var req = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            else cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
