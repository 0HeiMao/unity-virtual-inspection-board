using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace VirtualInspection
{
    public sealed class InspectionApp : MonoBehaviour
    {
        private const float RayDistance = 100f;

        private Camera _camera;
        private InspectionCameraController _cameraController;
        private InspectionApiClient _api;
        private RuntimeUI _ui;
        private Transform _markerRoot;
        private GameObject _pendingMarker;
        private IssuePosition _pendingPosition;
        private int _savedIssueCount;
        private bool _submitting;

        private void Awake()
        {
            Application.targetFrameRate = 60;

            _camera = InspectionSceneBuilder.Build(out _cameraController);
            _markerRoot = new GameObject("Issue Markers").transform;
            _api = gameObject.AddComponent<InspectionApiClient>();

            EnsureEventSystem();
            _ui = gameObject.AddComponent<RuntimeUI>();
            _ui.Initialize(RuntimeFonts.Get(), SubmitIssue, CancelPendingIssue, CloseDetails);
            _ui.SetBackendStatus("正在连接问题服务...", false);
        }

        private void Start()
        {
            StartCoroutine(_api.FetchIssues(OnIssuesLoaded, OnIssuesLoadFailed));
        }

        private void Update()
        {
            _cameraController.InputEnabled = !_ui.ModalOpen;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_ui.FormOpen && !_submitting)
                {
                    CancelPendingIssue();
                }
                else if (_ui.DetailOpen)
                {
                    CloseDetails();
                }
            }

            if (!_ui.ModalOpen &&
                !_submitting &&
                Input.GetMouseButtonDown(0) &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                TryInspectAtPointer();
            }
        }

        private void TryInspectAtPointer()
        {
            if (_camera == null)
            {
                return;
            }

            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, RayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                _ui.ShowToast("未命中可巡检表面，请瞄准设备或地面", ToastKind.Info);
                return;
            }

            var marker = hit.collider.GetComponentInParent<IssueMarker>();
            if (marker != null)
            {
                if (marker.Issue != null)
                {
                    _ui.ShowDetails(marker.Issue);
                }
                return;
            }

            var surface = hit.collider.GetComponentInParent<InspectableSurface>();
            if (surface == null)
            {
                return;
            }

            if (_pendingMarker != null)
            {
                _ui.ShowToast("已存在待提交标记，请先提交或取消当前问题", ToastKind.Info);
                return;
            }

            _pendingPosition = IssuePosition.FromWorld(hit.point);
            _pendingMarker = CreateMarker(hit.point, null, true);
            _ui.ShowForm(surface.DisplayName, hit.point);
        }

        private void SubmitIssue(string title, string description, string priority)
        {
            if (_submitting)
            {
                return;
            }
            if (_pendingMarker == null || _pendingPosition == null)
            {
                _ui.SetFormError("待提交标记不存在，请取消后重新点击场景表面");
                return;
            }

            title = title?.Trim() ?? string.Empty;
            description = description?.Trim() ?? string.Empty;
            if (title.Length == 0)
            {
                _ui.SetFormError("标题为必填项");
                return;
            }
            if (title.Length > 120)
            {
                _ui.SetFormError("标题不能超过 120 个字符");
                return;
            }

            _submitting = true;
            _ui.SetFormError(string.Empty);
            _ui.SetSubmitting(true);

            var input = new CreateIssueRequest
            {
                title = title,
                description = description,
                priority = priority,
                position = _pendingPosition
            };
            StartCoroutine(_api.CreateIssue(input, OnIssueCreated, OnCreateIssueFailed));
        }

        private void OnIssueCreated(Issue created)
        {
            _submitting = false;
            if (_pendingMarker != null)
            {
                Destroy(_pendingMarker);
                _pendingMarker = null;
            }
            _pendingPosition = null;

            CreateMarker(created.position.ToWorld(), created, false);
            _savedIssueCount++;
            _ui.CloseForm();
            UpdateBackendStatus(true);
            _ui.ShowToast("问题已成功上报，管理端刷新后即可查看", ToastKind.Success);
        }

        private void OnCreateIssueFailed(string message)
        {
            _submitting = false;
            _ui.SetSubmitting(false);
            _ui.SetFormError(message);
            _ui.ShowToast($"上报失败：{message}", ToastKind.Error);
        }

        private void CancelPendingIssue()
        {
            if (_submitting)
            {
                return;
            }
            if (_pendingMarker != null)
            {
                Destroy(_pendingMarker);
                _pendingMarker = null;
            }
            _pendingPosition = null;
            _ui.CloseForm();
            _ui.ShowToast("已取消填写，未保存标记已移除", ToastKind.Info);
        }

        private void CloseDetails()
        {
            _ui.CloseDetails();
        }

        private void OnIssuesLoaded(Issue[] issues)
        {
            _savedIssueCount = 0;
            foreach (var issue in issues)
            {
                if (issue?.position == null)
                {
                    continue;
                }
                CreateMarker(issue.position.ToWorld(), issue, false);
                _savedIssueCount++;
            }
            UpdateBackendStatus(true);
        }

        private void OnIssuesLoadFailed(string message)
        {
            _ui.SetBackendStatus("问题服务未连接 · 场景仍可浏览", false);
            _ui.ShowToast($"加载已保存标记失败：{message}", ToastKind.Error);
        }

        private void UpdateBackendStatus(bool online)
        {
            _ui.SetBackendStatus(
                online
                    ? $"问题服务已连接 · {_savedIssueCount} 个标记"
                    : "问题服务未连接 · 场景仍可浏览",
                online);
        }

        private GameObject CreateMarker(Vector3 worldPosition, Issue issue, bool pending)
        {
            var root = new GameObject(pending ? "Pending Issue Marker" : $"Issue Marker - {issue.id}");
            root.transform.SetParent(_markerRoot, false);
            root.transform.position = worldPosition;

            var marker = root.AddComponent<IssueMarker>();
            if (pending)
            {
                marker.ConfigurePending();
            }
            else
            {
                marker.ConfigureSaved(issue);
            }

            var color = pending ? new Color(1f, 0.63f, 0.18f) : StatusColor(issue.status);
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Marker Head";
            sphere.transform.SetParent(root.transform, false);
            sphere.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            sphere.transform.localScale = Vector3.one * (pending ? 0.28f : 0.22f);
            sphere.GetComponent<Renderer>().sharedMaterial = CreateMarkerMaterial(color);
            sphere.AddComponent<MarkerPulse>();

            var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.name = "Marker Stem";
            stem.transform.SetParent(root.transform, false);
            stem.transform.localPosition = new Vector3(0f, -0.03f, 0f);
            stem.transform.localScale = new Vector3(0.035f, 0.18f, 0.035f);
            stem.GetComponent<Renderer>().sharedMaterial = CreateMarkerMaterial(color * 0.55f);
            Destroy(stem.GetComponent<Collider>());

            return root;
        }

        private static Material CreateMarkerMaterial(Color color)
        {
            var material = new Material(Shader.Find("Standard"))
            {
                color = color
            };
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.5f);
            return material;
        }

        private static Color StatusColor(string status)
        {
            return status switch
            {
                "open" => new Color(1f, 0.33f, 0.29f),
                "in_progress" => new Color(1f, 0.68f, 0.21f),
                "resolved" => new Color(0.25f, 0.88f, 0.64f),
                _ => new Color(0.65f, 0.75f, 0.8f)
            };
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }
            gameObject.AddComponent<EventSystem>();
            gameObject.AddComponent<StandaloneInputModule>();
        }
    }
}
