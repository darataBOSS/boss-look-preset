using UnityEditor;
using UnityEngine;
#if BOSS_LOOK_PRESET_HAS_SRPCORE
using UnityEngine.Rendering;
#endif
#if BOSS_LOOK_PRESET_HAS_URP
using UnityEngine.Rendering.Universal;
#endif

namespace DarataBOSS.BOSSLookPreset.Editor.Ops
{
    /// <summary>
    /// URP path for Module C.
    /// Creates a global Volume + VolumeProfile in the scene and configures the
    /// VolumeComponents that match BOSSLookPreset's shared effect toggles.
    /// Camera post-processing toggle and Renderer Features (e.g. SSAO) must be
    /// enabled by the user manually — that's a project-asset side concern.
    /// </summary>
    public static class PostProcessOpsURP
    {
        public static bool IsAvailable
        {
            get
            {
#if BOSS_LOOK_PRESET_HAS_URP
                return true;
#else
                return false;
#endif
            }
        }

        public static void Setup(BOSSLookPreset preset)
        {
            if (preset == null) return;
#if !BOSS_LOOK_PRESET_HAS_URP
            EditorUtility.DisplayDialog("BOSS Look Preset",
                "URP が見つかりません。Package Manager で com.unity.render-pipelines.universal を追加してください。",
                "OK");
            return;
#else
            string profilePath = $"{preset.folderPath}/{preset.baseName}_VolumeProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }

            ApplyEffectToggles(profile, preset);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            preset.urpVolumeProfile = profile;

            var volume = preset.urpVolume;
            if (volume == null)
            {
                var volumeGO = new GameObject($"{preset.baseName} Volume");
                Undo.RegisterCreatedObjectUndo(volumeGO, "Create URP Volume");
                volume = volumeGO.AddComponent<Volume>();
                preset.urpVolume = volume;
            }
            volume.isGlobal = true;
            volume.weight = 1f;
            volume.priority = 0f;
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(volume);

            // The volume does nothing until the camera opts in, so flip the
            // URP per-camera toggle automatically.
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                var camData = mainCam.GetUniversalAdditionalCameraData();
                if (camData != null && !camData.renderPostProcessing)
                {
                    Undo.RecordObject(camData, "Enable URP Post Processing");
                    camData.renderPostProcessing = true;
                    EditorUtility.SetDirty(camData);
                    Debug.Log("[BOSS Look] Main Camera の Post Processing を ON にしました。");
                }
            }
            else
            {
                Debug.LogWarning("[BOSS Look] MainCamera が見つかりませんでした。Camera の Post Processing を手動で ON にしてください。");
            }

            EditorUtility.SetDirty(preset);

            if (!PostProcessOps.IsLinearColorSpace)
            {
                Debug.LogWarning("[BOSS Look] Color Space が Linear ではありません。Player Settings で Linear に変更することを推奨します。");
            }

            if (preset.postAOEnabled)
            {
                Debug.LogWarning(
                    "[BOSS Look] URP の Ambient Occlusion は VolumeComponent ではなく Renderer Feature です。" +
                    "URP Renderer Asset を選択し、Add Renderer Feature → Screen Space Ambient Occlusion を追加してください。");
            }

            if (Camera.main != null && !Camera.main.allowHDR)
            {
                Debug.LogWarning("[BOSS Look] Main Camera で HDR が無効です。Bloom / Tonemapping を効かせるには Camera の HDR を ON にしてください。");
            }
#endif
        }

