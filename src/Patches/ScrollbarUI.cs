using System;
using System.Reflection;
using HarmonyLib;
using HMUI;
using UnityEngine;
using UnityEngine.UI;
using VRUIControls;
using BetterMusicPacks.Configuration;
using UnityEngine.XR;
using Tweening;
using UnityEngine.EventSystems;

namespace BetterMusicPacks.Patches;

internal static class ScrollbarUI
{
        private const int MaxVisibleRows = 5;
        private const float ScrollbarGap = 4f;
        private const float ButtonSize = 4f;
        private const float ButtonGap = 1f;

        private static AnnotatedBeatmapLevelCollectionsGridViewAnimator? _animator;
        private static AnnotatedBeatmapLevelCollectionsGridView? _gridView;
        private static GameObject? _scrollbarObj;
        private static VerticalScrollIndicator? _indicator;
        private static RectTransform? _scrollbarRT;
        private static GameObject? _upBtnObj;
        private static GameObject? _downBtnObj;
        private static RectTransform? _upBtnRT;
        private static RectTransform? _downBtnRT;
        private static GameObject? _hoverGuardObj;
        private static RectTransform? _hoverGuardRT;
        private static Transform? _vcTransform;

        private static bool _created;
        private static bool _visible;
        public static bool IsVisible => _visible;
        internal static int ArrowClickFrame = -1;
        internal static int HoverCount = 0;

        public static readonly AccessTools.FieldRef<AnnotatedBeatmapLevelCollectionsGridViewAnimator, int> SelectedRow = AccessTools.FieldRefAccess<AnnotatedBeatmapLevelCollectionsGridViewAnimator, int>("_selectedRow");
        public static readonly AccessTools.FieldRef<AnnotatedBeatmapLevelCollectionsGridViewAnimator, int> RowCount = AccessTools.FieldRefAccess<AnnotatedBeatmapLevelCollectionsGridViewAnimator, int>("_rowCount");
        public static readonly AccessTools.FieldRef<AnnotatedBeatmapLevelCollectionsGridViewAnimator, float> RowHeight = AccessTools.FieldRefAccess<AnnotatedBeatmapLevelCollectionsGridViewAnimator, float>("_rowHeight");
        public static readonly AccessTools.FieldRef<AnnotatedBeatmapLevelCollectionsGridViewAnimator, RectTransform> ContentTransform = AccessTools.FieldRefAccess<AnnotatedBeatmapLevelCollectionsGridViewAnimator, RectTransform>("_contentTransform");
        public static readonly AccessTools.FieldRef<AnnotatedBeatmapLevelCollectionsGridViewAnimator, RectTransform> ViewportTransform = AccessTools.FieldRefAccess<AnnotatedBeatmapLevelCollectionsGridViewAnimator, RectTransform>("_viewportTransform");
        public static readonly AccessTools.FieldRef<AnnotatedBeatmapLevelCollectionsGridViewAnimator, Vector2Tween?> ViewportSizeTween = AccessTools.FieldRefAccess<AnnotatedBeatmapLevelCollectionsGridViewAnimator, Vector2Tween?>("_viewportSizeTween");
        public static readonly AccessTools.FieldRef<AnnotatedBeatmapLevelCollectionsGridViewAnimator, Vector2Tween?> ContentPositionTween = AccessTools.FieldRefAccess<AnnotatedBeatmapLevelCollectionsGridViewAnimator, Vector2Tween?>("_contentPositionTween");

        private static readonly AccessTools.FieldRef<ScrollView, Button> SV_PageUpButton = AccessTools.FieldRefAccess<ScrollView, Button>("_pageUpButton");
        private static readonly AccessTools.FieldRef<ScrollView, Button> SV_PageDownButton = AccessTools.FieldRefAccess<ScrollView, Button>("_pageDownButton");
        private static readonly AccessTools.FieldRef<VRGraphicRaycaster, VRUIControls.PhysicsRaycasterWithCache> VRGraphicRaycaster_PhysicsRaycaster = AccessTools.FieldRefAccess<VRGraphicRaycaster, VRUIControls.PhysicsRaycasterWithCache>("_physicsRaycaster");
        private static readonly MethodInfo? CloseLevelCollectionMethod = AccessTools.Method(typeof(AnnotatedBeatmapLevelCollectionsGridView), "CloseLevelCollection", new[] { typeof(bool) });

