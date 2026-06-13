using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DarataBOSS.BOSSLookPreset.Editor.Ops
{
    /// <summary>
    /// Module G (looks): writes a <see cref="LookStyle"/> into the preset's
    /// visual fields and re-applies the live systems (post profile, fog, rig).
    /// Only touches look knobs — never bake data or generated objects.
    /// </summary>
    public static class LookStyleOps
    {
        /// <summary>Applies a look at the given strength (0..1), blended from the
        /// neutral baseline so the slider is absolute and predictable.</summary>
        public static void Apply(BOSSLookPreset preset, LookStyle style, float strength = 1f)
        {
            if (preset == null) return;
            strength = Mathf.Clamp01(strength);
            var n = LookStyle.Neutral;

            Undo.RecordObject(preset, "Apply Look Style");

            preset.gradePostExposure = Mathf.Lerp(n.exposure, style.exposure, strength);
            preset.gradeContrast = Mathf.Lerp(n.contrast, style.contrast, strength);
            preset.gradeSaturation = Mathf.Lerp(n.saturation, style.saturation, strength);
            preset.gradeColorFilter = Color.Lerp(n.colorFilter, style.colorFilter, strength);
            preset.gradeTemperature = Mathf.Lerp(n.temperature, style.temperature, strength);
            preset.gradeTint = Mathf.Lerp(n.tint, style.tint, strength);

            preset.postColorGradingEnabled = style.colorGrading;
            preset.postBloomEnabled = style.bloom;
            preset.bloomIntensity = Mathf.Lerp(n.bloomIntensity, style.bloomIntensity, strength);
            preset.postVignetteEnabled = style.vignette;
            preset.vignetteIntensity = Mathf.Lerp(n.vignetteIntensity, style.vignetteIntensity, strength);

            preset.fogEnabled = style.fog;
            preset.fogMode = style.fogMode;
            preset.fogColor = style.fogColor;
            preset.fogDensity = Mathf.Lerp(n.fogDensity, style.fogDensity, strength);

            preset.keyFillRatio = Mathf.Lerp(n.keyFillRatio, style.keyFillRatio, strength);
            preset.skyFillRatio = Mathf.Lerp(n.skyFillRatio, style.skyFillRatio, strength);

            EditorUtility.SetDirty(preset);

            // Re-apply the live systems so the look shows immediately.
            if (PostProcessOps.HasProfile(preset)) PostProcessOps.ReapplyProfile(preset);
            FogOps.Apply(preset);
            if (LightRigOps.RigExists(preset)) LightRigOps.CreateOrUpdateRig(preset);
        }

        /// <summary>Reads the preset's current visual fields back into a look,
        /// for saving the user's hand-tuned state as a custom style.</summary>
        public static LookStyle Capture(BOSSLookPreset preset, string name)
        {
            return new LookStyle
            {
                name = name,
                description = "保存されたカスタムルック。",
                exposure = preset.gradePostExposure,
                contrast = preset.gradeContrast,
                saturation = preset.gradeSaturation,
                colorFilter = preset.gradeColorFilter,
                temperature = preset.gradeTemperature,
                tint = preset.gradeTint,
                colorGrading = preset.postColorGradingEnabled,
                bloom = preset.postBloomEnabled,
                bloomIntensity = preset.bloomIntensity,
                vignette = preset.postVignetteEnabled,
                vignetteIntensity = preset.vignetteIntensity,
                fog = preset.fogEnabled,
                fogMode = preset.fogMode,
                fogColor = preset.fogColor,
                fogDensity = preset.fogDensity,
                keyFillRatio = preset.keyFillRatio,
                skyFillRatio = preset.skyFillRatio,
            };
        }

        public static BOSSLookStyleAsset SaveCustom(BOSSLookPreset preset, LookStyle style)
        {
            if (preset == null) return null;
            string looksFolder = $"{preset.folderPath}/Looks";
            EnsureFolder(looksFolder);

            string safe = MakeSafeFileName(string.IsNullOrEmpty(style.name) ? "Look" : style.name);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{looksFolder}/{safe}.asset");

            var asset = ScriptableObject.CreateInstance<BOSSLookStyleAsset>();
            asset.style = style;
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        public static List<BOSSLookStyleAsset> LoadCustom(BOSSLookPreset preset)
        {
            var list = new List<BOSSLookStyleAsset>();
            if (preset == null) return list;
            string looksFolder = $"{preset.folderPath}/Looks";
            if (!AssetDatabase.IsValidFolder(looksFolder)) return list;

            foreach (var guid in AssetDatabase.FindAssets("t:BOSSLookStyleAsset", new[] { looksFolder }))
            {
                var a = AssetDatabase.LoadAssetAtPath<BOSSLookStyleAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null) list.Add(a);
            }
            return list;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            string leaf = Path.GetFileName(folderPath);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        private static string MakeSafeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}
