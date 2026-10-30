using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Unity3DInspection.EditorTools
{
    /// <summary>一键生成巡检场景，避免手写 .unity YAML 与丢失引用。</summary>
    public static class SceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Tools/生成巡检场景")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateBox("Floor", new Vector3(0f, 0f, 0f), new Vector3(40f, 0.2f, 40f));
            CreateBox("Wall_North", new Vector3(0f, 1.5f, 19.5f), new Vector3(40f, 3f, 0.5f));
            CreateBox("Wall_West", new Vector3(-19.5f, 1.5f, 0f), new Vector3(0.5f, 3f, 40f));
            CreateBox("Cabinet_A", new Vector3(-4f, 1f, 4f), new Vector3(2f, 2f, 1.2f));
            CreateBox("Cabinet_B", new Vector3(3.5f, 1f, -2.5f), new Vector3(2f, 2f, 1.2f));
            CreatePipe("Pipe_Run", new Vector3(6f, 2.4f, 6f));

            CreateLight();

            GameObject rig = new GameObject("CameraRig");
            rig.transform.position = new Vector3(0f, 1.6f, -9f);

            GameObject cameraGo = new GameObject("Main Camera");
            cameraGo.transform.SetParent(rig.transform, false);
            cameraGo.tag = "MainCamera";
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 62f;
            cameraGo.AddComponent<AudioListener>();

            GameObject system = new GameObject("IssueSystem");
            IssueMarkerManager markers = system.AddComponent<IssueMarkerManager>();
            IssueApiClient apiClient = system.AddComponent<IssueApiClient>();

            GameObject canvasGo = new GameObject("IssueCanvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            IssueFormUI form = canvasGo.AddComponent<IssueFormUI>();
            form.markerManager = markers;
            form.apiClient = apiClient;

            CameraRigController rigController = rig.AddComponent<CameraRigController>();
            rigController.enabled = true;
            SceneClickPicker picker = rig.AddComponent<SceneClickPicker>();
            picker.targetCamera = camera;
            picker.markerManager = markers;
            picker.formUI = form;

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            Debug.Log("巡检场景已生成：" + ScenePath + "，按 Play 即可开始点击设备上报。");
        }

        private static GameObject CreateBox(string name, Vector3 position, Vector3 size)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = size;
            return go;
        }

        private static GameObject CreatePipe(string name, Vector3 position)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(0.35f, 5f, 0.35f);
            return go;
        }

        private static void CreateLight()
        {
            GameObject go = new GameObject("Directional Light");
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
        }
    }
}
