using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarataBOSS.BOSSLookPreset.Editor.Ops
{
    public static class EnvironmentOps
    {
        // ---------------- Preset creation / loading ----------------

        public static BOSSLookPreset CreateOrLoadPreset(string baseName, string parentFolderPath)
        {
            if (string.IsNullOrEmpty(baseName))
            {
                baseName = BOSSLookDefaults.DefaultBaseName;
            }

            if (string.IsNullOrEmpty(parentFolderPath))
            {
                parentFolderPath = BOSSLookDefaults.DefaultFolderRoot;
            }

            EnsureFolder(parentFolderPath);
            string presetFolder = $"{parentFolderPath}/{baseName}";
            EnsureFolder(presetFolder);

            string presetPath = $"{presetFolder}/{baseName}_Preset.asset";
            var preset = AssetDatabase.LoadAssetAtPath<BOSSLookPreset>(presetPath);
            if (preset == null)
            {
                preset = ScriptableObject.CreateInstance<BOSSLookPreset>();
                preset.baseName = baseName;
                preset.folderPath = presetFolder;
                preset.phase = BOSSLookPhase.NotCreated;
                ApplyDefaultsTo(preset);
                AssetDatabase.CreateAsset(preset, presetPath);
                AssetDatabase.SaveAssets();
            }
            else
            {
                preset.baseName = baseName;
                preset.folderPath = presetFolder;
            }

            return preset;
        }

        private static void ApplyDefaultsTo(BOSSLookPreset preset)
        {
            preset.environmentIntensity = BOSSLookDefaults.EnvironmentIntensity;
            preset.lightmapResolution = BOSSLookDefaults.LightmapResolution;
            preset.atlasSize = BOSSLookDefaults.AtlasSize;
            preset.directSamples = BOSSLookDefaults.DirectSamples;
            preset.indirectSamples = BOSSLookDefaults.IndirectSamples;
            preset.bounces = BOSSLookDefaults.Bounces;
            preset.compressLightmaps = BOSSLookDefaults.CompressLightmaps;
            preset.ambientOcclusion = BOSSLookDefaults.AmbientOcclusion;
            preset.probeSpacing = BOSSLookDefaults.ProbeSpacing;
            preset.verticalLayers = BOSSLookDefaults.VerticalLayers;
            preset.skipInsideColliders = BOSSLookDefaults.SkipInsideColliders;
            preset.colliderCheckRadius = BOSSLookDefaults.ColliderCheckRadius;
            preset.reflectionBoxPadding = BOSSLookDefaults.ReflectionBoxPadding;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }
            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            string leaf = Path.GetFileName(folderPath);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        // ---------------- Skybox ----------------

        public static void SetupSkybox(BOSSLookPreset preset)
        {
            if (preset == null || preset.hdriTexture == null)
            {
                Debug.LogWarning("[BOSS Look] HDRI texture is required.");
                return;
            }

            string matPath = $"{preset.folderPath}/{preset.baseName}_Skybox.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            bool isCubemap = preset.hdriTexture is Cubemap;
            Shader shader = isCubemap
                ? Shader.Find("Skybox/Cubemap")
                : Shader.Find("Skybox/Panoramic");

            if (shader == null)
            {
                Debug.LogError("[BOSS Look] Skybox shader not found.");
                return;
            }

            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            if (isCubemap)
            {
                mat.SetTexture("_Tex", preset.hdriTexture);
            }
            else
            {
                mat.SetTexture("_MainTex", preset.hdriTexture);
            }

            ApplySkyboxParams(mat, preset);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            preset.skyboxMaterial = mat;
            RenderSettings.skybox = mat;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.ambientIntensity = preset.environmentIntensity;
            RenderSettings.reflectionIntensity = preset.environmentIntensity;
            DynamicGI.UpdateEnvironment();
            EditorUtility.SetDirty(preset);
        }

        private static void ApplySkyboxParams(Material mat, BOSSLookPreset preset)
        {
            if (mat == null) return;
            if (mat.HasProperty("_Rotation")) mat.SetFloat("_Rotation", preset.skyboxRotation);
            if (mat.HasProperty("_Exposure")) mat.SetFloat("_Exposure", preset.skyboxExposure);
        }

        /// <summary>Live-applies rotation / exposure / intensity to the existing
        /// skybox without regenerating the material, for slider dragging.</summary>
        public static void ApplyEnvironmentLive(BOSSLookPreset preset)
        {
            if (preset == null || preset.skyboxMaterial == null) return;
            ApplySkyboxParams(preset.skyboxMaterial, preset);
            RenderSettings.ambientIntensity = preset.environmentIntensity;
            RenderSettings.reflectionIntensity = preset.environmentIntensity;
            EditorUtility.SetDirty(preset.skyboxMaterial);
            DynamicGI.UpdateEnvironment();
        }

        // ---------------- Lighting Settings ----------------

        public static LightingSettings CreateOrUpdateLightingSettings(BOSSLookPreset preset)
        {
            string path = $"{preset.folderPath}/{preset.baseName}_LightingSettings.lighting";
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (settings == null)
            {
                settings = new LightingSettings { name = $"{preset.baseName}_LightingSettings" };
                AssetDatabase.CreateAsset(settings, path);
            }

            settings.autoGenerate = false;
            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.lightmapResolution = preset.lightmapResolution;
            settings.lightmapMaxSize = preset.atlasSize;
            settings.directSampleCount = preset.directSamples;
            settings.indirectSampleCount = preset.indirectSamples;
            settings.maxBounces = preset.bounces;
            settings.lightmapper = preset.lightmapperKind;
            settings.compressLightmaps = preset.compressLightmaps;
            settings.ao = preset.ambientOcclusion;

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            preset.lightingSettings = settings;
            Lightmapping.lightingSettings = settings;
#if !UNITY_6000_0_OR_NEWER
            // Obsolete in Unity 6; settings.autoGenerate = false covers it there.
            Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;
#endif

            EditorUtility.SetDirty(preset);
            return settings;
        }

        // ---------------- Static Flags ----------------

        public static int ApplyStaticFlags(BOSSLookPreset preset, out List<GameObject> missingUv2)
        {
            missingUv2 = new List<GameObject>();
            var targets = ResolveStaticTargets(preset);
            int touched = 0;

            foreach (var go in targets)
            {
                if (go == null) continue;
                var flags = GameObjectUtility.GetStaticEditorFlags(go);
                flags |= StaticEditorFlags.ContributeGI;
                GameObjectUtility.SetStaticEditorFlags(go, flags);

                var mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && (mf.sharedMesh.uv2 == null || mf.sharedMesh.uv2.Length == 0))
                {
                    missingUv2.Add(go);
                }
                touched++;
            }
            return touched;
        }

        public static List<GameObject> ResolveStaticTargets(BOSSLookPreset preset)
        {
            var list = new List<GameObject>();
            switch (preset.staticTargetSource)
            {
                case StaticTargetSource.ManualList:
                    if (preset.staticTargets != null)
                    {
                        foreach (var go in preset.staticTargets)
                        {
                            if (go != null) list.Add(go);
                        }
                    }
                    break;
                case StaticTargetSource.LayerMask:
                    foreach (var go in SceneRelinkOps.FindAllInScene<MeshRenderer>())
                    {
                        if (((1 << go.gameObject.layer) & preset.targetLayerMask.value) != 0)
                        {
                            list.Add(go.gameObject);
                        }
                    }
                    break;
                case StaticTargetSource.Tag:
                    if (!string.IsNullOrEmpty(preset.targetTag))
                    {
                        foreach (var go in GameObject.FindGameObjectsWithTag(preset.targetTag))
                        {
                            list.Add(go);
                        }
                    }
                    break;
            }
            return list;
        }

        // ---------------- Light Probes ----------------

        public static LightProbeGroup CreateOrUpdateProbeGroup(BOSSLookPreset preset)
        {
            SceneRelinkOps.RelinkAll(preset);
            var group = preset.lightProbeGroup;
            if (group == null)
            {
                var go = new GameObject($"{preset.baseName}_LightProbeGroup");
                Undo.RegisterCreatedObjectUndo(go, "Create Light Probe Group");
                group = go.AddComponent<LightProbeGroup>();
                preset.lightProbeGroup = group;
            }

            var positions = GenerateProbeGrid(preset);
            group.probePositions = positions.ToArray();

            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(preset);
            return group;
        }

        private static List<Vector3> GenerateProbeGrid(BOSSLookPreset preset)
        {
            var positions = new List<Vector3>();
            var area = preset.probeArea;
            float spacing = Mathf.Max(0.1f, preset.probeSpacing);
            int layers = Mathf.Max(1, preset.verticalLayers);

            Vector3 origin = area.min;
            Vector3 size = area.size;

            int xCount = Mathf.Max(2, Mathf.CeilToInt(size.x / spacing) + 1);
            int zCount = Mathf.Max(2, Mathf.CeilToInt(size.z / spacing) + 1);
            float yStep = layers <= 1 ? 0f : size.y / (layers - 1);

            for (int yi = 0; yi < layers; yi++)
            {
                float y = origin.y + yi * yStep;
                for (int xi = 0; xi < xCount; xi++)
                {
                    float x = origin.x + Mathf.Min(xi * spacing, size.x);
                    for (int zi = 0; zi < zCount; zi++)
                    {
                        float z = origin.z + Mathf.Min(zi * spacing, size.z);
                        var p = new Vector3(x, y, z);
                        if (preset.skipInsideColliders &&
                            Physics.CheckSphere(p, preset.colliderCheckRadius))
                        {
                            continue;
                        }
                        positions.Add(p - preset.lightProbeGroup.transform.position);
                    }
                }
            }
            return positions;
        }

        // ---------------- Reflection Probes ----------------

        public static ReflectionProbe CreateOrUpdateReflectionProbe(BOSSLookPreset preset)
        {
            SceneRelinkOps.RelinkAll(preset);

            ReflectionProbe probe = null;
            if (preset.reflectionProbes != null && preset.reflectionProbes.Count > 0)
            {
                probe = preset.reflectionProbes[0];
            }

            if (probe == null)
            {
                var go = new GameObject($"{preset.baseName}_ReflectionProbe");
                Undo.RegisterCreatedObjectUndo(go, "Create Reflection Probe");
                probe = go.AddComponent<ReflectionProbe>();
                if (preset.reflectionProbes == null)
                {
                    preset.reflectionProbes = new List<ReflectionProbe>();
                }
                preset.reflectionProbes.Add(probe);
            }

            probe.mode = ReflectionProbeMode.Baked;
            probe.boxProjection = true;
            probe.resolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(preset.reflectionResolution), 16, 2048);
            probe.hdr = true;
            probe.center = preset.probeArea.center - probe.transform.position;
            probe.size = preset.probeArea.size + Vector3.one * preset.reflectionBoxPadding * 2f;

            // Mode B: render the probe background as neutral gray instead of the
            // skybox, so the HDRI sky never bakes into reflections. The sky still
            // contributes to ambient GI — this only affects the probe's own render.
            if (preset.reflectionMode == ReflectionBakeMode.B_NeutralReplace)
            {
                probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
                probe.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            }
            else
            {
                probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            }

            EditorUtility.SetDirty(probe);
            EditorUtility.SetDirty(preset);
            return probe;
        }

        // ---------------- Bake ----------------

        public static bool CanBake(BOSSLookPreset preset, out string reason)
        {
            if (preset.phase == BOSSLookPhase.Finalized)
            {
                reason = "AR化済み (Finalized) のため再ベイクはブロックされます。先に「AR化を解除」してください。";
                return false;
            }
            if (RenderSettings.skybox == null)
            {
                reason = "スカイボックスが未設定です。Step 1 でHDRIを設定してください。";
                return false;
            }
            if (preset.lightingSettings == null)
            {
                reason = "Lighting Settings が未生成です。Step 2 を完了してください。";
                return false;
            }
            reason = null;
            return true;
        }

        public static void StartBake(BOSSLookPreset preset)
        {
            if (!CanBake(preset, out string reason))
            {
                EditorUtility.DisplayDialog("BOSS Look Preset", reason, "OK");
                return;
            }
#if !UNITY_6000_0_OR_NEWER
            Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;
#endif
            Lightmapping.lightingSettings = preset.lightingSettings;
            Lightmapping.BakeAsync();
        }

        public static void CancelBake()
        {
            if (Lightmapping.isRunning)
            {
                Lightmapping.Cancel();
            }
        }

        // ---------------- Finalize (AR化) ----------------

        public static void Finalize(BOSSLookPreset preset)
        {
            if (preset == null) return;
            if (Lightmapping.isRunning)
            {
                EditorUtility.DisplayDialog("BOSS Look Preset",
                    "ベイク実行中はAR化できません。完了を待つかキャンセルしてください。", "OK");
                return;
            }

            Undo.RecordObject(preset, "AR化 (Finalize)");
            preset.stashedSkybox = RenderSettings.skybox;
            RenderSettings.skybox = null;
            DynamicGI.UpdateEnvironment();
            preset.phase = BOSSLookPhase.Finalized;
            EditorUtility.SetDirty(preset);
        }

        public static void Unfinalize(BOSSLookPreset preset)
        {
            if (preset == null) return;
            Undo.RecordObject(preset, "AR化を解除 (Unfinalize)");
            Material restore = preset.stashedSkybox != null ? preset.stashedSkybox : preset.skyboxMaterial;
            if (restore != null)
            {
                RenderSettings.skybox = restore;
                DynamicGI.UpdateEnvironment();
            }
            preset.phase = BOSSLookPhase.Active;
            EditorUtility.SetDirty(preset);
        }

        // ---------------- One-click setup ----------------

        /// <summary>Runs Step 1–5 in one go with scene-derived bounds.
        /// Everything it does is the same as the individual steps, so the
        /// result remains fully adjustable afterwards.</summary>
        public static string AutoSetupAll(BOSSLookPreset preset)
        {
            if (preset == null) return "プリセットがありません。";
            if (preset.hdriTexture == null) return "HDRI テクスチャを先に指定してください (Step 1)。";

            var report = new StringBuilder();

            SetupSkybox(preset);
            report.AppendLine("✓ スカイボックス生成・環境光を Skybox に設定");

            CreateOrUpdateLightingSettings(preset);
            report.AppendLine("✓ Lighting Settings 生成 (Auto Generate OFF)");

            // Scene bounds from every mesh renderer
            var renderers = new List<MeshRenderer>(SceneRelinkOps.FindAllInScene<MeshRenderer>());
            renderers.RemoveAll(r => r == null);
            if (renderers.Count > 0)
            {
                Bounds b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                b.Expand(0.5f);
                preset.probeArea = b;
                report.AppendLine($"✓ プローブ範囲をシーン全体から自動設定 ({renderers.Count} renderers)");
            }
            else
            {
                report.AppendLine("⚠ MeshRenderer が無いためプローブ範囲は既定値のまま");
            }

            // Static targets: if the manual list is empty, fill it with the whole
            // scene so the user can prune it afterwards instead of starting blind.
            if (preset.staticTargetSource == StaticTargetSource.ManualList &&
                (preset.staticTargets == null || preset.staticTargets.Count == 0))
            {
                preset.staticTargets = new List<GameObject>();
                foreach (var r in renderers) preset.staticTargets.Add(r.gameObject);
                report.AppendLine($"✓ スタティック対象リストにシーンの全 MeshRenderer を投入 ({renderers.Count} 個)");
            }
            int flagged = ApplyStaticFlags(preset, out var missingUv2);
            report.AppendLine($"✓ ContributeGI 付与 ({flagged} 個)");
            if (missingUv2.Count > 0)
            {
                report.AppendLine($"⚠ UV2 が空のメッシュ {missingUv2.Count} 個 (Generate Lightmap UVs を確認)");
            }

            CreateOrUpdateProbeGroup(preset);
            report.AppendLine("✓ ライトプローブグリッド生成");

            CreateOrUpdateReflectionProbe(preset);
            report.AppendLine("✓ リフレクションプローブ生成 (Baked)");

            EditorUtility.SetDirty(preset);
            return report.ToString();
        }
    }
}
