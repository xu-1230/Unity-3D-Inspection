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
                SceneBuilder.BuildScene();
            }

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
    }
}
