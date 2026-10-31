using System.Collections.Generic;
using UnityEngine;

namespace VirtualInspection
{
    /// <summary>
    /// 设备查看：靠近点位按 F 打开参数面板，按 ESC 或再次按 F 关闭。
    /// 不再统计巡检进度、不记录完成状态、没有结算弹窗。
    /// </summary>
    public class InspectionSystem : MonoBehaviour
    {
        private List<InspectionPoint> points = new List<InspectionPoint>();
        private FirstPersonPlayer player;
        private HudController hud;

        private InspectionPoint nearest;
        private InspectionPoint highlighted;
        private InspectionPoint detailPoint;
        private bool detailOpen;

        public void Setup(List<InspectionPoint> inspectionPoints, FirstPersonPlayer firstPersonPlayer, HudController hudController)
        {
            points = inspectionPoints;
            player = firstPersonPlayer;
            hud = hudController;
        }

        private void Update()
        {
            if (player == null || hud == null) return;

            // 上报表单打开时让出输入，避免 F / Esc 两套交互互相抢占。
            if (VirtualInspection.Reporting.IssueReportSystem.IsFormOpen) return;

            if (detailOpen)
            {
                if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return))
                {
                    CloseDetail();
                }
                return;
            }

            hud.UpdateMinimap(player.transform.position, player.Yaw);

            nearest = FindNearest();
            UpdateHighlight();

            if (nearest != null && Input.GetKeyDown(KeyCode.F))
            {
                OpenDetail(nearest);
            }
        }

        private InspectionPoint FindNearest()
        {
            InspectionPoint best = null;
            float bestDistance = float.MaxValue;
            Vector3 position = player.transform.position;

            for (int i = 0; i < points.Count; i++)
            {
                InspectionPoint point = points[i];
                if (point == null) continue;

                Vector3 delta = point.transform.position - position;
                delta.y = 0f;
                float distance = delta.magnitude;
                if (distance <= point.InteractRadius && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = point;
                }
            }

            return best;
        }

        private void UpdateHighlight()
        {
            if (highlighted == nearest) return;

            if (highlighted != null) highlighted.SetHighlighted(false);
            highlighted = nearest;
            if (highlighted != null) highlighted.SetHighlighted(true);
        }

        private void OpenDetail(InspectionPoint point)
        {
            detailPoint = point;
            detailOpen = true;

            if (highlighted != null)
            {
                highlighted.SetHighlighted(false);
                highlighted = null;
                nearest = null;
            }

            player.SetControls(false);
            hud.ShowDetail(point);
        }

        private void CloseDetail()
        {
            detailOpen = false;
            detailPoint = null;
            hud.HideDetail();
            player.SetControls(true);
        }
    }
}
