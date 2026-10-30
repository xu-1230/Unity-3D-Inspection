using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Unity3DInspection
{
    /// <summary>运行时构建的问题上报表单，挂在场景中的 Canvas 上。</summary>
    public class IssueFormUI : MonoBehaviour
    {
        public IssueMarkerManager markerManager;
        public IssueApiClient apiClient;

        private GameObject _panel;
        private InputField _titleInput;
        private InputField _descriptionInput;
        private Text _coordText;
        private Text _statusText;
        private Button _submitButton;
        private Button _cancelButton;
        private readonly Dictionary<string, Button> _priorityButtons = new Dictionary<string, Button>();

        private string _priority = IssuePriority.Medium;
        private Vector3 _pendingPosition;
        private bool _submitting;
        private Font _font;

        private static readonly Color ErrorColor = new Color(1f, 0.45f, 0.42f);
        private static readonly Color SuccessColor = new Color(0.55f, 0.88f, 0.62f);
        private static readonly Color NeutralColor = new Color(0.82f, 0.84f, 0.86f);

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null)
            {
                _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            BuildUi();
            Hide();
        }

        /// <summary>打开表单并显示命中点坐标。</summary>
        public void Show(Vector3 worldPosition)
        {
            _pendingPosition = worldPosition;
            _coordText.text = string.Format("坐标：({0:F2}, {1:F2}, {2:F2})", worldPosition.x, worldPosition.y, worldPosition.z);
            SetStatus(string.Empty, NeutralColor);
            _panel.SetActive(true);
        }

        private void Hide()
        {
            if (_panel != null)
            {
                _panel.SetActive(false);
            }
        }

        private void OnCancelClicked()
        {
            if (_submitting)
            {
                return;
            }
            Hide();
            if (markerManager != null)
            {
                markerManager.Clear();
            }
        }

        private void OnSubmitClicked()
        {
            if (_submitting)
            {
                return;
            }

            string title = _titleInput.text != null ? _titleInput.text.Trim() : string.Empty;
            if (title.Length == 0)
            {
                SetStatus("标题不能为空", ErrorColor);
                return;
            }

            IssuePayload payload = new IssuePayload
            {
                title = title,
                description = _descriptionInput.text ?? string.Empty,
                priority = _priority,
                location = new IssueLocationDto
                {
                    x = _pendingPosition.x,
                    y = _pendingPosition.y,
                    z = _pendingPosition.z,
                },
            };

            StartCoroutine(Submit(payload));
        }

        private IEnumerator Submit(IssuePayload payload)
        {
            _submitting = true;
            _submitButton.interactable = false;
            _cancelButton.interactable = false;
            SetStatus("提交中…", NeutralColor);

            bool finished = false;
            string failure = null;

            yield return apiClient.CreateIssue(
                payload,
                _ => finished = true,
                message =>
                {
                    failure = message;
                    finished = true;
                });

            _submitting = false;
            _submitButton.interactable = true;
            _cancelButton.interactable = true;

            if (finished && failure == null)
            {
                SetStatus("提交成功", SuccessColor);
                Hide();
                _titleInput.text = string.Empty;
                _descriptionInput.text = string.Empty;
                if (markerManager != null)
                {
                    markerManager.Clear();
                }
            }
            else
            {
                SetStatus("提交失败：" + (failure ?? "未知错误"), ErrorColor);
            }
        }

        private void SetStatus(string message, Color color)
        {
            _statusText.text = message;
            _statusText.color = color;
        }

        private void SetPriority(string priority)
        {
            _priority = priority;
            foreach (KeyValuePair<string, Button> pair in _priorityButtons)
            {
                bool selected = pair.Key == priority;
                ColorBlock colors = pair.Value.colors;
                colors.normalColor = selected ? new Color(0.26f, 0.52f, 0.96f) : new Color(0.28f, 0.30f, 0.33f);
                colors.selectedColor = colors.normalColor;
                pair.Value.colors = colors;
            }
        }

        private void BuildUi()
        {
            RectTransform canvasRect = GetComponent<RectTransform>();
            if (canvasRect == null)
            {
                canvasRect = gameObject.AddComponent<RectTransform>();
            }

            _panel = new GameObject("IssuePanel", typeof(RectTransform));
            _panel.transform.SetParent(transform, false);

            RectTransform panelRect = _panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(420f, 0f);
            panelRect.anchoredPosition = Vector2.zero;

            Image background = _panel.AddComponent<Image>();
            background.color = new Color(0.11f, 0.12f, 0.14f, 0.95f);

            VerticalLayoutGroup layout = _panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 18, 18);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = _panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateLabel("上报问题", 20, Color.white, 30f);
            CreateLabel("标题（必填）", 13, NeutralColor, 20f);
            _titleInput = CreateInput("请输入问题标题", 34f, false);
            CreateLabel("描述（可选）", 13, NeutralColor, 20f);
            _descriptionInput = CreateInput("补充说明", 64f, true);
            CreateLabel("优先级", 13, NeutralColor, 20f);
            CreatePriorityRow();

            _coordText = CreateLabel("坐标：-", 13, NeutralColor, 20f);
            _statusText = CreateLabel(string.Empty, 13, NeutralColor, 20f);

            CreateButtonRow();

            SetPriority(IssuePriority.Medium);
        }

        private Text CreateLabel(string content, int fontSize, Color color, float height)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(_panel.transform, false);

            Text text = go.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.color = color;
            text.text = content;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            LayoutElement element = go.AddComponent<LayoutElement>();
            element.preferredHeight = height;

            return text;
        }

        private InputField CreateInput(string placeholder, float height, bool multiline)
        {
            GameObject go = new GameObject("Input", typeof(RectTransform));
            go.transform.SetParent(_panel.transform, false);

            Image background = go.AddComponent<Image>();
            background.color = new Color(0.18f, 0.19f, 0.22f, 1f);

            InputField input = go.AddComponent<InputField>();
            input.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;

            Text text = CreateStretchedText(go.transform, Color.white, multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft);
            text.fontSize = 15;
            input.textComponent = text;

            Text placeholderText = CreateStretchedText(go.transform, new Color(0.6f, 0.62f, 0.65f, 1f), TextAnchor.MiddleLeft);
            placeholderText.fontSize = 15;
            placeholderText.text = placeholder;
            input.placeholder = placeholderText;

            LayoutElement element = go.AddComponent<LayoutElement>();
            element.preferredHeight = height;

            return input;
        }

        private Text CreateStretchedText(Transform parent, Color color, TextAnchor anchor)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8f, 4f);
            rect.offsetMax = new Vector2(-8f, -4f);

            Text text = go.AddComponent<Text>();
            text.font = _font;
            text.color = color;
            text.alignment = anchor;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private Button CreateButton(Transform parent, string label, float preferredWidth, UnityAction onClick)
        {
            GameObject go = new GameObject("Button_" + label, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            Image background = go.AddComponent<Image>();
            background.color = new Color(0.28f, 0.30f, 0.33f, 1f);

            Button button = go.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(onClick);

            Text text = CreateStretchedText(go.transform, Color.white, TextAnchor.MiddleCenter);
            text.fontSize = 15;
            text.text = label;

            LayoutElement element = go.AddComponent<LayoutElement>();
            element.preferredHeight = 32f;
            if (preferredWidth > 0f)
            {
                element.preferredWidth = preferredWidth;
            }
            return button;
        }

        private void CreatePriorityRow()
        {
            GameObject row = new GameObject("PriorityRow", typeof(RectTransform));
            row.transform.SetParent(_panel.transform, false);

            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            LayoutElement element = row.AddComponent<LayoutElement>();
            element.preferredHeight = 32f;

            AddPriorityButton(row.transform, "低", IssuePriority.Low);
            AddPriorityButton(row.transform, "中", IssuePriority.Medium);
            AddPriorityButton(row.transform, "高", IssuePriority.High);
        }

        private void AddPriorityButton(Transform parent, string label, string value)
        {
            Button button = CreateButton(parent, label, 0f, () => SetPriority(value));
            _priorityButtons[value] = button;
        }

        private void CreateButtonRow()
        {
            GameObject row = new GameObject("ActionRow", typeof(RectTransform));
            row.transform.SetParent(_panel.transform, false);

            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            LayoutElement element = row.AddComponent<LayoutElement>();
            element.preferredHeight = 34f;

            _submitButton = CreateButton(row.transform, "提交", 0f, OnSubmitClicked);
            _cancelButton = CreateButton(row.transform, "取消", 0f, OnCancelClicked);
        }
    }
}
