using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Unity3DInspection
{
    /// <summary>唯一发起 HTTP 请求的组件。</summary>
    public class IssueApiClient : MonoBehaviour
    {
        [Tooltip("后端基址，不带 /api 前缀")]
        public string baseUrl = "http://localhost:8080";

        public float timeoutSeconds = 10f;

        /// <summary>提交问题；成功回调返回后端保存后的对象，失败回调返回可直接展示的中文原因。</summary>
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
                    onSuccess?.Invoke(response);
                    yield break;
                }

                onError?.Invoke(ExtractMessage(request));
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
