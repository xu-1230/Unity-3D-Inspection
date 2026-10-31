using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace VirtualInspection.AR
{
    /// <summary>
    /// AR 问题标记应用入口：
    /// 移动端使用 AR Foundation（Session / 平面检测 / 射线检测 / 图像跟踪）在真实场景中放置标记；
    /// 编辑器或桌面端进入模拟模式，方便在没有 AR 设备时预览和验证完整流程。
    /// </summary>
    public class ARIssueApp : MonoBehaviour
    {
        private ARSession session;
        private XROrigin xrOrigin;
        private ARRaycastManager raycastManager;
        private ARTrackedImageManager imageManager;

        private Camera targetCamera;
        private bool useAR;
        private SiteAnchorTracker site;
        private IssueMarkerStore store;
        private ARIssueUI ui;
        private ARPlacementController placement;
        private bool restored;

        private readonly List<ARRaycastHit> arHits = new List<ARRaycastHit>();
        private GameObject simulationRoot;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            if (Application.isMobilePlatform)
            {
                Screen.orientation = ScreenOrientation.Portrait;
            }

            store = new IssueMarkerStore();
            store.Load();

            if (Application.isMobilePlatform)
            {
                SetupAR();
            }
            else
            {
                SetupSimulation();
            }

            if (targetCamera == null)
            {
                Debug.LogError("[AR 巡检] 场景中没有可用相机，无法启动 AR 问题标记。");
                return;
            }

            BuildUI();
            BuildPlacement();
            RestoreMarkersIfReady();

            if (site != null && !site.IsLocalized)
            {
                site.Localized += OnSiteLocalized;
            }

            UpdateStatus();

            if (store.LoadWarning != null && ui != null)
            {
                ui.ShowToast("读取历史标记失败：" + store.LoadWarning, false);
            }
            else if (store.Markers.Count > 0 && ui != null)
            {
                ui.ShowToast("已载入 " + store.Markers.Count + " 个历史问题标记", true);
            }
        }

        private void Update()
        {
            if (site != null) site.Tick();
            UpdateStatus();
        }

        // ------------------------------------------------------------ 模式装配

        private void SetupAR()
        {
            session = FindObjectOfType<ARSession>();
            xrOrigin = FindObjectOfType<XROrigin>();
            raycastManager = FindObjectOfType<ARRaycastManager>();
            imageManager = FindObjectOfType<ARTrackedImageManager>();

            if (xrOrigin == null || raycastManager == null || imageManager == null || xrOrigin.Camera == null)
            {
                Debug.LogWarning("[AR 巡检] AR 组件不完整，退回到模拟模式。");
                SetupSimulation();
                return;
            }

            useAR = true;
            targetCamera = xrOrigin.Camera;
            site = new SiteAnchorTracker(imageManager, "Site Anchor Root");

            Debug.Log("[AR 巡检] AR Foundation 已启动，等待平面与现场基准图。");
        }

        private void SetupSimulation()
        {
            useAR = false;

            if (session != null) session.gameObject.SetActive(false);
            if (xrOrigin != null) xrOrigin.gameObject.SetActive(false);
            if (raycastManager != null) raycastManager.enabled = false;
            if (imageManager != null) imageManager.enabled = false;

            simulationRoot = new GameObject("AR 模拟环境");
            BuildSimulationEnvironment(simulationRoot.transform);

            targetCamera = CreateSimulationCamera(simulationRoot.transform);

            site = new SiteAnchorTracker(null, "Site Anchor Root");
            site.SetSimulatedSite(new Vector3(0f, 0f, 1f), Quaternion.identity);

            Debug.Log("[AR 巡检] 当前不是移动平台，已进入模拟模式（鼠标点击地面放置标记）。");
        }

        private void BuildSimulationEnvironment(Transform root)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.46f, 0.49f, 0.53f);
            RenderSettings.fog = false;

            Material groundMaterial = CreateLitMaterial("模拟地面", new Color(0.30f, 0.32f, 0.35f), 0.02f);
            Material lineMaterial = CreateLitMaterial("模拟网格", new Color(0.55f, 0.58f, 0.62f), 0f);

            Shape.Box(root, "模拟地面", new Vector3(0f, -0.05f, 0f), new Vector3(24f, 0.1f, 24f), groundMaterial);

            for (int i = -6; i <= 6; i++)
            {
                Shape.Box(root, "网格线", new Vector3(i * 2f, 0.002f, 0f), new Vector3(0.02f, 0.004f, 24f), lineMaterial, false);
                Shape.Box(root, "网格线", new Vector3(0f, 0.002f, i * 2f), new Vector3(24f, 0.004f, 0.02f), lineMaterial, false);
            }

            CreateSiteTargetVisual(root);

            GameObject lightGo = new GameObject("模拟灯光");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            light.color = new Color(1f, 0.98f, 0.94f);
        }

        private void CreateSiteTargetVisual(Transform root)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "现场基准图（模拟）";
            quad.transform.SetParent(root, false);
            quad.transform.position = new Vector3(0f, 0.006f, 1f);
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(0.6f, 0.6f, 1f);

            Collider collider = quad.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            Texture2D texture = Resources.Load<Texture2D>("SiteAnchor");
            Material material = CreateLitMaterial("模拟基准图", Color.white, 0f);
            if (texture != null) material.mainTexture = texture;
            else material.color = new Color(0.85f, 0.65f, 0.2f);

            Renderer renderer = quad.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
        }

        private Camera CreateSimulationCamera(Transform root)
        {
            GameObject cameraGo = new GameObject("模拟相机");
            cameraGo.transform.SetParent(root, false);
            cameraGo.transform.position = new Vector3(0f, 1.65f, -2.7f);
            cameraGo.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.25f, 1.1f) - cameraGo.transform.position, Vector3.up);
            cameraGo.tag = "MainCamera";

            Camera camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 62f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 60f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.07f, 0.09f, 0.12f);
            cameraGo.AddComponent<AudioListener>();

            return camera;
        }

        private static Material CreateLitMaterial(string name, Color color, float metallic)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");

            Material material = new Material(shader);
            material.name = name;
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.2f);
            return material;
        }

        // ------------------------------------------------------------ 界面与交互

        private void BuildUI()
        {
            GameObject uiGo = new GameObject("AR UI");
            uiGo.transform.SetParent(transform, false);

            ui = uiGo.AddComponent<ARIssueUI>();
            ui.Build(targetCamera, !useAR);
        }

        private void BuildPlacement()
        {
            placement = gameObject.AddComponent<ARPlacementController>();
            placement.Initialize(targetCamera, ui, site, store, TryRaycast);
        }

        private bool TryRaycast(Vector2 screenPoint, out Pose pose)
        {
            pose = default(Pose);
            if (targetCamera == null) return false;

            if (!useAR)
            {
                Ray ray = targetCamera.ScreenPointToRay(screenPoint);
                RaycastHit hit;
                if (!Physics.Raycast(ray, out hit, 40f, ~0, QueryTriggerInteraction.Ignore)) return false;

                pose = new Pose(hit.point, Quaternion.FromToRotation(Vector3.up, hit.normal));
                return true;
            }

            if (raycastManager == null) return false;

            arHits.Clear();
            if (!raycastManager.Raycast(screenPoint, arHits, TrackableType.PlaneWithinPolygon)) return false;
            if (arHits.Count == 0) return false;

            pose = arHits[0].pose;
            return true;
        }

        private void OnSiteLocalized()
        {
            RestoreMarkersIfReady();
            UpdateStatus();
        }

        private void RestoreMarkersIfReady()
        {
            if (restored || site == null || !site.IsLocalized) return;
            restored = true;

            for (int i = 0; i < store.Markers.Count; i++)
            {
                IssueMarkerVisual.Create(site.Root, store.Markers[i], targetCamera, i + 1);
            }

            if (ui != null) ui.UpdateCount(store.Markers.Count);
            Debug.Log("[AR 巡检] 已在现场基准图上恢复 " + store.Markers.Count + " 个问题标记。");
        }

        private void UpdateStatus()
        {
            if (ui == null) return;

            if (!useAR)
            {
                ui.SetStatus("模拟模式 · 鼠标点击地面放置标记", new Color(0.38f, 0.86f, 0.52f, 1f));
                ui.SetBanner("模拟模式仅用于编辑器/桌面预览；真机将使用 AR Foundation 识别平面与现场基准图", new Color(1f, 0.72f, 0.28f, 1f));
                return;
            }

            ARSessionState state = ARSession.state;
            if (state == ARSessionState.Unsupported)
            {
                ui.SetStatus("当前设备不支持 AR", new Color(1f, 0.42f, 0.34f, 1f));
                ui.SetBanner("请使用支持 ARCore / ARKit 的设备，并确认已启用对应 XR 插件。", new Color(1f, 0.42f, 0.34f, 1f));
                return;
            }

            if (state != ARSessionState.SessionTracking)
            {
                ui.SetStatus("正在初始化 AR…", new Color(0.62f, 0.70f, 0.76f, 1f));
                ui.SetBanner("请缓慢移动设备，等待平面检测完成。", new Color(1f, 0.72f, 0.28f, 1f));
                return;
            }

            if (site != null && site.IsLocalized)
            {
                ui.SetStatus(site.IsImageVisible ? "基准图已锁定 · 点击地面放置标记" : "基准图移出视野 · 保持上次定位", new Color(0.38f, 0.86f, 0.52f, 1f));
                ui.SetBanner("已定位现场坐标 · 已保存 " + store.Markers.Count + " 个问题标记", new Color(0.38f, 0.86f, 0.52f, 1f));
            }
            else
            {
                ui.SetStatus("等待识别现场基准图", new Color(1f, 0.72f, 0.28f, 1f));
                ui.SetBanner("请对准现场基准图：未定位时历史标记不可见，新标记也无法保存", new Color(1f, 0.72f, 0.28f, 1f));
            }
        }

        private void OnDestroy()
        {
            if (site != null)
            {
                site.Localized -= OnSiteLocalized;
                site.Dispose();
            }
        }
    }
}
