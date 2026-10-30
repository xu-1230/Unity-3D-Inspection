using UnityEngine;
using UnityEngine.EventSystems;

namespace Unity3DInspection
{
    /// <summary>射线拾取入口：命中场景表面后放置标记并打开表单。</summary>
    public class SceneClickPicker : MonoBehaviour
    {
        public Camera targetCamera;
        public IssueMarkerManager markerManager;
        public IssueFormUI formUI;
        public float maxDistance = 100f;

        private void Update()
        {
            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            // 指针在界面元素上时不做射线检测，避免误放标记
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null)
            {
                return;
            }

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance))
            {
                return;
            }

            markerManager.Show(hit.point);
            formUI.Show(hit.point);
        }
    }
}
