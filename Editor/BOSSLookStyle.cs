using System;
using UnityEngine;

namespace DarataBOSS.BOSSLookPreset.Editor
{
    /// <summary>
    /// A "look" — a cohesive bundle of the purely-visual knobs (color grade,
    /// bloom/vignette, fog, lighting mood) that can be applied in one click.
    /// Deliberately excludes anything structural (bake data, generated objects,
    /// folders) so applying a look never destroys work — it only re-skins.
    /// </summary>
    [Serializable]
    public struct LookStyle
    {
        public string name;
        [TextArea] public string description;

        // Color grade
        public float exposure;       // EV, -3..3
        public float contrast;       // -100..100
        public float saturation;     // -100..100
        public Color colorFilter;
        public float temperature;    // -100..100 (cool..warm)
        public float tint;           // -100..100 (green..magenta)

        // Bloom / vignette
        public bool colorGrading;
        public bool bloom;
        public float bloomIntensity; // 0..2
        public bool vignette;
        public float vignetteIntensity; // 0..1

        // Fog
        public bool fog;
        public FogMode fogMode;
        public Color fogColor;
        public float fogDensity;     // 0..0.15

        // Lighting mood (only takes effect if a rig exists)
        public float keyFillRatio;   // 3-point drama (1 flat .. 8 dramatic)
        public float skyFillRatio;   // sun drama (2 flat .. 10 dramatic)

        public static LookStyle Neutral => new LookStyle
        {
            name = "ニュートラル",
            description = "グレードなしの素の状態。",
            exposure = 0f, contrast = 0f, saturation = 0f,
            colorFilter = Color.white, temperature = 0f, tint = 0f,
            colorGrading = true,
            bloom = true, bloomIntensity = 0.4f,
            vignette = true, vignetteIntensity = 0.25f,
            fog = false, fogMode = FogMode.ExponentialSquared,
            fogColor = new Color(0.6f, 0.65f, 0.7f, 1f), fogDensity = 0.02f,
            keyFillRatio = 2f, skyFillRatio = 4f,
        };
    }

    /// <summary>Saved custom look, written into the preset's Looks/ folder.</summary>
    public class BOSSLookStyleAsset : ScriptableObject
    {
        public LookStyle style = LookStyle.Neutral;
    }

    public static class LookStyleLibrary
    {
        public static LookStyle[] BuiltIn => new[]
        {
            new LookStyle
            {
                name = "シネマティック",
                description = "やや沈めた露出・高コントラスト・寒色寄り。映画的でドラマチック。",
                exposure = -0.2f, contrast = 15f, saturation = -8f,
                colorFilter = new Color(0.92f, 0.96f, 1f, 1f), temperature = -6f, tint = 2f,
                colorGrading = true,
                bloom = true, bloomIntensity = 0.5f,
                vignette = true, vignetteIntensity = 0.35f,
                fog = true, fogMode = FogMode.ExponentialSquared,
                fogColor = new Color(0.55f, 0.6f, 0.7f, 1f), fogDensity = 0.012f,
                keyFillRatio = 4f, skyFillRatio = 6f,
            },
            new LookStyle
            {
                name = "明るい物販",
                description = "均一でクリーンな明るさ。商品・展示向け。影もフラット。",
                exposure = 0.2f, contrast = -4f, saturation = 6f,
                colorFilter = Color.white, temperature = 0f, tint = 0f,
                colorGrading = true,
                bloom = true, bloomIntensity = 0.22f,
                vignette = false, vignetteIntensity = 0.1f,
                fog = false, fogMode = FogMode.ExponentialSquared,
                fogColor = new Color(0.7f, 0.72f, 0.75f, 1f), fogDensity = 0.005f,
                keyFillRatio = 1.5f, skyFillRatio = 2.5f,
            },
            new LookStyle
            {
                name = "ムーディ",
                description = "暗部を締めた高コントラスト・寒色・濃いめの空気感。雰囲気重視。",
                exposure = -0.4f, contrast = 25f, saturation = -18f,
                colorFilter = new Color(0.85f, 0.92f, 1f, 1f), temperature = -15f, tint = 4f,
                colorGrading = true,
                bloom = true, bloomIntensity = 0.4f,
                vignette = true, vignetteIntensity = 0.45f,
                fog = true, fogMode = FogMode.ExponentialSquared,
                fogColor = new Color(0.4f, 0.45f, 0.55f, 1f), fogDensity = 0.03f,
                keyFillRatio = 6f, skyFillRatio = 8f,
            },
            new LookStyle
            {
                name = "ビビッド",
                description = "高彩度・やや暖色で華やか。ポップでにぎやか。",
                exposure = 0.1f, contrast = 10f, saturation = 25f,
                colorFilter = new Color(1f, 0.98f, 0.94f, 1f), temperature = 8f, tint = -2f,
                colorGrading = true,
                bloom = true, bloomIntensity = 0.6f,
                vignette = true, vignetteIntensity = 0.2f,
                fog = false, fogMode = FogMode.ExponentialSquared,
                fogColor = new Color(0.7f, 0.7f, 0.7f, 1f), fogDensity = 0.008f,
                keyFillRatio = 2.5f, skyFillRatio = 4f,
            },
        };
    }
}
