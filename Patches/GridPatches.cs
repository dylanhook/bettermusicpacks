using System.Reflection;
using HarmonyLib;
using HMUI;
using Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using BetterMusicPacks.Configuration;

namespace BetterMusicPacks.Patches
{
    [HarmonyPatch(typeof(GridView), "ReloadData")]
    internal static class GridView_ReloadData
    {
        private static readonly FieldInfo _columnCount = AccessTools.Field(typeof(GridView), "_columnCount");
        private static readonly FieldInfo _visibleColumnCount = AccessTools.Field(typeof(GridView), "_visibleColumnCount");

        static void Prefix(GridView __instance)
        {
            if (!PluginConfig.Instance.Enabled) return;
            _columnCount.SetValue(__instance, 5);
            _visibleColumnCount.SetValue(__instance, 5);
        }

        static void Postfix(GridView __instance)
        {
            if (!PluginConfig.Instance.Enabled) return;
            var gridView = __instance.GetComponentInParent<AnnotatedBeatmapLevelCollectionsGridView>();
            if (gridView == null) return;

            var pageControl = AccessTools.Field(typeof(AnnotatedBeatmapLevelCollectionsGridView), "_pageControl").GetValue(gridView) as PageControl;
            if (pageControl != null) pageControl.gameObject.SetActive(false);
        }
    }

    [HarmonyPatch(typeof(AnnotatedBeatmapLevelCollectionsGridViewAnimator), "Init")]
    internal static class Animator_Init
    {
        static void Prefix(ref int columnCount, ref int visibleColumnCount)
        {
            if (!PluginConfig.Instance.Enabled) return;
            columnCount = 5;
            visibleColumnCount = 5;
        }

        static void Postfix() => ScrollbarUI.Reset();
    }

    [HarmonyPatch(typeof(AnnotatedBeatmapLevelCollectionsGridViewAnimator), "GetContentXOffset")]
    internal static class Animator_GetContentXOffset
    {
        static bool Prefix(ref float __result)
        {
            if (!PluginConfig.Instance.Enabled) return true;
            __result = 0f;
            return false;
        }
    }

    [HarmonyPatch(typeof(AnnotatedBeatmapLevelCollectionsGridViewAnimator), "ScrollToRowIdxInstant")]
    internal static class Animator_ScrollToRowIdxInstant
    {
        static bool Prefix(AnnotatedBeatmapLevelCollectionsGridViewAnimator __instance, int selectedColumn, int selectedRow, RectTransform ____contentTransform, float ____rowHeight, int ____rowCount)
        {
            if (!PluginConfig.Instance.Enabled) return true;
            AccessTools.Field(typeof(AnnotatedBeatmapLevelCollectionsGridViewAnimator), "_selectedColumn").SetValue(__instance, selectedColumn);
            AccessTools.Field(typeof(AnnotatedBeatmapLevelCollectionsGridViewAnimator), "_selectedRow").SetValue(__instance, selectedRow);

            float yOffset = ((float)selectedRow - (float)(____rowCount - 1) * 0.5f) * ____rowHeight;
            ____contentTransform.anchoredPosition = new Vector2(0f, yOffset);
            return false;
        }
    }

