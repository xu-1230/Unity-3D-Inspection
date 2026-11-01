using System;
using UnityEngine;

namespace VirtualInspection.AR
{
    /// <summary>问题优先级。</summary>
    public enum IssuePriority
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    /// <summary>
    /// 一条问题标记。position / rotation 均保存在“现场基准图”的局部坐标系中，
    /// 因此下次启动重新识别到同一张基准图后，可以还原到同一个真实位置。
    /// </summary>
    [Serializable]
    public class IssueMarkerRecord
    {
        public string id;
        public string title;
        public string description;
        public int priority;
        public string createdAt;
        public Vector3 position;
        public Quaternion rotation;
    }

    [Serializable]
    public class IssueMarkerCollection
    {
        public int version = 1;
        public string siteName = "SiteAnchor";
        public System.Collections.Generic.List<IssueMarkerRecord> markers =
            new System.Collections.Generic.List<IssueMarkerRecord>();
    }

    public static class IssuePriorityUtility
    {
        public static string ToLabel(IssuePriority priority)
        {
            switch (priority)
            {
                case IssuePriority.High: return "高";
                case IssuePriority.Low: return "低";
                default: return "中";
            }
        }

        public static string ToCode(IssuePriority priority)
        {
            switch (priority)
            {
                case IssuePriority.High: return "high";
                case IssuePriority.Low: return "low";
                default: return "medium";
            }
        }

        public static Color ToColor(IssuePriority priority)
        {
            switch (priority)
            {
                case IssuePriority.High: return new Color(1f, 0.36f, 0.30f, 1f);
                case IssuePriority.Low: return new Color(0.32f, 0.84f, 0.60f, 1f);
                default: return new Color(1f, 0.72f, 0.25f, 1f);
            }
        }

        public static IssuePriority FromInt(int value)
        {
            if (value <= 0) return IssuePriority.Low;
            if (value >= 2) return IssuePriority.High;
            return IssuePriority.Medium;
        }
    }
}
