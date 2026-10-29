using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Doozy.Runtime.UIManager.Components;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using BelmontsCurseMasterOptimizer.Patches;

namespace BelmontsCurseMasterOptimizer.UI
{
    public static class VideoOptions
    {
        private const string Table = "UI_Settings";
        private const string TemplatePath = "video/antialiasing";

        public static readonly System.Collections.Generic.List<VideoOption> All = new System.Collections.Generic.List<VideoOption>
        {
            new VideoOption
            {
                Path = "video/optimizermenu",
                Key = "DeckFix_SubMenu",
                Title = "Master Optimizer",
                Choices = new (string, string, int)[3]
                {
                    ("DeckFix_SubMenu_Std", "Standard Video", 0),
                    ("DeckFix_SubMenu_Gfx", "Graphics & Display", 1),
                    ("DeckFix_SubMenu_Sys", "Performance & Engine", 2)
                },
                Default = () => (Plugin.OptimizerMenuPageConfig != null) ? Math.Clamp(Plugin.OptimizerMenuPageConfig.Value, 0, 2) : 0,
                Apply = (int v) =>
                {
                    v = Math.Clamp(v, 0, 2);
                    CurrentPage = v;
                    if (Plugin.OptimizerMenuPageConfig != null) Plugin.OptimizerMenuPageConfig.Value = v;
                    Plugin.Instance?.Config.Save();
                    ApplyMenuVisibility();
                }
            },
            new VideoOption
            {
                Path = "video/quality",
                Key = "DeckFix_Quality",
                Title = "Graphics Quality",
                Choices = new (string, string, int)[3]
                {
                    ("DeckFix_Quality_Low", "Low", 0),
                    ("DeckFix_Quality_Medium", "Medium", 1),
                    ("DeckFix_Quality_High", "High", 2)
                },
                Default = () => (Plugin.QualityPresetConfig != null) ? Math.Clamp(Plugin.QualityPresetConfig.Value, 0, 2) : 2,
                Apply = (int v) =>
                {
                    v = Math.Clamp(v, 0, ((Il2CppArrayBase<string>)(object)QualitySettings.names).Length - 1);
                    if (QualitySettings.GetQualityLevel() != v)
                    {
                        int vSyncCount = QualitySettings.vSyncCount;
                        int targetFrameRate = Application.targetFrameRate;
                        QualitySettings.SetQualityLevel(v, true);
                        QualitySettings.vSyncCount = vSyncCount;
                        Application.targetFrameRate = targetFrameRate;
                        CullingAndCPUPatches.EnsureSRPBatcher();
                        ReapplyFrames = 3;
                    }
                    if (Plugin.QualityPresetConfig != null) Plugin.QualityPresetConfig.Value = v;
                    Plugin.Instance?.Config.Save();
                }
            },
            new VideoOption
            {
                Path = "video/renderscale3d",
                Key = "DeckFix_RenderScale3D",
                Title = "3D Resolution (UI stays native)",
                Choices = RenderScaleDecoupler.Percents.Select((p, i) => ($"DeckFix_RenderScale3D_{p}", p == 100 ? "100% (Native)" : $"{p}%", i)).ToArray(),
                Default = () => RenderScaleDecoupler.PercentToIndex(Plugin.RenderScale3DConfig != null ? Plugin.RenderScale3DConfig.Value : 100),
                Apply = (int v) =>
                {
                    int percent = RenderScaleDecoupler.Percents[Math.Clamp(v, 0, RenderScaleDecoupler.Percents.Length - 1)];
                    if (Plugin.RenderScale3DConfig != null) Plugin.RenderScale3DConfig.Value = percent;
                    Plugin.Instance?.Config.Save();
                    RenderScaleDecoupler.SetUserPercent(percent);
                }
            },
            new VideoOption
            {
                Path = "video/particledensity",
                Key = "DeckFix_ParticleDensity",
                Title = "Particle Density",
                Choices = new (string, string, int)[3]
                {
                    ("DeckFix_Particles_100", "100% (Full)", 0),
                    ("DeckFix_Particles_60", "60% (Optimized)", 1),
                    ("DeckFix_Particles_30", "30% (Low)", 2)
                },
                Default = () => (Plugin.ParticleDensityConfig != null) ? Math.Clamp(Plugin.ParticleDensityConfig.Value, 0, 2) : 1,
                Apply = (int v) =>
                {
                    v = Math.Clamp(v, 0, 2);
                    if (Plugin.ParticleDensityConfig != null) Plugin.ParticleDensityConfig.Value = v;
                    Plugin.Instance?.Config.Save();
                    ParticlePatches.ApplyToAllActiveParticles();
                }
            },
            new VideoOption
            {
                Path = "video/waterreflections",
                Key = "DeckFix_Reflections",
                Title = "Water Reflections",
                Choices = new (string, string, int)[3]
                {
                    ("DeckFix_Refl_Opt", "Optimized (60 FPS)", 0),
                    ("DeckFix_Refl_Skip", "Frame-Skip (30 FPS)", 1),
                    ("DeckFix_Refl_Off", "Off", 2)
                },
                Default = () => Math.Clamp(Plugin.ReflectionModeConfig.Value, 0, 2),
                Apply = (int v) =>
                {
                    WaterReflectionPatches.ReflectionMode = Math.Clamp(v, 0, 2);
                    WaterReflectionPatches.ReapplyAllWaterSettings();
                    if (Plugin.ReflectionModeConfig != null) Plugin.ReflectionModeConfig.Value = WaterReflectionPatches.ReflectionMode;
                    Plugin.Instance?.Config.Save();
                }
            },
            new VideoOption
            {
                Path = "video/fastmap",
                Key = "DeckFix_FastMap",
                Title = "Fast Map Screen",
                Choices = new (string, string, int)[2]
                {
                    ("DeckFix_FastMap_On", "Enabled (No Lag)", 0),
                    ("DeckFix_FastMap_Off", "Disabled", 1)
                },
                Default = () => Plugin.FastMapConfig.Value ? 0 : 1,
                Apply = (int v) =>
                {
                    MapPatches.FastMapEnabled = (v == 0);
                    if (Plugin.FastMapConfig != null) Plugin.FastMapConfig.Value = MapPatches.FastMapEnabled;
                    Plugin.Instance?.Config.Save();
                }
            },
            new VideoOption
            {
                Path = "video/zoomoptimizer",
                Key = "DeckFix_ZoomOptimizer",
                Title = "Wide View Optimizer",
                Choices = new (string, string, int)[2]
                {
                    ("DeckFix_Zoom_On", "Enabled", 0),
                    ("DeckFix_Zoom_Off", "Disabled", 1)
                },
                Default = () => Plugin.ZoomOptimizerConfig.Value ? 0 : 1,
                Apply = (int v) =>
                {
                    CameraZoomPatches.ZoomOptimizerEnabled = (v == 0);
                    if (Plugin.ZoomOptimizerConfig != null) Plugin.ZoomOptimizerConfig.Value = CameraZoomPatches.ZoomOptimizerEnabled;
                    Plugin.Instance?.Config.Save();
                }
            },
            new VideoOption
            {
                Path = "video/cpuculling",
                Key = "DeckFix_CpuCulling",
                Title = "Off-Screen CPU Culling",
                Choices = new (string, string, int)[2]
                {
                    ("DeckFix_Culling_On", "Enabled", 0),
                    ("DeckFix_Culling_Off", "Disabled", 1)
                },
                Default = () => Plugin.CpuCullingConfig.Value ? 0 : 1,
                Apply = (int v) =>
                {
                    CullingAndCPUPatches.CpuCullingEnabled = (v == 0);
                    if (Plugin.CpuCullingConfig != null) Plugin.CpuCullingConfig.Value = CullingAndCPUPatches.CpuCullingEnabled;
                    Plugin.Instance?.Config.Save();
                }
            },
            new VideoOption
            {
                Path = "video/stutterfix",
                Key = "DeckFix_StutterFix",
                Title = "Stutter & Traversal Fix",
                Choices = new (string, string, int)[2]
                {
                    ("DeckFix_Stutter_On", "Enabled", 0),
                    ("DeckFix_Stutter_Off", "Disabled", 1)
                },
                Default = () => Plugin.StutterFixConfig.Value ? 0 : 1,
                Apply = (int v) =>
                {
                    StutterAndGCPatches.StutterFixEnabled = (v == 0);
                    if (Plugin.StutterFixConfig != null) Plugin.StutterFixConfig.Value = StutterAndGCPatches.StutterFixEnabled;
                    Plugin.Instance?.Config.Save();
                }
            }
        };