        /// <summary>Re-applies effect values to the existing profile only
        /// (no volume/camera work), for cheap live slider updates.</summary>
        public static void ReapplyProfile(BOSSLookPreset preset)
        {
#if BOSS_LOOK_PRESET_HAS_URP
            if (preset != null && preset.urpVolumeProfile is VolumeProfile profile)
            {
                ApplyEffectToggles(profile, preset);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }
#endif
        }

#if BOSS_LOOK_PRESET_HAS_URP
        private static void ApplyEffectToggles(VolumeProfile profile, BOSSLookPreset preset)
        {
            CleanProfile(profile);

            EnsureComponent<Bloom>(profile, preset.postBloomEnabled, b =>
            {
                b.intensity.overrideState = true;
                b.intensity.value = preset.bloomIntensity;
                b.threshold.overrideState = true;
                b.threshold.value = 1.1f;
            });

            // Color grading bundle: Tonemapping + ColorAdjustments + WhiteBalance
            EnsureComponent<Tonemapping>(profile, preset.postColorGradingEnabled, t =>
            {
                t.mode.overrideState = true;
                t.mode.value = TonemappingMode.ACES;
            });
            EnsureComponent<ColorAdjustments>(profile, preset.postColorGradingEnabled, c =>
            {
                c.postExposure.overrideState = true;
                c.postExposure.value = preset.gradePostExposure;
                c.contrast.overrideState = true;
                c.contrast.value = preset.gradeContrast;
                c.saturation.overrideState = true;
                c.saturation.value = preset.gradeSaturation;
                c.colorFilter.overrideState = true;
                c.colorFilter.value = preset.gradeColorFilter;
            });
            EnsureComponent<WhiteBalance>(profile, preset.postColorGradingEnabled, w =>
            {
                w.temperature.overrideState = true;
                w.temperature.value = preset.gradeTemperature;
                w.tint.overrideState = true;
                w.tint.value = preset.gradeTint;
            });

            EnsureComponent<Vignette>(profile, preset.postVignetteEnabled, v =>
            {
                v.intensity.overrideState = true;
                v.intensity.value = preset.vignetteIntensity;
                v.smoothness.overrideState = true;
                v.smoothness.value = 0.5f;
            });

            EnsureComponent<DepthOfField>(profile, preset.postDepthOfFieldEnabled, null);
            EnsureComponent<MotionBlur>(profile, preset.postMotionBlurEnabled, null);

            // AO in URP is a Renderer Feature; warned about in Setup().
        }

        private static void EnsureComponent<T>(VolumeProfile profile, bool enabled, System.Action<T> configure)
            where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var component))
            {
                component = profile.Add<T>(overrides: true);
            }

            // Same persistence fix as the Built-in path: VolumeProfile.Add only
            // touches the in-memory components list, so without this the saved /
            // uploaded profile has no effects. Guard with IsPersistent (not
            // IsSubAsset) + try/catch to avoid the double-add throw after reimport.
            if (AssetDatabase.Contains(profile) && !EditorUtility.IsPersistent(component))
            {
                component.hideFlags = HideFlags.HideInHierarchy;
                try
                {
                    AssetDatabase.AddObjectToAsset(component, profile);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[BOSS Look] エフェクト {typeof(T).Name} の保存に失敗しました: {e.Message}");
                }
            }

            component.active = enabled;
            configure?.Invoke(component);
            EditorUtility.SetDirty(component);
        }

        private static void CleanProfile(VolumeProfile profile)
        {
            if (profile.components == null) return;

            // Null + duplicate-type removal only; no disk-orphan destroy (instance
            // identity differs after reimport and would delete live sub-assets).
            var seenTypes = new System.Collections.Generic.HashSet<System.Type>();
            for (int i = profile.components.Count - 1; i >= 0; i--)
            {
                var c = profile.components[i];
                if (c == null)
                {
                    profile.components.RemoveAt(i);
                    continue;
                }
                if (!seenTypes.Add(c.GetType()))
                {
                    profile.components.RemoveAt(i);
                }
            }
            EditorUtility.SetDirty(profile);
        }
#endif

        public static void Remove(BOSSLookPreset preset)
        {
            if (preset == null) return;
#if BOSS_LOOK_PRESET_HAS_URP
            if (preset.urpVolume != null)
            {
                Undo.DestroyObjectImmediate(preset.urpVolume.gameObject);
            }
#endif
            preset.urpVolume = null;
            EditorUtility.SetDirty(preset);
        }
    }
}
