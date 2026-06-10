using UnityEditor;
using UnityEngine;

namespace DarataBOSS.BOSSLookPreset.Editor.Ops
{
    /// <summary>
    /// Dispatcher for Module C. Picks PPv2 (Built-in RP) or the URP Volume
    /// framework based on the currently active render pipeline asset.
    /// </summary>
    public static class PostProcessOps
    {
        public static bool IsLinearColorSpace => PlayerSettings.colorSpace == ColorSpace.Linear;

        /// <summary>True for backwards compatibility; the wizard now asks the
        /// active backend (PPv2 for Built-in, URP Volume for URP) directly via
        /// <see cref="BackendAvailable"/>.</summary>
        public static bool IsPPv2Available => PostProcessOpsBuiltin.IsAvailable;

        /// <summary>Whether the backend matching the currently active RP is usable.</summary>
        public static bool BackendAvailable
        {
            get
            {
                switch (RenderPipelineDetector.Active)
                {
                    case RenderPipelineDetector.ActiveRP.BuiltIn: return PostProcessOpsBuiltin.IsAvailable;
                    case RenderPipelineDetector.ActiveRP.URP: return PostProcessOpsURP.IsAvailable;
                    default: return false;
                }
            }
        }

        public static string BackendName
        {
            get
            {
                switch (RenderPipelineDetector.Active)
                {
                    case RenderPipelineDetector.ActiveRP.BuiltIn: return "Post Processing Stack v2";
                    case RenderPipelineDetector.ActiveRP.URP: return "URP Volume framework";
                    case RenderPipelineDetector.ActiveRP.HDRP: return "HDRP (not supported in this version)";
                    default: return "Unknown / Custom SRP (not supported)";
                }
            }
        }

        public static void Setup(BOSSLookPreset preset)
        {
            if (preset == null) return;
            switch (RenderPipelineDetector.Active)
            {
                case RenderPipelineDetector.ActiveRP.BuiltIn:
                    PostProcessOpsBuiltin.Setup(preset);
                    break;
                case RenderPipelineDetector.ActiveRP.URP:
                    PostProcessOpsURP.Setup(preset);
                    break;
                default:
                    EditorUtility.DisplayDialog("BOSS Look Preset",
                        $"このレンダーパイプライン ({RenderPipelineDetector.DisplayName}) は本バージョンではサポートしていません。",
                        "OK");
                    break;
            }
        }

        /// <summary>Removes from whichever backend has artefacts. Safe to call
        /// even after switching render pipelines mid-project — both branches
        /// are cleaned.</summary>
        public static void Remove(BOSSLookPreset preset)
        {
            if (preset == null) return;
            PostProcessOpsBuiltin.Remove(preset);
            PostProcessOpsURP.Remove(preset);
        }
    }
}