        internal static bool Registered;
        internal static int ReapplyFrames = -1;
        internal static VideoSettings Video;

        internal static int CurrentPage = 0;
        private static GameObject MenuSelectorRow = null;
        private static readonly System.Collections.Generic.List<GameObject> VanillaRows = new System.Collections.Generic.List<GameObject>();
        private static readonly System.Collections.Generic.List<GameObject> GraphicsRows = new System.Collections.Generic.List<GameObject>();
        private static readonly System.Collections.Generic.List<GameObject> EngineRows = new System.Collections.Generic.List<GameObject>();

        private static readonly System.Collections.Generic.HashSet<string> GraphicsPaths = new System.Collections.Generic.HashSet<string>
        {
            "video/quality",
            "video/renderscale3d",
            "video/particledensity",
            "video/waterreflections"
        };

        private static readonly System.Collections.Generic.HashSet<string> EnginePaths = new System.Collections.Generic.HashSet<string>
        {
            "video/fastmap",
            "video/zoomoptimizer",
            "video/cpuculling",
            "video/stutterfix"
        };

        public static void ApplyMenuVisibility()
        {
            try
            {
                if (MenuSelectorRow == null) return;

                bool showVanilla = (CurrentPage == 0);
                bool showGraphics = (CurrentPage == 1);
                bool showEngine = (CurrentPage == 2);

                for (int i = 0; i < VanillaRows.Count; i++)
                {
                    var r = VanillaRows[i];
                    if (r != null) r.SetActive(showVanilla);
                }

                for (int i = 0; i < GraphicsRows.Count; i++)
                {
                    var r = GraphicsRows[i];
                    if (r != null) r.SetActive(showGraphics);
                }

                for (int i = 0; i < EngineRows.Count; i++)
                {
                    var r = EngineRows[i];
                    if (r != null) r.SetActive(showEngine);
                }

                if (MenuSelectorRow != null)
                {
                    MenuSelectorRow.SetActive(true);
                }

                RebuildNavigation();
            }
            catch (System.Exception ex)
            {
                Plugin.L?.LogWarning($"[VideoOptions] ApplyMenuVisibility error: {ex.Message}");
            }
        }

