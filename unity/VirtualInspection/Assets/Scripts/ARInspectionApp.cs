using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace VirtualInspection
{
    public sealed class ARInspectionApp : MonoBehaviour
    {
        private static readonly List<ARRaycastHit> RaycastHits = new();

        private const string DefaultArea = "AR 模拟场地";
        private const string DefaultAsset = "已检测平面";

        private Camera _camera;
        private ARSession _session;
        private ARCameraManager _cameraManager;
        private ARCameraBackground _cameraBackground;
        private ARRaycastManager _raycastManager;
        private ARPlaneManager _planeManager;
        private ARAnchorManager _anchorManager;
        private XRManagerSettings _xrManager;
        private InspectionApiClient _api;
        private RuntimeUI _ui;
        private Transform _markerRoot;
        private GameObject _pendingMarker;
        private IssuePosition _pendingPosition;
        private string _pendingArea = DefaultArea;
        private string _pendingAsset = DefaultAsset;
        private int _savedIssueCount;
        private bool _backendOnline;
        private bool _submitting;
        private bool _tracking;
        private Material _planeMaterial;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            _markerRoot = new GameObject("AR Issue Markers").transform;
            _api = gameObject.AddComponent<InspectionApiClient>();

            EnsureEventSystem();
            _ui = gameObject.AddComponent<RuntimeUI>();
            _ui.Initialize(RuntimeFonts.Get(), SubmitIssue, CancelPendingIssue, CloseDetails);
            _ui.SetHudCopy(
                "AR Simulation：移动模拟设备扫描平面，再点击平面放置问题标记。",
                "按住鼠标右键  转动模拟设备     ·     鼠标左键  放置标记     ·     ESC  取消");
            _ui.SetBackendStatus("正在初始化 XR Simulation...", false);

            ResolveArComponents();
        }

        private void Start()
        {
            StartCoroutine(StartArSession());
            StartCoroutine(ClearEditorConsole());
        }

        private static IEnumerator ClearEditorConsole()
        {
            yield return null;
#if UNITY_EDITOR
            Debug.ClearDeveloperConsole();
#endif
        }

        private void Update()
        {
            if (_ui == null)
            {
                return;
            }

            UpdateTrackingStatus();
            UpdatePlaneVisuals();
            HandleEscape();

            if (!_tracking ||
                _ui.ModalOpen ||
                _submitting ||
                EventSystem.current == null)
            {
                return;
            }

            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == UnityEngine.TouchPhase.Ended &&
                    !EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                {
                    InspectAtScreenPoint(touch.position);
                }
                return;
            }

            if (Input.GetMouseButtonUp(0) &&
                !EventSystem.current.IsPointerOverGameObject())
            {
                InspectAtScreenPoint(Input.mousePosition);
            }
        }

        private void ResolveArComponents()
        {
            _session = FindObjectOfType<ARSession>();
            _cameraManager = FindObjectOfType<ARCameraManager>();
            _cameraBackground = FindObjectOfType<ARCameraBackground>();
            _raycastManager = FindObjectOfType<ARRaycastManager>();
            _planeManager = FindObjectOfType<ARPlaneManager>();
            _anchorManager = FindObjectOfType<ARAnchorManager>();
            _camera = _cameraManager == null ? Camera.main : _cameraManager.GetComponent<Camera>();
        }

        private IEnumerator StartArSession()
        {
            var settings = XRGeneralSettings.Instance;
            _xrManager = settings?.Manager;
            if (_xrManager == null ||
                _session == null ||
                _camera == null ||
                _cameraBackground == null ||
                _raycastManager == null ||
                _planeManager == null)
            {
                FailInitialization("AR 场景组件不完整，请重新执行 AR Simulation 配置");
                yield break;
            }

            if (!_xrManager.isInitializationComplete)
            {
                yield return new WaitUntil(() => _xrManager.isInitializationComplete);
            }
            if (_xrManager.activeLoader == null)
            {
                FailInitialization("XR Simulation Loader 初始化失败");
                yield break;
            }

            var deadline = Time.realtimeSinceStartup + 15f;
            while (ARSession.state != ARSessionState.SessionTracking &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (ARSession.state != ARSessionState.SessionTracking)
            {
                FailInitialization($"AR Tracking 异常：{ARSession.state}");
                yield break;
            }

            _tracking = true;
            _ui.ShowToast("XR Simulation 已启动，点击模拟平面即可放置标记", ToastKind.Success);
            StartCoroutine(_api.FetchIssues(OnIssuesLoaded, OnIssuesLoadFailed));
        }

        private void InspectAtScreenPoint(Vector2 screenPoint)
        {
            var physicsRay = _camera.ScreenPointToRay(screenPoint);
            // 标记在 Ignore Raycast 层，这里只检测标记层，避免它干扰后续的平面射线
            if (Physics.Raycast(physicsRay, out var physicsHit, 30f, IssueMarkerFactory.MarkerMask, QueryTriggerInteraction.Ignore))
            {
                var marker = physicsHit.collider.GetComponentInParent<IssueMarker>();
                if (marker?.Issue != null)
                {
                    _ui.ShowDetails(marker.Issue);
                    return;
                }
            }

            if (_pendingMarker != null)
            {
                _ui.ShowToast("已存在待提交标记，请先提交或取消当前问题", ToastKind.Info);
                return;
            }

            if (!_raycastManager.Raycast(screenPoint, RaycastHits, TrackableType.PlaneWithinPolygon))
            {
                _ui.ShowToast("尚未命中已检测平面，请先扫描并点击地面", ToastKind.Info);
                return;
            }

            var hit = RaycastHits[0];
            _pendingPosition = IssuePosition.FromWorld(hit.pose.position);
            _pendingMarker = CreateAnchoredMarker(hit.pose.position, null, true);
            _pendingArea = DefaultArea;
            _pendingAsset = DefaultAsset;
            _ui.ShowForm(DefaultAsset, DefaultArea, hit.pose.position);
        }

        private GameObject CreateAnchoredMarker(Vector3 position, Issue issue, bool pending)
        {
            var marker = IssueMarkerFactory.Create(_markerRoot, position, issue, pending);
            marker.AddComponent<ARAnchor>();
            return marker;
        }

        private void SubmitIssue(string title, string description, string priority)
        {
            if (_submitting)
            {
                return;
            }
            if (_pendingMarker == null || _pendingPosition == null)
            {
                _ui.SetFormError("待提交标记不存在，请取消后重新选择平面");
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
            StartCoroutine(_api.CreateIssue(
                new CreateIssueRequest
                {
                    title = title,
                    description = description,
                    priority = priority,
                    area = _pendingArea,
                    asset = _pendingAsset,
                    position = _pendingPosition
                },
                OnIssueCreated,
                OnCreateIssueFailed));
        }

        private void OnIssueCreated(Issue issue)
        {
            _submitting = false;
            if (_pendingMarker != null)
            {
                Destroy(_pendingMarker);
                _pendingMarker = null;
            }
            _pendingPosition = null;
            CreateAnchoredMarker(issue.position.ToWorld(), issue, false);
            _savedIssueCount++;
            _backendOnline = true;
            _ui.CloseForm();
            _ui.ShowToast("AR 问题已成功上报", ToastKind.Success);
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
            _ui.ShowToast("已取消填写，AR 标记和锚点已移除", ToastKind.Info);
        }

        private void CloseDetails()
        {
            _ui.CloseDetails();
        }

        private void HandleEscape()
        {
            if (!Input.GetKeyDown(KeyCode.Escape))
            {
                return;
            }
            if (_ui.FormOpen && !_submitting)
            {
                CancelPendingIssue();
            }
            else if (_ui.DetailOpen)
            {
                CloseDetails();
            }
        }

        private void OnIssuesLoaded(Issue[] issues)
        {
            _savedIssueCount = issues.Length;
            _backendOnline = true;
            _ui.ShowToast(
                $"已加载 {issues.Length} 条历史问题；旧坐标不自动恢复为 AR 锚点",
                ToastKind.Info);
        }

        private void OnIssuesLoadFailed(string message)
        {
            _backendOnline = false;
            _ui.ShowToast($"加载问题失败：{message}", ToastKind.Error);
        }

        private void UpdateTrackingStatus()
        {
            if (_session == null || _planeManager == null)
            {
                return;
            }

            _tracking = ARSession.state == ARSessionState.SessionTracking;
            var stateText = _tracking ? "Tracking 正常" : ARSession.state.ToString();
            var planeCount = _planeManager.trackables.count;
            var backendText = _backendOnline ? "后端已连接" : "后端未连接";
            _ui.SetBackendStatus(
                $"AR Simulation · {stateText} · 平面 {planeCount} · 历史 {_savedIssueCount} · {backendText}",
                _tracking && _backendOnline);
        }

        private void UpdatePlaneVisuals()
        {
            if (_planeManager == null)
            {
                return;
            }
            if (_planeMaterial == null)
            {
                _planeMaterial = CreatePlaneMaterial();
            }

            foreach (var plane in _planeManager.trackables)
            {
                if (plane.GetComponent<MeshFilter>() != null)
                {
                    continue;
                }
                plane.gameObject.AddComponent<MeshFilter>();
                var meshRenderer = plane.gameObject.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = _planeMaterial;
                plane.gameObject.AddComponent<ARPlaneMeshVisualizer>();
            }
        }

        private static Material CreatePlaneMaterial()
        {
            var material = new Material(Shader.Find("Standard"))
            {
                color = new Color(0.35f, 0.9f, 1f, 0.2f),
                renderQueue = 3000
            };
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            return material;
        }

        private void FailInitialization(string message)
        {
            _tracking = false;
            _ui.SetBackendStatus($"AR 初始化失败：{message}", false);
            _ui.ShowToast($"{message}。请检查 XR Plug-in Management 与 XR Simulation。", ToastKind.Error);
            Debug.LogError($"AR simulation initialization failed: {message}");
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