    [HarmonyPatch(typeof(AnnotatedBeatmapLevelCollectionsGridViewAnimator), "AnimateOpen")]
    internal static class Animator_AnimateOpen
    {
        static bool Prefix(AnnotatedBeatmapLevelCollectionsGridViewAnimator __instance, bool animated, RectTransform ____viewportTransform, RectTransform ____contentTransform, float ____rowHeight, int ____selectedRow, int ____rowCount, float ____transitionDuration, EaseType ____easeType, TimeTweeningManager ____tweeningManager, ref Vector2Tween? ____viewportSizeTween, ref Vector2Tween? ____contentPositionTween)
        {
            if (!PluginConfig.Instance.Enabled) return true;
            if (____rowCount <= 1) return false;

            ScrollbarUI.DespawnTween(ref ____viewportSizeTween);
            ScrollbarUI.DespawnTween(ref ____contentPositionTween);
            ScrollbarUI.EnsureCreated(__instance);

            Vector2 curVSize = ____viewportTransform.sizeDelta;
            Vector2 curCPos  = ____contentTransform.anchoredPosition;

            int visibleRows = Mathf.Min(____rowCount, 5);
            float targetHeight = ____rowHeight * visibleRows;
            float heightDiff = targetHeight - ____rowHeight;

            Vector2 tgtVSize = new Vector2(curVSize.x, targetHeight);
            float clampedRow = (____rowCount > 5) ? Mathf.Clamp(____selectedRow, 2, ____rowCount - 3) : (____rowCount - 1) * 0.5f;

            Vector2 tgtCPos = new Vector2(0f, (clampedRow - (____rowCount - 1) * 0.5f) * ____rowHeight);
            ____viewportTransform.anchoredPosition = new Vector2(____viewportTransform.anchoredPosition.x, -heightDiff * 0.5f);

            ScrollbarUI.UpdateLayout(curVSize.x, targetHeight, -heightDiff * 0.5f, ____selectedRow, ____rowCount, true);

            if (!animated || ____tweeningManager == null)
            {
                ____viewportTransform.sizeDelta = tgtVSize;
                ____contentTransform.anchoredPosition = tgtCPos;
                return false;
            }

            var sizeTween = Vector2Tween.Pool.Spawn(curVSize, tgtVSize, v => ____viewportTransform.sizeDelta = v, ____transitionDuration, ____easeType, 0f);
            sizeTween.onCompleted = () =>
            {
                var t = ScrollbarUI.ViewportSizeTween.GetValue(__instance) as Vector2Tween;
                if (t != null) { Vector2Tween.Pool.Despawn(t); ScrollbarUI.ViewportSizeTween.SetValue(__instance, null); }
            };
            ____viewportSizeTween = sizeTween;

            var posTween = Vector2Tween.Pool.Spawn(curCPos, tgtCPos, v => ____contentTransform.anchoredPosition = v, ____transitionDuration, ____easeType, 0f);
            posTween.onCompleted = () =>
            {
                var t = ScrollbarUI.ContentPositionTween.GetValue(__instance) as Vector2Tween;
                if (t != null) { Vector2Tween.Pool.Despawn(t); ScrollbarUI.ContentPositionTween.SetValue(__instance, null); }
            };
            ____contentPositionTween = posTween;

            ____tweeningManager.RestartTween(sizeTween, __instance);
            ____tweeningManager.RestartTween(posTween, __instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(AnnotatedBeatmapLevelCollectionsGridViewAnimator), "AnimateClose")]
    internal static class Animator_AnimateClose
    {
        static bool Prefix(AnnotatedBeatmapLevelCollectionsGridViewAnimator __instance, int selectedColumn, int selectedRow, ref bool animated, RectTransform ____viewportTransform, RectTransform ____contentTransform, float ____columnWidth, float ____rowHeight, int ____visibleColumnCount, float ____transitionDuration, EaseType ____easeType, TimeTweeningManager ____tweeningManager, ref Vector2Tween? ____viewportSizeTween, ref Vector2Tween? ____contentPositionTween)
        {
            if (!PluginConfig.Instance.Enabled) return true;
            animated = false;
            AccessTools.Field(typeof(AnnotatedBeatmapLevelCollectionsGridViewAnimator), "_selectedColumn").SetValue(__instance, selectedColumn);
            AccessTools.Field(typeof(AnnotatedBeatmapLevelCollectionsGridViewAnimator), "_selectedRow").SetValue(__instance, selectedRow);

            ScrollbarUI.DespawnTween(ref ____viewportSizeTween);
            ScrollbarUI.DespawnTween(ref ____contentPositionTween);
            ScrollbarUI.UpdateLayout(0, 0, 0, selectedRow, 0, false);

            ____viewportTransform.anchoredPosition = new Vector2(____viewportTransform.anchoredPosition.x, 0f);

            Vector2 curVSize = ____viewportTransform.sizeDelta;
            Vector2 curCPos  = ____contentTransform.anchoredPosition;

            Vector2 tgtVSize = new Vector2((float)____visibleColumnCount * ____columnWidth, ____rowHeight);
            int rowCount = (int)ScrollbarUI.RowCount.GetValue(__instance);
            float yOffset = ((float)selectedRow - (float)(rowCount - 1) * 0.5f) * ____rowHeight;
            Vector2 tgtCPos = new Vector2(0f, yOffset);

            if (!animated || ____tweeningManager == null)
            {
                ____viewportTransform.sizeDelta = tgtVSize;
                ____contentTransform.anchoredPosition = tgtCPos;
                return false;
            }

            var sizeTween = Vector2Tween.Pool.Spawn(curVSize, tgtVSize, v => ____viewportTransform.sizeDelta = v, ____transitionDuration, ____easeType, 0f);
            sizeTween.onCompleted = () =>
            {
                var t = ScrollbarUI.ViewportSizeTween.GetValue(__instance) as Vector2Tween;
                if (t != null) { Vector2Tween.Pool.Despawn(t); ScrollbarUI.ViewportSizeTween.SetValue(__instance, null); }
            };
            ____viewportSizeTween = sizeTween;

            var posTween = Vector2Tween.Pool.Spawn(curCPos, tgtCPos, v => ____contentTransform.anchoredPosition = v, ____transitionDuration, ____easeType, 0f);
            posTween.onCompleted = () =>
            {
                var t = ScrollbarUI.ContentPositionTween.GetValue(__instance) as Vector2Tween;
                if (t != null) { Vector2Tween.Pool.Despawn(t); ScrollbarUI.ContentPositionTween.SetValue(__instance, null); }
            };
            ____contentPositionTween = posTween;

            ____tweeningManager.RestartTween(sizeTween, __instance);
            ____tweeningManager.RestartTween(posTween, __instance);
            return false;
        }
    }


    [HarmonyPatch(typeof(AnnotatedBeatmapLevelCollectionsGridView), "OnPointerExit")]
    internal static class GridView_OnPointerExit
    {
        static bool Prefix()
        {
            if (!PluginConfig.Instance.Enabled) return true;
            return !ScrollbarUI.IsVisible;
        }
    }
}