        public static void RebuildNavigation()
        {
            try
            {
                if (MenuSelectorRow == null) return;

                VerticalNavigationGroup navGroup = MenuSelectorRow.GetComponentInParent<VerticalNavigationGroup>(true);
                if (navGroup == null) return;

                Transform parent = MenuSelectorRow.transform.parent;
                if (parent == null) return;

                var activeSelectables = new System.Collections.Generic.List<UISelectable>();
                for (int i = 0; i < parent.childCount; i++)
                {
                    Transform child = parent.GetChild(i);
                    if (child != null && child.gameObject != null && child.gameObject.activeSelf)
                    {
                        UISelectable sel = child.GetComponent<UISelectable>();
                        if (sel != null)
                        {
                            activeSelectables.Add(sel);
                        }
                    }
                }

                Il2CppReferenceArray<UISelectable> array = new Il2CppReferenceArray<UISelectable>((long)activeSelectables.Count);
                for (int i = 0; i < activeSelectables.Count; i++)
                {
                    ((Il2CppArrayBase<UISelectable>)(object)array)[i] = activeSelectables[i];
                }

                navGroup.selectables = array;
                ((UINavigationGroup)navGroup).UpdateNavigation();
            }
            catch (System.Exception ex)
            {
                Plugin.L?.LogWarning($"[VideoOptions] RebuildNavigation error: {ex.Message}");
            }
        }

