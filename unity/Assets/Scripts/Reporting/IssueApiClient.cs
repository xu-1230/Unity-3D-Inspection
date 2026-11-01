using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace VirtualInspection.Reporting
{
    /// <summary>唯一发起 HTTP 请求的组件，POST 到 Go 后端的 /api/issues。</summary>
    public class IssueApiClient : MonoBehaviour
    {
        /// <summary>与 exe 同目录的可选配置文件，用于把客户端指向另一台机器的后端。</summary>
        private const string ConfigFileName = "backend-config.json";

        [Tooltip("后端基址，不带 /api 前缀；会被同目录 backend-config.json 中合法的 baseUrl 覆盖")]
        public string baseUrl = "http://localhost:8080";

        public float timeoutSeconds = 10f;

        [Serializable]
        private class BackendConfig
        {
            public string baseUrl;
        }

        private void Awake()
        {
            string configured = LoadConfiguredBaseUrl();
            if (!string.IsNullOrEmpty(configured)) baseUrl = configured;

            Debug.Log("[巡检客户端] 后端地址：" + baseUrl);
        }

        /// <summary>提交问题；成功回调带回后端保存后的对象，失败回调返回可直接展示的中文原因。</summary>
        public IEnumerator CreateIssue(IssuePayload payload, Action<IssueResponse> onSuccess, Action<string> onError)
        {
            string url = baseUrl.TrimEnd('/') + "/api/issues";
            string body = JsonUtility.ToJson(payload);
            byte[] raw = Encoding.UTF8.GetBytes(body);

            using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(raw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = Mathf.CeilToInt(timeoutSeconds);

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success && request.responseCode == 201)
                {
                    IssueResponse response = null;
                    try
                    {
                        response = JsonUtility.FromJson<IssueResponse>(request.downloadHandler.text);
                    }
                    catch (Exception)
                    {
                        // 响应不可解析时仍视为成功，提示文案不依赖响应内容
                    }

                    if (onSuccess != null) onSuccess(response);
                    yield break;
                }

                if (onError != null) onError(ExtractMessage(request));
            }
        }

        /// <summary>拉取全部问题，用于启动时还原标记与核对状态。</summary>
        public IEnumerator FetchIssues(Action<IssueItem[]> onSuccess, Action<string> onError)
        {
            string url = baseUrl.TrimEnd('/') + "/api/issues";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = Mathf.CeilToInt(timeoutSeconds);

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success && request.responseCode == 200)
                {
                    IssueItem[] items = null;
                    try
                    {
                        string wrapped = "{\"items\":" + request.downloadHandler.text + "}";
                        IssueListWrapper parsed = JsonUtility.FromJson<IssueListWrapper>(wrapped);
                        if (parsed != null) items = parsed.items;
                    }
                    catch (Exception)
                    {
                        items = null;
                    }

                    if (items == null)
                    {
                        if (onError != null) onError("问题列表解析失败");
                        yield break;
                    }

                    if (onSuccess != null) onSuccess(items);
                    yield break;
                }

                if (onError != null) onError(ExtractMessage(request));
            }
        }

        /// <summary>
        /// 读取 exe 同目录的 backend-config.json。
        /// 文件缺失、JSON 非法或地址不是 http(s) 时返回 null，调用方保留默认地址。
        /// </summary>
        private static string LoadConfiguredBaseUrl()
        {
            string directory;
            try
            {
                directory = Path.GetDirectoryName(Application.dataPath);
            }
            catch (Exception)
            {
                return null;
            }

            if (string.IsNullOrEmpty(directory)) return null;

            string path = Path.Combine(directory, ConfigFileName);
            if (!File.Exists(path)) return null;

            try
            {
                BackendConfig config = JsonUtility.FromJson<BackendConfig>(File.ReadAllText(path));
                string url = config != null && config.baseUrl != null ? config.baseUrl.Trim() : null;
                if (string.IsNullOrEmpty(url))
                {
                    Debug.LogWarning("[巡检客户端] " + ConfigFileName + " 未提供 baseUrl，继续使用默认地址");
                    return null;
                }
                if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                {
                    Debug.LogWarning("[巡检客户端] " + ConfigFileName + " 的 baseUrl 必须以 http:// 或 https:// 开头，已忽略：" + url);
                    return null;
                }
                return url.TrimEnd('/');
            }
            catch (Exception error)
            {
                Debug.LogWarning("[巡检客户端] 读取 " + ConfigFileName + " 失败，继续使用默认地址：" + error.Message);
                return null;
            }
        }

        private static string ExtractMessage(UnityWebRequest request)
        {
            string text = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            if (!string.IsNullOrEmpty(text))
            {
                try
                {
                    ApiErrorBody parsed = JsonUtility.FromJson<ApiErrorBody>(text);
                    if (parsed != null && parsed.error != null && !string.IsNullOrEmpty(parsed.error.message))
                    {
                        return parsed.error.message;
                    }
                }
                catch (Exception)
                {
                    // 落到下面的通用文案
                }
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                return string.IsNullOrEmpty(request.error) ? "网络请求失败" : request.error;
            }

            return "服务端返回 HTTP " + request.responseCode;
        }
    }
}
