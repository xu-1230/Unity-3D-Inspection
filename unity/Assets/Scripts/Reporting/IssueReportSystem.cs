using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VirtualInspection.Reporting
{
    /// <summary>
    /// 点击上报与问题查看：
    /// 启动时从后端拉取全部问题并还原标记；准星指向标记会发光，点击标记查看问题信息；
    /// 准星指向其他表面时左键新建上报。
    /// </summary>
    public class IssueReportSystem : MonoBehaviour
    {
        private const float MaxPickDistance = 45f;

        /// <summary>后台轮询间隔，与管理端轮询节奏保持一致。</summary>
        private const float PollIntervalSeconds = 5f;

        /// <summary>连续失败若干次后把间隔拉长，成功一次即恢复。</summary>
        private const int PollFailThreshold = 3;

        private static readonly Color PanelColor = new Color(0.031f, 0.043f, 0.059f, 0.96f);
        private static readonly Color FieldColor = new Color(0.09f, 0.11f, 0.14f, 0.98f);
        private static readonly Color AccentColor = new Color(0.26f, 0.52f, 0.96f, 1f);
        private static readonly Color IdleColor = new Color(0.18f, 0.21f, 0.26f, 1f);
        private static readonly Color ErrorColor = new Color(1f, 0.45f, 0.42f, 1f);
        private static readonly Color SuccessColor = new Color(0.38f, 0.86f, 0.52f, 1f);
        private static readonly Color NeutralColor = new Color(0.82f, 0.84f, 0.86f, 1f);
        private static readonly Color MarkerColor = new Color(0.97f, 0.35f, 0.28f, 1f);

        /// <summary>颜色只表示状态：待处理=红、处理中=橙、已解决=绿。</summary>
        private static Color StatusColor(string status)
        {
            if (status == "resolved") return new Color(0.36f, 0.80f, 0.44f, 1f);
            if (status == "in_progress") return new Color(0.98f, 0.68f, 0.18f, 1f);
            return new Color(0.93f, 0.29f, 0.24f, 1f);
        }

        private static float PriorityScale(string issuePriority)
        {
            if (issuePriority == IssuePriorityLevel.High) return 0.38f;
            if (issuePriority == IssuePriorityLevel.Low) return 0.24f;
            return 0.31f;
        }

        /// <summary>任一弹窗（上报表单 / 问题详情）打开时置为 true，供巡检系统让出输入。</summary>
        public static bool IsFormOpen { get; private set; }

        private class MarkerRecord
        {
            public string Id;
            public string Title;
            public string Description;
            public string Priority;
            public string Status;
            public string CreatedAt;
            public Vector3 Position;
            public GameObject Marker;
            public Material Material;
        }

        private FirstPersonPlayer player;
        private IssueApiClient apiClient;
        private Camera viewCamera;

        private RectTransform formPanel;
        private InputField titleInput;
        private InputField descriptionInput;
        private Text coordText;
        private Text statusText;
        private Button submitButton;
        private Button cancelButton;
        private readonly Dictionary<string, Button> priorityButtons = new Dictionary<string, Button>();

        private RectTransform resultCanvas;
        private RectTransform resultPanel;
        private Image resultAccent;
        private Text resultTitle;
        private Text resultBody;
        private Text resultHint;
        private float resultTimer;
        private bool resultAutoClose;

        private RectTransform infoPanel;
        private Image infoAccent;
        private Text infoTitle;
        private Text infoMeta;
        private Text infoDescription;
        private Text infoCoord;
        private Text infoHint;

        private readonly List<MarkerRecord> markers = new List<MarkerRecord>();
        private readonly Dictionary<GameObject, MarkerRecord> markerIndex = new Dictionary<GameObject, MarkerRecord>();
        private MarkerRecord hovered;
        private MarkerRecord infoRecord;
        private GameObject pendingMarker;
        private Material pendingMaterial;

        private string priority = IssuePriorityLevel.Medium;
        private Vector3 pendingPosition;
        private string pendingDeviceId = string.Empty;
        private bool submitting;

        public void Setup(FirstPersonPlayer firstPersonPlayer)
        {
            player = firstPersonPlayer;
            viewCamera = player != null ? player.ViewCamera : Camera.main;

            apiClient = GetComponent<IssueApiClient>();
            if (apiClient == null) apiClient = gameObject.AddComponent<IssueApiClient>();

            BuildForm();
            BuildInfoPanel();
            BuildResultPanel();
            HideForm();
            HideInfo();
            HideResult();
        }

        private void Start()
        {
            StartCoroutine(LoadExistingIssues());
            StartCoroutine(PollIssues());
        }

        private IEnumerator LoadExistingIssues()
        {
            yield return apiClient.FetchIssues(
                items =>
                {
                    Reconcile(items);
                    Debug.Log("[上报] 已从后端还原问题标记 " + markers.Count + " 个");
                },
                error => Debug.LogWarning("[上报] 拉取历史问题失败：" + error));
        }

        /// <summary>后台轮询：弹窗打开或窗口失焦时跳过，避免打断输入与无谓请求。</summary>
        private IEnumerator PollIssues()
        {
            float interval = PollIntervalSeconds;
            int failStreak = 0;

            while (true)
            {
                yield return new WaitForSecondsRealtime(interval);

                if (IsFormOpen) continue;
                if (!Application.isFocused) continue;

                IssueItem[] items = null;
                yield return apiClient.FetchIssues(
                    result => items = result,
                    error => Debug.LogWarning("[上报] 自动刷新失败：" + error));

                if (items != null)
                {
                    failStreak = 0;
                    interval = PollIntervalSeconds;
                    Reconcile(items);
                }
                else
                {
                    failStreak++;
                    if (failStreak >= PollFailThreshold) interval = PollIntervalSeconds * 3f;
                }
            }
        }

        /// <summary>
        /// 把场景标记与后端列表对齐：新增的问题补标记，消失的标记销毁，
        /// 变化的就地更新状态与优先级。正在 hover 或被详情面板引用的标记会先清理引用。
        /// </summary>
        private void Reconcile(IssueItem[] items)
        {
            if (items == null) return;

            HashSet<string> alive = new HashSet<string>();
            int added = 0;
            int updated = 0;

            for (int i = 0; i < items.Length; i++)
            {
                IssueItem item = items[i];
                if (item == null || item.location == null || string.IsNullOrEmpty(item.id)) continue;

                alive.Add(item.id);
                MarkerRecord record = FindById(item.id);

                if (record == null)
                {
                    CreateMarkerRecord(item.id, item.title, item.description, item.priority, item.status,
                        item.createdAt, new Vector3(item.location.x, item.location.y, item.location.z));
                    added++;
                    continue;
                }

                bool changed = record.Status != item.status
                    || record.Priority != item.priority
                    || record.Title != item.title
                    || record.Description != item.description;

                record.Title = item.title;
                record.Description = item.description;
                record.Priority = item.priority;
                record.Status = item.status;

                if (changed)
                {
                    ApplyStatusVisual(record);
                    updated++;
                }
            }

            int removed = 0;
            for (int i = markers.Count - 1; i >= 0; i--)
            {
                MarkerRecord record = markers[i];
                if (string.IsNullOrEmpty(record.Id)) continue;
                if (alive.Contains(record.Id)) continue;

                RemoveMarker(record);
                removed++;
            }

            if (added + updated + removed > 0)
            {
                Debug.Log("[上报] 标记同步：新增 " + added + "，变更 " + updated + "，移除 " + removed);
            }
        }

        private MarkerRecord FindById(string id)
        {
            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i].Id == id) return markers[i];
            }

            return null;
        }

        private void RemoveMarker(MarkerRecord record)
        {
            if (record == null) return;

            if (hovered == record) hovered = null;
            if (infoRecord == record) HideInfo();

            if (record.Marker != null) markerIndex.Remove(record.Marker);
            markers.Remove(record);

            if (record.Marker != null) Destroy(record.Marker);
        }

        private void Update()
        {
            UpdateMarkerGlow();

            if (resultCanvas != null && resultCanvas.gameObject.activeSelf)
            {
                if (resultAutoClose)
                {
                    resultTimer -= Time.unscaledDeltaTime;
                    if (resultTimer <= 0f)
                    {
                        HideResult();
                        return;
                    }
                }

                if (Input.GetKeyDown(KeyCode.Escape)) HideResult();
                return;
            }

            if (infoPanel != null && infoPanel.gameObject.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) HideInfo();
                return;
            }

            if (formPanel != null && formPanel.gameObject.activeSelf)
            {
                if (!submitting && Input.GetKeyDown(KeyCode.Escape)) Cancel();
                return;
            }

            if (viewCamera == null && player != null) viewCamera = player.ViewCamera;
            if (viewCamera == null) return;

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                SetHovered(null);
                return;
            }

            Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit hit;
            bool hasHit = Physics.Raycast(ray, out hit, MaxPickDistance, ~0, QueryTriggerInteraction.Ignore);

            MarkerRecord target = hasHit ? FindMarker(hit.collider.gameObject) : null;
            SetHovered(target);

            if (!Input.GetMouseButtonDown(0)) return;

            if (target != null)
            {
                ShowInfo(target);
            }
            else if (hasHit)
            {
                if (player != null && hit.collider != null && hit.collider.transform.IsChildOf(player.transform)) return;

                // 以命中对象名作为设备编号，便于管理端按设备聚合；后端按可选字段存储。
                string deviceId = hit.collider != null ? hit.collider.gameObject.name : string.Empty;
                OpenForm(hit.point, deviceId);
            }
        }

        // ------------------------------------------------------------ 标记

        private MarkerRecord FindMarker(GameObject go)
        {
            if (go == null) return null;

            MarkerRecord record;
            if (markerIndex.TryGetValue(go, out record)) return record;

            Transform parent = go.transform.parent;
            while (parent != null)
            {
                if (markerIndex.TryGetValue(parent.gameObject, out record)) return record;
                parent = parent.parent;
            }

            return null;
        }

        private MarkerRecord CreateMarkerRecord(string id, string title, string description, string issuePriority,
            string status, string createdAt, Vector3 position)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "问题标记 " + (string.IsNullOrEmpty(id) ? title : id);
            marker.transform.SetParent(transform, false);
            marker.transform.position = position;
            marker.transform.localScale = Vector3.one * PriorityScale(issuePriority);

            Renderer renderer = marker.GetComponent<Renderer>();
            Material material = null;
            if (renderer != null)
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                material = new Material(shader);
                material.color = StatusColor(status);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                renderer.material = material;
            }

            MarkerRecord record = new MarkerRecord
            {
                Id = id,
                Title = title,
                Description = description,
                Priority = issuePriority,
                Status = status,
                CreatedAt = createdAt,
                Position = position,
                Marker = marker,
                Material = material,
            };

            markers.Add(record);
            markerIndex[marker] = record;
            ApplyStatusVisual(record);
            return record;
        }

        /// <summary>
        /// 状态只改颜色（待处理红 / 处理中橙 / 已解决绿），优先级只改尺寸，两个维度互不抢占。
        /// </summary>
        private void ApplyStatusVisual(MarkerRecord record)
        {
            if (record == null || record.Marker == null) return;

            record.Marker.transform.localScale = Vector3.one * PriorityScale(record.Priority);

            if (record.Material != null)
            {
                record.Material.color = StatusColor(record.Status);
            }
        }

        private void SetHovered(MarkerRecord record)
        {
            if (hovered == record) return;
            hovered = record;
        }

        private void UpdateMarkerGlow()
        {
            float pulse = 1.1f + Mathf.Sin(Time.time * 4f) * 0.35f;

            for (int i = 0; i < markers.Count; i++)
            {
                MarkerRecord record = markers[i];
                if (record.Material == null) continue;

                bool on = record == hovered;
                Color glow = Color.Lerp(record.Material.color, Color.white, 0.45f) * pulse;
                record.Material.EnableKeyword("_EMISSION");
                record.Material.SetColor("_EmissionColor", on ? glow : Color.black);
            }
        }

        // ------------------------------------------------------------ 上报表单

        private void OpenForm(Vector3 worldPosition, string deviceId)
        {
            pendingPosition = worldPosition;
            pendingDeviceId = deviceId ?? string.Empty;

            if (pendingMarker != null) Destroy(pendingMarker);
            pendingMarker = CreatePendingMarker(worldPosition);

            coordText.text = string.Format("坐标：({0:F2}, {1:F2}, {2:F2})", worldPosition.x, worldPosition.y, worldPosition.z);
            SetStatus("填写后提交，按 Esc 取消", NeutralColor);

            SetPriority(IssuePriorityLevel.Medium);
            IsFormOpen = true;
            formPanel.gameObject.SetActive(true);

            if (player != null) player.SetControls(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            titleInput.ActivateInputField();
        }

        private GameObject CreatePendingMarker(Vector3 position)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "待提交标记";
            marker.transform.SetParent(transform, false);
            marker.transform.position = position;
            marker.transform.localScale = Vector3.one * PriorityScale(priority);

            Collider collider = marker.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            Renderer renderer = marker.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                Material material = new Material(shader);
                material.color = StatusColor("open");
                renderer.material = material;
                pendingMaterial = material;
            }

            return marker;
        }

        private void HideForm()
        {
            if (formPanel != null) formPanel.gameObject.SetActive(false);
            IsFormOpen = false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void Cancel()
        {
            if (pendingMarker != null)
            {
                Destroy(pendingMarker);
                pendingMarker = null;
                pendingMaterial = null;
            }

            HideForm();
            if (player != null) player.SetControls(true);
        }

        private void OnSubmitClicked()
        {
            if (submitting) return;

            string title = titleInput.text != null ? titleInput.text.Trim() : string.Empty;
            if (title.Length == 0)
            {
                SetStatus("标题不能为空", ErrorColor);
                return;
            }

            IssuePayload payload = new IssuePayload
            {
                title = title,
                description = descriptionInput.text ?? string.Empty,
                priority = priority,
                deviceId = pendingDeviceId,
                area = InspectionRegions.Resolve(pendingPosition),
                location = new IssueLocationDto
                {
                    x = pendingPosition.x,
                    y = pendingPosition.y,
                    z = pendingPosition.z,
                },
            };

            StartCoroutine(Submit(payload));
        }

        private IEnumerator Submit(IssuePayload payload)
        {
            submitting = true;
            submitButton.interactable = false;
            cancelButton.interactable = false;
            SetStatus("提交中…", NeutralColor);

            bool finished = false;
            string failure = null;
            IssueResponse response = null;

            yield return apiClient.CreateIssue(
                payload,
                created =>
                {
                    response = created;
                    finished = true;
                },
                message =>
                {
                    failure = message;
                    finished = true;
                });

            submitting = false;
            submitButton.interactable = true;
            cancelButton.interactable = true;

            if (finished && failure == null)
            {
                string id = response != null ? response.id : string.Empty;
                string status = response != null ? response.status : "open";

                if (pendingMarker != null)
                {
                    Destroy(pendingMarker);
                    pendingMarker = null;
                    pendingMaterial = null;
                }

                CreateMarkerRecord(id, payload.title, payload.description, payload.priority, status,
                    System.DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss"), pendingPosition);

                titleInput.text = string.Empty;
                descriptionInput.text = string.Empty;
                HideForm();

                ShowResult(
                    true,
                    "上报成功",
                    "编号 " + (string.IsNullOrEmpty(id) ? "未同步" : id)
                        + "    优先级 " + IssuePriorityLevel.ToLabel(payload.priority)
                        + "\n坐标 (" + pendingPosition.x.ToString("F2") + ", "
                        + pendingPosition.y.ToString("F2") + ", "
                        + pendingPosition.z.ToString("F2") + ")",
                    2.5f);
            }
            else
            {
                string reason = failure ?? "未知错误";
                SetStatus("提交失败：" + reason, ErrorColor);
                ShowResult(false, "上报失败", reason, 0f);
            }
        }

        private void SetStatus(string message, Color color)
        {
            statusText.text = message;
            statusText.color = color;
        }

        private void SetPriority(string value)
        {
            priority = value;
            foreach (KeyValuePair<string, Button> pair in priorityButtons)
            {
                bool selected = pair.Key == value;
                Color baseColor = selected ? AccentColor : IdleColor;

                // 鼠标停留在按钮上时显示的是 highlightedColor，必须一并改，否则选中态看不出来。
                ColorBlock colors = pair.Value.colors;
                colors.normalColor = baseColor;
                colors.highlightedColor = selected ? baseColor : new Color(0.28f, 0.32f, 0.38f, 1f);
                colors.pressedColor = baseColor;
                colors.selectedColor = baseColor;
                pair.Value.colors = colors;
            }

            // 待提交标记随优先级实时变大小（颜色只表示状态，待提交恒为待处理的红色）。
            if (pendingMarker != null)
            {
                pendingMarker.transform.localScale = Vector3.one * PriorityScale(value);
            }
        }

        // ------------------------------------------------------------ 问题详情

        private void ShowInfo(MarkerRecord record)
        {
            infoRecord = record;
            IsFormOpen = true;

            if (infoAccent != null) infoAccent.color = StatusColor(record.Status);
            infoTitle.text = record.Title;
            infoMeta.text = "编号 " + (string.IsNullOrEmpty(record.Id) ? "未同步" : record.Id)
                + "     优先级 " + IssuePriorityLevel.ToLabel(record.Priority)
                + "     状态 " + IssuePriorityLevel.StatusLabel(record.Status);
            infoDescription.text = string.IsNullOrEmpty(record.Description) ? "（无描述）" : record.Description;
            infoCoord.text = string.Format("坐标：({0:F2}, {1:F2}, {2:F2})", record.Position.x, record.Position.y, record.Position.z);
            infoHint.text = "正在核对最新状态…";

            infoPanel.gameObject.SetActive(true);
            if (player != null) player.SetControls(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

            if (!string.IsNullOrEmpty(record.Id)) StartCoroutine(RefreshStatus(record));
        }

        private IEnumerator RefreshStatus(MarkerRecord record)
        {
            yield return apiClient.FetchIssues(
                items =>
                {
                    for (int i = 0; i < items.Length; i++)
                    {
                        if (items[i] == null || items[i].id != record.Id) continue;

                        record.Status = items[i].status;
                        record.Title = items[i].title;
                        record.Description = items[i].description;
                        record.Priority = items[i].priority;

                        // 状态可能被管理端改过，标记外形要跟着变（光环 / 变暗）。
                        ApplyStatusVisual(record);

                        if (infoRecord == record)
                        {
                            infoTitle.text = record.Title;
                            infoMeta.text = "编号 " + record.Id
                                + "     优先级 " + IssuePriorityLevel.ToLabel(record.Priority)
                                + "     状态 " + IssuePriorityLevel.StatusLabel(record.Status);
                            infoDescription.text = string.IsNullOrEmpty(record.Description) ? "（无描述）" : record.Description;
                            infoHint.text = "已同步后端最新状态    ·    按 [ESC] 关闭";
                        }

                        return;
                    }

                    if (infoRecord == record) infoHint.text = "后端已无此问题    ·    按 [ESC] 关闭";
                },
                error =>
                {
                    if (infoRecord == record) infoHint.text = "核对失败：" + error + "    ·    按 [ESC] 关闭";
                });
        }

        private void HideInfo()
        {
            infoRecord = null;
            if (infoPanel != null) infoPanel.gameObject.SetActive(false);
            IsFormOpen = false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (player != null) player.SetControls(true);
        }

        // ------------------------------------------------------------ 界面构建

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject eventGo = new GameObject("上报事件系统", typeof(EventSystem), typeof(StandaloneInputModule));
            eventGo.transform.SetParent(transform, false);
        }

        private RectTransform CreateCanvas(string name, int order)
        {
            GameObject canvasGo = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            return canvasGo.GetComponent<RectTransform>();
        }

        private void BuildForm()
        {
            EnsureEventSystem();

            RectTransform canvas = CreateCanvas("上报界面", 120);
            formPanel = UiFactory.Panel(canvas, "上报面板", PanelColor);
            UiFactory.Place(formPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(760f, 476f));
            UiFactory.AddOutline(formPanel.GetComponent<Image>(), new Color(0.35f, 0.55f, 0.85f, 0.35f));

            Text heading = UiFactory.Label(formPanel, "标题文字", "上报问题", 26, Color.white, TextAnchor.MiddleLeft);
            UiFactory.Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -18f), new Vector2(400f, 34f));

            coordText = UiFactory.Label(formPanel, "坐标", "坐标：-", 16, NeutralColor, TextAnchor.MiddleLeft);
            UiFactory.Place(coordText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -54f), new Vector2(700f, 22f));

            Text titleLabel = UiFactory.Label(formPanel, "标题标签", "标题（必填）", 15, NeutralColor, TextAnchor.MiddleLeft);
            UiFactory.Place(titleLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -88f), new Vector2(300f, 20f));
            titleInput = CreateInput(formPanel, "标题输入", new Vector2(24f, -112f), new Vector2(712f, 42f), "例如：P-101A 泵体渗油", false);

            Text descLabel = UiFactory.Label(formPanel, "描述标签", "描述（可选）", 15, NeutralColor, TextAnchor.MiddleLeft);
            UiFactory.Place(descLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -164f), new Vector2(300f, 20f));
            descriptionInput = CreateInput(formPanel, "描述输入", new Vector2(24f, -188f), new Vector2(712f, 82f), "补充现场情况", true);

            Text priorityLabel = UiFactory.Label(formPanel, "优先级标签", "优先级", 15, NeutralColor, TextAnchor.MiddleLeft);
            UiFactory.Place(priorityLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -284f), new Vector2(300f, 20f));

            CreatePriorityButton("低", IssuePriorityLevel.Low, new Vector2(24f, -308f));
            CreatePriorityButton("中", IssuePriorityLevel.Medium, new Vector2(264f, -308f));
            CreatePriorityButton("高", IssuePriorityLevel.High, new Vector2(504f, -308f));

            statusText = UiFactory.Label(formPanel, "状态", string.Empty, 15, NeutralColor, TextAnchor.MiddleLeft);
            UiFactory.Place(statusText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -356f), new Vector2(712f, 22f));

            submitButton = CreateButton(formPanel, "提交", new Vector2(24f, -392f), new Vector2(348f, 46f));
            submitButton.onClick.AddListener(OnSubmitClicked);

            cancelButton = CreateButton(formPanel, "取消", new Vector2(388f, -392f), new Vector2(348f, 46f));
            cancelButton.onClick.AddListener(Cancel);

            SetPriority(IssuePriorityLevel.Medium);
        }

        private void BuildInfoPanel()
        {
            RectTransform canvas = CreateCanvas("问题详情界面", 121);

            infoPanel = UiFactory.Panel(canvas, "问题详情", PanelColor);
            UiFactory.Place(infoPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(760f, 384f));
            UiFactory.AddOutline(infoPanel.GetComponent<Image>(), new Color(0.85f, 0.45f, 0.35f, 0.35f));

            infoAccent = UiFactory.Panel(infoPanel, "强调条", MarkerColor).GetComponent<Image>();
            UiFactory.Place(infoAccent.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(760f, 3f));

            infoTitle = UiFactory.Label(infoPanel, "问题标题", "", 26, Color.white, TextAnchor.MiddleLeft);
            UiFactory.Place(infoTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -18f), new Vector2(712f, 36f));

            infoMeta = UiFactory.Label(infoPanel, "问题元信息", "", 16, NeutralColor, TextAnchor.MiddleLeft);
            UiFactory.Place(infoMeta.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -58f), new Vector2(712f, 22f));

            Text descLabel = UiFactory.Label(infoPanel, "描述标签", "问题描述", 15, NeutralColor, TextAnchor.MiddleLeft);
            UiFactory.Place(descLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -96f), new Vector2(300f, 20f));

            infoDescription = UiFactory.Label(infoPanel, "问题描述", "", 17, new Color(0.86f, 0.90f, 0.93f, 1f), TextAnchor.UpperLeft);
            infoDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
            infoDescription.lineSpacing = 1.2f;
            UiFactory.Place(infoDescription.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -122f), new Vector2(712f, 120f));

            infoCoord = UiFactory.Label(infoPanel, "问题坐标", "", 16, NeutralColor, TextAnchor.MiddleLeft);
            UiFactory.Place(infoCoord.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -256f), new Vector2(712f, 22f));

            infoHint = UiFactory.Label(infoPanel, "问题提示", "", 15, AccentColor, TextAnchor.MiddleLeft);
            UiFactory.Place(infoHint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -320f), new Vector2(712f, 22f));
        }

        private void BuildResultPanel()
        {
            resultCanvas = CreateCanvas("结果提示界面", 122);

            // 半透明遮罩同时挡住下层的表单，避免弹窗期间误点提交按钮。
            // 遮罩与面板同属一块画布，显隐必须整体切换，否则残留的遮罩会吃掉所有点击。
            Image blocker = RaycastImage(resultCanvas, "结果遮罩", new Color(0f, 0f, 0f, 0.35f));
            UiFactory.Stretch(blocker.rectTransform, 0f);

            resultPanel = UiFactory.Panel(resultCanvas, "结果提示", PanelColor);
            UiFactory.Place(resultPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(560f, 208f));

            resultAccent = UiFactory.Panel(resultPanel, "强调条", SuccessColor).GetComponent<Image>();
            UiFactory.Place(resultAccent.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(560f, 4f));

            resultTitle = UiFactory.Label(resultPanel, "结果标题", "", 26, Color.white, TextAnchor.MiddleLeft);
            UiFactory.Place(resultTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -22f), new Vector2(508f, 34f));

            resultBody = UiFactory.Label(resultPanel, "结果内容", "", 17, new Color(0.86f, 0.90f, 0.93f, 1f), TextAnchor.UpperLeft);
            resultBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            resultBody.lineSpacing = 1.2f;
            UiFactory.Place(resultBody.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -70f), new Vector2(508f, 88f));

            resultHint = UiFactory.Label(resultPanel, "结果提示", "", 15, NeutralColor, TextAnchor.MiddleLeft);
            UiFactory.Place(resultHint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -164f), new Vector2(508f, 22f));
        }

        /// <summary>展示提交结果；自动关闭秒数为 0 时只能按 Esc 关闭（失败场景，需要用户看清原因）。</summary>
        private void ShowResult(bool success, string title, string body, float autoCloseSeconds)
        {
            if (resultPanel == null) return;

            resultTitle.text = title;
            resultBody.text = body;
            resultAccent.color = success ? SuccessColor : ErrorColor;

            resultAutoClose = autoCloseSeconds > 0f;
            resultTimer = autoCloseSeconds;
            resultHint.text = resultAutoClose
                ? string.Format("{0:F1} 秒后自动关闭 · 按 [ESC] 立即关闭", autoCloseSeconds)
                : "按 [ESC] 关闭提示，返回表单修改后重试";

            IsFormOpen = true;
            resultCanvas.gameObject.SetActive(true);
            resultPanel.gameObject.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void HideResult()
        {
            if (resultPanel != null) resultPanel.gameObject.SetActive(false);
            if (resultCanvas != null) resultCanvas.gameObject.SetActive(false);

            bool formOpen = formPanel != null && formPanel.gameObject.activeSelf;
            bool infoOpen = infoPanel != null && infoPanel.gameObject.activeSelf;

            if (!formOpen && !infoOpen)
            {
                IsFormOpen = false;
                if (player != null) player.SetControls(true);
            }
        }

        private InputField CreateInput(RectTransform parent, string name, Vector2 position, Vector2 size, string placeholderText, bool multiline)
        {
            Image background = RaycastImage(parent, name, FieldColor);
            RectTransform rect = background.rectTransform;
            UiFactory.Place(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);

            InputField input = background.gameObject.AddComponent<InputField>();
            input.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            input.targetGraphic = background;

            Text text = CreateFieldText(rect, Color.white, multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft);
            input.textComponent = text;

            Text placeholder = CreateFieldText(rect, new Color(0.55f, 0.58f, 0.62f, 1f), multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft);
            placeholder.text = placeholderText;
            input.placeholder = placeholder;

            return input;
        }

        private static Text CreateFieldText(RectTransform parent, Color color, TextAnchor anchor)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12f, 8f);
            rect.offsetMax = new Vector2(-12f, -8f);

            Text text = go.GetComponent<Text>();
            text.font = UiFactory.UiFont;
            text.fontSize = 18;
            text.color = color;
            text.alignment = anchor;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private void CreatePriorityButton(string label, string value, Vector2 position)
        {
            Button button = CreateButton(formPanel, label, position, new Vector2(232f, 38f));
            button.onClick.AddListener(() => SetPriority(value));
            priorityButtons[value] = button;
        }

        private Button CreateButton(RectTransform parent, string label, Vector2 position, Vector2 size)
        {
            Image background = RaycastImage(parent, "按钮_" + label, IdleColor);
            UiFactory.Place(background.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);

            Button button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            Text text = UiFactory.Label(background.rectTransform, "文字", label, 18, Color.white, TextAnchor.MiddleCenter);
            UiFactory.Stretch(text.rectTransform, 0f);
            return button;
        }

        private static Image RaycastImage(RectTransform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return image;
        }
    }
}
