using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VirtualInspection.AR
{
    /// <summary>
    /// AR 问题标记界面：顶部状态、准星、操作提示、问题表单与提交结果提示。
    /// 表单遮罩、按钮与输入框都会拦截点击，避免在操作 UI 时误放标记。
    /// </summary>
    public class ARIssueUI : MonoBehaviour
    {
        private static readonly Color TextDim = new Color(0.62f, 0.70f, 0.76f, 1f);
        private static readonly Color Accent = new Color(0.25f, 0.80f, 0.92f, 1f);
        private static readonly Color Warn = new Color(1f, 0.72f, 0.28f, 1f);
        private static readonly Color Ok = new Color(0.38f, 0.86f, 0.52f, 1f);
        private static readonly Color Bad = new Color(1f, 0.42f, 0.34f, 1f);

        private Transform canvasRoot;
        private Text statusText;
        private Text bannerText;
        private Text countText;

        private GameObject overlay;
        private GameObject formPanel;
        private InputField titleInput;
        private InputField descriptionInput;
        private Text positionText;
        private Text errorText;

        private Button[] priorityButtons;
        private Text[] priorityLabels;
        private IssuePriority selectedPriority = IssuePriority.Medium;

        private GameObject toastPanel;
        private Image toastBackground;
        private CanvasGroup toastGroup;
        private Text toastText;
        private float toastTimer;

        private string lastBanner;
        private Color lastBannerColor;
        private string lastStatus;
        private Color lastStatusColor;
        private int lastCount = -1;

        public bool IsFormOpen { get; private set; }
        public event Action<string, string, IssuePriority> SubmitRequested;
        public event Action CancelRequested;

        public void Build(Camera camera, bool simulated)
        {
            BuildCanvas();
            BuildTopBar(simulated);
            BuildCrosshair();
            BuildHint();
            BuildForm();
            BuildToast();

            if (overlay != null) overlay.SetActive(false);
            if (formPanel != null) formPanel.SetActive(false);
            if (toastPanel != null) toastPanel.SetActive(false);
        }

        // ------------------------------------------------------------ 构建

        private void BuildCanvas()
        {
            GameObject canvasGo = new GameObject("AR UI Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasRoot = canvasGo.transform;

            if (EventSystem.current == null)
            {
                GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                eventSystem.transform.SetParent(transform, false);
            }
        }

        private void BuildTopBar(bool simulated)
        {
            RectTransform bar = UiFactory.Panel(canvasRoot, "顶部状态", new Color(0.02f, 0.035f, 0.05f, 0.86f));
            bar.GetComponent<Image>().raycastTarget = true;
            UiFactory.Place(bar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1080f, 156f));

            Text title = AddText(bar, "标题", simulated ? "AR 问题标记 · 模拟" : "AR 问题标记", 32, Color.white, 40f, -26f, 560f, 46f, TextAnchor.MiddleLeft, true);
            UiFactory.AddOutline(title, new Color(0f, 0f, 0f, 0.6f));

            countText = AddText(bar, "数量", "已保存 0 个", 22, TextDim, 0f, 0f, 400f, 40f, TextAnchor.MiddleRight);
            RectTransform countRect = countText.rectTransform;
            countRect.anchorMin = new Vector2(1f, 1f);
            countRect.anchorMax = new Vector2(1f, 1f);
            countRect.pivot = new Vector2(1f, 1f);
            countRect.anchoredPosition = new Vector2(-40f, -30f);
            countRect.sizeDelta = new Vector2(400f, 40f);

            statusText = AddText(bar, "状态", "正在初始化 AR…", 21, TextDim, 40f, -88f, 1000f, 44f, TextAnchor.MiddleLeft);

            RectTransform accent = UiFactory.Panel(bar, "强调条", Accent);
            UiFactory.Place(accent, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(1080f, 4f));

            bannerText = AddText(canvasRoot, "定位提示", "", 22, Warn, 40f, -170f, 1000f, 48f, TextAnchor.MiddleLeft);
            bannerText.rectTransform.anchorMin = new Vector2(0f, 1f);
            bannerText.rectTransform.anchorMax = new Vector2(0f, 1f);
            bannerText.rectTransform.pivot = new Vector2(0f, 1f);
            bannerText.rectTransform.anchoredPosition = new Vector2(40f, -170f);
            bannerText.rectTransform.sizeDelta = new Vector2(1000f, 48f);
        }

        private void BuildCrosshair()
        {
            RectTransform root = UiFactory.Panel(canvasRoot, "准星", new Color(0f, 0f, 0f, 0f));
            UiFactory.Place(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48f, 48f));

            AddBar(root, "横线", new Vector2(30f, 2f));
            AddBar(root, "竖线", new Vector2(2f, 30f));

            Image dot = UiFactory.Panel(root, "点", new Color(0.85f, 1f, 1f, 0.9f)).GetComponent<Image>();
            UiFactory.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));
        }

        private void AddBar(Transform parent, string name, Vector2 size)
        {
            Image bar = UiFactory.Panel(parent, name, new Color(1f, 1f, 1f, 0.35f)).GetComponent<Image>();
            UiFactory.Place(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        }

        private void BuildHint()
        {
            RectTransform hint = UiFactory.Panel(canvasRoot, "操作提示", new Color(0.02f, 0.035f, 0.05f, 0.72f));
            hint.GetComponent<Image>().raycastTarget = true;
            UiFactory.Place(hint, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(1000f, 92f));

            Text text = UiFactory.Label(hint, "文字", "点击地面放置问题标记", 26, Color.white, TextAnchor.MiddleCenter);
            UiFactory.Stretch(text.rectTransform, 20f, 6f);
        }

        private void BuildForm()
        {
            RectTransform overlayRect = UiFactory.Panel(canvasRoot, "表单遮罩", new Color(0f, 0f, 0f, 0.55f));
            overlay = overlayRect.gameObject;
            overlayRect.GetComponent<Image>().raycastTarget = true;
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            Transform overlayTransform = overlay.transform;

            RectTransform panel = UiFactory.Panel(overlayTransform, "问题表单", new Color(0.035f, 0.05f, 0.07f, 0.98f));
            panel.GetComponent<Image>().raycastTarget = true;
            UiFactory.Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 1040f));
            formPanel = panel.gameObject;

            RectTransform accent = UiFactory.Panel(panel, "顶部强调条", Accent);
            UiFactory.Place(accent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(980f, 4f));

            AddText(panel, "表单标题", "新建问题标记", 34, Color.white, 40f, -28f, 600f, 48f, TextAnchor.MiddleLeft, true);
            AddText(panel, "表单说明", "保存后随现场基准图定位，下次启动可还原", 18, TextDim, 40f, -78f, 900f, 30f, TextAnchor.MiddleLeft);

            AddText(panel, "标题标签", "标题 *", 22, Color.white, 40f, -126f, 900f, 32f, TextAnchor.MiddleLeft);
            titleInput = CreateInput(panel, "标题输入", "请输入问题标题（必填）", false, new Vector2(40f, -164f), new Vector2(900f, 96f));

            AddText(panel, "描述标签", "描述（可选）", 22, TextDim, 40f, -286f, 900f, 32f, TextAnchor.MiddleLeft);
            descriptionInput = CreateInput(panel, "描述输入", "补充现象、位置说明…", true, new Vector2(40f, -322f), new Vector2(900f, 186f));

            AddText(panel, "优先级标签", "优先级", 22, Color.white, 40f, -538f, 900f, 32f, TextAnchor.MiddleLeft);

            priorityButtons = new Button[3];
            priorityLabels = new Text[3];
            string[] names = { "低", "中", "高" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                Button button = CreateButton(panel, "优先级" + names[i], names[i], new Vector2(40f + i * 317f, -576f), new Vector2(286f, 84f),
                    new Color(0.10f, 0.13f, 0.16f, 1f), delegate { SetPriority((IssuePriority)index); });
                button.transition = Selectable.Transition.None;
                priorityButtons[i] = button;
                priorityLabels[i] = button.GetComponentInChildren<Text>();
            }

            AddText(panel, "位置标签", "位置（现场坐标）", 22, TextDim, 40f, -682f, 900f, 32f, TextAnchor.MiddleLeft);
            positionText = AddText(panel, "位置值", "未定位", 24, Color.white, 40f, -718f, 900f, 40f, TextAnchor.MiddleLeft);

            errorText = AddText(panel, "错误", "", 22, Bad, 40f, -780f, 900f, 64f, TextAnchor.UpperLeft);
            errorText.horizontalOverflow = HorizontalWrapMode.Wrap;
            errorText.lineSpacing = 1.1f;

            Button cancel = CreateButton(panel, "取消", "取消", new Vector2(40f, -880f), new Vector2(440f, 96f), new Color(0.12f, 0.15f, 0.18f, 1f), OnCancelClicked);
            Button submit = CreateButton(panel, "提交", "提交标记", new Vector2(500f, -880f), new Vector2(440f, 96f), new Color(0.10f, 0.48f, 0.62f, 1f), OnSubmitClicked);
            submit.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;

            SetPriority(IssuePriority.Medium);
        }

        private void BuildToast()
        {
            toastBackground = UiFactory.Panel(canvasRoot, "结果提示", new Color(0.10f, 0.42f, 0.26f, 0.96f)).GetComponent<Image>();
            toastPanel = toastBackground.gameObject;
            UiFactory.Place(toastBackground.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -232f), new Vector2(940f, 112f));
            toastGroup = toastPanel.AddComponent<CanvasGroup>();

            toastText = UiFactory.Label(toastBackground.rectTransform, "结果文字", "", 24, Color.white, TextAnchor.MiddleCenter);
            toastText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiFactory.Stretch(toastText.rectTransform, 28f, 14f);
        }

        // ------------------------------------------------------------ 对外接口

        public void OpenForm(bool localized, string positionDescription)
        {
            IsFormOpen = true;

            titleInput.text = string.Empty;
            descriptionInput.text = string.Empty;
            errorText.text = string.Empty;
            SetPriority(IssuePriority.Medium);

            positionText.text = positionDescription;
            positionText.color = localized ? Color.white : Warn;

            overlay.SetActive(true);
            formPanel.SetActive(true);

            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        public void CloseForm()
        {
            IsFormOpen = false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (overlay != null) overlay.SetActive(false);
            if (formPanel != null) formPanel.SetActive(false);
        }

        public void ShowFormError(string message)
        {
            if (errorText != null) errorText.text = message == null ? string.Empty : message;
        }

        public void SetStatus(string text, Color color)
        {
            if (statusText == null) return;
            if (text == lastStatus && color == lastStatusColor) return;

            lastStatus = text;
            lastStatusColor = color;
            statusText.text = text;
            statusText.color = color;
        }

        public void SetBanner(string text, Color color)
        {
            if (bannerText == null) return;
            if (text == lastBanner && color == lastBannerColor) return;

            lastBanner = text;
            lastBannerColor = color;
            bannerText.text = text;
            bannerText.color = color;
        }

        public void UpdateCount(int count)
        {
            if (countText == null || count == lastCount) return;
            lastCount = count;
            countText.text = "已保存 " + count + " 个";
        }

        public void ShowToast(string message, bool success)
        {
            if (toastPanel == null) return;

            toastPanel.SetActive(true);
            toastText.text = message;
            toastBackground.color = success
                ? new Color(0.10f, 0.42f, 0.26f, 0.96f)
                : new Color(0.50f, 0.14f, 0.12f, 0.96f);

            toastTimer = 3.2f;
            if (toastGroup != null) toastGroup.alpha = 1f;
        }

        public void SetPriority(IssuePriority priority)
        {
            selectedPriority = priority;
            UpdatePriorityButtons();
        }

        private void UpdatePriorityButtons()
        {
            if (priorityButtons == null) return;

            for (int i = 0; i < priorityButtons.Length; i++)
            {
                bool selected = (int)selectedPriority == i;
                Color color = selected ? IssuePriorityUtility.ToColor(selectedPriority) : new Color(0.10f, 0.13f, 0.16f, 1f);

                if (priorityButtons[i] != null)
                {
                    Image image = priorityButtons[i].GetComponent<Image>();
                    if (image != null) image.color = color;
                }

                if (priorityLabels[i] != null)
                {
                    priorityLabels[i].color = selected ? Color.white : TextDim;
                    priorityLabels[i].fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
                }
            }
        }

        private void OnSubmitClicked()
        {
            if (SubmitRequested != null)
            {
                SubmitRequested(titleInput.text, descriptionInput.text, selectedPriority);
            }
        }

        private void OnCancelClicked()
        {
            if (CancelRequested != null) CancelRequested();
        }

        private void Update()
        {
            if (toastTimer <= 0f) return;

            toastTimer -= Time.deltaTime;
            if (toastTimer <= 0f)
            {
                if (toastPanel != null) toastPanel.SetActive(false);
                return;
            }

            if (toastGroup != null) toastGroup.alpha = Mathf.Clamp01(toastTimer / 0.55f);
        }

        // ------------------------------------------------------------ 工具

        private InputField CreateInput(Transform parent, string name, string placeholder, bool multiline, Vector2 position, Vector2 size)
        {
            RectTransform box = UiFactory.Panel(parent, name, new Color(0.93f, 0.95f, 0.97f, 1f));
            UiFactory.Place(box, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);
            Image background = box.GetComponent<Image>();
            background.raycastTarget = true;

            Text text = UiFactory.Label(box, "文本", string.Empty, 26, new Color(0.07f, 0.09f, 0.11f, 1f),
                multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft);
            text.supportRichText = false;
            text.horizontalOverflow = multiline ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(22f, multiline ? 14f : 4f);
            textRect.offsetMax = new Vector2(-22f, multiline ? -12f : -4f);

            Text hint = UiFactory.Label(box, "占位", placeholder, 26, new Color(0.45f, 0.50f, 0.55f, 1f),
                multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft);
            RectTransform hintRect = hint.rectTransform;
            hintRect.anchorMin = Vector2.zero;
            hintRect.anchorMax = Vector2.one;
            hintRect.offsetMin = new Vector2(22f, multiline ? 14f : 4f);
            hintRect.offsetMax = new Vector2(-22f, multiline ? -12f : -4f);

            InputField field = box.gameObject.AddComponent<InputField>();
            field.targetGraphic = background;
            field.textComponent = text;
            field.placeholder = hint;
            field.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            field.characterLimit = multiline ? 200 : 40;
            field.customCaretColor = true;
            field.caretColor = new Color(0.10f, 0.20f, 0.30f, 1f);
            field.selectionColor = new Color(0.25f, 0.55f, 0.75f, 0.35f);

            return field;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Color color, UnityAction onClick)
        {
            RectTransform rect = UiFactory.Panel(parent, name, color);
            rect.GetComponent<Image>().raycastTarget = true;
            UiFactory.Place(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(onClick);

            Text text = UiFactory.Label(rect, "文字", label, 26, Color.white, TextAnchor.MiddleCenter);
            UiFactory.Stretch(text.rectTransform, 8f, 4f);

            return button;
        }

        private static Text AddText(Transform parent, string name, string content, int size, Color color,
            float x, float y, float width, float height, TextAnchor alignment, bool bold = false)
        {
            Text text = UiFactory.Label(parent, name, content, size, color, alignment);
            UiFactory.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(width, height));
            if (bold) text.fontStyle = FontStyle.Bold;
            return text;
        }
    }
}
