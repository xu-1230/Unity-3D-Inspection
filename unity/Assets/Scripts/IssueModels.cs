using System;

namespace Unity3DInspection
{
    /// <summary>与后端契约对应的坐标结构。</summary>
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
        public IssueLocationDto location;
    }

    /// <summary>创建成功后的响应，只需读取展示用字段。</summary>
    [Serializable]
    public class IssueResponse
    {
        public string id;
        public string title;
        public string status;
        public string priority;
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
    public static class IssuePriority
    {
        public const string Low = "low";
        public const string Medium = "medium";
        public const string High = "high";
    }
}
