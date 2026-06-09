using UnityEngine;

namespace DarataBOSS.BOSSLookPreset.Editor
{
    public static class BOSSLookDefaults
    {
        public const string DefaultFolderRoot = "Assets/BOSSLookPreset";
        public const string DefaultBaseName = "BOSSLook";
        public const string RigRootName = "BOSS Light Rig";

        public const float EnvironmentIntensity = 1.0f;

        public const float LightmapResolution = 20f;
        public const int AtlasSize = 1024;
        public const int DirectSamples = 32;
        public const int IndirectSamples = 256;
        public const int Bounces = 2;
        public const bool CompressLightmaps = true;
        public const bool AmbientOcclusion = true;

        public const float ProbeSpacing = 2.0f;
        public const int VerticalLayers = 2;
        public const bool SkipInsideColliders = true;
        public const float ColliderCheckRadius = 0.25f;

        public const float ReflectionBoxPadding = 0.5f;

        public const float KeyFillRatio = 2.0f;
        public const float KeyIntensity = 2.0f;
        public const float KeyColorTemperatureKelvin = 5600f;
        public const float FillColorTemperatureKelvin = 6500f;
        public const float BackColorTemperatureKelvin = 5000f;
        public const float SpotAngleDeg = 60f;
        public static readonly Vector2 AreaSize = new Vector2(2f, 2f);

        public const float PostBloomIntensity = 0.4f;
        public const float PostVignetteIntensity = 0.25f;
    }
}
