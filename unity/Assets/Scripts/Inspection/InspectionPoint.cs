using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualInspection
{
    /// <summary>
    /// 现场巡检点：不再绘制悬浮标记，改为准星指向设备（类似 hover）时让设备自身发光。
    /// 未完成 = 青色，正常完成 = 绿色，异常完成 = 红色。
    /// </summary>
    public class InspectionPoint : MonoBehaviour
    {
        public InspectionDefinition Definition { get; private set; }
        public float InteractRadius = 3.2f;

        /// <summary>收集设备 Renderer 的搜索半径。</summary>
        private const float GlowRadius = 4f;

        /// <summary>设备包围盒到点位的最大距离，避免把相邻设备一起点亮。</summary>
        private const float GlowAttachDistance = 2.2f;

        /// <summary>准星射线的最远距离。</summary>
        private const float GlowRayDistance = 30f;

        private const float BaseEmission = 0.55f;
        private const float PulseAmplitude = 0.28f;

        private static readonly Color PendingColor = new Color(0.30f, 0.82f, 0.95f, 1f);

        private Camera mainCamera;
        private Transform labelTransform;
        private Text labelText;

        private readonly List<Renderer> glowRenderers = new List<Renderer>();
        private readonly Dictionary<Renderer, Material[]> baseMaterials = new Dictionary<Renderer, Material[]>();
        private readonly Dictionary<Renderer, Material[]> glowMaterials = new Dictionary<Renderer, Material[]>();
        private readonly HashSet<Renderer> glowLookup = new HashSet<Renderer>();

        private bool highlighted;
        private bool hovered;
        private bool glowing;
        private bool glowReady;

        public void Initialize(InspectionDefinition definition, MaterialLibrary mats, Camera camera)
        {
            Definition = definition;
            mainCamera = camera;

            transform.position = definition.Position;
            BuildLabel();
            RefreshLabel();
            // 设备碰撞体与本次 Awake 同帧创建，物理查询要等一次同步后才能命中，放到首帧构建。
        }

        public void SetHighlighted(bool value)
        {
            highlighted = value;
        }

        /// <summary>准星命中该点位的设备时返回 true。</summary>
        public bool IsHovered { get { return hovered; } }

        // ------------------------------------------------------------ 发光目标

        /// <summary>
        /// 在点位附近收集设备自身的 Renderer。排除地面、墙体这类大尺度物体，
        /// 以及点位自身的标签，避免整片地面被点亮。
        /// </summary>
        private void BuildGlowTargets()
        {
            Physics.SyncTransforms();

            Vector3 center = transform.position + Vector3.up * 0.9f;
            Collider[] colliders = Physics.OverlapSphere(center, GlowRadius, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null) continue;
                if (collider.transform.IsChildOf(transform)) continue;

                Renderer renderer = collider.GetComponent<Renderer>();
                if (renderer == null) renderer = collider.GetComponentInParent<Renderer>();
                if (renderer == null) continue;
                if (glowLookup.Contains(renderer)) continue;

                Vector3 size = renderer.bounds.size;
                if (size.x > 8f || size.y > 8f || size.z > 8f) continue;

                // 用包围盒最近点判断归属：点位通常在设备前方 2~3 米处，而不是设备中心。
                Vector3 closest = renderer.bounds.ClosestPoint(transform.position);
                Vector3 delta = closest - transform.position;
                delta.y = 0f;
                if (delta.magnitude > GlowAttachDistance) continue;

                glowRenderers.Add(renderer);
                glowLookup.Add(renderer);
            }

            // 个别设备的图元没有碰撞体（例如传送带），退化为按包围盒就近绑定。
            if (glowRenderers.Count == 0)
            {
                BindNearestRenderers(center);
            }

            Debug.Log("[巡检点] " + Definition.Id + " 绑定设备部件 " + glowRenderers.Count + " 个");
        }

        private void BindNearestRenderers(Vector3 center)
        {
            Renderer[] all = FindObjectsOfType<Renderer>();
            List<Renderer> candidates = new List<Renderer>();

            for (int i = 0; i < all.Length; i++)
            {
                Renderer renderer = all[i];
                if (renderer == null) continue;
                if (renderer.transform.IsChildOf(transform)) continue;

                Vector3 size = renderer.bounds.size;
                if (size.x > 8f || size.y > 8f || size.z > 8f) continue;

                Vector3 closest = renderer.bounds.ClosestPoint(center);
                if ((closest - center).magnitude > 2.6f) continue;

                candidates.Add(renderer);
            }

            candidates.Sort(delegate (Renderer a, Renderer b)
            {
                float da = (a.bounds.ClosestPoint(center) - center).sqrMagnitude;
                float db = (b.bounds.ClosestPoint(center) - center).sqrMagnitude;
                return da.CompareTo(db);
            });

            int take = Mathf.Min(3, candidates.Count);
            for (int i = 0; i < take; i++)
            {
                glowRenderers.Add(candidates[i]);
                glowLookup.Add(candidates[i]);
            }
        }

        private Color CurrentGlowColor()
        {
            return PendingColor;
        }

        private void SetGlow(bool on)
        {
            if (on == glowing) return;
            glowing = on;

            for (int i = 0; i < glowRenderers.Count; i++)
            {
                Renderer renderer = glowRenderers[i];
                if (renderer == null) continue;

                if (on)
                {
                    if (!baseMaterials.ContainsKey(renderer))
                    {
                        baseMaterials[renderer] = renderer.sharedMaterials;
                        glowMaterials[renderer] = CreateGlowInstance(renderer.sharedMaterials);
                    }

                    renderer.sharedMaterials = glowMaterials[renderer];
                }
                else if (baseMaterials.ContainsKey(renderer))
                {
                    renderer.sharedMaterials = baseMaterials[renderer];
                }
            }
        }

        private static Material[] CreateGlowInstance(Material[] source)
        {
            Material[] instances = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                Material original = source[i];
                if (original == null)
                {
                    instances[i] = null;
                    continue;
                }

                Material instance = new Material(original);
                instance.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                instances[i] = instance;
            }

            return instances;
        }

        private void ApplyGlowColor(float intensity)
        {
            Color color = CurrentGlowColor() * intensity;
            for (int i = 0; i < glowRenderers.Count; i++)
            {
                Renderer renderer = glowRenderers[i];
                if (renderer == null) continue;

                Material[] materials;
                if (!glowMaterials.TryGetValue(renderer, out materials)) continue;

                for (int m = 0; m < materials.Length; m++)
                {
                    Material material = materials[m];
                    if (material == null) continue;

                    if (!material.HasProperty("_EmissionColor")) continue;
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color);
                }
            }
        }

        // ------------------------------------------------------------ 每帧

        private void Update()
        {
            if (!glowReady)
            {
                BuildGlowTargets();
                glowReady = true;
            }

            UpdateHover();
            UpdateGlow();
            UpdateLabelFacing();
        }

        private void UpdateHover()
        {
            if (mainCamera == null || glowLookup.Count == 0)
            {
                hovered = false;
                return;
            }

            Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, GlowRayDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                Renderer target = hit.collider != null ? hit.collider.GetComponentInParent<Renderer>() : null;
                if (target != null && glowLookup.Contains(target))
                {
                    hovered = true;
                    return;
                }
            }

            // 部分设备图元没有碰撞体，退化用「准星是否指向其包围盒」判断。
            float best = float.MaxValue;
            for (int i = 0; i < glowRenderers.Count; i++)
            {
                Renderer renderer = glowRenderers[i];
                if (renderer == null) continue;

                Vector3 toCenter = renderer.bounds.center - ray.origin;
                float distance = Vector3.Dot(toCenter, ray.direction);
                if (distance < 0f || distance > GlowRayDistance) continue;

                float offset = (toCenter - ray.direction * distance).magnitude;
                if (offset < best) best = offset;
            }

            hovered = best <= 1.6f;
        }

        private void UpdateGlow()
        {
            SetGlow(hovered);

            if (!glowing) return;

            float pulse = Mathf.Sin(Time.time * 3.2f) * PulseAmplitude;
            float intensity = BaseEmission + pulse + (highlighted ? 0.25f : 0f);
            ApplyGlowColor(Mathf.Max(0.15f, intensity));
        }

        private void UpdateLabelFacing()
        {
            if (labelTransform == null || mainCamera == null) return;

            Vector3 direction = labelTransform.position - mainCamera.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                labelTransform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        // ------------------------------------------------------------ 标签

        private void BuildLabel()
        {
            GameObject canvasGo = new GameObject("标签", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, 2.1f, 0f);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = mainCamera;
            canvas.sortingOrder = 10;

            labelTransform = canvasGo.transform;
            RectTransform rect = (RectTransform)canvasGo.transform;
            rect.sizeDelta = new Vector2(360f, 52f);
            rect.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            RectTransform background = UiFactory.Panel(labelTransform, "背景", new Color(0.02f, 0.05f, 0.07f, 0.45f));
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.offsetMin = Vector2.zero;
            background.offsetMax = Vector2.zero;

            Text text = UiFactory.Label(labelTransform, "名称", Definition.Name, 22, new Color(1f, 1f, 1f, 0.85f), TextAnchor.MiddleCenter);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6f, 0f);
            textRect.offsetMax = new Vector2(-6f, 0f);
            UiFactory.AddOutline(text, new Color(0f, 0f, 0f, 0.65f));

            labelText = text;
        }

        private void RefreshLabel()
        {
            if (labelText == null || Definition == null) return;

            // 世界中只显示设备编号，名称与区域收进 F 参数面板。
            labelText.text = Definition.Id;
            labelText.color = new Color(1f, 1f, 1f, 0.85f);
        }
    }
}