        internal static void ReapplyGameFrameSettings()
        {
            if ((UnityEngine.Object)(object)Video == (UnityEngine.Object)null)
            {
                return;
            }
            try
            {
                if (Video._vsync != null)
                {
                    Video.OnVSyncChanged((Setting<bool>)(object)Video._vsync);
                }
                if (Video._maxFPS != null)
                {
                    Video.OnFPSChanged((Setting<int>)(object)Video._maxFPS);
                }
            }
            catch (System.Exception ex)
            {
                Plugin.L.LogWarning("Reapply frame settings: " + ex.Message);
            }
        }

        private static LocalizedString Loc(string key)
        {
            TableReference tr = "UI_Settings";
            TableEntryReference ter = key;
            return new LocalizedString(tr, ter);
        }

        private static void AddStrings()
        {
            var enumerator = LocalizationSettings.AvailableLocales.Locales.GetEnumerator();
            while (enumerator.MoveNext())
            {
                UnityEngine.Localization.Locale current = enumerator.Current;
                try
                {
                    TableReference tr = "UI_Settings";
                    StringTable table = ((LocalizedDatabase<StringTable, StringTableEntry>)(object)LocalizationSettings.StringDatabase).GetTable(tr, current);
                    if ((UnityEngine.Object)(object)table == (UnityEngine.Object)null)
                    {
                        continue;
                    }
                    foreach (VideoOption item in All)
                    {
                        if (((DetailedLocalizationTable<StringTableEntry>)(object)table).GetEntry(item.Key) == null)
                        {
                            ((DetailedLocalizationTable<StringTableEntry>)(object)table).AddEntry(item.Key, item.Title);
                        }
                        (string, string, int)[] choices = item.Choices;
                        for (int i = 0; i < choices.Length; i++)
                        {
                            (string, string, int) tuple = choices[i];
                            if (((DetailedLocalizationTable<StringTableEntry>)(object)table).GetEntry(tuple.Item1) == null)
                            {
                                ((DetailedLocalizationTable<StringTableEntry>)(object)table).AddEntry(tuple.Item1, tuple.Item2);
                            }
                        }
                    }

                    // Register custom strings for Steam Deck 40 FPS & 45 FPS settings
                    if (((DetailedLocalizationTable<StringTableEntry>)(object)table).GetEntry("fps_40") == null)
                    {
                        ((DetailedLocalizationTable<StringTableEntry>)(object)table).AddEntry("fps_40", "40 FPS");
                    }
                    if (((DetailedLocalizationTable<StringTableEntry>)(object)table).GetEntry("fps_45") == null)
                    {
                        ((DetailedLocalizationTable<StringTableEntry>)(object)table).AddEntry("fps_45", "45 FPS");
                    }
                }
                catch (System.Exception ex)
                {
                    Plugin.L.LogWarning($"Option strings for {(current != null ? current.ToString() : null)}: {ex.Message}");
                }
            }
        }