        public static void Reset()
        {
            _created = false;
            _animator = null;
            _visible = false;
            DestroyAll();
        }

        public static void EnsureCreated(AnnotatedBeatmapLevelCollectionsGridViewAnimator animator)
        {
            _animator = animator;
            if (_created) return;
            _created = true;
            var animTransform = animator.transform;
            _gridView = animTransform.GetComponentInParent<AnnotatedBeatmapLevelCollectionsGridView>();
            _vcTransform = FindViewController(animTransform);
            var buttonParent = _vcTransform ?? animTransform;

            try
            {
                CreateScrollbar(animTransform);
                CreateArrowButtons(buttonParent);
                CreateHoverGuard(_gridView, animTransform);
                if (animTransform.GetComponent<CleanupHelper>() == null) animTransform.gameObject.AddComponent<CleanupHelper>();
                if (animTransform.GetComponent<GridClickWatcher>() == null) animTransform.gameObject.AddComponent<GridClickWatcher>();
            }
            catch (Exception ex) { Plugin.Log.Error($"Create UI error: {ex}"); }
        }

        private static void DestroyAll()
        {
            SafeDestroy(ref _scrollbarObj);
            SafeDestroy(ref _upBtnObj);
            SafeDestroy(ref _downBtnObj);
            SafeDestroy(ref _hoverGuardObj);
            _indicator = null;
            _scrollbarRT = null;
            _upBtnRT = null;
            _downBtnRT = null;
            _hoverGuardRT = null;
            _vcTransform = null;
        }

        private static void SafeDestroy(ref GameObject? obj)
        {
            if (obj != null) { UnityEngine.Object.Destroy(obj); obj = null; }
        }

