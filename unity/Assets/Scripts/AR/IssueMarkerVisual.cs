using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualInspection.AR
{
    /// <summary>现场问题标记的三维表现：地面圆环 + 立柱 + 优先级配色 + 世界空间标题。</summary>
    public class IssueMarkerVisual : MonoBehaviour
    {
        private static readonly Dictionary<IssuePriority, Material> MarkerMaterials = new Dictionary<IssuePriority, Material>();

        private Camera targetCamera;
        private Transform labelTransform;

        public static IssueMarkerVisual Create(Transform parent, IssueMarkerRecord record, Camera camera, int index)
        {
            if (parent == null || record == null) return null;

            GameObject go = new GameObject("问题标记 #" + index + " " + record.title);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = record.position;
            go.transform.localRotation = record.rotation;

            IssueMarkerVisual visual = go.AddComponent<IssueMarkerVisual>();
            visual.Build(record, camera, index);
            return visual;
        }

        private void Build(IssueMarkerRecord record, Camera camera, int index)
        {
            targetCamera = camera;

            IssuePriority priority = IssuePriorityUtility.FromInt(record.priority);
            Material material = GetMaterial(priority);

            Shape.Cylinder(transform, "地面圆环", new Vector3(0f, 0.003f, 0f), 0.085f, 0.004f, material, Vector3.up, false);
            Shape.Cylinder(transform, "标记立柱", new Vector3(0f, 0.1f, 0f), 0.006f, 0.19f, material, Vector3.up, false);
            Shape.Sphere(transform, "标记头", new Vector3(0f, 0.215f, 0f), new Vector3(0.045f, 0.045f, 0.045f), material, false);

            BuildLabel(record, index, priority);
        }

        private void BuildLabel(IssueMarkerRecord record, int index, IssuePriority priority)
        {
            GameObject canvasGo = new GameObject("标记标签", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, 0.33f, 0f);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = targetCamera;
            canvas.sortingOrder = 20;

            labelTransform = canvasGo.transform;
            RectTransform rect = (RectTransform)canvasGo.transform;
            rect.sizeDelta = new Vector2(440f, 104f);
            rect.localScale = new Vector3(0.0011f, 0.0011f, 0.0011f);

            RectTransform background = UiFactory.Panel(labelTransform, "背景", new Color(0.03f, 0.05f, 0.07f, 0.82f));
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.offsetMin = Vector2.zero;
            background.offsetMax = Vector2.zero;

            RectTransform bar = UiFactory.Panel(labelTransform, "优先级色条", IssuePriorityUtility.ToColor(priority));
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(0f, 1f);
            bar.pivot = new Vector2(0f, 0.5f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(10f, 0f);

            string rich = string.Format(
                "<b>#{0}  {1}</b>\n<size=26><color=#{2}>{3}优先级</color></size>",
                index,
                string.IsNullOrEmpty(record.title) ? "未命名" : record.title,
                ColorUtility.ToHtmlStringRGB(IssuePriorityUtility.ToColor(priority)),
                IssuePriorityUtility.ToLabel(priority));

            Text text = UiFactory.Label(labelTransform, "文字", rich, 32, Color.white, TextAnchor.MiddleLeft);
            text.supportRichText = true;
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(24f, 8f);
            textRect.offsetMax = new Vector2(-10f, -8f);
            text.verticalOverflow = VerticalWrapMode.Truncate;
            UiFactory.AddOutline(text, new Color(0f, 0f, 0f, 0.7f));
        }

        private void LateUpdate()
        {
            if (labelTransform == null || targetCamera == null) return;

            Vector3 direction = labelTransform.position - targetCamera.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                labelTransform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        private static Material GetMaterial(IssuePriority priority)
        {
            Material cached;
            if (MarkerMaterials.TryGetValue(priority, out cached) && cached != null) return cached;

            Color color = IssuePriorityUtility.ToColor(priority);

            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");

            Material material = new Material(shader);
            material.name = "AR问题标记-" + priority;

            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.35f);

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.6f);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            MarkerMaterials[priority] = material;
            return material;
        }
    }
}
