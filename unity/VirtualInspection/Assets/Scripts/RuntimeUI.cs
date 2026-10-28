using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VirtualInspection
{
    public sealed class RuntimeUI : MonoBehaviour
    {
        private static readonly Color Accent = new Color(0.82f, 1f, 0.3f);
        private static readonly Color PanelColor = new Color(0.035f, 0.06f, 0.075f, 0.96f);
        private static readonly Color Muted = new Color(0.55f, 0.65f, 0.7f);
        private static readonly Color Danger = new Color(1f, 0.42f, 0.37f);
        private static readonly Color Success = new Color(0.3f, 0.84f, 0.65f);

        private Font _font;
        private Action<string, string, string> _onSubmit;
        private Action _onCancel;
        private Action _onCloseDetails;

        private Text _backendStatus;
        private Image _backendDot;
        private Text _subtitle;
        private Text _controls;
        private GameObject _formOverlay;
        private GameObject _detailOverlay;
        private GameObject _toast;
        private Text _toastText;
        private Image _toastImage;
        private Text _formSurface;
        private Text _formCoordinates;
        private Text _formError;
        private InputField _titleInput;
        private InputField _descriptionInput;
        private Button _cancelButton;
        private Button _submitButton;
        private Text _submitButtonText;
        private Text _detailTitle;
        private Text _detailStatus;
        private Text _detailPriority;
        private Text _detailDescription;
        private Text _detailCoordinates;
        private Text _detailMetadata;
        private string _selectedPriority = "medium";
        private readonly Dictionary<string, Image> _priorityButtons = new();
        private Coroutine _toastRoutine;

        public bool FormOpen => _formOverlay != null && _formOverlay.activeSelf;
        public bool DetailOpen => _detailOverlay != null && _detailOverlay.activeSelf;
        public bool ModalOpen => FormOpen || DetailOpen;

        public void Initialize(
            Font font,
            Action<string, string, string> onSubmit,
            Action onCancel,
            Action onCloseDetails)
        {
            _font = font;
            _onSubmit = onSubmit;
            _onCancel = onCancel;
            _onCloseDetails = onCloseDetails;

            var canvasRoot = new GameObject("Runtime UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRoot.transform.SetParent(transform, false);
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            BuildHud(canvasRoot.transform);
            BuildForm(canvasRoot.transform);
            BuildDetails(canvasRoot.transform);
            BuildToast(canvasRoot.transform);
            _formOverlay.SetActive(false);
            _detailOverlay.SetActive(false);
            _toast.SetActive(false);
        }

        public void SetBackendStatus(string message, bool online)
        {
            _backendStatus.text = message;
            _backendStatus.color = online ? new Color(0.78f, 0.9f, 0.84f) : new Color(1f, 0.7f, 0.66f);
            _backendDot.color = online ? Success : Danger;
        }

        public void SetHudCopy(string subtitle, string controls)
        {
            _subtitle.text = subtitle;
            _controls.text = controls;
        }

        public void ShowForm(string surfaceName, string areaName, Vector3 position)
        {
            var areaText = string.IsNullOrWhiteSpace(areaName) ? "未归属区域" : areaName.Trim();
            _formSurface.text = $"{areaText} · {surfaceName} · 待提交标记已创建";
            _formCoordinates.text = FormatPosition(position);
            _formError.text = string.Empty;
            _titleInput.text = string.Empty;
            _descriptionInput.text = string.Empty;
            SelectPriority("medium");
            _cancelButton.targetGraphic.raycastTarget = true;
            _submitButton.targetGraphic.raycastTarget = true;
            SetSubmitting(false);
            _formOverlay.SetActive(true);
            _detailOverlay.SetActive(false);
            _titleInput.Select();
            _titleInput.ActivateInputField();
        }

        public void CloseForm()
        {
            _formOverlay.SetActive(false);
            _formError.text = string.Empty;
            SetSubmitting(false);
        }

        public void SetSubmitting(bool submitting)
        {
            _submitButton.interactable = !submitting;
            _cancelButton.interactable = !submitting;
            _titleInput.interactable = !submitting;
            _descriptionInput.interactable = !submitting;
            foreach (var button in _priorityButtons.Values)
            {
                button.raycastTarget = !submitting;
            }
            _submitButtonText.text = submitting ? "正在上报..." : "提交问题";
        }

        public void SetFormError(string message)
        {
            _formError.text = message;
        }

        public void ShowDetails(Issue issue)
        {
            if (issue == null)
            {
                return;
            }

            _detailTitle.text = issue.title;
            _detailStatus.text = $"状态：{StatusLabel(issue.status)}";
            _detailStatus.color = StatusColor(issue.status);
            _detailPriority.text = $"优先级：{PriorityLabel(issue.priority)}";
            _detailPriority.color = PriorityColor(issue.priority);
            _detailDescription.text = string.IsNullOrWhiteSpace(issue.description)
                ? "未填写问题描述"
                : issue.description;
            _detailCoordinates.text = issue.position == null
                ? "坐标不可用"
                : $"命中坐标  {FormatPosition(issue.position.ToWorld())}";
            var ownership = string.IsNullOrWhiteSpace(issue.area) ? "未归属区域" : issue.area;
            var assetName = string.IsNullOrWhiteSpace(issue.asset) ? "未登记设备" : issue.asset;
            _detailMetadata.text = $"{ownership} · 设备 {assetName}\nID  {issue.id}\n上报  {FormatTime(issue.createdAt)}    更新  {FormatTime(issue.updatedAt)}";
            _detailOverlay.SetActive(true);
            _formOverlay.SetActive(false);
        }

        public void CloseDetails()
        {
            _detailOverlay.SetActive(false);
        }

        public void ShowToast(string message, ToastKind kind = ToastKind.Info)
        {
            if (_toastRoutine != null)
            {
                StopCoroutine(_toastRoutine);
            }
            _toastRoutine = StartCoroutine(ShowToastRoutine(message, kind));
        }

        private IEnumerator ShowToastRoutine(string message, ToastKind kind)
        {
            _toastText.text = message;
            switch (kind)
            {
                case ToastKind.Success:
                    _toastImage.color = new Color(0.05f, 0.21f, 0.17f, 0.98f);
                    _toastText.color = new Color(0.72f, 1f, 0.9f);
                    break;
                case ToastKind.Error:
                    _toastImage.color = new Color(0.27f, 0.07f, 0.065f, 0.98f);
                    _toastText.color = new Color(1f, 0.8f, 0.77f);
                    break;
                default:
                    _toastImage.color = new Color(0.05f, 0.1f, 0.13f, 0.98f);
                    _toastText.color = new Color(0.84f, 0.92f, 0.95f);
                    break;
            }

            _toast.SetActive(true);
            yield return new WaitForSecondsRealtime(3.2f);
            _toast.SetActive(false);
            _toastRoutine = null;
        }

        private void BuildHud(Transform parent)
        {
            var header = CreatePanel("Header", parent, new Color(0.025f, 0.045f, 0.055f, 0.93f));
            StretchHorizontal(header.rectTransform, 22f, 110f, 22f);

            var marker = CreatePanel("Brand Marker", header.transform, Accent);
            Place(marker.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -22f), new Vector2(5f, 66f));

            var title = CreateText(
                header.transform,
                "Title",
                "VIRTUAL PATROL / 虚拟现场巡检",
                24,
                new Color(0.92f, 0.97f, 0.98f),
                TextAnchor.MiddleLeft,
                FontStyle.Bold);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(46f, -18f), new Vector2(650f, 42f));

            _subtitle = CreateText(
                header.transform,
                "Subtitle",
                "发现问题后点击设备或场景表面，在命中位置创建标记。",
                16,
                Muted,
                TextAnchor.MiddleLeft);
            Place(_subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(47f, -60f), new Vector2(760f, 28f));

            var statusPanel = CreatePanel("Backend Status", header.transform, new Color(0.04f, 0.075f, 0.085f, 0.9f));
            Place(statusPanel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 0f), new Vector2(344f, 46f));

            _backendDot = CreatePanel("Dot", statusPanel.transform, new Color(0.55f, 0.65f, 0.7f)).GetComponent<Image>();
            Place(_backendDot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(8f, 8f));

            _backendStatus = CreateText(
                statusPanel.transform,
                "Backend Status Text",
                "正在连接问题服务...",
                15,
                new Color(0.75f, 0.82f, 0.85f),
                TextAnchor.MiddleLeft);
            Place(_backendStatus.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(294f, 32f));

            var instructions = CreatePanel("Instructions", parent, new Color(0.025f, 0.045f, 0.055f, 0.91f));
            StretchBottom(instructions.rectTransform, 22f, 66f, 22f);

            _controls = CreateText(
                instructions.transform,
                "Controls",
                "W A S D  移动     ·     按住鼠标右键  转动视角     ·     鼠标左键  标记问题     ·     ESC  取消",
                15,
                new Color(0.74f, 0.83f, 0.87f),
                TextAnchor.MiddleLeft);
            Place(_controls.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(22f, 0f), new Vector2(970f, 66f));

            var legend = CreateText(
                instructions.transform,
                "Legend",
                "标记状态   ● 待处理   ● 处理中   ● 已解决",
                14,
                Muted,
                TextAnchor.MiddleRight);
            Place(legend.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-22f, 0f), new Vector2(480f, 66f));
            legend.supportRichText = true;
        }

        private void BuildForm(Transform parent)
        {
            _formOverlay = CreateOverlay("Issue Form Overlay", parent, out var overlayImage);
            overlayImage.raycastTarget = true;

            var card = CreatePanel("Issue Form", _formOverlay.transform, PanelColor);
            Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 610f));

            var accent = CreatePanel("Accent", card.transform, Accent);
            Place(accent.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(800f, 5f));

            var title = CreateText(card.transform, "Form Title", "上报巡检问题", 28, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -26f), new Vector2(500f, 40f));

            var subtitle = CreateText(
                card.transform,
                "Form Subtitle",
                "当前位置已保留为待提交标记；取消不会写入后端。",
                15,
                Muted,
                TextAnchor.MiddleLeft);
            Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -64f), new Vector2(650f, 28f));

            _formSurface = CreateText(
                card.transform,
                "Form Surface",
                string.Empty,
                15,
                Accent,
                TextAnchor.MiddleLeft);
            Place(_formSurface.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -101f), new Vector2(460f, 25f));

            _formCoordinates = CreateText(
                card.transform,
                "Form Coordinates",
                string.Empty,
                13,
                Muted,
                TextAnchor.MiddleRight);
            Place(_formCoordinates.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-32f, -101f), new Vector2(240f, 25f));

            PlaceLabel(card.transform, "标题 *", 137f, new Vector2(32f, 0f));
            _titleInput = CreateInput(
                card.transform,
                "Title Input",
                "例如：1 号机柜温度异常",
                false,
                120);
            Place(_titleInput.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -158f), new Vector2(736f, 48f));

            PlaceLabel(card.transform, "描述（可选）", 221f, new Vector2(32f, 0f));
            _descriptionInput = CreateInput(
                card.transform,
                "Description Input",
                "补充异常现象、影响范围或建议处理方式",
                true,
                2000);
            Place(_descriptionInput.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -242f), new Vector2(736f, 94f));

            PlaceLabel(card.transform, "优先级", 351f, new Vector2(32f, 0f));
            CreatePriorityButton(card.transform, "low", "低", 32f);
            CreatePriorityButton(card.transform, "medium", "中", 190f);
            CreatePriorityButton(card.transform, "high", "高", 348f);

            _formError = CreateText(
                card.transform,
                "Form Error",
                string.Empty,
                14,
                Danger,
                TextAnchor.MiddleLeft);
            Place(_formError.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -444f), new Vector2(736f, 34f));

            var divider = CreatePanel("Divider", card.transform, new Color(0.15f, 0.22f, 0.25f, 0.7f));
            Place(divider.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -492f), new Vector2(736f, 1f));

            _cancelButton = CreateButton(card.transform, "取消填写", new Color(0.09f, 0.13f, 0.15f), new Color(0.72f, 0.8f, 0.83f));
            Place(_cancelButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(32f, 30f), new Vector2(170f, 46f));
            _cancelButton.onClick.AddListener(() => _onCancel?.Invoke());

            _submitButton = CreateButton(card.transform, "提交问题", Accent, new Color(0.08f, 0.1f, 0.02f));
            Place(_submitButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-32f, 30f), new Vector2(190f, 46f));
            _submitButtonText = _submitButton.GetComponentInChildren<Text>();
            _submitButton.onClick.AddListener(() =>
            {
                _onSubmit?.Invoke(_titleInput.text, _descriptionInput.text, _selectedPriority);
            });

            var shortcut = CreateText(
                card.transform,
                "Shortcut",
                "Esc 取消",
                13,
                Muted,
                TextAnchor.MiddleLeft);
            Place(shortcut.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(218f, 30f), new Vector2(100f, 46f));
        }

        private void BuildDetails(Transform parent)
        {
            _detailOverlay = CreateOverlay("Issue Details Overlay", parent, out var overlayImage);
            overlayImage.raycastTarget = true;

            var card = CreatePanel("Issue Details", _detailOverlay.transform, PanelColor);
            Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, 470f));

            var accent = CreatePanel("Accent", card.transform, Accent);
            Place(accent.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(680f, 5f));

            var kicker = CreateText(card.transform, "Kicker", "SAVED ISSUE / 已保存问题", 13, Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            Place(kicker.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -28f), new Vector2(350f, 25f));

            _detailTitle = CreateText(card.transform, "Detail Title", string.Empty, 25, Color.white, TextAnchor.UpperLeft, FontStyle.Bold);
            Place(_detailTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -64f), new Vector2(540f, 70f));

            _detailStatus = CreateText(card.transform, "Detail Status", string.Empty, 14, Success, TextAnchor.MiddleLeft);
            Place(_detailStatus.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -138f), new Vector2(180f, 28f));

            _detailPriority = CreateText(card.transform, "Detail Priority", string.Empty, 14, Muted, TextAnchor.MiddleLeft);
            Place(_detailPriority.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(210f, -138f), new Vector2(180f, 28f));

            var divider = CreatePanel("Divider", card.transform, new Color(0.15f, 0.22f, 0.25f, 0.7f));
            Place(divider.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -184f), new Vector2(620f, 1f));

            _detailDescription = CreateText(card.transform, "Detail Description", string.Empty, 16, new Color(0.78f, 0.84f, 0.87f), TextAnchor.UpperLeft);
            Place(_detailDescription.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -205f), new Vector2(620f, 92f));

            _detailCoordinates = CreateText(card.transform, "Detail Coordinates", string.Empty, 13, Accent, TextAnchor.MiddleLeft);
            Place(_detailCoordinates.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -316f), new Vector2(620f, 28f));

            _detailMetadata = CreateText(card.transform, "Detail Metadata", string.Empty, 12, Muted, TextAnchor.UpperLeft);
            Place(_detailMetadata.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -356f), new Vector2(620f, 56f));

            var close = CreateButton(card.transform, "关闭详情", new Color(0.09f, 0.13f, 0.15f), new Color(0.78f, 0.86f, 0.89f));
            Place(close.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 26f), new Vector2(150f, 44f));
            close.onClick.AddListener(() => _onCloseDetails?.Invoke());
        }

        private void BuildToast(Transform parent)
        {
            var toast = CreatePanel("Toast", parent, new Color(0.05f, 0.1f, 0.13f, 0.98f));
            Place(toast.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(620f, 58f));
            _toast = toast.gameObject;
            _toastImage = toast.GetComponent<Image>();

            _toastText = CreateText(
                toast.transform,
                "Toast Text",
                string.Empty,
                16,
                new Color(0.84f, 0.92f, 0.95f),
                TextAnchor.MiddleCenter);
            SetStretch(_toastText.rectTransform, 16f, 16f, 8f, 8f);
        }

        private void CreatePriorityButton(Transform parent, string priority, string label, float x)
        {
            var button = CreateButton(parent, label, new Color(0.07f, 0.11f, 0.13f), Muted);
            Place(button.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -382f), new Vector2(138f, 44f));
            var image = button.GetComponent<Image>();
            _priorityButtons[priority] = image;
            button.onClick.AddListener(() => SelectPriority(priority));
        }

        private void SelectPriority(string priority)
        {
            _selectedPriority = priority;
            foreach (var item in _priorityButtons)
            {
                var selected = item.Key == priority;
                item.Value.color = selected ? Accent : new Color(0.07f, 0.11f, 0.13f);
                var text = item.Value.GetComponentInChildren<Text>();
                text.color = selected ? new Color(0.08f, 0.1f, 0.02f) : Muted;
            }
        }

        private void PlaceLabel(Transform parent, string value, float topOffset, Vector2 position)
        {
            var label = CreateText(parent, $"{value} Label", value, 14, Muted, TextAnchor.MiddleLeft);
            Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(position.x, -topOffset), new Vector2(300f, 25f));
        }

        private InputField CreateInput(
            Transform parent,
            string name,
            string placeholderValue,
            bool multiline,
            int characterLimit)
        {
            var background = CreatePanel(name, parent, new Color(0.025f, 0.045f, 0.055f, 1f));
            var input = background.gameObject.AddComponent<InputField>();
            input.targetGraphic = background.GetComponent<Image>();
            input.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            input.characterLimit = characterLimit;
            input.caretColor = Accent;
            input.selectionColor = new Color(0.82f, 1f, 0.3f, 0.25f);

            var text = CreateText(
                background.transform,
                "Text",
                string.Empty,
                16,
                new Color(0.88f, 0.93f, 0.95f),
                TextAnchor.UpperLeft);
            text.supportRichText = false;
            SetStretch(text.rectTransform, 14f, 14f, 10f, 8f);

            var placeholder = CreateText(
                background.transform,
                "Placeholder",
                placeholderValue,
                16,
                new Color(0.42f, 0.51f, 0.56f),
                TextAnchor.UpperLeft);
            placeholder.fontStyle = FontStyle.Italic;
            SetStretch(placeholder.rectTransform, 14f, 14f, 10f, 8f);

            input.textComponent = text;
            input.placeholder = placeholder;
            return input;
        }

        private Button CreateButton(Transform parent, string label, Color background, Color foreground)
        {
            var panel = CreatePanel($"Button - {label}", parent, background);
            var button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = panel.GetComponent<Image>();
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1f, 1f, 1f, 0.9f),
                pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(1f, 1f, 1f, 0.4f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };

            var text = CreateText(panel.transform, "Label", label, 16, foreground, TextAnchor.MiddleCenter, FontStyle.Bold);
            SetStretch(text.rectTransform, 8f, 8f, 4f, 4f);
            return button;
        }

        private GameObject CreateOverlay(string name, Transform parent, out Image image)
        {
            image = CreatePanel(name, parent, new Color(0.01f, 0.02f, 0.025f, 0.82f));
            SetStretch(image.rectTransform, 0f, 0f, 0f, 0f);
            return image.gameObject;
        }

        private Text CreateText(
            Transform parent,
            string name,
            string value,
            int size,
            Color color,
            TextAnchor alignment,
            FontStyle style = FontStyle.Normal)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            var text = gameObject.GetComponent<Text>();
            text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1.1f;
            text.raycastTarget = false;
            return text;
        }

        private static Image CreatePanel(string name, Transform parent, Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static void Place(
            RectTransform rect,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetStretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void StretchHorizontal(RectTransform rect, float left, float height, float right)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -18f);
            rect.sizeDelta = new Vector2(-(left + right), height);
        }

        private static void StretchBottom(RectTransform rect, float left, float height, float right)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 18f);
            rect.sizeDelta = new Vector2(-(left + right), height);
        }

        private static string FormatPosition(Vector3 position)
        {
            return $"X {position.x:F2}    Y {position.y:F2}    Z {position.z:F2}";
        }

        private static string FormatTime(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "--" : value.Replace('T', ' ').Replace("Z", " UTC");
        }

        private static string StatusLabel(string status)
        {
            return status switch
            {
                "open" => "待处理",
                "in_progress" => "处理中",
                "resolved" => "已解决",
                _ => status
            };
        }

        private static Color StatusColor(string status)
        {
            return status switch
            {
                "open" => Danger,
                "in_progress" => new Color(1f, 0.72f, 0.3f),
                "resolved" => Success,
                _ => Muted
            };
        }

        private static string PriorityLabel(string priority)
        {
            return priority switch
            {
                "low" => "低",
                "medium" => "中",
                "high" => "高",
                _ => priority
            };
        }

        private static Color PriorityColor(string priority)
        {
            return priority switch
            {
                "low" => new Color(0.39f, 0.73f, 1f),
                "medium" => new Color(1f, 0.72f, 0.3f),
                "high" => Danger,
                _ => Muted
            };
        }
    }

    public enum ToastKind
    {
        Info,
        Success,
        Error
    }
}
