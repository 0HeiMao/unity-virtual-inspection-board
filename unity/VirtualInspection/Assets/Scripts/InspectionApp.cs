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
        private string _pendingArea = string.Empty;
        private string _pendingAsset = string.Empty;
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
                Input.GetMouseButtonUp(0) &&
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

            // 两段式检测：标记在 Ignore Raycast 层，与表面层互不干扰。
            // 这样已有标记不会挡住其身后表面的落点，远处的标记也不会抢走近处表面的点击。
            var markerHitFound = Physics.Raycast(ray, out var markerHit, RayDistance, IssueMarkerFactory.MarkerMask, QueryTriggerInteraction.Ignore);
            var surfaceHitFound = Physics.Raycast(ray, out var surfaceHit, RayDistance, IssueMarkerFactory.SurfaceMask, QueryTriggerInteraction.Ignore);

            if (markerHitFound && (!surfaceHitFound || markerHit.distance <= surfaceHit.distance))
            {
                var marker = markerHit.collider.GetComponentInParent<IssueMarker>();
                if (marker != null && marker.Issue != null)
                {
                    _ui.ShowDetails(marker.Issue);
                    return;
                }

                // 点到的是待提交标记：忽略它，继续按它身后的表面处理，下方会给出待提交提示
            }

            if (!surfaceHitFound)
            {
                _ui.ShowToast("未命中可巡检表面，请瞄准设备或地面", ToastKind.Info);
                return;
            }

            var surface = surfaceHit.collider.GetComponentInParent<InspectableSurface>();
            if (surface == null)
            {
                return;
            }

            if (_pendingMarker != null)
            {
                _ui.ShowToast("已存在待提交标记，请先提交或取消当前问题", ToastKind.Info);
                return;
            }

            _pendingPosition = IssuePosition.FromWorld(surfaceHit.point);
            _pendingArea = surface.AreaName;
            _pendingAsset = surface.DisplayName;
            _pendingMarker = IssueMarkerFactory.Create(_markerRoot, surfaceHit.point, null, true);
            _ui.ShowForm(surface.DisplayName, surface.AreaName, surfaceHit.point);
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
                area = _pendingArea,
                asset = _pendingAsset,
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
            _pendingArea = string.Empty;
            _pendingAsset = string.Empty;

            IssueMarkerFactory.Create(_markerRoot, created.position.ToWorld(), created, false);
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
            _pendingArea = string.Empty;
            _pendingAsset = string.Empty;
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
                IssueMarkerFactory.Create(_markerRoot, issue.position.ToWorld(), issue, false);
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