        public static void UpdateLayout(float viewportWidth, float viewportHeight, float viewportY, int selectedRow, int totalRows, bool open)
        {
            bool shouldShow = PluginConfig.Instance.Enabled && open && totalRows > MaxVisibleRows;
            _visible = shouldShow;

            float xCenter = viewportWidth * 0.5f + ScrollbarGap;
            float topY = viewportY + viewportHeight * 0.5f - ButtonSize * 0.5f;
            float botY = viewportY - viewportHeight * 0.5f + ButtonSize * 0.5f;
            float scrollH = Mathf.Max((topY - ButtonSize * 0.5f - ButtonGap) - (botY + ButtonSize * 0.5f + ButtonGap), 10f);
            float scrollCenterY = (topY + botY) * 0.5f;

            if (_scrollbarObj != null && _indicator != null && _scrollbarRT != null)
            {
                _scrollbarObj.SetActive(shouldShow);
                if (shouldShow)
                {
                    _scrollbarRT.anchorMin = _scrollbarRT.anchorMax = new Vector2(0.5f, 0.5f);
                    _scrollbarRT.pivot = new Vector2(0.5f, 0.5f);
                    _scrollbarRT.anchoredPosition = new Vector2(xCenter, scrollCenterY);
                    _scrollbarRT.sizeDelta = new Vector2(_scrollbarRT.sizeDelta.x, scrollH);
                    _indicator.normalizedPageHeight = (float)MaxVisibleRows / totalRows;
                    int maxFirst = totalRows - MaxVisibleRows;
                    int firstVis = Mathf.Clamp(selectedRow - 2, 0, maxFirst);
                    _indicator.progress = (float)firstVis / maxFirst;
                }
            }

            Vector2 upPosVC, downPosVC;
            if (_animator != null && _vcTransform != null)
            {
                var animT = _animator.transform;
                Vector3 upWorld = animT.TransformPoint(new Vector3(xCenter, topY, 0));
                Vector3 downWorld = animT.TransformPoint(new Vector3(xCenter, botY, 0));
                Vector3 upLocal = _vcTransform.InverseTransformPoint(upWorld);
                Vector3 downLocal = _vcTransform.InverseTransformPoint(downWorld);
                upPosVC = new Vector2(upLocal.x, upLocal.y);
                downPosVC = new Vector2(downLocal.x, downLocal.y);
            }
            else
            {
                upPosVC = new Vector2(xCenter, topY);
                downPosVC = new Vector2(xCenter, botY);
            }

            if (_upBtnObj != null && _upBtnRT != null)
            {
                _upBtnObj.SetActive(shouldShow);
                if (shouldShow)
                {
                    _upBtnRT.anchoredPosition = upPosVC;
                    var btn = _upBtnObj.GetComponent<Button>();
                    if (btn != null) btn.interactable = true;
                }
            }

            if (_downBtnObj != null && _downBtnRT != null)
            {
                _downBtnObj.SetActive(shouldShow);
                if (shouldShow)
                {
                    _downBtnRT.anchoredPosition = downPosVC;
                    var btn = _downBtnObj.GetComponent<Button>();
                    if (btn != null) btn.interactable = true;
                }
            }

            if (_hoverGuardObj != null && _hoverGuardRT != null)
            {
                _hoverGuardObj.SetActive(shouldShow);
                if (shouldShow)
                {
                    float guardLeftEdge = -viewportWidth * 0.5f - 2f;
                    float guardRightEdge = xCenter + ButtonSize * 0.5f + 2f;
                    float guardWidth = guardRightEdge - guardLeftEdge;
                    float guardCenterX = (guardLeftEdge + guardRightEdge) * 0.5f;

                    if (_animator != null && _hoverGuardRT.parent != null)
                    {
                        var animT = _animator.transform;
                        var guardParent = _hoverGuardRT.parent;
                        Vector3 centerWorld = animT.TransformPoint(new Vector3(guardCenterX, viewportY, 0f));
                        Vector3 centerLocal = guardParent.InverseTransformPoint(centerWorld);
                        _hoverGuardRT.anchoredPosition = new Vector2(centerLocal.x, centerLocal.y);
                    }
                    else _hoverGuardRT.anchoredPosition = new Vector2(guardCenterX, viewportY);
                    _hoverGuardRT.sizeDelta = new Vector2(guardWidth, viewportHeight + 6f);
                }
            }
        }

        public static void ScrollBy(int rowDelta)
        {
            if (_animator == null || !PluginConfig.Instance.Enabled) return;
            int currentRow = SelectedRow(_animator);
            int rowCount   = RowCount(_animator);
            float rowHeight = RowHeight(_animator);
            var contentRT   = ContentTransform(_animator);
            var viewportRT  = ViewportTransform(_animator);

            int targetRow = Mathf.Clamp(currentRow + rowDelta, 0, rowCount - 1);
            SelectedRow(_animator) = targetRow;

            float clampedRow = (rowCount > MaxVisibleRows) ? Mathf.Clamp(targetRow, 2, rowCount - 3) : (rowCount - 1) * 0.5f;
            contentRT.anchoredPosition = new Vector2(0f, (clampedRow - (rowCount - 1) * 0.5f) * rowHeight);

            UpdateLayout(viewportRT.sizeDelta.x, viewportRT.sizeDelta.y, viewportRT.anchoredPosition.y, targetRow, rowCount, true);
        }

