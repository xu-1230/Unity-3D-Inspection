using UnityEngine;

namespace VirtualInspection
{
    /// <summary>
    /// 基于 Unity 内置图元快速拼装厂区模型，单位统一为米。
    /// </summary>
    public static class Shape
    {
        /// <summary>小地图相机忽略该层，避免屋顶遮挡俯视图。</summary>
        public const int MinimapHiddenLayer = 30;

        public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material material, bool withCollider = true)
        {
            GameObject go = Create(PrimitiveType.Cube, parent, name, center, material);
            go.transform.localScale = size;
            if (!withCollider) RemoveCollider(go);
            return go;
        }

        public static GameObject Sphere(Transform parent, string name, Vector3 center, Vector3 size, Material material, bool withCollider = false)
        {
            GameObject go = Create(PrimitiveType.Sphere, parent, name, center, material);
            go.transform.localScale = size;
            if (!withCollider) RemoveCollider(go);
            return go;
        }

        /// <summary>圆柱体，axis 指定轴向（up / right / forward）。</summary>
        public static GameObject Cylinder(Transform parent, string name, Vector3 center, float radius, float height, Material material, Vector3 axis, bool withCollider = true)
        {
            GameObject go = Create(PrimitiveType.Cylinder, parent, name, center, material);
            Vector3 direction = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.up;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            if (!withCollider) RemoveCollider(go);
            return go;
        }

        /// <summary>在两点之间生成一段管线。</summary>
        public static GameObject Pipe(Transform parent, string name, Vector3 from, Vector3 to, float radius, Material material)
        {
            Vector3 delta = to - from;
            float length = Mathf.Max(0.001f, delta.magnitude);
            return Cylinder(parent, name, (from + to) * 0.5f, radius, length, material, delta, false);
        }

        public static void HideFromMinimap(GameObject go)
        {
            if (go != null) go.layer = MinimapHiddenLayer;
        }

        private static GameObject Create(PrimitiveType type, Transform parent, string name, Vector3 center, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;
            return go;
        }

        private static void RemoveCollider(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider == null) return;

            // 先禁用再销毁，避免同帧内的物理射线命中装饰物。
            collider.enabled = false;
            Object.Destroy(collider);
        }
    }
}
