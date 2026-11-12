using System;
using System.IO;
using jjevol;
using jjevol.Audio;
using jjevol.Effect;
using MV.Boot;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MV.Editor.Setup
{
    /// <summary>
    /// MV-00 프로젝트 골격(기획서 10 §10.3·§10.6, 11 MV-00). 있는 것은 건드리지 않고 없는 것만 만든다.
    /// 메뉴 「MoonVillage/Setup/Build Project Skeleton」 또는 배치 실행 <see cref="RunBatch"/>.
    /// </summary>
    public static class MvProjectSkeleton
    {
        public const string ScenesFolder = "Assets/Game/Scenes";
        public const string BootScenePath = ScenesFolder + "/Boot.unity";
        public const string GameScenePath = ScenesFolder + "/Game.unity";

        static readonly string[] Folders =
        {
            "Assets/Game/Addressables/Local/UI",
            "Assets/Game/Addressables/Local/Font",
            "Assets/Game/Addressables/Local/UIAtlas",
            "Assets/Game/Addressables/Local/WorldAtlas",
            "Assets/Game/Addressables/Local/Texture",
            "Assets/Game/Addressables/Local/Audio/Sfx",
            "Assets/Game/Addressables/Local/Audio/Music",
            "Assets/Game/Addressables/Local/Effect",
            "Assets/Game/Addressables/Local/Mode",
            "Assets/Game/Addressables/Local/Theme/Update",
            "Assets/Game/Art/Source",
            "Assets/Game/GameData/Table/TSV",
            "Assets/Game/GameData/Table/Resources",
            ScenesFolder,
        };

        [MenuItem("MoonVillage/Setup/Build Project Skeleton")]
        public static void BuildFromMenu()
        {
            Build();
            Debug.Log("[MoonVillage] Project skeleton is ready.");
        }

        /// <summary>배치 실행용. 끝나면 스스로 Editor를 닫는다.</summary>
        public static void RunBatch()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void Build()
        {
            EnsureFolders();
            EnsureAddressableSettings();
            if (!File.Exists(BootScenePath)) CreateBootScene();
            if (!File.Exists(GameScenePath)) CreateGameScene();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
            };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void EnsureFolders()
        {
            foreach (var folder in Folders)
            {
                Directory.CreateDirectory(folder);
                var keep = Path.Combine(folder, ".gitkeep");
                if (Directory.GetFileSystemEntries(folder).Length == 0) File.WriteAllText(keep, string.Empty);
            }
            AssetDatabase.Refresh();
        }

        static void EnsureAddressableSettings()
        {
            // 기본 Addressables 설정만 만든다. Managed Addressables Project Config는 만들면 모든 Player 빌드가
            // BootstrapSplit 경로를 타므로(Framework NewGame.md 3단계 주의) 첫 Addressable 콘텐츠가 들어올 때 만든다.
            if (AddressableAssetSettingsDefaultObject.GetSettings(true) == null)
                throw new InvalidOperationException("Addressable Asset Settings could not be created.");
        }

        static void CreateBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = CreateMainCamera(Color.black);

            var uiObject = new GameObject("UI");
            var creator = uiObject.AddComponent<UIManagerCreater>();
            creator.mainCamera = camera;
            var eventSystem = new GameObject("EventSystem");
            eventSystem.transform.SetParent(uiObject.transform, false);
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            new GameObject("AudioManager").AddComponent<AudioManager>();
            new GameObject("EffectManager").AddComponent<EffectManager>();

            var boot = new GameObject("Boot").AddComponent<GameBootstrap>();
            var serialized = new SerializedObject(boot);
            serialized.FindProperty("ui").objectReferenceValue = creator;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Save(scene, BootScenePath);
        }

        static void CreateGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateMainCamera(new Color(0.08f, 0.07f, 0.12f));

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            new GameObject("GameInitializer").AddComponent<GameInitializer>();

            Save(scene, GameScenePath);
        }

        static Camera CreateMainCamera(Color background)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            go.AddComponent<AudioListener>();
            camera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Base;
            return camera;
        }

        static void Save(Scene scene, string path)
        {
            if (!EditorSceneManager.SaveScene(scene, path))
                throw new InvalidOperationException("Could not save " + path);
        }
    }
}
