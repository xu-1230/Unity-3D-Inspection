using UnityEngine;
using UnityEngine.UI;

namespace VirtualInspection
{
    /// <summary>uGUI 轻量封装，负责字体、面板与文本的快速创建。</summary>
    public static class UiFactory
    {
        private static Font cachedFont;

        /// <summary>优先使用系统中文字体，避免中文显示为方块。</summary>
        public static Font UiFont
        {
            get
            {
                if (cachedFont != null) return cachedFont;

                string[] candidates =
                {
                    "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑", "PingFang SC",
                    "Hiragino Sans GB", "Noto Sans CJK SC", "Source Han Sans SC", "SimHei", "Segoe UI"
                };

                try { cachedFont = Font.CreateDynamicFontFromOSFont(candidates, 28); }
                catch (System.Exception) { cachedFont = null; }

                if (cachedFont == null)
                {
                    try { cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                    catch (System.Exception) { cachedFont = null; }
                }

                if (cachedFont == null)
                {
                    try { cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                    catch (System.Exception) { cachedFont = null; }
                }

                return cachedFont;
            }
        }

        public static RectTransform Panel(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return (RectTransform)go.transform;
        }

        public static Text Label(Transform parent, string name, string content, int fontSize, Color color, TextAnchor alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            Text text = go.GetComponent<Text>();
            text.font = UiFont;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return text;
        }

        /// <summary>按锚点定位矩形（anchorMin = anchorMax = anchor）。</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static void Stretch(RectTransform rect, float padding)
        {
            Stretch(rect, padding, padding);
        }

        public static void Stretch(RectTransform rect, float horizontal, float vertical)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontal, vertical);
            rect.offsetMax = new Vector2(-horizontal, -vertical);
        }

        public static void AddOutline(Graphic graphic, Color color)
        {
            if (graphic == null) return;
            Outline outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(1.1f, -1.1f);
            outline.useGraphicAlpha = true;
        }
    }
}
