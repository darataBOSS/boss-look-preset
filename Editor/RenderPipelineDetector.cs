using UnityEngine.Rendering;
#if BOSS_LOOK_PRESET_HAS_URP
using UnityEngine.Rendering.Universal;
#endif

namespace DarataBOSS.BOSSLookPreset.Editor
{
    /// <summary>
    /// Detects which render pipeline is currently active in the project so that
    /// Module C can dispatch between PPv2 (Built-in RP) and the Volume framework (URP).
    /// </summary>
    public static class RenderPipelineDetector
    {
        public enum ActiveRP
        {
            BuiltIn = 0,
            URP = 1,
            HDRP = 2,
            Unknown = 3,
        }

        public static ActiveRP Active
        {
            get
            {
                var asset = GraphicsSettings.defaultRenderPipeline;
                if (asset == null)
                {
                    return ActiveRP.BuiltIn;
                }
#if BOSS_LOOK_PRESET_HAS_URP
                if (asset is UniversalRenderPipelineAsset)
                {
                    return ActiveRP.URP;
                }
#endif
                // Fall back to a string check so HDRP / older URP without the
                // versionDefine still report something useful.
                string typeName = asset.GetType().FullName ?? string.Empty;
                if (typeName.Contains("Universal")) return ActiveRP.URP;
                if (typeName.Contains("HDRender") || typeName.Contains("HDRenderPipeline")) return ActiveRP.HDRP;
                return ActiveRP.Unknown;
            }
        }

        public static bool IsBuiltIn => Active == ActiveRP.BuiltIn;
        public static bool IsURP => Active == ActiveRP.URP;
        public static bool IsHDRP => Active == ActiveRP.HDRP;

        public static string DisplayName
        {
            get
            {
                switch (Active)
                {
                    case ActiveRP.BuiltIn: return "Built-in RP";
                    case ActiveRP.URP: return "Universal RP (URP)";
                    case ActiveRP.HDRP: return "High Definition RP (HDRP)";
                    default: return "Unknown / Custom SRP";
                }
            }
        }
    }
}
