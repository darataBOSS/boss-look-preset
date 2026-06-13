using UnityEditor;
using UnityEngine;
#if BOSS_LOOK_PRESET_HAS_PPV2
using UnityEngine.Rendering.PostProcessing;
#endif

namespace DarataBOSS.BOSSLookPreset.Editor.Ops
{
    /// <summary>
    /// Built-in RP path for Module C, backed by Post Processing Stack v2.
    /// Creates a Profile asset, a global PostProcessVolume in the scene, and
    /// adds a PostProcessLayer to the main camera. Initialises the Layer with
    /// PostProcessResources auto-discovered from the project.
    /// </summary>
    public static class PostProcessOpsBuiltin
    {
        public const string DefaultPostLayerName = "PostProcessing";

        public static bool IsAvailable
        {
            get
            {
#if BOSS_LOOK_PRESET_HAS_PPV2
                return true;
#else
                return false;
#endif
            }
        }

        public static void Setup(BOSSLookPreset preset)
        {
            if (preset == null) return;
#if !BOSS_LOOK_PRESET_HAS_PPV2
            EditorUtility.DisplayDialog("BOSS Look Preset",
                "Post Processing Stack v2 が見つかりません。Package Manager で com.unity.postprocessing を追加してください。",
                "OK");
            return;
#else
            string profilePath = $"{preset.folderPath}/{preset.baseName}_PostProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<PostProcessProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }

            ApplyEffectToggles(profile, preset);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            preset.postProfile = profile;

            var volume = preset.postVolume;
            if (volume == null)
            {
                var volumeGO = new GameObject($"{preset.baseName} Post Process Volume");
                Undo.RegisterCreatedObjectUndo(volumeGO, "Create Post Process Volume");
                volume = volumeGO.AddComponent<PostProcessVolume>();
                preset.postVolume = volume;
            }
            volume.isGlobal = true;
            volume.weight = 1f;
            volume.priority = 0f;
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(volume);

            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[BOSS Look] MainCamera が見つかりませんでした。Post Process Layer の追加はスキップしました。");
            }
            else
            {
                var layer = cam.GetComponent<PostProcessLayer>();
                if (layer == null)
                {
                    layer = Undo.AddComponent<PostProcessLayer>(cam.gameObject);
                }
                int idx = LayerMask.NameToLayer(DefaultPostLayerName);
                if (idx < 0)
                {
                    Debug.LogWarning($"[BOSS Look] Layer \"{DefaultPostLayerName}\" が無いため Default を使います。Tags and Layers でレイヤーを追加してください。");
                    layer.volumeLayer = 1; // Default
                }
                else
                {
                    layer.volumeLayer = 1 << idx;
                    volume.gameObject.layer = idx;
                }

                var resources = LoadDefaultPostResources();
                if (resources != null)
                {
                    layer.Init(resources);
                }
                else
                {
                    Debug.LogWarning("[BOSS Look] PostProcessResources が見つかりませんでした。手動で Layer を初期化してください。");
                }

                EditorUtility.SetDirty(layer);
                preset.postLayer = layer;
            }

            EditorUtility.SetDirty(preset);

            if (!PostProcessOps.IsLinearColorSpace)
            {
                Debug.LogWarning("[BOSS Look] Color Space が Linear ではありません。Player Settings で Linear に変更することを推奨します。");
            }
#endif
        }

        /// <summary>Re-applies effect values to the existing profile only
        /// (no volume/layer/camera work), for cheap live slider updates.</summary>
        public static void ReapplyProfile(BOSSLookPreset preset)
        {
#if BOSS_LOOK_PRESET_HAS_PPV2
            if (preset != null && preset.postProfile is PostProcessProfile profile)
            {
                ApplyEffectToggles(profile, preset);
                EditorUtility.SetDirty(profile);
            }
#endif
        }

#if BOSS_LOOK_PRESET_HAS_PPV2
        private static PostProcessResources LoadDefaultPostResources()
        {
            string[] guids = AssetDatabase.FindAssets("t:PostProcessResources");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var res = AssetDatabase.LoadAssetAtPath<PostProcessResources>(path);
                if (res != null) return res;
            }
            return null;
        }

        private static void ApplyEffectToggles(PostProcessProfile profile, BOSSLookPreset preset)
        {
            EnsureEffect<Bloom>(profile, preset.postBloomEnabled, e =>
            {
                e.intensity.overrideState = true;
                e.intensity.value = preset.bloomIntensity;
                e.threshold.overrideState = true;
                e.threshold.value = 1.1f;
            });

            EnsureEffect<ColorGrading>(profile, preset.postColorGradingEnabled, e =>
            {
                e.tonemapper.overrideState = true;
                e.tonemapper.value = Tonemapper.ACES;
                e.postExposure.overrideState = true;
                e.postExposure.value = preset.gradePostExposure;
                e.contrast.overrideState = true;
                e.contrast.value = preset.gradeContrast;
                e.saturation.overrideState = true;
                e.saturation.value = preset.gradeSaturation;
                e.colorFilter.overrideState = true;
                e.colorFilter.value = preset.gradeColorFilter;
                e.temperature.overrideState = true;
                e.temperature.value = preset.gradeTemperature;
                e.tint.overrideState = true;
                e.tint.value = preset.gradeTint;
            });

            EnsureEffect<Vignette>(profile, preset.postVignetteEnabled, e =>
            {
                e.intensity.overrideState = true;
                e.intensity.value = preset.vignetteIntensity;
                e.smoothness.overrideState = true;
                e.smoothness.value = 0.5f;
            });

            EnsureEffect<DepthOfField>(profile, preset.postDepthOfFieldEnabled, null);
            EnsureEffect<MotionBlur>(profile, preset.postMotionBlurEnabled, null);
            EnsureEffect<AmbientOcclusion>(profile, preset.postAOEnabled, e =>
            {
                // Light, grounding AO — not a heavy crease-darkening pass.
                e.intensity.overrideState = true;
                e.intensity.value = 0.5f;
                e.radius.overrideState = true;
                e.radius.value = 0.25f;
            });
        }

        private static void EnsureEffect<T>(PostProcessProfile profile, bool enabled, System.Action<T> configure)
            where T : PostProcessEffectSettings
        {
            T effect = profile.HasSettings<T>() ? profile.GetSetting<T>() : profile.AddSettings<T>();
            effect.enabled.overrideState = true;
            effect.enabled.value = enabled;
            configure?.Invoke(effect);
        }
#endif

        public static void Remove(BOSSLookPreset preset)
        {
            if (preset == null) return;
#if BOSS_LOOK_PRESET_HAS_PPV2
            if (preset.postVolume != null)
            {
                Undo.DestroyObjectImmediate(preset.postVolume.gameObject);
            }
            if (preset.postLayer != null)
            {
                Undo.DestroyObjectImmediate(preset.postLayer);
            }
#endif
            preset.postVolume = null;
            preset.postLayer = null;
            EditorUtility.SetDirty(preset);
        }
    }
}
