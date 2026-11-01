using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Unity3DInspection.EditorTools
{
    /// <summary>把巡检场景打成 Windows 独立包，供启动器直接运行。</summary>
    public static class InspectionBuild
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string OutputDir = "Build";
        private const string ExeName = "UnityInspection.exe";

        [MenuItem("Tools/构建 Windows 运行包")]
        public static void BuildFromMenu()
        {
            BuildPlayer();
        }

        /// <summary>批处理入口，供 start-unity.cmd 调用。</summary>
        public static void BuildFromCommandLine()
        {
            bool ok = BuildPlayer();
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(ok ? 0 : 1);
            }
        }

        private static bool BuildPlayer()
        {
            if (!File.Exists(ScenePath))
            {
                // 现场内容由 InspectionBootstrap 在运行时装配，场景文件只需存在且为空。
                UnityEngine.SceneManagement.Scene scene =
                    UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                        UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                        UnityEditor.SceneManagement.NewSceneMode.Single);

                if (!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, ScenePath))
                {
                    Debug.LogError("构建失败：无法创建场景 " + ScenePath);
                    return false;
                }

                AssetDatabase.Refresh();
            }

            EnsureStandardShaderIncluded();
            Directory.CreateDirectory(OutputDir);
            string target = Path.GetFullPath(Path.Combine(OutputDir, ExeName));

            PlayerSettings.companyName = "Unity3DInspection";
            PlayerSettings.productName = "Unity-3D巡检";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = target,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError(string.Format(
                    "构建失败：{0}，错误 {1} 个", summary.result, summary.totalErrors));
                return false;
            }

            Debug.Log(string.Format(
                "构建完成：{0}（{1} MB，耗时 {2}）",
                target,
                summary.totalSize / 1024 / 1024,
                summary.totalTime));
            return true;
        }

        /// <summary>
        /// 现场材质在运行期用 Shader.Find("Standard") 创建，需确保着色器被打进包体。
        /// </summary>
        public static void EnsureStandardShaderIncluded()
        {
            Shader standard = Shader.Find("Standard");
            if (standard == null) return;

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length == 0) return;

            SerializedObject settings = new SerializedObject(assets[0]);
            SerializedProperty shaders = settings.FindProperty("m_AlwaysIncludedShaders");
            if (shaders == null) return;

            for (int i = 0; i < shaders.arraySize; i++)
            {
                if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == standard) return;
            }

            shaders.arraySize++;
            shaders.GetArrayElementAtIndex(shaders.arraySize - 1).objectReferenceValue = standard;
            settings.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("[巡检客户端] 已将 Standard 着色器加入 Always Included Shaders");
        }
    }
}