        internal static void InjectSteamDeckFPSOptions(EnumSetting maxFpsSetting)
        {
            if (maxFpsSetting == null || maxFpsSetting._options == null) return;

            try
            {
                var existing = maxFpsSetting._options;
                bool has40 = false;
                bool has45 = false;
                for (int i = 0; i < existing.Length; i++)
                {
                    var opt = ((Il2CppArrayBase<EnumSetting.EnumSettingOption>)(object)existing)[i];
                    if (opt != null)
                    {
                        if (Mathf.Approximately(opt.data, 40f)) has40 = true;
                        if (Mathf.Approximately(opt.data, 45f)) has45 = true;
                    }
                }

                if (has40 && has45) return;

                var list = new System.Collections.Generic.List<EnumSetting.EnumSettingOption>();
                for (int i = 0; i < existing.Length; i++)
                {
                    list.Add(((Il2CppArrayBase<EnumSetting.EnumSettingOption>)(object)existing)[i]);
                }

                if (!has40)
                {
                    var opt40 = new EnumSetting.EnumSettingOption
                    {
                        id = "video/maxfps/40",
                        data = 40.0f,
                        supported = true,
                        name = Loc("fps_40")
                    };
                    list.Add(opt40);
                }

                if (!has45)
                {
                    var opt45 = new EnumSetting.EnumSettingOption
                    {
                        id = "video/maxfps/45",
                        data = 45.0f,
                        supported = true,
                        name = Loc("fps_45")
                    };
                    list.Add(opt45);
                }

                // Sort: 30, 40, 45, 60, 120... (unlimited <= 0 at the end)
                list.Sort((a, b) =>
                {
                    float dataA = a.data <= 0f ? 99999f : a.data;
                    float dataB = b.data <= 0f ? 99999f : b.data;
                    return dataA.CompareTo(dataB);
                });

                for (int i = 0; i < list.Count; i++)
                {
                    list[i].value = i;
                }

                var newArray = new Il2CppReferenceArray<EnumSetting.EnumSettingOption>((long)list.Count);
                for (int i = 0; i < list.Count; i++)
                {
                    ((Il2CppArrayBase<EnumSetting.EnumSettingOption>)(object)newArray)[i] = list[i];
                }

                maxFpsSetting._options = newArray;
                Plugin.L.LogInfo("[FPS] Successfully injected 40 FPS and 45 FPS options into Max FPS setting.");
            }
            catch (System.Exception ex)
            {
                Plugin.L.LogWarning("[FPS] Failed to inject Steam Deck FPS options: " + ex.Message);
            }
        }

        internal static VideoOption Find(string path)
        {
            return All.FirstOrDefault(o => o.Path == path && o.Setting != null);
        }

        private static Il2CppSystem.Action<UnityEngine.Localization.Locale> _pinnedLocaleChangedDelegate;
        private static float _localeRetryTimer = 0f;
        private static bool _localeHooked = false;

        private static void OnLocaleChanged(UnityEngine.Localization.Locale newLocale)
        {
            AddStrings();
            _localeRetryTimer = 2.5f; // Poll for 2.5s while new locale string tables load asynchronously
        }

        public static void PollPendingLocaleStrings(float unscaledDeltaTime)
        {
            if (_localeRetryTimer <= 0f) return;
            _localeRetryTimer -= unscaledDeltaTime;
            AddStrings();
        }

        internal static void Register(VideoSettings vs)
        {
            if (Registered)
            {
                return;
            }
            if (!_localeHooked)
            {
                try
                {
                    _pinnedLocaleChangedDelegate = (Il2CppSystem.Action<UnityEngine.Localization.Locale>)new Action<UnityEngine.Localization.Locale>(OnLocaleChanged);
                    LocalizationSettings.add_SelectedLocaleChanged(_pinnedLocaleChangedDelegate);
                    _localeHooked = true;
                }
                catch (Exception ex)
                {
                    Plugin.L.LogWarning("[VideoOptions] Failed to subscribe to SelectedLocaleChanged: " + ex.Message);
                }
            }
            Video = vs;
            AddStrings();
            if (vs._maxFPS != null)
            {
                InjectSteamDeckFPSOptions(vs._maxFPS);
            }
            System.Collections.Generic.List<EnumSetting> list = ((System.Collections.Generic.IEnumerable<EnumSetting>)((CommonSettings)vs)._enumSettings).ToList();
            foreach (VideoOption item in All)
            {
                EnumSetting val = new EnumSetting();
                ((Setting<int>)(object)val)._id = item.Path;
                ((Setting<int>)(object)val)._name = Loc(item.Key);
                ((Setting<int>)(object)val)._description = new LocalizedString();
                Il2CppReferenceArray<EnumSetting.EnumSettingOption> val2 = new Il2CppReferenceArray<EnumSetting.EnumSettingOption>((long)item.Choices.Length);
                for (int i = 0; i < item.Choices.Length; i++)
                {
                    (string, string, int) tuple = item.Choices[i];
                    EnumSetting.EnumSettingOption val3 = new EnumSetting.EnumSettingOption
                    {
                        id = tuple.Item1,
                        value = i,
                        data = tuple.Item3,
                        supported = true,
                        name = Loc(tuple.Item1)
                    };
                    ((Il2CppArrayBase<EnumSetting.EnumSettingOption>)(object)val2)[i] = val3;
                }
                val._options = val2;
                int configValue = Math.Clamp(item.Default(), 0, item.Choices.Length - 1);
                ((Setting<int>)(object)val).SetDefaultValue(configValue);
                ((Setting<int>)(object)val).Load();
                // Enforce BepInEx .cfg as authoritative on startup so stale PlayerPrefs never overwrite user edits
                ((Setting<int>)(object)val).value = configValue;
                ((Setting<int>)(object)val).Save();
                item.Setting = val;
                list.Add(val);
                Plugin.L.LogInfo($"Video option '{item.Title}' registered, value: {item.Choices[Math.Clamp(((Setting<int>)(object)val).value, 0, item.Choices.Length - 1)].text}");
            }
            Il2CppReferenceArray<EnumSetting> val5 = new Il2CppReferenceArray<EnumSetting>((long)list.Count);
            for (int j = 0; j < list.Count; j++)
            {
                ((Il2CppArrayBase<EnumSetting>)(object)val5)[j] = list[j];
            }
            ((CommonSettings)vs)._enumSettings = val5;
            Registered = true;
            ApplyAll();
        }

