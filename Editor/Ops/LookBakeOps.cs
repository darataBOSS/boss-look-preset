using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarataBOSS.BOSSLookPreset.Editor.Ops
{
    /// <summary>
    /// The "first layer" of a STYLY-mobile look: pushes the look's color intent
    /// into the environment (skybox tint/exposure → ambient + reflections) and
    /// ambient intensity. This survives Gamma and a dropped post stack because
    /// it lives in standard scene/lighting data, not a camera effect.
    ///
    /// Deliberately idempotent: every value is derived from the look + the
    /// preset's own base exposure, never multiplied onto the current state, so
    /// re-applying doesn't compound.
    /// </summary>
    public static class LookBakeOps
    {
        public static void ApplyToLighting(BOSSLookPreset preset, LookStyle style, float strength)
        {
            if (preset == null) return;
            strength = Mathf.Clamp01(strength);

            Color tint = MoodColor(style.temperature, style.tint, style.colorFilter, strength);

            var mat = preset.skyboxMaterial;
            if (mat != null)
            {
                // Skybox shaders treat mid-grey (0.5) as neutral tint; modulate
                // around that. Exposure rides on the user's base skybox exposure.
                if (mat.HasProperty("_Tint"))
                {
                    Color t = tint * 0.5f;
                    t.a = 1f;
                    mat.SetColor("_Tint", ClampColor01(t));
                }
                if (mat.HasProperty("_Exposure"))
                {
                    float exp = preset.skyboxExposure * Mathf.Pow(2f, style.exposure * strength * 0.5f);
                    mat.SetFloat("_Exposure", Mathf.Clamp(exp, 0f, 8f));
                }
                EditorUtility.SetDirty(mat);
            }

            // Ambient intensity follows exposure a little, so the whole scene
            // brightens/darkens with the look even without post.
            RenderSettings.ambientIntensity = Mathf.Clamp(
                preset.environmentIntensity * Mathf.Pow(2f, style.exposure * strength * 0.35f), 0f, 8f);
            RenderSettings.reflectionIntensity = Mathf.Clamp(preset.environmentIntensity, 0f, 1f);

            DynamicGI.UpdateEnvironment();
            EditorUtility.SetDirty(preset);
        }

        /// <summary>Restores the skybox tint/exposure to neutral (used when
        /// switching back to the general target or clearing a mobile look).</summary>
        public static void ResetLightingTint(BOSSLookPreset preset)
        {
            if (preset == null || preset.skyboxMaterial == null) return;
            var mat = preset.skyboxMaterial;
            if (mat.HasProperty("_Tint")) mat.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 1f));
            if (mat.HasProperty("_Exposure")) mat.SetFloat("_Exposure", preset.skyboxExposure);
            RenderSettings.ambientIntensity = preset.environmentIntensity;
            EditorUtility.SetDirty(mat);
            DynamicGI.UpdateEnvironment();
        }

        /// <summary>Maps white-balance-style intent to an RGB multiplier centered
        /// on white (1,1,1 = neutral).</summary>
        private static Color MoodColor(float temperature, float tint, Color colorFilter, float strength)
        {
            float t = Mathf.Clamp(temperature / 100f, -1f, 1f) * strength;
            // warm (t>0): lift red, drop blue.
            Color warm = new Color(1f + 0.15f * t, 1f, 1f - 0.15f * t, 1f);

            float g = Mathf.Clamp(tint / 100f, -1f, 1f) * strength;
            // tint + = magenta (r,b up / g down); tint - = green.
            Color tn = new Color(1f + 0.10f * g, 1f - 0.10f * g, 1f + 0.10f * g, 1f);

            Color filter = Color.Lerp(Color.white, colorFilter, strength);

            return new Color(
                warm.r * tn.r * filter.r,
                warm.g * tn.g * filter.g,
                warm.b * tn.b * filter.b,
                1f);
        }

        private static Color ClampColor01(Color c)
        {
            return new Color(
                Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), Mathf.Clamp01(c.a));
        }
    }
}
