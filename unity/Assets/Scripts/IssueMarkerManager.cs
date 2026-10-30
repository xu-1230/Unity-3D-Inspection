using UnityEngine;

namespace Unity3DInspection
{
    /// <summary>维护场景中唯一的待提交标记。</summary>
    public class IssueMarkerManager : MonoBehaviour
    {
        public Color markerColor = new Color(0.95f, 0.26f, 0.21f, 1f);
        public float markerDiameter = 0.25f;

        private GameObject _current;
        private Material _material;

        public bool HasMarker => _current != null;

        /// <summary>在命中点创建标记，旧标记先销毁。</summary>
        public void Show(Vector3 worldPosition)
        {
            Clear();

            _current = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _current.name = "PendingIssueMarker";
            _current.transform.position = worldPosition;
            _current.transform.localScale = Vector3.one * markerDiameter;

            Collider collider = _current.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = _current.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = GetMarkerMaterial();
            }
        }

        /// <summary>销毁标记；取消与提交成功后都调用。</summary>
        public void Clear()
        {
            if (_current != null)
            {
                Destroy(_current);
                _current = null;
            }
        }

        private Material GetMarkerMaterial()
        {
            if (_material != null)
            {
                return _material;
            }

            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            _material = new Material(shader) { color = markerColor };
            return _material;
        }
    }
}
