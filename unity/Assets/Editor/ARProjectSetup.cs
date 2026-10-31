using System.Collections.Generic;
using System.IO;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace VirtualInspection.EditorTools
{
    /// <summary>
    /// AR 问题标记工程的资源与场景生成：
    /// 现场基准图 PNG → 参考图库 → AR 场景（AR Session / XR Origin / 平面与图像跟踪 / ARIssueApp）。
    /// 同时配置 Android ARCore 与 iOS ARKit 的 XR 加载器。
    /// </summary>
    public static class ARProjectSetup
    {
        private const string ResourcesFolder = "Assets/Resources";
        private const string AnchorTexturePath = "Assets/Resources/SiteAnchor.png";
        private const string LibraryPath = "Assets/Resources/ARSiteReferenceLibrary.asset";
        private const string ScenePath = "Assets/Scenes/ARIssueMarkers.unity";
        private const string SiteImageName = "SiteAnchor";
        private const float SiteImageSizeMeters = 0.3f;

        [MenuItem("Tools/AR 问题标记/生成或修复 AR 场景与资源", false, 20)]
        public static void SetupFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SetupAll();
            EditorUtility.DisplayDialog("AR 问题标记", "AR 场景、现场基准图与参考图库已准备完成。\n\n场景：Assets/Scenes/ARIssueMarkers.unity", "好");
        }

        [MenuItem("Tools/AR 问题标记/打开 AR 场景", false, 21)]
        public static void OpenSceneFromMenu()
        {
            if (!File.Exists(ScenePath))
            {
                SetupAll();
            }

            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        [MenuItem("Tools/AR 问题标记/构建 Android AR 运行包", false, 22)]
        public static void BuildAndroidFromMenu()
        {
            BuildAndroid(false);
        }

        [MenuItem("Tools/AR 问题标记/构建 Windows 预览包（模拟模式）", false, 23)]
        public static void BuildWindowsPreviewFromMenu()
        {
            BuildWindowsPreview(false);
        }

        /// <summary>供 -executeMethod 调用。</summary>
        public static void SetupAll()
        {
            EnsureFolder(ResourcesFolder);
            EnsureFolder("Assets/Scenes");

            Texture2D anchorTexture = EnsureAnchorTexture();
            XRReferenceImageLibrary library = EnsureReferenceLibrary(anchorTexture);

            EnsureARScene(library);
            RegisterScene();
            ConfigureXR();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[AR 巡检] 初始化完成：基准图 " + AnchorTexturePath + "，参考图库 " + LibraryPath + "，场景 " + ScenePath);
        }

        /// <summary>供构建脚本 -executeMethod 调用。</summary>
        public static void BuildAndroidFromCommandLine()
        {
            BuildAndroid(true);
        }

        /// <summary>供构建脚本 -executeMethod 调用：构建无需 AR 设备的 Windows 预览包。</summary>
        public static void BuildWindowsPreviewFromCommandLine()
        {
            BuildWindowsPreview(true);
        }

        private static void BuildWindowsPreview(bool fromCommandLine)
        {
            if (!File.Exists(ScenePath)) SetupAll();

            Unity3DInspection.EditorTools.InspectionBuild.EnsureStandardShaderIncluded();
            Directory.CreateDirectory("Build");

            PlayerSettings.productName = "AR 问题标记 预览";
            PlayerSettings.companyName = "Virtual Inspection";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;

            BuildPlayerOptions options = new BuildPlayerOptions();
            options.scenes = new[] { ScenePath };
            options.locationPathName = "Build/ARIssuePreview.exe";
            options.target = BuildTarget.StandaloneWindows64;
            options.targetGroup = BuildTargetGroup.Standalone;
            options.options = BuildOptions.None;

            BuildReport report = BuildPipeline.BuildPlayer(options);
            bool success = report != null && report.summary.result == BuildResult.Succeeded;

            if (success)
            {
                Debug.Log("[AR 巡检] Windows 预览包构建完成：Build/ARIssuePreview.exe");
            }
            else
            {
                Debug.LogError("[AR 巡检] Windows 预览包构建失败：" + (report != null ? report.summary.result.ToString() : "无构建报告"));
            }

            if (fromCommandLine) EditorApplication.Exit(success ? 0 : 1);
        }

        // ------------------------------------------------------------ 基准图

        private static Texture2D EnsureAnchorTexture()
        {
            if (!File.Exists(AnchorTexturePath))
            {
                GenerateAnchorPng(AnchorTexturePath);
                AssetDatabase.Refresh();
            }

            ConfigureTextureImporter(AnchorTexturePath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(AnchorTexturePath);
        }

        /// <summary>
        /// 生成高对比度、非重复的现场基准图（打印 30 x 30 cm）。
        /// 随机黑白块能提供足够的特征点，非对称图案可避免 180° 误识别。
        /// </summary>
        private static void GenerateAnchorPng(string path)
        {
            const int size = 640;
            const int border = 8;
            const int cells = 26;
            const int cellSize = 24;

            Color white = new Color(0.97f, 0.97f, 0.97f, 1f);
            Color black = new Color(0.02f, 0.02f, 0.02f, 1f);
            Color accent = new Color(0.80f, 0.18f, 0.12f, 1f);

            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = white;

            System.Random random = new System.Random(20261029);

            for (int gridY = 0; gridY < cells; gridY++)
            {
                for (int gridX = 0; gridX < cells; gridX++)
                {
                    bool filled = random.NextDouble() < 0.46;
                    if (!filled) continue;

                    for (int y = 0; y < cellSize; y++)
                    {
                        for (int x = 0; x < cellSize; x++)
                        {
                            int px = border + gridX * cellSize + x;
                            int py = border + gridY * cellSize + y;
                            pixels[py * size + px] = black;
                        }
                    }
                }
            }

            // 左上角红色定位块：便于人工确认打印方向，也避免图案旋转对称
            for (int y = 0; y < cellSize * 2; y++)
            {
                for (int x = 0; x < cellSize * 2; x++)
                {
                    int px = border + cellSize + x;
                    int py = size - border - cellSize * 3 + y;
                    pixels[py * size + px] = accent;
                }
            }

            // 四周黑框
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (x < border || y < border || x >= size - border || y >= size - border)
                    {
                        pixels[y * size + x] = black;
                    }
                }
            }

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            Debug.Log("[AR 巡检] 已生成现场基准图：" + path);
        }

        private static void ConfigureTextureImporter(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("[AR 巡检] 无法读取基准图导入设置：" + path);
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 1024;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        // ------------------------------------------------------------ 参考图库

        private static XRReferenceImageLibrary EnsureReferenceLibrary(Texture2D texture)
        {
            if (texture == null)
            {
                Debug.LogError("[AR 巡检] 基准图缺失，无法创建参考图库。");
                return null;
            }

            XRReferenceImageLibrary library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<XRReferenceImageLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            SerializedObject serialized = new SerializedObject(library);
            SerializedProperty images = serialized.FindProperty("m_Images");
            if (images == null)
            {
                Debug.LogError("[AR 巡检] 参考图库结构异常。");
                return library;
            }

            if (images.arraySize == 0) images.arraySize = 1;

            SerializedProperty element = images.GetArrayElementAtIndex(0);
            element.FindPropertyRelative("m_Name").stringValue = SiteImageName;
            element.FindPropertyRelative("m_SpecifySize").boolValue = true;
            element.FindPropertyRelative("m_Size").vector2Value = new Vector2(SiteImageSizeMeters, SiteImageSizeMeters);
            element.FindPropertyRelative("m_Texture").objectReferenceValue = texture;
            SetGuid(element.FindPropertyRelative("m_SerializedGuid"), System.Guid.NewGuid());
            SetGuid(element.FindPropertyRelative("m_SerializedTextureGuid"), System.Guid.NewGuid());

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            return library;
        }

        private static void SetGuid(SerializedProperty property, System.Guid guid)
        {
            if (property == null) return;

            byte[] bytes = guid.ToByteArray();
            property.FindPropertyRelative("m_GuidLow").ulongValue = System.BitConverter.ToUInt64(bytes, 0);
            property.FindPropertyRelative("m_GuidHigh").ulongValue = System.BitConverter.ToUInt64(bytes, 8);
        }

        // ------------------------------------------------------------ AR 场景

        private static void EnsureARScene(XRReferenceImageLibrary library)
        {
            if (File.Exists(ScenePath)) return;

            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject sessionGo = new GameObject("AR Session");
            sessionGo.AddComponent<ARSession>();
            sessionGo.AddComponent<ARInputManager>();

            GameObject originGo = new GameObject("XR Origin");
            XROrigin origin = originGo.AddComponent<XROrigin>();
            origin.Origin = originGo;

            GameObject offsetGo = new GameObject("Camera Offset");
            offsetGo.transform.SetParent(originGo.transform, false);

            GameObject cameraGo = new GameObject("AR Camera");
            cameraGo.transform.SetParent(offsetGo.transform, false);
            cameraGo.tag = "MainCamera";

            Camera camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 50f;
            camera.fieldOfView = 60f;

            cameraGo.AddComponent<ARCameraManager>();
            cameraGo.AddComponent<ARCameraBackground>();
            cameraGo.AddComponent<AudioListener>();

            origin.Camera = camera;
            origin.CameraFloorOffsetObject = offsetGo;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.NotSpecified;
            origin.CameraYOffset = 0f;

            originGo.AddComponent<ARPlaneManager>();
            originGo.AddComponent<ARRaycastManager>();

            ARTrackedImageManager imageManager = originGo.AddComponent<ARTrackedImageManager>();
            imageManager.referenceLibrary = library;

            GameObject appGo = new GameObject("AR Issue App");
            appGo.AddComponent<VirtualInspection.AR.ARIssueApp>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[AR 巡检] 已生成 AR 场景：" + ScenePath);
        }

        private static void RegisterScene()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path != ScenePath) continue;

                if (!scenes[i].enabled)
                {
                    scenes[i] = new EditorBuildSettingsScene(ScenePath, true);
                    EditorBuildSettings.scenes = scenes.ToArray();
                }
                return;
            }

            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ------------------------------------------------------------ XR 配置

        private static void ConfigureXR()
        {
            AssignLoader(BuildTargetGroup.Android, "UnityEngine.XR.ARCore.ARCoreLoader");
            AssignLoader(BuildTargetGroup.iOS, "UnityEngine.XR.ARKit.ARKitLoader");

            PlayerSettings.iOS.cameraUsageDescription = "需要使用相机在真实场景中放置问题标记。";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.virtualinspection.arissue");
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.virtualinspection.arissue");

            if ((int)PlayerSettings.Android.minSdkVersion < 24)
            {
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            }

            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.buildAppBundle = false;
        }

        private static void AssignLoader(BuildTargetGroup group, string loaderTypeName)
        {
            try
            {
                XRGeneralSettingsPerBuildTarget targetSettings = FindOrCreateSettingsAsset();
                if (targetSettings == null)
                {
                    Debug.LogWarning("[AR 巡检] 无法创建 XR General Settings，请在 Project Settings > XR Plug-in Management 手动启用加载器。");
                    return;
                }

                if (!targetSettings.HasManagerSettingsForBuildTarget(group))
                {
                    targetSettings.CreateDefaultManagerSettingsForBuildTarget(group);
                }

                XRGeneralSettings settings = targetSettings.SettingsForBuildTarget(group);
                if (settings == null || settings.Manager == null)
                {
                    Debug.LogWarning("[AR 巡检] " + group + " 的 XR Manager 未创建。");
                    return;
                }

                bool assigned = XRPackageMetadataStore.AssignLoader(settings.Manager, loaderTypeName, group);
                Debug.Log("[AR 巡检] " + group + " 加载器 " + loaderTypeName + "：" + (assigned ? "已启用" : "启用失败，请手动检查"));
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[AR 巡检] 配置 " + group + " 加载器失败：" + exception.Message);
            }
        }

        private static XRGeneralSettingsPerBuildTarget FindOrCreateSettingsAsset()
        {
            string[] guids = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
            for (int i = 0; i < guids.Length; i++)
            {
                XRGeneralSettingsPerBuildTarget existing =
                    AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (existing != null) return existing;
            }

            EnsureFolder("Assets/XR");
            XRGeneralSettingsPerBuildTarget created = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(created, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            AssetDatabase.SaveAssets();

            return created;
        }

        // ------------------------------------------------------------ 构建

        private static void BuildAndroid(bool fromCommandLine)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                string message = "当前 Unity 未安装 Android Build Support，无法构建 APK。\n请在 Unity Hub 中为 2022.3.62f3c1 安装 Android Build Support（含 SDK/NDK）。";
                Debug.LogError("[AR 巡检] " + message.Replace("\n", " "));

                if (fromCommandLine) EditorApplication.Exit(2);
                else EditorUtility.DisplayDialog("缺少 Android 构建支持", message, "好");
                return;
            }

            if (!File.Exists(ScenePath))
            {
                SetupAll();
            }

            Unity3DInspection.EditorTools.InspectionBuild.EnsureStandardShaderIncluded();

            Directory.CreateDirectory("Build");

            PlayerSettings.productName = "AR 问题标记";
            PlayerSettings.companyName = "Virtual Inspection";

            BuildPlayerOptions options = new BuildPlayerOptions();
            options.scenes = new[] { ScenePath };
            options.locationPathName = "Build/ARInspection.apk";
            options.target = BuildTarget.Android;
            options.targetGroup = BuildTargetGroup.Android;
            options.options = BuildOptions.None;

            BuildReport report = BuildPipeline.BuildPlayer(options);
            bool success = report != null && report.summary.result == BuildResult.Succeeded;

            if (success)
            {
                Debug.Log("[AR 巡检] Android 构建完成：Build/ARInspection.apk");
            }
            else
            {
                Debug.LogError("[AR 巡检] Android 构建失败：" + (report != null ? report.summary.result.ToString() : "无构建报告"));
            }

            if (fromCommandLine) EditorApplication.Exit(success ? 0 : 1);
        }

        // ------------------------------------------------------------ 工具

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path);
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name)) return;

            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
