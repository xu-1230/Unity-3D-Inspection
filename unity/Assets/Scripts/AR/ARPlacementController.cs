using UnityEngine;
using UnityEngine.EventSystems;

namespace VirtualInspection.AR
{
    /// <summary>
    /// 点击命中平面 → 打开问题表单 → 校验 → 保存到现场坐标系并生成标记。
    /// 表单打开期间、以及点击落在任意 uGUI 元素上时，都不会放置标记。
    /// </summary>
    public class ARPlacementController : MonoBehaviour
    {
        public delegate bool RaycastFunc(Vector2 screenPoint, out Pose pose);

        private Camera targetCamera;
        private ARIssueUI ui;
        private SiteAnchorTracker site;
        private IssueMarkerStore store;
        private RaycastFunc raycast;

        private GameObject reticleRoot;
        private GameObject previewRoot;
        private Pose? pendingPose;

        public void Initialize(Camera camera, ARIssueUI issueUI, SiteAnchorTracker siteTracker, IssueMarkerStore markerStore, RaycastFunc raycastFunc)
        {
            targetCamera = camera;
            ui = issueUI;
            site = siteTracker;
            store = markerStore;
            raycast = raycastFunc;

            CreateReticle();

            ui.SubmitRequested += OnSubmitRequested;
            ui.CancelRequested += OnCancelRequested;
        }

        private void Update()
        {
            if (ui == null || raycast == null) return;

            if (ui.IsFormOpen)
            {
                if (reticleRoot != null) reticleRoot.SetActive(false);
                return;
            }

            UpdateReticle();
            HandlePointer();
        }

        private void UpdateReticle()
        {
            if (reticleRoot == null) return;

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Pose pose;

            if (raycast(center, out pose))
            {
                reticleRoot.SetActive(true);
                reticleRoot.transform.position = pose.position + pose.rotation * new Vector3(0f, 0.004f, 0f);
                reticleRoot.transform.rotation = pose.rotation;
            }
            else
            {
                reticleRoot.SetActive(false);
            }
        }

        private void HandlePointer()
        {
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.phase != TouchPhase.Began) continue;
                    if (IsPointerOverUI(touch.fingerId)) continue;

                    TryOpenForm(touch.position);
                    break;
                }

