using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarataBOSS.BOSSLookPreset.Editor.Ops
{
    /// <summary>
    /// Module D (fog): drives RenderSettings fog from the preset. Distance fog
    /// is pipeline-agnostic and essentially free, which makes it the cheapest
    /// "depth" lever for WebAR scenes.
    /// </summary>
    public static class FogOps
    {
        public static void Apply(BOSSLookPreset preset)
        {
            if (preset == null) return;
            RenderSettings.fog = preset.fogEnabled;
            RenderSettings.fogMode = preset.fogMode;
            RenderSettings.fogColor = preset.fogColor;
            RenderSettings.fogDensity = preset.fogDensity;
            RenderSettings.fogStartDistance = preset.fogStartDistance;
            RenderSettings.fogEndDistance = preset.fogEndDistance;
        }

        /// <summary>Samples the baked ambient probe at the horizon so the fog
        /// color matches the HDRI's sky without needing a readable texture.</summary>
        public static Color SuggestColorFromEnvironment()
        {
            var probe = RenderSettings.ambientProbe;
            Vector3[] directions =
            {
                Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
            };
            var results = new Color[directions.Length];
            probe.Evaluate(directions, results);

            Color sum = Color.black;
            foreach (var c in results) sum += c;
            Color avg = sum / directions.Length;
            avg.a = 1f;
            // Ambient probe values are HDR; clamp into displayable range.
            float max = Mathf.Max(avg.r, avg.g, avg.b);
            if (max > 1f) avg = new Color(avg.r / max, avg.g / max, avg.b / max, 1f);
            return avg;
        }
    }
}
