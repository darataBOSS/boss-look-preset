using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if BOSS_LOOK_PRESET_HAS_PPV2
using UnityEngine.Rendering.PostProcessing;
#endif
#if BOSS_LOOK_PRESET_HAS_URP
using UnityEngine.Rendering.Universal;
#endif

namespace DarataBOSS.BOSSLookPreset.Editor
{
    public enum BOSSLookPhase
    {
        NotCreated = 0,
        Active = 1,
        Finalized = 2,
    }

    public enum ReflectionBakeMode
    {
        A_UseHDRI = 0,
        B_NeutralReplace = 1,
    }

    public enum RigLightKind
    {
        Spot = 0,
        Area = 1,
    }

    public enum RigType
    {
        ThreePoint = 0,   // 被写体フォーカス (Key/Fill/Back)
        Sun = 1,          // 屋外: Directional 太陽 + 空フィル
        CeilingGrid = 2,  // 広い室内: 天井ライトのグリッド
    }

    public enum GridLightKind
    {
        Spot = 0,
        Point = 1,
        Area = 2,
    }

    public enum StaticTargetSource
    {
        ManualList = 0,
        LayerMask = 1,
        Tag = 2,
    }

    public enum AntiAliasingMode
    {
        None = 0,
        FXAA = 1,    // cheap post AA
        SMAA = 2,    // higher-quality post AA
        MSAA2x = 3,  // hardware MSAA
        MSAA4x = 4,
        MSAA8x = 5,
    }

    /// <summary>Delivery target. Drives how looks are realized: the general
    /// path assumes Linear + ACES post; the STYLY mobile path assumes Gamma
    /// (STYLY mobile can't use Linear) and bakes the look into lighting plus
    /// LDR-mode post so it survives.</summary>
    public enum OutputTarget
    {
        GeneralLinear = 0,    // 汎用 / PC: Linear, ACES tonemapping, full post
        STYLYMobileGamma = 1, // STYLY モバイル: Gamma, lighting-baked + LDR post
    }

    [Serializable]
    public class StashedLightInfo
    {
        public Light light;
        public bool wasEnabled;
        // Asset-to-scene references die on editor restart, so the hierarchy
        // path is the durable key used to re-find the light afterwards.
        public string scenePath;
    }

    public class BOSSLookPreset : ScriptableObject
    {
        // ---- Meta ----
        public string baseName = "BOSSLook";
        public string folderPath = "Assets/BOSSLookPreset";
        public BOSSLookPhase phase = BOSSLookPhase.NotCreated;
        public OutputTarget outputTarget = OutputTarget.GeneralLinear;

        // ---- Environment / Skybox ----
        public Material skyboxMaterial;
        public Texture hdriTexture;
        [Range(0f, 8f)] public float environmentIntensity = 1.0f;
        [Range(0f, 360f)] public float skyboxRotation = 0f;
        [Range(0f, 8f)] public float skyboxExposure = 1.0f;
        public Material stashedSkybox;

        // ---- Lighting Settings ----
        public LightingSettings lightingSettings;
        public float lightmapResolution = 20f;
        public int atlasSize = 1024;
        public int directSamples = 32;
        public int indirectSamples = 256;
        public int bounces = 2;
        public LightingSettings.Lightmapper lightmapperKind = LightingSettings.Lightmapper.ProgressiveGPU;
        public bool compressLightmaps = true;
        public bool ambientOcclusion = true;

        // ---- Static Targets ----
        public StaticTargetSource staticTargetSource = StaticTargetSource.ManualList;
        public List<GameObject> staticTargets = new List<GameObject>();
        public LayerMask targetLayerMask = ~0;
        public string targetTag = "Untagged";

        // ---- Light Probes ----
        public LightProbeGroup lightProbeGroup;
        public Bounds probeArea = new Bounds(Vector3.zero, new Vector3(10f, 4f, 10f));
        public float probeSpacing = 2.0f;
        public int verticalLayers = 2;
        public bool skipInsideColliders = true;
        public float colliderCheckRadius = 0.25f;

        // ---- Reflection Probes ----
        public List<ReflectionProbe> reflectionProbes = new List<ReflectionProbe>();
        public ReflectionBakeMode reflectionMode = ReflectionBakeMode.A_UseHDRI;
        public float reflectionBoxPadding = 0.5f;
        public int reflectionResolution = 128;