        internal static void ApplyAll()
        {
            foreach (VideoOption item in All)
            {
                if (item.Setting != null && ((Setting<int>)(object)item.Setting).value != item.Applied)
                {
                    item.Applied = ((Setting<int>)(object)item.Setting).value;
                    item.Apply(((Setting<int>)(object)item.Setting).value);
                    Plugin.L.LogInfo($"{item.Title} -> {item.Choices[Math.Clamp(item.Applied, 0, item.Choices.Length - 1)].text}");
                }
            }
        }

        internal static void AddRows(SettingsViewBase view, bool late = false)
        {
            if (!Registered || (UnityEngine.Object)(object)((Il2CppObjectBase)view).TryCast<VideoSettingsView>() == (UnityEngine.Object)null)
            {
                return;
            }
            Il2CppArrayBase<UIDropdownEnumSettingBinding> componentsInChildren = ((Component)view).GetComponentsInChildren<UIDropdownEnumSettingBinding>(true);
            UIDropdownEnumSettingBinding val = ((System.Collections.Generic.IEnumerable<UIDropdownEnumSettingBinding>)componentsInChildren).FirstOrDefault(b => ((UISettingBinding<EnumSetting, int>)(object)b).settingPath == "video/antialiasing") ?? ((System.Collections.Generic.IEnumerable<UIDropdownEnumSettingBinding>)componentsInChildren).FirstOrDefault(b => ((UISettingBinding<EnumSetting, int>)(object)b).settingPath == "video/maxfps");
            if ((UnityEngine.Object)(object)val == (UnityEngine.Object)null)
            {
                Plugin.L.LogWarning("Video options: no template row found");
                return;
            }

            VanillaRows.Clear();
            GraphicsRows.Clear();
            EngineRows.Clear();

            var allModPaths = new System.Collections.Generic.HashSet<string>(All.Select(a => a.Path));
            for (int i = 0; i < componentsInChildren.Length; i++)
            {
                var binding = componentsInChildren[i];
                if (binding != null && ((Component)binding).gameObject != null)
                {
                    string path = ((UISettingBinding<EnumSetting, int>)(object)binding).settingPath;
                    if (!allModPaths.Contains(path))
                    {
                        VanillaRows.Add(((Component)binding).gameObject);
                    }
                }
            }

            GameObject prevRow = ((Component)val).gameObject;
            foreach (VideoOption opt in All)
            {
                UIDropdownEnumSettingBinding val3 = ((System.Collections.Generic.IEnumerable<UIDropdownEnumSettingBinding>)componentsInChildren).FirstOrDefault(b => ((UISettingBinding<EnumSetting, int>)(object)b).settingPath == opt.Path);
                GameObject currentRow;
                if ((UnityEngine.Object)(object)val3 != (UnityEngine.Object)null)
                {
                    currentRow = ((Component)val3).gameObject;
                }
                else
                {
                    currentRow = UnityEngine.Object.Instantiate<GameObject>(((Component)val).gameObject, ((Component)val).transform.parent);
                    ((UnityEngine.Object)currentRow).name = "SettingsDisplay_" + opt.Key;
                    currentRow.transform.SetSiblingIndex(prevRow.transform.GetSiblingIndex() + 1);
                    UIDropdownEnumSettingBinding component = currentRow.GetComponent<UIDropdownEnumSettingBinding>();
                    ((UISettingBinding<EnumSetting, int>)(object)component).settingPath = opt.Path;
                    AddToNavigation(prevRow, currentRow);
                    if (late && view._settingEntryViews != null)
                    {
                        view._settingEntryViews.Add(((Il2CppObjectBase)component).Cast<IUISetting>());
                        ((UISettingBinding<EnumSetting, int>)(object)component).BindToSetting();
                        ((UISettingBinding<EnumSetting, int>)(object)component).RegisterLastSavedValue();
                    }
                    Plugin.L.LogInfo($"'{opt.Title}' row added to the video settings (late={late})");
                }

                if (opt.Path == "video/optimizermenu")
                {
                    MenuSelectorRow = currentRow;
                    currentRow.transform.SetSiblingIndex(0); // Pin to the top!
                }
                else if (GraphicsPaths.Contains(opt.Path))
                {
                    GraphicsRows.Add(currentRow);
                }
                else if (EnginePaths.Contains(opt.Path))
                {
                    EngineRows.Add(currentRow);
                }

                prevRow = currentRow;
            }

            CurrentPage = (Plugin.OptimizerMenuPageConfig != null) ? Math.Clamp(Plugin.OptimizerMenuPageConfig.Value, 0, 2) : 0;
            ApplyMenuVisibility();
        }

