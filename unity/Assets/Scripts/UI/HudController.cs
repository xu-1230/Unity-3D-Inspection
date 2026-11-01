using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualInspection
{
    /// <summary>
    /// 现场 HUD：只保留准星、设备参数面板与厂区平面小地图。
    /// </summary>
    public class HudController : MonoBehaviour
    {
        private static readonly Color AccentColor = new Color(0.22f, 0.80f, 0.92f, 1f);
        private static readonly Color PanelColor = new Color(0.035f, 0.055f, 0.075f, 0.85f);
        private static readonly Color DimColor = new Color(0.60f, 0.68f, 0.74f, 1f);
        private static readonly Color BadColor = new Color(1f, 0.42f, 0.34f, 1f);
        private static readonly Color WarnColor = new Color(1f, 0.76f, 0.28f, 1f);

        private Transform canvasRoot;
        private List<InspectionPoint> points;

        private Image[] mapBlips;

        private RectTransform detailPanel;
        private Text detailTitle;
        private Text detailZone;
        private Text detailStatus;
        private Image detailStatusBackground;
        private Text[] paramLabels;
        private Text[] paramValues;
        private Text detailNote;
        private Text detailHint;
        private Text detailFooter;

        private RectTransform mapRect;
        private RectTransform playerBlip;
        private Vector2 lastMapAnchor = new Vector2(-99f, -99f);
        private float lastMapAngle = -999f;

        public void Build(List<InspectionPoint> inspectionPoints, RenderTexture minimapTexture)
        {
            points = inspectionPoints;

            BuildCanvas();
            BuildCrosshair();
            BuildDetailPanel();
            BuildMinimap(minimapTexture);

            detailPanel.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------ 构建

        private void BuildCanvas()
        {
            GameObject canvasGo = new GameObject("HUD Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasRoot = canvasGo.transform;
        }

        private void BuildCrosshair()
        {
            RectTransform root = UiFactory.Panel(canvasRoot, "准星", new Color(0f, 0f, 0f, 0f));
            UiFactory.Place(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));

            Image horizontal = UiFactory.Panel(root, "横线", new Color(1f, 1f, 1f, 0.35f)).GetComponent<Image>();
            UiFactory.Place(horizontal.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 2f));

            Image vertical = UiFactory.Panel(root, "竖线", new Color(1f, 1f, 1f, 0.35f)).GetComponent<Image>();
            UiFactory.Place(vertical.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 26f));

            Image dot = UiFactory.Panel(root, "中心点", new Color(0.85f, 1f, 1f, 0.95f)).GetComponent<Image>();
            UiFactory.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5f, 5f));
        }

        private void BuildDetailPanel()
        {
            detailPanel = UiFactory.Panel(canvasRoot, "参数面板", new Color(0.02f, 0.032f, 0.045f, 0.95f));
            UiFactory.Place(detailPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(880f, 486f));

            RectTransform accent = UiFactory.Panel(detailPanel, "顶部强调条", AccentColor);
            UiFactory.Place(accent, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(880f, 3f));

            detailTitle = AddTextLeft(detailPanel, "设备名称", "", 30, Color.white, 36f, -24f, 600f, 46f, true);
            detailZone = AddTextLeft(detailPanel, "区域", "", 16, DimColor, 36f, -78f, 520f, 26f);

            detailStatusBackground = UiFactory.Panel(detailPanel, "状态", AccentColor).GetComponent<Image>();
            UiFactory.Place(detailStatusBackground.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -34f), new Vector2(156f, 38f));
            detailStatus = AddTextStretched(detailStatusBackground.rectTransform, "状态文字", "", 18, Color.white, TextAnchor.MiddleCenter, 4f, 2f);
            detailStatus.fontStyle = FontStyle.Bold;

            AddDivider(detailPanel, -118f);

            paramLabels = new Text[3];
            paramValues = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                float y = -140f - i * 56f;
                paramLabels[i] = AddTextLeft(detailPanel, "参数名", "", 19, DimColor, 36f, y, 440f, 32f);
                paramValues[i] = AddTextRight(detailPanel, "参数值", "", 21, Color.white, -36f, y, 340f, 32f, true);
            }

            AddTextLeft(detailPanel, "要点标题", "巡检要点", 14, DimColor, 36f, -304f, 300f, 24f);

            Text note = UiFactory.Label(detailPanel, "要点内容", "", 17, new Color(0.80f, 0.86f, 0.90f, 1f), TextAnchor.UpperLeft);
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            note.lineSpacing = 1.2f;
            RectTransform noteRect = note.rectTransform;
            noteRect.anchorMin = new Vector2(0f, 1f);
            noteRect.anchorMax = new Vector2(0f, 1f);
            noteRect.pivot = new Vector2(0f, 1f);
            noteRect.anchoredPosition = new Vector2(36f, -330f);
            noteRect.sizeDelta = new Vector2(808f, 66f);
            detailNote = note;

            AddDivider(detailPanel, -404f);

            detailHint = AddTextLeft(detailPanel, "操作", "", 18, AccentColor, 36f, -424f, 640f, 30f, true);
            detailFooter = AddTextRight(detailPanel, "时间", "", 15, DimColor, -36f, -424f, 300f, 30f);
        }

        private void BuildMinimap(RenderTexture minimapTexture)
        {
            RectTransform panel = UiFactory.Panel(canvasRoot, "小地图", PanelColor);
            UiFactory.Place(panel, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-26f, 26f), new Vector2(300f, 264f));

            AddTextLeft(panel, "地图标题", "厂区平面", 16, Color.white, 18f, -12f, 220f, 24f, true);

            RectTransform frame = UiFactory.Panel(panel, "地图边框", new Color(1f, 1f, 1f, 0.08f));
            UiFactory.Place(frame, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(276f, 196f));

            GameObject mapGo = new GameObject("地图画面", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            mapGo.transform.SetParent(frame, false);
            RawImage raw = mapGo.GetComponent<RawImage>();
            raw.texture = minimapTexture;
            raw.raycastTarget = false;

            mapRect = (RectTransform)mapGo.transform;
            UiFactory.Stretch(mapRect, 2f);
            mapGo.AddComponent<RectMask2D>();

            int count = points != null ? points.Count : 0;
            mapBlips = new Image[count];
            for (int i = 0; i < count; i++)
            {
                Image blip = UiFactory.Panel(mapRect, "点位", AccentColor).GetComponent<Image>();
                Vector2 anchor = SiteLayout.Normalize(points[i].Definition.Position);
                UiFactory.Place(blip.rectTransform, anchor, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7f, 7f));
                mapBlips[i] = blip;
            }

            playerBlip = UiFactory.Panel(mapRect, "巡检员", new Color(1f, 1f, 1f, 0f));
            playerBlip.sizeDelta = new Vector2(1f, 1f);
            playerBlip.pivot = new Vector2(0.5f, 0.5f);

            Image direction = UiFactory.Panel(playerBlip, "朝向", new Color(0.35f, 0.95f, 1f, 1f)).GetComponent<Image>();
            UiFactory.Place(direction.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0f, -1f), new Vector2(3f, 12f));

            Image dot = UiFactory.Panel(playerBlip, "位置", Color.white).GetComponent<Image>();
            UiFactory.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 8f));
        }

        // ------------------------------------------------------------ 对外接口

        /// <summary>打开设备参数面板（只读查看）。</summary>
        public void ShowDetail(InspectionPoint point)
        {
            if (point == null || detailPanel == null) return;

            detailPanel.gameObject.SetActive(true);
            detailTitle.text = point.Definition.Name;
            detailZone.text = "所属区域  " + point.Definition.Zone + "     设备编号  " + point.Definition.Id;

            bool abnormal = point.Definition.HasAbnormal;
            detailStatus.text = abnormal ? "存在异常" : "运行正常";
            detailStatusBackground.color = abnormal
                ? new Color(0.64f, 0.16f, 0.14f, 0.95f)
                : new Color(0.12f, 0.44f, 0.25f, 0.95f);

            InspectionReading[] readings = point.Definition.Readings;
            for (int i = 0; i < paramLabels.Length; i++)
            {
                bool hasData = readings != null && i < readings.Length;
                paramLabels[i].text = hasData ? readings[i].Label : string.Empty;
                paramValues[i].text = hasData ? readings[i].Value : string.Empty;

                bool over = hasData && readings[i].Abnormal;
                paramLabels[i].color = over ? BadColor : DimColor;
                paramValues[i].color = over ? BadColor : Color.white;
            }

            detailNote.text = point.Definition.Note;
            detailHint.text = abnormal ? "该设备存在异常读数    ·    按 [ESC] 返回现场" : "按 [ESC] 返回现场";
            detailHint.color = abnormal ? WarnColor : AccentColor;
            detailFooter.text = System.DateTime.Now.ToString("HH:mm:ss");
        }

        public void HideDetail()
        {
            if (detailPanel != null) detailPanel.gameObject.SetActive(false);
        }

        public void UpdateMinimap(Vector3 worldPosition, float yaw)
        {
            if (playerBlip == null) return;

            Vector2 anchor = SiteLayout.Normalize(worldPosition);
            if ((anchor - lastMapAnchor).sqrMagnitude > 0.0000005f)
            {
                lastMapAnchor = anchor;
                playerBlip.anchorMin = anchor;
                playerBlip.anchorMax = anchor;
            }

            float angle = -yaw;
            if (!Mathf.Approximately(angle, lastMapAngle))
            {
                lastMapAngle = angle;
                playerBlip.localEulerAngles = new Vector3(0f, 0f, angle);
            }
        }

        // ------------------------------------------------------------ 内部

        private void AddDivider(Transform parent, float y)
        {
            RectTransform line = UiFactory.Panel(parent, "分隔线", new Color(1f, 1f, 1f, 0.10f));
            UiFactory.Place(line, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, y), new Vector2(808f, 1f));
        }

        private static Text AddTextLeft(Transform parent, string name, string content, int size, Color color,
            float x, float y, float width, float height, bool bold = false)
        {
            Text text = UiFactory.Label(parent, name, content, size, color, TextAnchor.UpperLeft);
            UiFactory.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(width, height));
            if (bold) text.fontStyle = FontStyle.Bold;
            return text;
        }

        private static Text AddTextRight(Transform parent, string name, string content, int size, Color color,
            float x, float y, float width, float height, bool bold = false)
        {
            Text text = UiFactory.Label(parent, name, content, size, color, TextAnchor.UpperRight);
            UiFactory.Place(text.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(x, y), new Vector2(width, height));
            if (bold) text.fontStyle = FontStyle.Bold;
            return text;
        }

        private static Text AddTextStretched(Transform parent, string name, string content, int size, Color color,
            TextAnchor alignment, float horizontalPadding, float verticalPadding)
        {
            Text text = UiFactory.Label(parent, name, content, size, color, alignment);
            UiFactory.Stretch(text.rectTransform, horizontalPadding, verticalPadding);
            return text;
        }
    }
}