        // ---- Light Rig ----
        public RigType rigType = RigType.ThreePoint;
        public GameObject rigRoot;
        public Light keyLight;
        public Light fillLight;
        public Light backLight;
        public Transform subject;
        public RigLightKind rigLightKind = RigLightKind.Spot;

        // Sun rig (outdoor)
        public Light sunLight;
        public Light skyFillLight;
        [Range(0f, 90f)] public float sunElevationDeg = 50f;
        [Range(0f, 360f)] public float sunAzimuthDeg = 30f;
        public float sunIntensity = 1.2f;
        public float sunColorTemperatureKelvin = 5500f;
        public bool skyFillEnabled = true;
        [Range(2f, 10f)] public float skyFillRatio = 4f;
        public float skyFillColorTemperatureKelvin = 9000f;

        // Ceiling grid rig (large interior); covers probeArea horizontally.
        public List<Light> gridLights = new List<Light>();
        [Range(1, 8)] public int gridRows = 2;
        [Range(1, 8)] public int gridColumns = 3;
        public GridLightKind gridLightKind = GridLightKind.Spot;
        public float gridIntensity = 1.5f;
        public float gridColorTemperatureKelvin = 4500f;
        [Range(1f, 179f)] public float gridSpotAngle = 100f;
        [Range(1f, 8f)] public float keyFillRatio = 2.0f;
        public float keyIntensity = 2.0f;
        public float keyColorTemperatureKelvin = 5600f;
        public float fillColorTemperatureKelvin = 6500f;
        public float backColorTemperatureKelvin = 5000f;
        [Range(1f, 179f)] public float spotAngleDeg = 60f;
        public Vector2 areaSize = new Vector2(2f, 2f);
        public List<StashedLightInfo> stashedDirectionalLights = new List<StashedLightInfo>();

        // ---- Post Processing (Built-in / PPv2) ----
#if BOSS_LOOK_PRESET_HAS_PPV2
        public PostProcessProfile postProfile;
        public PostProcessVolume postVolume;
        public PostProcessLayer postLayer;
#else
        public ScriptableObject postProfile;
        public Component postVolume;
        public Component postLayer;
#endif

        // ---- Post Processing (URP / Volume framework) ----
#if BOSS_LOOK_PRESET_HAS_SRPCORE
        public Volume urpVolume;
        public VolumeProfile urpVolumeProfile;
#else
        public Component urpVolume;
        public ScriptableObject urpVolumeProfile;
#endif

        // Shared effect toggles (semantic, not API-specific)
        public bool postBloomEnabled = true;
        [Range(0f, 2f)] public float bloomIntensity = 0.4f;
        public bool postColorGradingEnabled = true;
        public bool postVignetteEnabled = true;
        [Range(0f, 1f)] public float vignetteIntensity = 0.25f;
        public bool postDepthOfFieldEnabled = false;
        public bool postMotionBlurEnabled = false;
        public bool postAOEnabled = true;

        // Color grading controls (mapped to PPv2 ColorGrading / URP ColorAdjustments+WhiteBalance)
        [Range(-3f, 3f)] public float gradePostExposure = 0f;
        [Range(-100f, 100f)] public float gradeContrast = 0f;
        [Range(-100f, 100f)] public float gradeSaturation = 0f;
        public Color gradeColorFilter = Color.white;
        [Range(-100f, 100f)] public float gradeTemperature = 0f;
        [Range(-100f, 100f)] public float gradeTint = 0f;

        // ---- Quality: Anti-aliasing & Shadows (Module F) ----
        public AntiAliasingMode antiAliasing = AntiAliasingMode.SMAA;
        public float shadowDistance = 30f;
        public UnityEngine.ShadowResolution shadowResolution = UnityEngine.ShadowResolution.High;
        public int shadowCascades = 2;
        public bool softShadows = true;

        // ---- Ground Shadow (Module D) ----
        public GameObject groundShadowPlane;
        public Material groundShadowMaterial;
        [Range(0f, 1f)] public float groundShadowOpacity = 0.6f;
        public float groundShadowSizeMultiplier = 2.0f;

        // ---- Fog (Module D) ----
        public bool fogEnabled = false;
        public FogMode fogMode = FogMode.ExponentialSquared;
        public Color fogColor = new Color(0.6f, 0.65f, 0.7f, 1f);
        public float fogDensity = 0.02f;
        public float fogStartDistance = 10f;
        public float fogEndDistance = 60f;
    }
}