        private static void CreateScrollbar(Transform parent)
        {
            var donors = Resources.FindObjectsOfTypeAll<VerticalScrollIndicator>();
            if (donors.Length == 0) return;
            VerticalScrollIndicator donor = donors[0];
            foreach (var d in donors) if (d.gameObject.activeInHierarchy) { donor = d; break; }

            _scrollbarObj = UnityEngine.Object.Instantiate(donor.gameObject, parent);
            _scrollbarObj.name = "BMP_Scrollbar";
            _indicator = _scrollbarObj.GetComponent<VerticalScrollIndicator>();
            _scrollbarRT = _scrollbarObj.GetComponent<RectTransform>();

            var img = _scrollbarObj.GetComponent<Image>() ?? _scrollbarObj.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0);
            img.raycastTarget = true;
            foreach (var g in _scrollbarObj.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = true;
            _scrollbarObj.AddComponent<UIHoverWatcher>();
            _scrollbarObj.SetActive(false);
        }

        private static void CreateArrowButtons(Transform parent)
        {
            Button? upDonor = null;
            foreach (var sv in Resources.FindObjectsOfTypeAll<ScrollView>())
            {
                var up = SV_PageUpButton(sv);
                var down = SV_PageDownButton(sv);
                if (up != null && down != null) { upDonor = up; break; }
            }
            if (upDonor == null) return;

            _upBtnObj = BuildArrowButton(upDonor.gameObject, parent, "BMP_UpBtn", () => ScrollBy(-3), false);
            _upBtnRT = _upBtnObj.GetComponent<RectTransform>();
            _downBtnObj = BuildArrowButton(upDonor.gameObject, parent, "BMP_DownBtn", () => ScrollBy(3), true);
            _downBtnRT = _downBtnObj.GetComponent<RectTransform>();
        }

