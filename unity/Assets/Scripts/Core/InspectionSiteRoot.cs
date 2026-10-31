using System.Collections.Generic;
using UnityEngine;
using VirtualInspection.Reporting;

namespace VirtualInspection
{
    /// <summary>
    /// 巡检现场总装配：环境光照 -> 厂区模型 -> 巡检员 -> 巡检点 -> HUD -> 流程控制。
    /// 整套现场由代码生成，不依赖预制体与外部美术资源。
    /// </summary>
    public class InspectionSiteRoot : MonoBehaviour
    {
        private readonly List<InspectionPoint> points = new List<InspectionPoint>();
        private MaterialLibrary materials;
        private FirstPersonPlayer player;
        private HudController hud;
        private RenderTexture minimapTexture;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.pixelLightCount = 8;

            PrepareScene();

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.49f, 0.54f);
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.007f;
            RenderSettings.fogColor = new Color(0.15f, 0.18f, 0.21f);

            materials = new MaterialLibrary();

            Transform world = new GameObject("厂区").transform;
            world.SetParent(transform, false);
            SiteGeometry.Build(world, materials);

            player = CreatePlayer();
            CreateInspectionPoints(player.ViewCamera);
            hud = CreateHud();

            InspectionSystem system = gameObject.AddComponent<InspectionSystem>();
            system.Setup(points, player, hud);

            // 点击上报：与巡检流程并存，表单打开时由巡检系统让出输入。
            IssueReportSystem reporting = gameObject.AddComponent<IssueReportSystem>();
            reporting.Setup(player);

            Debug.Log("[虚拟巡检] 现场装配完成：巡检点 " + points.Count + " 个，厂区 " + SiteLayout.HallWidth + " x " + SiteLayout.HallDepth + " m");
        }

        /// <summary>
        /// 现场自带相机与照明，运行时关闭场景中已有的相机和方向光，
        /// 避免默认场景的 Main Camera / Directional Light 造成重影或屋顶阴影压暗整个车间。
        /// </summary>
        private void PrepareScene()
        {
            Camera[] cameras = FindObjectsOfType<Camera>();
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && !cameras[i].transform.IsChildOf(transform))
                {
                    cameras[i].gameObject.SetActive(false);
                }
            }

            Light[] lights = FindObjectsOfType<Light>();
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] != null && !lights[i].transform.IsChildOf(transform))
                {
                    lights[i].gameObject.SetActive(false);
                }
            }
        }

        private FirstPersonPlayer CreatePlayer()
        {
            GameObject go = new GameObject("巡检员");
            go.transform.SetParent(transform, false);
            go.transform.position = SiteLayout.PlayerSpawn;
            go.transform.rotation = Quaternion.identity;

            FirstPersonPlayer firstPersonPlayer = go.AddComponent<FirstPersonPlayer>();
            firstPersonPlayer.Initialize();
            return firstPersonPlayer;
        }

        private void CreateInspectionPoints(Camera playerCamera)
        {
            List<InspectionDefinition> definitions = InspectionCatalog.Create();

            for (int i = 0; i < definitions.Count; i++)
            {
                InspectionDefinition definition = definitions[i];

                GameObject go = new GameObject("巡检点 " + definition.Id);
                go.transform.SetParent(transform, false);
                go.transform.position = definition.Position;

                InspectionPoint point = go.AddComponent<InspectionPoint>();
                point.Initialize(definition, materials, playerCamera);
                points.Add(point);
            }
        }

        private HudController CreateHud()
        {
            minimapTexture = MinimapRig.Create();

            GameObject go = new GameObject("巡检界面");
            go.transform.SetParent(transform, false);

            HudController controller = go.AddComponent<HudController>();
            controller.Build(points, minimapTexture);
            return controller;
        }

        private void OnDestroy()
        {
            if (minimapTexture != null)
            {
                minimapTexture.Release();
                Destroy(minimapTexture);
            }
        }
    }
}