        private static void AddToNavigation(GameObject previous, GameObject row)
        {
            VerticalNavigationGroup componentInParent = previous.GetComponentInParent<VerticalNavigationGroup>(true);
            UISelectable component = previous.GetComponent<UISelectable>();
            UISelectable component2 = row.GetComponent<UISelectable>();
            if ((UnityEngine.Object)(object)componentInParent == (UnityEngine.Object)null || (UnityEngine.Object)(object)component == (UnityEngine.Object)null || (UnityEngine.Object)(object)component2 == (UnityEngine.Object)null || componentInParent.selectables == null)
            {
                Plugin.L.LogWarning($"Row navigation: nav={(UnityEngine.Object)(object)componentInParent != (UnityEngine.Object)null} prev={(UnityEngine.Object)(object)component != (UnityEngine.Object)null} row={(UnityEngine.Object)(object)component2 != (UnityEngine.Object)null}");
                return;
            }
            System.Collections.Generic.List<UISelectable> list = new System.Collections.Generic.List<UISelectable>();
            bool inserted = false;
            foreach (UISelectable item in (Il2CppArrayBase<UISelectable>)(object)componentInParent.selectables)
            {
                list.Add(item);
                if (!inserted && (UnityEngine.Object)(object)item != (UnityEngine.Object)null && ((Il2CppObjectBase)item).Pointer == ((Il2CppObjectBase)component).Pointer)
                {
                    list.Add(component2);
                    inserted = true;
                }
            }
            if (!inserted)
            {
                list.Add(component2);
            }
            Il2CppReferenceArray<UISelectable> val2 = new Il2CppReferenceArray<UISelectable>((long)list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                ((Il2CppArrayBase<UISelectable>)(object)val2)[i] = list[i];
            }
            componentInParent.selectables = val2;
            ((UINavigationGroup)componentInParent).UpdateNavigation();
        }
    }
}
