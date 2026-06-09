using System.Collections.Generic;
using DarataBOSS.BOSSLookPreset.Editor.Ops;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarataBOSS.BOSSLookPreset.Editor
{
    public class BOSSLookWizard : EditorWindow
    {
        private enum Module { A_Environment, B_LightRig, C_PostProcess }
        private enum EnvStep
        {
            S0_NameFolder = 0,
            S1_HDRI = 1,
            S2_LightingSettings = 2,
            S3_StaticFlags = 3,
            S4_LightProbes = 4,
            S5_ReflectionProbes = 5,
            S6_Bake = 6,
            S7_Finalize = 7,
        }

        private BOSSLookPreset preset;
        private Module currentModule = Module.A_Environment;
        private EnvStep currentStep = EnvStep.S0_NameFolder;
        private string newBaseName = BOSSLookDefaults.DefaultBaseName;
        private string newFolderPath = BOSSLookDefaults.DefaultFolderRoot;
        private Vector2 scroll;
        private List<GameObject> lastMissingUv2 = new List<GameObject>();
        private bool bakeWasRunning;

        [MenuItem("BOSS/Look Preset")]
        public static void Open()
        {
            var w = GetWindow<BOSSLookWizard>("BOSS Look Preset");
            w.minSize = new Vector2(420f, 520f);
            w.Show();
        }

        private void OnEnable()
        {
            Lightmapping.bakeCompleted += OnBakeCompleted;
        }

        private void OnDisable()
        {
            Lightmapping.bakeCompleted -= OnBakeCompleted;
        }

        private void OnBakeCompleted()
        {
            Repaint();
        }

        private void OnInspectorUpdate()
        {
            if (Lightmapping.isRunning != bakeWasRunning)
            {
                bakeWasRunning = Lightmapping.isRunning;
                Repaint();
            }
            else if (Lightmapping.isRunning)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space(4);
            DrawModuleTabs();
            EditorGUILayout.Space(4);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            switch (currentModule)
            {
                case Module.A_Environment:
                    DrawModuleA();
                    break;
                case Module.B_LightRig:
                    DrawModuleB();
                    break;
                case Module.C_PostProcess:
                    DrawModuleC();
                    break;
            }
            EditorGUILayout.EndScrollView();
        }

        // ---------------- Header ----------------

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("BOSS Look Preset", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                var picked = (BOSSLookPreset)EditorGUILayout.ObjectField(
                    "Preset", preset, typeof(BOSSLookPreset), false);
                if (EditorGUI.EndChangeCheck())
                {
                    preset = picked;
                    if (preset != null)
                    {
                        newBaseName = preset.baseName;
                        newFolderPath = SafeParent(preset.folderPath);
                    }
                }
            }

            if (preset != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Phase", GUILayout.Width(80));
                    EditorGUILayout.LabelField(preset.phase.ToString(), EditorStyles.boldLabel);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("プリセット未選択。Module A の Step 0 で新規作成、または既存プリセットを上のスロットにドラッグしてください。",
                    MessageType.Info);
            }
        }

        private void DrawModuleTabs()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                DrawTab("A: 環境光 / ベイク", Module.A_Environment);
                DrawTab("B: ライトリグ", Module.B_LightRig);
                DrawTab("C: ポストプロセス", Module.C_PostProcess);
            }
        }

        private void DrawTab(string label, Module m)
        {
            bool on = currentModule == m;
            if (GUILayout.Toggle(on, label, EditorStyles.toolbarButton) != on)
            {
                currentModule = m;
            }
        }

        // ---------------- Module A: Environment ----------------

        private void DrawModuleA()
        {
            DrawStepStrip();
            EditorGUILayout.Space(4);

            switch (currentStep)
            {
                case EnvStep.S0_NameFolder: DrawS0(); break;
                case EnvStep.S1_HDRI: DrawS1(); break;
                case EnvStep.S2_LightingSettings: DrawS2(); break;
                case EnvStep.S3_StaticFlags: DrawS3(); break;
                case EnvStep.S4_LightProbes: DrawS4(); break;
                case EnvStep.S5_ReflectionProbes: DrawS5(); break;
                case EnvStep.S6_Bake: DrawS6(); break;
                case EnvStep.S7_Finalize: DrawS7(); break;
            }

            EditorGUILayout.Space(8);
            DrawNextBack();
        }

        private void DrawStepStrip()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                for (int i = 0; i <= 7; i++)
                {
                    var s = (EnvStep)i;
                    bool on = currentStep == s;
                    if (GUILayout.Toggle(on, i.ToString(), EditorStyles.toolbarButton, GUILayout.Width(28f)) != on)
                    {
                        currentStep = s;
                    }
                }
            }
            EditorGUILayout.LabelField($"Step {(int)currentStep}: {StepTitle(currentStep)}", EditorStyles.boldLabel);
        }

        private static string StepTitle(EnvStep s)
        {
            switch (s)
            {
                case EnvStep.S0_NameFolder: return "名前 / フォルダ";
                case EnvStep.S1_HDRI: return "HDRI / スカイボックス";
                case EnvStep.S2_LightingSettings: return "Lighting Settings";
                case EnvStep.S3_StaticFlags: return "スタティック設定";
                case EnvStep.S4_LightProbes: return "ライトプローブ";
                case EnvStep.S5_ReflectionProbes: return "リフレクションプローブ";
                case EnvStep.S6_Bake: return "ベイク";
                case EnvStep.S7_Finalize: return "AR化 (仕上げ)";
                default: return "";
            }
        }

        // ---- S0 ----
        private void DrawS0()
        {
            EditorGUILayout.LabelField("プリセットのベース名と親フォルダを指定し、生成します。",
                EditorStyles.wordWrappedLabel);
            newBaseName = EditorGUILayout.TextField("Base Name", newBaseName);
            newFolderPath = EditorGUILayout.TextField("Parent Folder", newFolderPath);

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(newBaseName)))
            {
                if (GUILayout.Button(preset == null ? "プリセットを作成 (初期値を書き込む)" : "プリセットを更新 (フォルダ / 名前)"))
                {
                    preset = EnvironmentOps.CreateOrLoadPreset(newBaseName, newFolderPath);
                    if (preset.phase == BOSSLookPhase.NotCreated)
                    {
                        preset.phase = BOSSLookPhase.Active;
                        EditorUtility.SetDirty(preset);
                    }
                    Selection.activeObject = preset;
                }
            }

            if (preset != null)
            {
                EditorGUILayout.HelpBox($"生成先: {preset.folderPath}", MessageType.None);
            }
        }

        // ---- S1 ----
        private void DrawS1()
        {
            if (!RequirePreset()) return;

            preset.hdriTexture = (Texture)EditorGUILayout.ObjectField(
                "HDRI Texture", preset.hdriTexture, typeof(Texture), false);
            preset.environmentIntensity = EditorGUILayout.Slider(
                "Environment Intensity", preset.environmentIntensity, 0f, 8f);

            EditorGUILayout.HelpBox(
                "Texture2D ならパノラマ (Skybox/Panoramic)、Cubemap なら Skybox/Cubemap として自動でスカイボックスマテリアルを作ります。",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(preset.hdriTexture == null))
            {
                if (GUILayout.Button("スカイボックスを生成 / 更新"))
                {
                    EnvironmentOps.SetupSkybox(preset);
                }
            }

            preset.skyboxMaterial = (Material)EditorGUILayout.ObjectField(
                "Skybox Material", preset.skyboxMaterial, typeof(Material), false);
        }

        // ---- S2 ----
        private void DrawS2()
        {
            if (!RequirePreset()) return;

            preset.lightmapResolution = EditorGUILayout.FloatField("Lightmap Resolution", preset.lightmapResolution);
            preset.atlasSize = EditorGUILayout.IntPopup("Atlas Size",
                preset.atlasSize,
                new[] { "512", "1024", "2048", "4096" },
                new[] { 512, 1024, 2048, 4096 });
            preset.directSamples = EditorGUILayout.IntField("Direct Samples", preset.directSamples);
            preset.indirectSamples = EditorGUILayout.IntField("Indirect Samples", preset.indirectSamples);
            preset.bounces = EditorGUILayout.IntSlider("Bounces", preset.bounces, 0, 4);
            preset.lightmapperKind = (LightingSettings.Lightmapper)EditorGUILayout.EnumPopup(
                "Lightmapper", preset.lightmapperKind);
            preset.compressLightmaps = EditorGUILayout.Toggle("Compress Lightmaps", preset.compressLightmaps);
            preset.ambientOcclusion = EditorGUILayout.Toggle("Ambient Occlusion", preset.ambientOcclusion);

            if (GUILayout.Button("Lighting Settings を生成 / 更新 (Auto Generate OFF)"))
            {
                EnvironmentOps.CreateOrUpdateLightingSettings(preset);
            }

            preset.lightingSettings = (LightingSettings)EditorGUILayout.ObjectField(
                "Lighting Settings", preset.lightingSettings, typeof(LightingSettings), false);
        }

        // ---- S3 ----
        private void DrawS3()
        {
            if (!RequirePreset()) return;

            preset.staticTargetSource = (StaticTargetSource)EditorGUILayout.EnumPopup(
                "Target Source", preset.staticTargetSource);

            switch (preset.staticTargetSource)
            {
                case StaticTargetSource.ManualList:
                    EditorGUILayout.HelpBox("対象 GameObject をリストに追加するか、選択中の GameObject を一括追加してください。", MessageType.Info);
                    var serialized = new SerializedObject(preset);
                    EditorGUILayout.PropertyField(serialized.FindProperty("staticTargets"), true);
                    serialized.ApplyModifiedProperties();
                    if (GUILayout.Button("選択中の GameObject を追加"))
                    {
                        foreach (var go in Selection.gameObjects)
                        {
                            if (go != null && !preset.staticTargets.Contains(go))
                            {
                                preset.staticTargets.Add(go);
                            }
                        }
                        EditorUtility.SetDirty(preset);
                    }
                    break;
                case StaticTargetSource.LayerMask:
                    preset.targetLayerMask = LayerMaskField("Target Layers", preset.targetLayerMask);
                    break;
                case StaticTargetSource.Tag:
                    preset.targetTag = EditorGUILayout.TagField("Target Tag", preset.targetTag);
                    break;
            }

            if (GUILayout.Button("ContributeGI を付与"))
            {
                int n = EnvironmentOps.ApplyStaticFlags(preset, out lastMissingUv2);
                Debug.Log($"[BOSS Look] ContributeGI を {n} 個に付与しました。");
            }

            if (lastMissingUv2 != null && lastMissingUv2.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{lastMissingUv2.Count} 個のメッシュで UV2 が空です。ImportSettings で Generate Lightmap UVs を有効にすると解決します。",
                    MessageType.Warning);
            }
        }

        // ---- S4 ----
        private void DrawS4()
        {
            if (!RequirePreset()) return;

            EditorGUILayout.LabelField("Probe Area (World Bounds)", EditorStyles.boldLabel);
            preset.probeArea.center = EditorGUILayout.Vector3Field("Center", preset.probeArea.center);
            preset.probeArea.size = EditorGUILayout.Vector3Field("Size", preset.probeArea.size);

            if (GUILayout.Button("選択中のバウンディングから設定"))
            {
                TrySetProbeAreaFromSelection();
            }

            preset.probeSpacing = EditorGUILayout.FloatField("Probe Spacing (m)", preset.probeSpacing);
            preset.verticalLayers = EditorGUILayout.IntSlider("Vertical Layers", preset.verticalLayers, 1, 6);
            preset.skipInsideColliders = EditorGUILayout.Toggle("Skip Inside Colliders", preset.skipInsideColliders);
            using (new EditorGUI.DisabledScope(!preset.skipInsideColliders))
            {
                preset.colliderCheckRadius = EditorGUILayout.FloatField("Collider Check Radius", preset.colliderCheckRadius);
            }

            if (GUILayout.Button("ライトプローブグリッドを生成 / 更新"))
            {
                EnvironmentOps.CreateOrUpdateProbeGroup(preset);
            }

            preset.lightProbeGroup = (LightProbeGroup)EditorGUILayout.ObjectField(
                "Light Probe Group", preset.lightProbeGroup, typeof(LightProbeGroup), true);
        }

        private void TrySetProbeAreaFromSelection()
        {
            var sel = Selection.gameObjects;
            if (sel == null || sel.Length == 0) return;
            Bounds? b = null;
            foreach (var go in sel)
            {
                var r = go.GetComponentInChildren<Renderer>();
                if (r == null) continue;
                if (b == null) b = r.bounds;
                else { var tmp = b.Value; tmp.Encapsulate(r.bounds); b = tmp; }
            }
            if (b.HasValue)
            {
                preset.probeArea = b.Value;
                EditorUtility.SetDirty(preset);
            }
        }

        // ---- S5 ----
        private void DrawS5()
        {
            if (!RequirePreset()) return;

            preset.reflectionMode = (ReflectionBakeMode)EditorGUILayout.EnumPopup(
                "Reflection Mode", preset.reflectionMode);
            EditorGUILayout.HelpBox(
                preset.reflectionMode == ReflectionBakeMode.A_UseHDRI
                    ? "A: HDRI そのままで焼きます。空が反射に焼き込まれる点に注意。"
                    : "B: ニュートラルなスカイボックスに一時的に差し替えて焼きます (今回は実装未完: A と同じ動作)。",
                MessageType.Info);

            preset.reflectionBoxPadding = EditorGUILayout.FloatField("Box Padding", preset.reflectionBoxPadding);

            if (GUILayout.Button("Reflection Probe を生成 / 更新 (Baked)"))
            {
                EnvironmentOps.CreateOrUpdateReflectionProbe(preset);
            }

            var serialized = new SerializedObject(preset);
            EditorGUILayout.PropertyField(serialized.FindProperty("reflectionProbes"), true);
            serialized.ApplyModifiedProperties();
        }

        // ---- S6 ----
        private void DrawS6()
        {
            if (!RequirePreset()) return;

            bool canBake = EnvironmentOps.CanBake(preset, out string reason);
            if (!canBake)
            {
                EditorGUILayout.HelpBox(reason, MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Lightmapping.BakeAsync で実行します。完了するまで Next は無効です。",
                    MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(!canBake || Lightmapping.isRunning))
            {
                if (GUILayout.Button("ベイクを実行"))
                {
                    EnvironmentOps.StartBake(preset);
                }
            }

            if (Lightmapping.isRunning)
            {
                EditorGUILayout.LabelField($"Baking... {Lightmapping.buildProgress * 100f:F1}%");
                if (GUILayout.Button("ベイクをキャンセル"))
                {
                    EnvironmentOps.CancelBake();
                }
            }
        }

        // ---- S7 ----
        private void DrawS7()
        {
            if (!RequirePreset()) return;

            EditorGUILayout.HelpBox(
                "「AR化する」を押すとスカイボックスを外し Finalized 状態になります。以後ベイクはブロックされます。\n" +
                "「AR化を解除」で Active に戻り、再ベイクが可能になります。",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(preset.phase == BOSSLookPhase.Finalized))
            {
                if (GUILayout.Button("AR化する (スカイボックスOFF)"))
                {
                    EnvironmentOps.Finalize(preset);
                }
            }
            using (new EditorGUI.DisabledScope(preset.phase != BOSSLookPhase.Finalized))
            {
                if (GUILayout.Button("AR化を解除 (スカイボックス復元)"))
                {
                    EnvironmentOps.Unfinalize(preset);
                }
            }
        }

        // ---- Next / Back ----
        private void DrawNextBack()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope((int)currentStep <= 0))
                {
                    if (GUILayout.Button("< Back", GUILayout.Width(80)))
                    {
                        currentStep = (EnvStep)Mathf.Max(0, (int)currentStep - 1);
                    }
                }
                GUILayout.FlexibleSpace();
                bool canGoNext = CanAdvance(currentStep);
                using (new EditorGUI.DisabledScope(!canGoNext || (int)currentStep >= 7))
                {
                    if (GUILayout.Button("Next >", GUILayout.Width(80)))
                    {
                        currentStep = (EnvStep)Mathf.Min(7, (int)currentStep + 1);
                    }
                }
            }
        }

        private bool CanAdvance(EnvStep s)
        {
            switch (s)
            {
                case EnvStep.S0_NameFolder: return preset != null;
                case EnvStep.S1_HDRI: return preset != null && preset.skyboxMaterial != null;
                case EnvStep.S2_LightingSettings: return preset != null && preset.lightingSettings != null;
                case EnvStep.S6_Bake: return !Lightmapping.isRunning;
                default: return preset != null;
            }
        }

        // ---------------- Module B: Light Rig ----------------

        private void DrawModuleB()
        {
            if (!RequirePreset()) return;

            EditorGUILayout.LabelField("3点ライトリグ (Spot / Area)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "被写体を中心に Key / Fill / Back を配置します。既定は Mixed なのでベイクループに乗ります。",
                MessageType.Info);

            preset.subject = (Transform)EditorGUILayout.ObjectField("Subject", preset.subject, typeof(Transform), true);
            preset.rigLightKind = (RigLightKind)EditorGUILayout.EnumPopup("Light Type", preset.rigLightKind);
            preset.keyIntensity = EditorGUILayout.FloatField("Key Intensity", preset.keyIntensity);
            preset.keyFillRatio = EditorGUILayout.Slider("Key:Fill Ratio", preset.keyFillRatio, 1f, 8f);
            preset.keyColorTemperatureKelvin = EditorGUILayout.FloatField("Key Color Temp (K)", preset.keyColorTemperatureKelvin);
            preset.fillColorTemperatureKelvin = EditorGUILayout.FloatField("Fill Color Temp (K)", preset.fillColorTemperatureKelvin);
            preset.backColorTemperatureKelvin = EditorGUILayout.FloatField("Back Color Temp (K)", preset.backColorTemperatureKelvin);

            if (preset.rigLightKind == RigLightKind.Spot)
            {
                preset.spotAngleDeg = EditorGUILayout.Slider("Spot Angle", preset.spotAngleDeg, 1f, 179f);
            }
            else
            {
                preset.areaSize = EditorGUILayout.Vector2Field("Area Size", preset.areaSize);
            }

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("既存ディレクショナルライトを退避"))
                {
                    LightRigOps.StashExistingDirectionalLights(preset);
                }
                if (GUILayout.Button("退避を復元"))
                {
                    LightRigOps.RestoreStashedLights(preset);
                }
            }
            if (GUILayout.Button("退避ライトを完全に削除"))
            {
                if (EditorUtility.DisplayDialog("BOSS Look Preset",
                        "退避中のディレクショナルライトをシーンから削除します。よろしいですか?",
                        "削除", "キャンセル"))
                {
                    LightRigOps.DeleteStashedLights(preset);
                }
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("3点リグを生成 / 更新"))
            {
                LightRigOps.CreateOrUpdateRig(preset);
            }
            if (GUILayout.Button("リグを削除"))
            {
                if (EditorUtility.DisplayDialog("BOSS Look Preset",
                        "ライトリグをシーンから削除します。よろしいですか?",
                        "削除", "キャンセル"))
                {
                    LightRigOps.DeleteRig(preset);
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Generated", EditorStyles.boldLabel);
            preset.rigRoot = (GameObject)EditorGUILayout.ObjectField("Rig Root", preset.rigRoot, typeof(GameObject), true);
            preset.keyLight = (Light)EditorGUILayout.ObjectField("Key", preset.keyLight, typeof(Light), true);
            preset.fillLight = (Light)EditorGUILayout.ObjectField("Fill", preset.fillLight, typeof(Light), true);
            preset.backLight = (Light)EditorGUILayout.ObjectField("Back", preset.backLight, typeof(Light), true);
        }

        // ---------------- Module C: Post Process ----------------

        private void DrawModuleC()
        {
            if (!RequirePreset()) return;

            EditorGUILayout.LabelField("ポストプロセス (PPv2)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Built-in RP 用 Post Processing Stack v2。Profile + Volume + Layer 一式をセットアップします。\n" +
                "状態機械の外なので AR化後でも操作できます。",
                MessageType.Info);

            if (!PostProcessOps.IsPPv2Available)
            {
                EditorGUILayout.HelpBox(
                    "Post Processing Stack v2 が見つかりません。Package Manager から com.unity.postprocessing を追加してください。",
                    MessageType.Error);
                return;
            }

            if (!PostProcessOps.IsLinearColorSpace)
            {
                EditorGUILayout.HelpBox(
                    "Color Space が Linear ではありません。Player Settings → Other Settings で Linear に変更することを推奨します。",
                    MessageType.Warning);
            }

            preset.postBloomEnabled = EditorGUILayout.Toggle("Bloom", preset.postBloomEnabled);
            preset.postColorGradingEnabled = EditorGUILayout.Toggle("Color Grading", preset.postColorGradingEnabled);
            preset.postVignetteEnabled = EditorGUILayout.Toggle("Vignette", preset.postVignetteEnabled);
            preset.postDepthOfFieldEnabled = EditorGUILayout.Toggle("Depth of Field", preset.postDepthOfFieldEnabled);
            preset.postMotionBlurEnabled = EditorGUILayout.Toggle("Motion Blur", preset.postMotionBlurEnabled);
            preset.postAOEnabled = EditorGUILayout.Toggle("Ambient Occlusion", preset.postAOEnabled);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("ポストプロセスをセットアップ / 更新"))
            {
                PostProcessOps.Setup(preset);
            }
            if (GUILayout.Button("ポストプロセスを外す (Volume / Layer 除去)"))
            {
                if (EditorUtility.DisplayDialog("BOSS Look Preset",
                        "Post Process Volume と Layer をシーンから除去します。Profile アセットは残します。",
                        "外す", "キャンセル"))
                {
                    PostProcessOps.Remove(preset);
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Generated", EditorStyles.boldLabel);
#if BOSS_LOOK_PRESET_HAS_PPV2
            preset.postProfile = (UnityEngine.Rendering.PostProcessing.PostProcessProfile)EditorGUILayout.ObjectField(
                "Post Profile", preset.postProfile, typeof(UnityEngine.Rendering.PostProcessing.PostProcessProfile), false);
            preset.postVolume = (UnityEngine.Rendering.PostProcessing.PostProcessVolume)EditorGUILayout.ObjectField(
                "Post Volume", preset.postVolume, typeof(UnityEngine.Rendering.PostProcessing.PostProcessVolume), true);
            preset.postLayer = (UnityEngine.Rendering.PostProcessing.PostProcessLayer)EditorGUILayout.ObjectField(
                "Post Layer", preset.postLayer, typeof(UnityEngine.Rendering.PostProcessing.PostProcessLayer), true);
#else
            EditorGUILayout.HelpBox("PPv2 が無いため参照を表示できません。", MessageType.None);
#endif
        }

        // ---------------- Helpers ----------------

        private bool RequirePreset()
        {
            if (preset == null)
            {
                EditorGUILayout.HelpBox("プリセットが選択されていません。Module A / Step 0 で新規作成してください。",
                    MessageType.Warning);
                return false;
            }
            return true;
        }

        private static string SafeParent(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return BOSSLookDefaults.DefaultFolderRoot;
            int slash = folderPath.LastIndexOf('/');
            return slash > 0 ? folderPath.Substring(0, slash) : folderPath;
        }

        private static LayerMask LayerMaskField(string label, LayerMask mask)
        {
            var layers = new List<string>();
            var indices = new List<int>();
            for (int i = 0; i < 32; i++)
            {
                string n = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(n))
                {
                    layers.Add(n);
                    indices.Add(i);
                }
            }
            int compactMask = 0;
            for (int i = 0; i < indices.Count; i++)
            {
                if ((mask.value & (1 << indices[i])) != 0) compactMask |= (1 << i);
            }
            int newCompact = EditorGUILayout.MaskField(label, compactMask, layers.ToArray());
            int newMask = 0;
            for (int i = 0; i < indices.Count; i++)
            {
                if ((newCompact & (1 << i)) != 0) newMask |= (1 << indices[i]);
            }
            return newMask;
        }
    }
}