        private static GameObject BuildArrowButton(GameObject donor, Transform parent, string name, Action onClick, bool rotateDown)
        {
            var go = UnityEngine.Object.Instantiate(donor, parent);
            go.name = name;
            go.SetActive(false);
            go.layer = donor.layer;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localRotation = rotateDown ? Quaternion.Euler(0, 0, 180) : Quaternion.identity;
            rt.sizeDelta = new Vector2(ButtonSize, ButtonSize);
            rt.localScale = Vector3.one;

            var canvas = go.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 30000;
            var donorCanvas = donor.GetComponentInParent<Canvas>();
            if (donorCanvas != null)
            {
                canvas.sortingLayerID = donorCanvas.sortingLayerID;
                canvas.sortingLayerName = donorCanvas.sortingLayerName;
                canvas.additionalShaderChannels = donorCanvas.additionalShaderChannels;
            }

            if (go.GetComponent<VRGraphicRaycaster>() == null)
            {
                var rc = go.AddComponent<VRGraphicRaycaster>();
                InjectPhysicsRaycaster(rc);
            }

            for (int i = 0; i < go.transform.childCount; i++)
            {
                var childRT = go.transform.GetChild(i).GetComponent<RectTransform>();
                if (childRT != null)
                {
                    childRT.anchorMin = Vector2.zero;
                    childRT.anchorMax = Vector2.one;
                    childRT.offsetMin = childRT.offsetMax = Vector2.zero;
                    childRT.localScale = Vector3.one;
                }
            }

            var btn = go.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => { ArrowClickFrame = Time.frameCount; onClick(); });
                btn.interactable = true;
            }

            foreach (var g in go.GetComponentsInChildren<Graphic>(true))
            {
                g.raycastTarget = true;
                g.SetMaterialDirty();
                g.SetVerticesDirty();
            }

            var cg = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;

            go.AddComponent<UIHoverWatcher>();

            return go;
        }

        private static void CreateHoverGuard(AnnotatedBeatmapLevelCollectionsGridView? gridView, Transform animTransform)
        {
            Transform guardParent = gridView != null ? gridView.transform : animTransform;
            _hoverGuardObj = new GameObject("BMP_HoverGuard");
            _hoverGuardObj.transform.SetParent(guardParent, false);
            _hoverGuardObj.layer = guardParent.gameObject.layer;

            var img = _hoverGuardObj.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0);
            img.raycastTarget = true;

            _hoverGuardObj.AddComponent<UIHoverWatcher>();

            _hoverGuardRT = _hoverGuardObj.GetComponent<RectTransform>();
            _hoverGuardRT.anchorMin = _hoverGuardRT.anchorMax = _hoverGuardRT.pivot = new Vector2(0.5f, 0.5f);
            _hoverGuardObj.SetActive(false);
        }

        private static Transform? FindViewController(Transform from)
        {
            Transform t = from;
            while (t != null) { if (t.GetComponent<CanvasGroup>() != null) return t; t = t.parent; }
            return null;
        }

        private static void InjectPhysicsRaycaster(VRGraphicRaycaster target)
        {
            try
            {
                foreach (var donor in Resources.FindObjectsOfTypeAll<VRGraphicRaycaster>())
                {
                    if (donor == target) continue;
                    var pr = VRGraphicRaycaster_PhysicsRaycaster(donor);
                    if (pr != null) { VRGraphicRaycaster_PhysicsRaycaster(target) = pr; return; }
                }
            }
            catch {}
        }

        public static void DespawnTween<T>(ref T? tween) where T : Tweening.Tween
        {
            if (tween != null)
            {
                tween.Kill();
                if (tween is Tweening.Vector2Tween vTween) Tweening.Vector2Tween.Pool.Despawn(vTween);
                tween = null;
            }
        }

        public static void OnCleanup()
        {
            _created = false;
            _animator = null;
            _gridView = null;
            _visible = false;
            ArrowClickFrame = -1;
            HoverCount = 0;
            _scrollbarObj = null; _indicator = null; _scrollbarRT = null;
            _upBtnObj = null; _downBtnObj = null;
            _upBtnRT = null; _downBtnRT = null;
            _hoverGuardObj = null; _hoverGuardRT = null;
        }

        public static void CloseGrid()
        {
            if (_gridView == null || !_visible) return;
            try
            {
                CloseLevelCollectionMethod?.Invoke(_gridView, new object[] { false });
            }
            catch {}
        }
    }

    public class CleanupHelper : MonoBehaviour
    {
        private void OnDestroy() => ScrollbarUI.OnCleanup();
    }

    public class GridClickWatcher : MonoBehaviour
    {
        private bool _wasLeftTrigger;
        private bool _wasRightTrigger;

        private void LateUpdate()
        {
            if (!ScrollbarUI.IsVisible || !PluginConfig.Instance.Enabled) return;

            bool pcClicked = UnityEngine.Input.GetMouseButtonDown(0) || UnityEngine.Input.GetMouseButtonDown(1);

            var leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            bool leftTrigger = false;
            bool rightTrigger = false;

            if (leftDevice.isValid) leftDevice.TryGetFeatureValue(CommonUsages.triggerButton, out leftTrigger);
            if (rightDevice.isValid) rightDevice.TryGetFeatureValue(CommonUsages.triggerButton, out rightTrigger);

            bool vrClicked = (leftTrigger && !_wasLeftTrigger) || (rightTrigger && !_wasRightTrigger);

            _wasLeftTrigger = leftTrigger;
            _wasRightTrigger = rightTrigger;

            if (pcClicked || vrClicked)
            {
                if (Time.frameCount == ScrollbarUI.ArrowClickFrame) return;
                if (ScrollbarUI.HoverCount > 0) return;
                ScrollbarUI.CloseGrid();
            }
        }
    }

    public class UIHoverWatcher : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private bool _isHovered;
        public void OnPointerEnter(PointerEventData eventData) { if (!_isHovered) { _isHovered = true; ScrollbarUI.HoverCount++; } }
        public void OnPointerExit(PointerEventData eventData) { if (_isHovered) { _isHovered = false; ScrollbarUI.HoverCount--; } }
        private void OnDisable() { if (_isHovered) { _isHovered = false; ScrollbarUI.HoverCount--; } }
        private void OnDestroy() { if (_isHovered) { _isHovered = false; ScrollbarUI.HoverCount--; } }
    }