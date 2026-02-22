using System;
using System.Reflection;
using HarmonyLib;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
using BeatSaberMarkupLanguage.Settings;
using IPALogger = IPA.Logging.Logger;
using BetterMusicPacks.Configuration;

namespace BetterMusicPacks;

[Plugin(RuntimeOptions.SingleStartInit)]
public class Plugin
{
    internal static IPALogger Log { get; private set; } = null!;

        private const string HarmonyId = "com.dylan.bettermusicpacks";
        private Harmony _harmony = null!;

        [Init]
        public void Init(IPALogger logger, Config conf)
        {
            Log = logger;
            PluginConfig.Instance = conf.Generated<PluginConfig>();
        }

        [OnStart]
        public void OnStart()
        {
            _harmony = new Harmony(HarmonyId);
            if (PluginConfig.Instance.Enabled) PurgeConflictingPatches();
            _harmony.PatchAll(typeof(Plugin).Assembly);
            BeatSaberMarkupLanguage.Util.MainMenuAwaiter.MainMenuInitializing += () =>
            {
                BSMLSettings.Instance.AddSettingsMenu("BetterMusicPacks", "BetterMusicPacks.src.Resources.BSML.SettingsUI.bsml", PluginConfig.Instance);
            };
        }

        [OnExit]
        public void OnExit()
        {
            _harmony?.UnpatchSelf();
        }

        private void PurgeConflictingPatches()
        {
            try
            {
                var animatorType = typeof(AnnotatedBeatmapLevelCollectionsGridViewAnimator);
                var viewType = typeof(AnnotatedBeatmapLevelCollectionsGridView);
                var gridViewType = typeof(GridView);

                PurgeMethod(AccessTools.Method(animatorType, "AnimateOpen", new[] { typeof(bool) }));
                PurgeMethod(AccessTools.Method(animatorType, "AnimateClose", new[] { typeof(int), typeof(int), typeof(bool) }));
                PurgeMethod(AccessTools.Method(animatorType, "ScrollToRowIdxInstant", new[] { typeof(int), typeof(int) }));
                PurgeMethod(AccessTools.Method(animatorType, "GetContentXOffset"));
                PurgeMethod(AccessTools.Method(animatorType, "Init",
                    new[] { typeof(float), typeof(float), typeof(int), typeof(int), typeof(int) }));

                PurgeMethod(AccessTools.Method(gridViewType, "ReloadData"));
                PurgeMethod(AccessTools.Method(viewType, "OnPointerExit",
                    new[] { typeof(UnityEngine.EventSystems.PointerEventData) }));
            }
            catch (Exception ex)
            {
                Log.Error($"Purge error: {ex.Message}");
            }
        }

    private void PurgeMethod(MethodInfo? method)
    {
        if (method is null) return;
        if (Harmony.GetPatchInfo(method) is not { } info) return;

        void UnpatchP(System.Collections.ObjectModel.ReadOnlyCollection<Patch> patches)
        {
            foreach (var p in patches)
            {
                if (p.owner == HarmonyId) continue;
                Log.Info($"Purging {method.Name} from {p.owner}");
                _harmony.Unpatch(method, p.PatchMethod);
            }
        }

        UnpatchP(info.Prefixes);
        UnpatchP(info.Postfixes);
        UnpatchP(info.Transpilers);
    }
}