                return;
            }

            if (Input.GetMouseButtonDown(0) && !IsPointerOverUI(-1))
            {
                TryOpenForm(Input.mousePosition);
            }
        }

        /// <summary>
        /// 触摸按 fingerId、鼠标按无参重载判断是否点在 UI 上。
        /// 表单背景、按钮、输入框都是 raycastTarget，因此不会穿透到场景。
        /// </summary>
        private static bool IsPointerOverUI(int pointerId)
        {
            if (EventSystem.current == null) return false;

            return pointerId >= 0
                ? EventSystem.current.IsPointerOverGameObject(pointerId)
                : EventSystem.current.IsPointerOverGameObject();
        }

        private void TryOpenForm(Vector2 screenPoint)
        {
            Pose pose;
            if (!raycast(screenPoint, out pose))
            {
                ui.ShowToast("未命中平面：请对准地面后重试", false);
                return;
            }

            pendingPose = pose;

            Vector3 localPosition = Vector3.zero;
            Quaternion localRotation = Quaternion.identity;
            bool localized = site != null && site.TryToLocal(pose, out localPosition, out localRotation);

            ShowPreview(pose);
            ui.OpenForm(localized, localized ? FormatPosition(localPosition) : "未定位 · 请对准现场基准图后提交");
        }

        private void OnSubmitRequested(string title, string description, IssuePriority priority)
        {
            string trimmedTitle = (title ?? string.Empty).Trim();
            string trimmedDescription = (description ?? string.Empty).Trim();

            if (trimmedTitle.Length == 0)
            {
                Fail("提交失败：标题不能为空", "标题不能为空，请填写后再提交。");
                return;
            }

            if (!pendingPose.HasValue)
            {
                Fail("提交失败：位置已失效，请重新点击地面", "没有有效的落点，请关闭表单后重新点击地面。");
                return;
            }

            if (site == null || !site.IsLocalized)
            {
                Fail("提交失败：未识别现场基准图", "未识别现场基准图，无法保存到现场坐标；请对准基准图后再提交。");
                return;
            }

            Vector3 localPosition;
            Quaternion localRotation;
            if (!site.TryToLocal(pendingPose.Value, out localPosition, out localRotation))
            {
                Fail("提交失败：无法换算现场坐标", "无法换算到现场坐标系，请重新对准基准图。");
                return;
            }

            IssueMarkerRecord record = new IssueMarkerRecord();
            record.id = System.Guid.NewGuid().ToString("N");
            record.title = trimmedTitle;
            record.description = trimmedDescription;
            record.priority = (int)priority;
            record.createdAt = System.DateTime.UtcNow.ToString("o");
            record.position = localPosition;
            record.rotation = localRotation;

            int index = (store != null ? store.Markers.Count : 0) + 1;
            IssueMarkerVisual visual = IssueMarkerVisual.Create(site.Root, record, targetCamera, index);
            if (visual == null)
            {
                Fail("提交失败：无法创建标记", "标记创建失败，请重试。");
                return;
            }

            string error = null;
            if (store == null || !store.TryAdd(record, out error))
            {
                Destroy(visual.gameObject);
                Fail("提交失败：无法写入本地存档", "保存失败：" + (string.IsNullOrEmpty(error) ? "未知错误" : error));
                return;
            }

            Debug.Log("[AR 巡检] 已保存问题标记：" + trimmedTitle + " @ " + FormatPosition(localPosition));

            ui.CloseForm();
            ClearPreview();
            ui.UpdateCount(store.Markers.Count);
            ui.ShowToast("提交成功：" + trimmedTitle + "（" + IssuePriorityUtility.ToLabel(priority) + "）", true);
        }

        private void OnCancelRequested()
        {
            ui.CloseForm();
            ClearPreview();
        }

        private void Fail(string toast, string formError)
        {
            ui.ShowFormError(formError);
            ui.ShowToast(toast, false);
        }

        private static string FormatPosition(Vector3 position)
        {
            return string.Format("X {0:F2}   Y {1:F2}   Z {2:F2} m", position.x, position.y, position.z);
        }

        private void ShowPreview(Pose pose)
        {
            ClearPreview();

            previewRoot = new GameObject("放置预览");
            previewRoot.transform.position = pose.position;
            previewRoot.transform.rotation = pose.rotation;

            Material material = CreateGhostMaterial();
            Shape.Cylinder(previewRoot.transform, "预览圆环", new Vector3(0f, 0.003f, 0f), 0.09f, 0.004f, material, Vector3.up, false);
            Shape.Cylinder(previewRoot.transform, "预览立柱", new Vector3(0f, 0.1f, 0f), 0.006f, 0.19f, material, Vector3.up, false);
            Shape.Sphere(previewRoot.transform, "预览头", new Vector3(0f, 0.215f, 0f), new Vector3(0.045f, 0.045f, 0.045f), material, false);
        }

        private void ClearPreview()
        {
            if (previewRoot != null) Destroy(previewRoot);
            previewRoot = null;
            pendingPose = null;
        }

        private void CreateReticle()
        {
            reticleRoot = new GameObject("放置准星");
            Material material = CreateGhostMaterial();

            Shape.Cylinder(reticleRoot.transform, "准星圆环", Vector3.zero, 0.12f, 0.005f, material, Vector3.up, false);
            Shape.Sphere(reticleRoot.transform, "准星点", new Vector3(0f, 0.004f, 0f), new Vector3(0.02f, 0.02f, 0.02f), material, false);
            reticleRoot.SetActive(false);
        }

        private static Material CreateGhostMaterial()
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");

            Material material = new Material(shader);
            material.name = "AR放置提示";

            Color color = new Color(0.3f, 0.95f, 1f, 1f);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2f);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            return material;
        }

        private void OnDestroy()
        {
            if (ui != null)
            {
                ui.SubmitRequested -= OnSubmitRequested;
                ui.CancelRequested -= OnCancelRequested;
            }

            if (reticleRoot != null) Destroy(reticleRoot);
            if (previewRoot != null) Destroy(previewRoot);
        }
    }
}
