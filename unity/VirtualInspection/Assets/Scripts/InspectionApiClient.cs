using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace VirtualInspection
{
    public sealed class InspectionApiClient : MonoBehaviour
    {
        private const string DefaultBaseUrl = "http://localhost:8080/api";
        private const int TimeoutSeconds = 10;

        private string _baseUrl;

        private void Awake()
        {
            var configured = Environment.GetEnvironmentVariable("INSPECTION_API_URL");
            _baseUrl = string.IsNullOrWhiteSpace(configured)
                ? DefaultBaseUrl
                : configured.TrimEnd('/');
        }

        public IEnumerator FetchIssues(Action<Issue[]> onSuccess, Action<string> onFailure)
        {
            using var request = UnityWebRequest.Get($"{_baseUrl}/issues");
            request.timeout = TimeoutSeconds;
            request.SetRequestHeader("Accept", "application/json");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onFailure?.Invoke(ReadRequestError(request, "无法连接问题服务"));
                yield break;
            }

            IssueListResponse payload;
            try
            {
                payload = JsonUtility.FromJson<IssueListResponse>(request.downloadHandler.text);
            }
            catch (Exception)
            {
                onFailure?.Invoke("问题列表响应格式错误");
                yield break;
            }

            if (payload?.issues == null)
            {
                onFailure?.Invoke("问题列表响应缺少 issues 字段");
                yield break;
            }
            onSuccess?.Invoke(payload.issues);
        }

        public IEnumerator CreateIssue(
            CreateIssueRequest input,
            Action<Issue> onSuccess,
            Action<string> onFailure)
        {
            var json = JsonUtility.ToJson(input);
            using var request = new UnityWebRequest($"{_baseUrl}/issues", UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = TimeoutSeconds
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onFailure?.Invoke(ReadRequestError(request, "问题提交失败"));
                yield break;
            }

            Issue created;
            try
            {
                created = JsonUtility.FromJson<Issue>(request.downloadHandler.text);
            }
            catch (Exception)
            {
                onFailure?.Invoke("服务端返回的问题格式错误");
                yield break;
            }

            if (created == null || string.IsNullOrWhiteSpace(created.id))
            {
                onFailure?.Invoke("服务端未返回有效的问题 ID");
                yield break;
            }
            onSuccess?.Invoke(created);
        }

        private static string ReadRequestError(UnityWebRequest request, string fallback)
        {
            var body = request.downloadHandler?.text;
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    var payload = JsonUtility.FromJson<ApiErrorResponse>(body);
                    if (payload != null && !string.IsNullOrWhiteSpace(payload.error))
                    {
                        return payload.error;
                    }
                }
                catch (Exception)
                {
                    // Fall through to the transport error below.
                }
            }

            if (!string.IsNullOrWhiteSpace(request.error))
            {
                return $"{fallback}：{request.error}";
            }
            return fallback;
        }
    }
}
