using System;

namespace VirtualInspection.Reporting
{
    /// <summary>与后端契约对应的坐标结构，Unity 坐标原样上报，不做换算。</summary>
    [Serializable]
    public class IssueLocationDto
    {
        public float x;
        public float y;
        public float z;
    }

    /// <summary>创建问题的请求体，字段名与后端契约一致。</summary>
    [Serializable]
    public class IssuePayload
    {
        public string title;
        public string description;
        public string priority;

        /// <summary>设备编号或名称，取点击到的场景对象名；后端按可选字段处理。</summary>
        public string deviceId;

        /// <summary>区域名称，留空表示未标注。</summary>
        public string area;

        public IssueLocationDto location;
    }

    /// <summary>创建成功后的响应，只取展示用字段。</summary>
    [Serializable]
    public class IssueResponse
    {
        public string id;
        public string title;
        public string status;
        public string priority;
    }

    /// <summary>后端返回的完整问题对象，用于启动时还原标记与查看详情。</summary>
    [Serializable]
    public class IssueItem
    {
        public string id;
        public string title;
        public string description;
        public string priority;
        public string status;
        public string deviceId;
        public string area;
        public IssueLocationDto location;
        public string createdAt;
        public string updatedAt;
    }

    /// <summary>JsonUtility 不支持顶层数组，列表响应包一层。</summary>
    [Serializable]
    public class IssueListWrapper
    {
        public IssueItem[] items;
    }

    /// <summary>后端统一错误响应。</summary>
    [Serializable]
    public class ApiErrorBody
    {
        public ApiErrorDetail error;
    }

    [Serializable]
    public class ApiErrorDetail
    {
        public string code;
        public string message;
        public string field;
    }

    /// <summary>优先级取值，避免在脚本里散落字符串字面量。</summary>
    public static class IssuePriorityLevel
    {
        public const string Low = "low";
        public const string Medium = "medium";
        public const string High = "high";

        public static string ToLabel(string value)
        {
            if (value == Low) return "低";
            if (value == High) return "高";
            return "中";
        }

        public static string StatusLabel(string value)
        {
            if (value == "in_progress") return "处理中";
            if (value == "resolved") return "已解决";
            return "待处理";
        }
    }
}
