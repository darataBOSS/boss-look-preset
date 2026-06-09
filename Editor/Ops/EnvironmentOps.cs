using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Oborokurage.BOSSLookPreset.Editor.Ops
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
            Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;

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
                    foreach (var go in Object.FindObjectsOfType<MeshRenderer>())
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
            probe.center = preset.probeArea.center - probe.transform.position;
            probe.size = preset.probeArea.size + Vector3.one * preset.reflectionBoxPadding * 2f;

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
            Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;
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

            preset.stashedSkybox = RenderSettings.skybox;
            RenderSettings.skybox = null;
            DynamicGI.UpdateEnvironment();
            preset.phase = BOSSLookPhase.Finalized;
            EditorUtility.SetDirty(preset);
        }

        public static void Unfinalize(BOSSLookPreset preset)
        {
            if (preset == null) return;
            Material restore = preset.stashedSkybox != null ? preset.stashedSkybox : preset.skyboxMaterial;
            if (restore != null)
            {
                RenderSettings.skybox = restore;
                DynamicGI.UpdateEnvironment();
            }
            preset.phase = BOSSLookPhase.Active;
            EditorUtility.SetDirty(preset);
        }
    }
}
