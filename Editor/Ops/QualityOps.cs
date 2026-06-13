using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
#if BOSS_LOOK_PRESET_HAS_PPV2
using UnityEngine.Rendering.PostProcessing;
#endif
#if BOSS_LOOK_PRESET_HAS_URP
using UnityEngine.Rendering.Universal;
#endif

namespace DarataBOSS.BOSSLookPreset.Editor.Ops
{
    /// <summary>
    /// Module F (quality): anti-aliasing and realtime shadow quality. Both are
    /// pipeline-specific — MSAA lives on QualitySettings (Built-in) or the URP
    /// asset, post AA lives on the PPv2 layer or the URP camera, and shadow
    /// distance/cascades are QualitySettings vs URP-asset. This routes each to
    /// the right place for the active pipeline.
    /// </summary>
    public static class QualityOps
    {
        // ---------------- Anti-aliasing ----------------

        public static void ApplyAntiAliasing(BOSSLookPreset preset)
        {
            if (preset == null) return;
            bool isMSAA = preset.antiAliasing == AntiAliasingMode.MSAA2x
                       || preset.antiAliasing == AntiAliasingMode.MSAA4x
                       || preset.antiAliasing == AntiAliasingMode.MSAA8x;
            int msaaSamples = preset.antiAliasing == AntiAliasingMode.MSAA2x ? 2
                            : preset.antiAliasing == AntiAliasingMode.MSAA4x ? 4
                            : preset.antiAliasing == AntiAliasingMode.MSAA8x ? 8 : 1;

            if (RenderPipelineDetector.IsURP)
            {
#if BOSS_LOOK_PRESET_HAS_URP
                var urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
                if (urp != null)
                {
                    urp.msaaSampleCount = isMSAA ? msaaSamples : 1;
                    EditorUtility.SetDirty(urp);
                }
                var cam = Camera.main;
                if (cam != null)
                {
                    var data = cam.GetUniversalAdditionalCameraData();
                    if (data != null)
                    {
                        Undo.RecordObject(data, "Set AA");
                        data.antialiasing = preset.antiAliasing == AntiAliasingMode.FXAA
                            ? AntialiasingMode.FastApproximateAntialiasing
                            : preset.antiAliasing == AntiAliasingMode.SMAA
                                ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
                                : AntialiasingMode.None;
                        data.antialiasingQuality = AntialiasingQuality.High;
                        EditorUtility.SetDirty(data);
                    }
                }
#endif
            }
            else // Built-in
            {
                QualitySettings.antiAliasing = isMSAA ? msaaSamples : 0;
#if BOSS_LOOK_PRESET_HAS_PPV2
                if (preset.postLayer is PostProcessLayer layer)
                {
                    Undo.RecordObject(layer, "Set AA");
                    switch (preset.antiAliasing)
                    {
                        case AntiAliasingMode.FXAA:
                            layer.antialiasingMode = PostProcessLayer.Antialiasing.FastApproximateAntialiasing;
                            break;
                        case AntiAliasingMode.SMAA:
                            layer.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
                            break;
                        default:
                            layer.antialiasingMode = PostProcessLayer.Antialiasing.None;
                            break;
                    }
                    EditorUtility.SetDirty(layer);
                }
                else if (preset.antiAliasing == AntiAliasingMode.FXAA || preset.antiAliasing == AntiAliasingMode.SMAA)
                {
                    Debug.LogWarning("[BOSS Look] FXAA/SMAA は Post Process Layer が必要です。先に Module C でポストプロセスをセットアップしてください (MSAA はそのまま使えます)。");
                }
#endif
            }
        }

        public static string AntiAliasingExplanation(BOSSLookPreset preset)
        {
            switch (preset.antiAliasing)
            {
                case AntiAliasingMode.None: return "なし。輪郭にジャギーが残ります。";
                case AntiAliasingMode.FXAA: return "FXAA: 軽量なポスト処理。WebARの第一候補。Post Process Layer / URP Camera が必要。";
                case AntiAliasingMode.SMAA: return "SMAA: FXAAより高品質なポスト処理。少し重い。WebARでも実用的。";
                case AntiAliasingMode.MSAA2x:
                case AntiAliasingMode.MSAA4x:
                case AntiAliasingMode.MSAA8x:
                    return "MSAA: ハードウェアAA。輪郭が最も綺麗だが負荷が高め。WebGL端末によっては未対応のことも。";
                default: return "";
            }
        }

        // ---------------- Shadow quality ----------------

        public static void ApplyShadows(BOSSLookPreset preset)
        {
            if (preset == null) return;

            if (RenderPipelineDetector.IsURP)
            {
#if BOSS_LOOK_PRESET_HAS_URP
                var urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
                if (urp != null)
                {
                    urp.shadowDistance = preset.shadowDistance;
                    // URP exposes 1..4 cascades; map our 1/2/4 directly.
                    urp.shadowCascadeCount = Mathf.Clamp(preset.shadowCascades, 1, 4);
                    EditorUtility.SetDirty(urp);
                }
                Debug.Log("[BOSS Look] URP の影解像度 / ソフトシャドウは URP Asset と各ライトの設定です。距離とカスケードを適用しました。");
#endif
            }
            else // Built-in
            {
                QualitySettings.shadowDistance = preset.shadowDistance;
                QualitySettings.shadowResolution = preset.shadowResolution;
                QualitySettings.shadowCascades = preset.shadowCascades == 4 ? 4
                    : preset.shadowCascades == 2 ? 2 : 1;
                QualitySettings.shadows = preset.softShadows ? ShadowQuality.All : ShadowQuality.HardOnly;
            }
        }

        /// <summary>Pulls shadow distance in to a few times the subject/scene
        /// size so contact shadows stay crisp instead of being spread thin over
        /// the default ~150m range.</summary>
        public static float SuggestShadowDistance(BOSSLookPreset preset)
        {
            float reach;
            if (preset.subject != null)
            {
                var bounds = new Bounds(preset.subject.position, Vector3.one);
                foreach (var r in preset.subject.GetComponentsInChildren<Renderer>())
                {
                    if (r != null) bounds.Encapsulate(r.bounds);
                }
                reach = bounds.size.magnitude * 4f;
            }
            else
            {
                reach = preset.probeArea.size.magnitude * 1.5f;
            }
            return Mathf.Clamp(reach, 10f, 150f);
        }
    }
}
