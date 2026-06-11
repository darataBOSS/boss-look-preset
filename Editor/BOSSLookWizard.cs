using System.Collections.Generic;
using DarataBOSS.BOSSLookPreset.Editor.Ops;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarataBOSS.BOSSLookPreset.Editor
{
    public class BOSSLookWizard : EditorWindow
    {
        private enum Module { A_Environment, B_LightRig, C_PostProcess, D_Finishing, E_Lint }
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

        [SerializeField] private BOSSLookPreset preset;
        private Module currentModule = Module.A_Environment;
        private EnvStep currentStep = EnvStep.S0_NameFolder;
        private string newBaseName = BOSSLookDefaults.DefaultBaseName;
        private string newFolderPath = BOSSLookDefaults.DefaultFolderRoot;
        private Vector2 scroll;
        private List<GameObject> lastMissingUv2 = new List<GameObject>();
        private bool bakeWasRunning;
        private readonly BoxBoundsHandle probeAreaHandle = new BoxBoundsHandle();

        private static string LastPresetPrefKey =>
            "BOSSLook.LastPresetGUID." + Application.dataPath.GetHashCode();

        // Button palette: green = generate, blue = auto, orange = bake,
        // purple = finalize, red = destructive.
        private static readonly Color ColorGenerate = new Color(0.6f, 0.9f, 0.6f);
        private static readonly Color ColorAuto = new Color(0.5f, 0.78f, 1f);
        private static readonly Color ColorBake = new Color(1f, 0.72f, 0.35f);
        private static readonly Color ColorFinalize = new Color(0.82f, 0.65f, 1f);
        private static readonly Color ColorDanger = new Color(1f, 0.55f, 0.55f);
        private static readonly Color ColorStepDone = new Color(0.6f, 1f, 0.6f);

        private static bool ColoredButton(string label, Color tint, params GUILayoutOption[] options)
        {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = tint;
            bool pressed = GUILayout.Button(label, options);
            GUI.backgroundColor = prev;
            return pressed;
        }

        /// <summary>Seeds Base Name / Parent Folder from the open scene so the
        /// generated assets land next to (and named after) the scene.</summary>
        private void ApplySceneBasedDefaults(bool force)
        {
            if (!force &&
                (newBaseName != BOSSLookDefaults.DefaultBaseName ||
                 newFolderPath != BOSSLookDefaults.DefaultFolderRoot))
            {
                return; // the user already typed something — don't stomp it
            }

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(scene.name))
            {
                newBaseName = scene.name;
            }
            if (!string.IsNullOrEmpty(scene.path))
            {
                string dir = System.IO.Path.GetDirectoryName(scene.path)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(dir)) newFolderPath = dir;
            }
        }

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
            SceneView.duringSceneGui += OnSceneGUI;

            if (preset == null)
            {
                TryAutoLoadPreset();
            }
            if (preset != null)
            {
                SceneRelinkOps.RelinkAll(preset);
                AdoptPreset(preset, jumpToStep: false);
            }
            else
            {
                ApplySceneBasedDefaults(force: false);
            }
        }

        private void OnDisable()
        {
            Lightmapping.bakeCompleted -= OnBakeCompleted;
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnBakeCompleted()
        {
            Repaint();
        }

        private void TryAutoLoadPreset()
        {
            // Last used preset first, then "the only one in the project".
            string guid = EditorPrefs.GetString(LastPresetPrefKey, null);
            if (!string.IsNullOrEmpty(guid))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                {
                    preset = AssetDatabase.LoadAssetAtPath<BOSSLookPreset>(path);
                }
            }
            if (preset == null)
            {
                var guids = AssetDatabase.FindAssets("t:BOSSLookPreset");
                if (guids.Length == 1)
                {
                    preset = AssetDatabase.LoadAssetAtPath<BOSSLookPreset>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }
        }

        /// <summary>Common handling when a preset becomes current: relink scene
        /// refs, remember it, sync the name fields, optionally resume at the
        /// first incomplete step.</summary>
        private void AdoptPreset(BOSSLookPreset p, bool jumpToStep)
        {
            preset = p;
            if (preset == null) return;

            SceneRelinkOps.RelinkAll(preset);
            newBaseName = preset.baseName;
            newFolderPath = SafeParent(preset.folderPath);

            string path = AssetDatabase.GetAssetPath(preset);
            if (!string.IsNullOrEmpty(path))
            {
                EditorPrefs.SetString(LastPresetPrefKey, AssetDatabase.AssetPathToGUID(path));
            }

            if (jumpToStep)
            {
                currentStep = FirstIncompleteStep();
            }
        }

        private EnvStep FirstIncompleteStep()
        {
            for (int i = 0; i <= 7; i++)
            {
                if (!IsStepComplete((EnvStep)i)) return (EnvStep)i;
            }
            return EnvStep.S7_Finalize;
        }

        private bool IsStepComplete(EnvStep s)
        {
            if (preset == null) return false;
            switch (s)
            {
                case EnvStep.S0_NameFolder:
                    return true; // preset exists
                case EnvStep.S1_HDRI:
                    return preset.hdriTexture != null && preset.skyboxMaterial != null;
                case EnvStep.S2_LightingSettings:
                    return preset.lightingSettings != null;
                case EnvStep.S3_StaticFlags:
                    // Layer/Tag mode counts as configured; manual mode needs entries.
                    return preset.staticTargetSource != StaticTargetSource.ManualList
                        || (preset.staticTargets != null && preset.staticTargets.Count > 0);
                case EnvStep.S4_LightProbes:
                    return preset.lightProbeGroup != null;
                case EnvStep.S5_ReflectionProbes:
                    return preset.reflectionProbes != null && preset.reflectionProbes.Count > 0
                        && preset.reflectionProbes[0] != null;
                case EnvStep.S6_Bake:
                    return Lightmapping.lightingDataAsset != null;
                case EnvStep.S7_Finalize:
                    return preset.phase == BOSSLookPhase.Finalized;
                default:
                    return false;
            }
        }

        // ---------------- Scene view (probe area gizmo) ----------------

        private void OnSceneGUI(SceneView sceneView)
        {
            if (preset == null) return;
            if (currentModule != Module.A_Environment || currentStep != EnvStep.S4_LightProbes) return;

            Handles.color = new Color(0.3f, 1f, 0.6f, 1f);
            probeAreaHandle.center = preset.probeArea.center;
            probeAreaHandle.size = preset.probeArea.size;
            probeAreaHandle.wireframeColor = Handles.color;
            probeAreaHandle.handleColor = Color.white;

            EditorGUI.BeginChangeCheck();
            probeAreaHandle.DrawHandle();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(preset, "Edit Probe Area");
                preset.probeArea = new Bounds(probeAreaHandle.center, probeAreaHandle.size);
                EditorUtility.SetDirty(preset);
                Repaint();
            }

            Handles.Label(preset.probeArea.center + Vector3.up * (preset.probeArea.extents.y + 0.3f),
                "Probe Area (BOSS Look)");
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
                case Module.D_Finishing:
                    DrawModuleD();
                    break;
                case Module.E_Lint:
                    DrawModuleE();
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
                    if (picked != null)
                    {
                        AdoptPreset(picked, jumpToStep: true);
                    }
                    else
                    {
                        preset = null;
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

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Render Pipeline", GUILayout.Width(110));
                EditorGUILayout.LabelField(RenderPipelineDetector.DisplayName, EditorStyles.miniBoldLabel);
            }
        }

        private void DrawModuleTabs()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                DrawTab("A: 環境光", Module.A_Environment);
                DrawTab("B: ライト", Module.B_LightRig);
                DrawTab("C: ポスト", Module.C_PostProcess);
                DrawTab("D: 仕上げ", Module.D_Finishing);
                DrawTab("E: 診断", Module.E_Lint);
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
                    bool done = IsStepComplete(s);
                    string label = done ? $"{i}✓" : i.ToString();
                    var prev = GUI.backgroundColor;
                    if (done) GUI.backgroundColor = ColorStepDone;
                    if (GUILayout.Toggle(on, label, EditorStyles.toolbarButton, GUILayout.Width(34f)) != on)
                    {
                        currentStep = s;
                    }
                    GUI.backgroundColor = prev;
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
            EditorGUILayout.LabelField("保存先と名前", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "「親フォルダ」の下に「ベース名」のフォルダを作り、プリセット・マテリアル・Lighting Settings をそこへ生成します。" +
                "初期値は開いているシーンの名前と場所から自動入力されます。",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(2);

            // Parent above child, mirroring the actual folder hierarchy.
            newFolderPath = EditorGUILayout.TextField("📁 親フォルダ", newFolderPath);
            using (new EditorGUI.IndentLevelScope())
            {
                newBaseName = EditorGUILayout.TextField("└ ベース名 (フォルダ名)", newBaseName);
            }

            EditorGUILayout.HelpBox(
                $"生成先:\n{newFolderPath}/{newBaseName}/{newBaseName}_Preset.asset",
                MessageType.None);

            if (GUILayout.Button("開いているシーンから名前を再取得"))
            {
                ApplySceneBasedDefaults(force: true);
            }

            EditorGUILayout.Space(4);
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(newBaseName)))
            {
                if (ColoredButton(
                        preset == null ? "プリセットを作成 (初期値を書き込む)" : "プリセットを更新 (フォルダ / 名前)",
                        ColorGenerate, GUILayout.Height(28f)))
                {
                    var created = EnvironmentOps.CreateOrLoadPreset(newBaseName, newFolderPath);
                    if (created.phase == BOSSLookPhase.NotCreated)
                    {
                        created.phase = BOSSLookPhase.Active;
                        EditorUtility.SetDirty(created);
                    }
                    AdoptPreset(created, jumpToStep: true);
                    Selection.activeObject = created;
                }
            }
        }

        // ---- S1 ----
        private void DrawS1()
        {
            if (!RequirePreset()) return;

            preset.hdriTexture = (Texture)EditorGUILayout.ObjectField(
                "HDRI Texture", preset.hdriTexture, typeof(Texture), false);

            EditorGUI.BeginChangeCheck();
            preset.environmentIntensity = EditorGUILayout.Slider(
                "Environment Intensity", preset.environmentIntensity, 0f, 8f);
            if (EditorGUI.EndChangeCheck() && preset.skyboxMaterial != null)
            {
                // Live preview: the slider drives the scene directly once a
                // skybox exists, no regenerate button needed.
                RenderSettings.ambientIntensity = preset.environmentIntensity;
                RenderSettings.reflectionIntensity = preset.environmentIntensity;
                DynamicGI.UpdateEnvironment();
                EditorUtility.SetDirty(preset);
            }

            EditorGUILayout.HelpBox(
                "Texture2D ならパノラマ (Skybox/Panoramic)、Cubemap なら Skybox/Cubemap として自動でスカイボックスマテリアルを作ります。",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(preset.hdriTexture == null))
            {
                if (ColoredButton("スカイボックスを生成 / 更新", ColorGenerate))
                {
                    EnvironmentOps.SetupSkybox(preset);
                }

                EditorGUILayout.Space(2);
                if (ColoredButton("⚡ おまかせセットアップ (Step 1〜5 を一括実行)", ColorAuto, GUILayout.Height(28f)))
                {
                    string report = EnvironmentOps.AutoSetupAll(preset);
                    Debug.Log($"[BOSS Look] おまかせセットアップ:\n{report}");
                    EditorUtility.DisplayDialog("BOSS Look Preset — おまかせセットアップ", report, "OK");
                    currentStep = EnvStep.S6_Bake;
                }
            }
            EditorGUILayout.HelpBox(
                "おまかせ: スカイボックス〜リフレクションまでシーン全体の自動計算で一括設定し、ベイクステップへ進みます。結果は各ステップで個別に調整できます。",
                MessageType.None);

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

            if (ColoredButton("Lighting Settings を生成 / 更新 (Auto Generate OFF)", ColorGenerate))
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

            if (ColoredButton("ContributeGI を付与", ColorGenerate))
            {
                int n = EnvironmentOps.ApplyStaticFlags(preset, out lastMissingUv2);
                Debug.Log($"[BOSS Look] ContributeGI を {n} 個に付与しました。");
            }

            if (lastMissingUv2 != null && lastMissingUv2.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{lastMissingUv2.Count} 個のメッシュで UV2 が空です。ImportSettings で Generate Lightmap UVs を有効にすると解決します。",
                    MessageType.Warning);
                if (GUILayout.Button("該当オブジェクトを選択"))
                {
                    Selection.objects = lastMissingUv2.ToArray();
                }
            }
        }

        // ---- S4 ----
        private void DrawS4()
        {
            if (!RequirePreset()) return;

            EditorGUILayout.LabelField("Probe Area (World Bounds)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "このステップを開いている間、シーンビューに緑のボックスが表示されます。ハンドルをドラッグして範囲を直接編集できます。",
                MessageType.Info);
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

            if (ColoredButton("ライトプローブグリッドを生成 / 更新", ColorGenerate))
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
                    : "B: プローブの背景をニュートラルグレー (SolidColor) にして焼きます。空は反射に映り込みません (環境光のGIには影響しません)。",
                MessageType.Info);

            preset.reflectionBoxPadding = EditorGUILayout.FloatField("Box Padding", preset.reflectionBoxPadding);

            if (ColoredButton("Reflection Probe を生成 / 更新 (Baked)", ColorGenerate))
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
                if (ColoredButton("🔥 ベイクを実行", ColorBake, GUILayout.Height(28f)))
                {
                    EnvironmentOps.StartBake(preset);
                }
            }

            if (Lightmapping.isRunning)
            {
                var rect = GUILayoutUtility.GetRect(18f, 22f, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(rect, Lightmapping.buildProgress,
                    $"Baking... {Lightmapping.buildProgress * 100f:F1}%");
                if (ColoredButton("ベイクをキャンセル", ColorDanger))
                {
                    EnvironmentOps.CancelBake();
                }
            }
            else if (IsStepComplete(EnvStep.S6_Bake))
            {
                EditorGUILayout.HelpBox("ベイク済みのライティングデータがあります。", MessageType.None);
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
                if (ColoredButton("📦 AR化する (スカイボックスOFF)", ColorFinalize, GUILayout.Height(28f)))
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

            if (!LightRigOps.ColorTemperatureSupported)
            {
                EditorGUILayout.HelpBox(
                    "Graphics Settings で「Lights Use Color Temperature」が無効なため、Kelvin 指定が効きません。",
                    MessageType.Warning);
                if (GUILayout.Button("色温度を有効化 (Linear Intensity + Color Temperature)"))
                {
                    if (EditorUtility.DisplayDialog("BOSS Look Preset",
                            "GraphicsSettings.lightsUseLinearIntensity / lightsUseColorTemperature を ON にします。\n" +
                            "既存ライトの見え方が変わる可能性があります。よろしいですか?",
                            "有効化", "キャンセル"))
                    {
                        LightRigOps.EnableColorTemperature();
                    }
                }
            }

            EditorGUI.BeginChangeCheck();
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
                EditorGUILayout.HelpBox("Area ライトは Baked 専用です (Mixed 不可)。", MessageType.None);
            }
            // Live apply: once the rig exists, parameter tweaks update the
            // scene immediately so lighting can be dialed in by eye.
            if (EditorGUI.EndChangeCheck() && preset.keyLight != null)
            {
                LightRigOps.CreateOrUpdateRig(preset);
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
            if (ColoredButton("退避ライトを完全に削除", ColorDanger))
            {
                if (EditorUtility.DisplayDialog("BOSS Look Preset",
                        "退避中のディレクショナルライトをシーンから削除します。よろしいですか?",
                        "削除", "キャンセル"))
                {
                    LightRigOps.DeleteStashedLights(preset);
                }
            }

            EditorGUILayout.Space(4);
            if (ColoredButton("3点リグを生成 / 更新", ColorGenerate, GUILayout.Height(28f)))
            {
                if (preset.subject == null && Selection.activeTransform != null)
                {
                    preset.subject = Selection.activeTransform;
                    Debug.Log($"[BOSS Look] Subject 未指定のため選択中の \"{preset.subject.name}\" を被写体にしました。");
                }
                LightRigOps.CreateOrUpdateRig(preset);
            }
            if (ColoredButton("リグを削除", ColorDanger))
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

            var rp = RenderPipelineDetector.Active;
            EditorGUILayout.LabelField($"ポストプロセス ({PostProcessOps.BackendName})", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                rp == RenderPipelineDetector.ActiveRP.URP
                    ? "URP の Volume フレームワークでセットアップします。Profile + 全体 Volume を作成。\n" +
                      "Camera の Post Processing トグル / Renderer Asset 側 (SSAO 等) は手動で ON にしてください。"
                    : "Built-in RP 用 Post Processing Stack v2。Profile + Volume + Layer 一式をセットアップします。\n" +
                      "状態機械の外なので AR化後でも操作できます。",
                MessageType.Info);

            if (!PostProcessOps.BackendAvailable)
            {
                string missing = rp == RenderPipelineDetector.ActiveRP.URP
                    ? "URP (com.unity.render-pipelines.universal)"
                    : "Post Processing Stack v2 (com.unity.postprocessing)";
                EditorGUILayout.HelpBox(
                    $"{missing} が見つからないため、現在のレンダーパイプライン ({RenderPipelineDetector.DisplayName}) ではポストプロセスをセットアップできません。",
                    MessageType.Error);
                return;
            }

            if (!PostProcessOps.IsLinearColorSpace)
            {
                EditorGUILayout.HelpBox(
                    "Color Space が Linear ではありません。Tonemapping / Color Grading は実質使えなくなります。Player Settings → Other Settings で Linear に変更することを推奨します。",
                    MessageType.Warning);
            }

            preset.postBloomEnabled = EditorGUILayout.Toggle("Bloom", preset.postBloomEnabled);
            preset.postColorGradingEnabled = EditorGUILayout.Toggle("Color Grading (Tonemapping)", preset.postColorGradingEnabled);
            preset.postVignetteEnabled = EditorGUILayout.Toggle("Vignette", preset.postVignetteEnabled);
            preset.postDepthOfFieldEnabled = EditorGUILayout.Toggle("Depth of Field", preset.postDepthOfFieldEnabled);
            preset.postMotionBlurEnabled = EditorGUILayout.Toggle("Motion Blur", preset.postMotionBlurEnabled);
            preset.postAOEnabled = EditorGUILayout.Toggle("Ambient Occlusion", preset.postAOEnabled);

            if (rp == RenderPipelineDetector.ActiveRP.URP && preset.postAOEnabled)
            {
                EditorGUILayout.HelpBox(
                    "URP の AO は Renderer Feature です。URP Renderer Asset → Add Renderer Feature → Screen Space Ambient Occlusion を手動で追加してください。",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(4);
            if (ColoredButton("ポストプロセスをセットアップ / 更新", ColorGenerate, GUILayout.Height(28f)))
            {
                PostProcessOps.Setup(preset);
            }
            if (ColoredButton("ポストプロセスを外す (Volume / Layer 除去)", ColorDanger))
            {
                if (EditorUtility.DisplayDialog("BOSS Look Preset",
                        "シーンの Volume / Post Process Volume / Post Process Layer を除去します。Profile アセットは残します。",
                        "外す", "キャンセル"))
                {
                    PostProcessOps.Remove(preset);
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Generated", EditorStyles.boldLabel);
#if BOSS_LOOK_PRESET_HAS_PPV2
            if (rp == RenderPipelineDetector.ActiveRP.BuiltIn)
            {
                preset.postProfile = (UnityEngine.Rendering.PostProcessing.PostProcessProfile)EditorGUILayout.ObjectField(
                    "Post Profile", preset.postProfile, typeof(UnityEngine.Rendering.PostProcessing.PostProcessProfile), false);
                preset.postVolume = (UnityEngine.Rendering.PostProcessing.PostProcessVolume)EditorGUILayout.ObjectField(
                    "Post Volume", preset.postVolume, typeof(UnityEngine.Rendering.PostProcessing.PostProcessVolume), true);
                preset.postLayer = (UnityEngine.Rendering.PostProcessing.PostProcessLayer)EditorGUILayout.ObjectField(
                    "Post Layer", preset.postLayer, typeof(UnityEngine.Rendering.PostProcessing.PostProcessLayer), true);
            }
#endif
#if BOSS_LOOK_PRESET_HAS_SRPCORE
            if (rp == RenderPipelineDetector.ActiveRP.URP)
            {
                preset.urpVolumeProfile = (UnityEngine.Rendering.VolumeProfile)EditorGUILayout.ObjectField(
                    "URP Volume Profile", preset.urpVolumeProfile, typeof(UnityEngine.Rendering.VolumeProfile), false);
                preset.urpVolume = (UnityEngine.Rendering.Volume)EditorGUILayout.ObjectField(
                    "URP Volume", preset.urpVolume, typeof(UnityEngine.Rendering.Volume), true);
            }
#endif
        }

        // ---------------- Module D: Finishing (ground shadow + fog) ----------------

        private void DrawModuleD()
        {
            if (!RequirePreset()) return;

            EditorGUILayout.LabelField("ARグラウンドシャドウ (シャドウキャッチャー)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "影だけを受ける透明な床を被写体の足元に生成します。AR表示で「浮いて見える」のを防ぎます。\n" +
                (RenderPipelineDetector.IsURP
                    ? "URP: メインライト + 追加ライト (スポットリグ) のリアルタイム影を受けます。"
                    : "Built-in: Directional ライトのリアルタイム影のみ受けます。スポットリグの影を床に落としたい場合は、影あり の弱い Directional を1本足してください。") +
                "\nシェーダとマテリアルはプリセットフォルダに生成され、シーンと一緒にアップロードされます。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            preset.groundShadowOpacity = EditorGUILayout.Slider("影の濃さ", preset.groundShadowOpacity, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                GroundShadowOps.ApplyOpacity(preset);
            }
            preset.groundShadowSizeMultiplier = EditorGUILayout.Slider(
                "サイズ倍率 (被写体の足元比)", preset.groundShadowSizeMultiplier, 1f, 6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (ColoredButton("グラウンドシャドウを生成 / 更新", ColorGenerate, GUILayout.Height(26f)))
                {
                    if (preset.subject == null && Selection.activeTransform != null)
                    {
                        preset.subject = Selection.activeTransform;
                        Debug.Log($"[BOSS Look] Subject 未指定のため選択中の \"{preset.subject.name}\" を被写体にしました。");
                    }
                    GroundShadowOps.CreateOrUpdate(preset);
                }
                if (ColoredButton("除去", ColorDanger, GUILayout.Width(64f), GUILayout.Height(26f)))
                {
                    GroundShadowOps.Remove(preset);
                }
            }
            preset.groundShadowPlane = (GameObject)EditorGUILayout.ObjectField(
                "Shadow Plane", preset.groundShadowPlane, typeof(GameObject), true);

            EditorGUILayout.Space(12);

            EditorGUILayout.LabelField("フォグ / 空気感", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "距離フォグで奥行きを出します。負荷はほぼゼロで、変更は即シーンに反映されます。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            preset.fogEnabled = EditorGUILayout.Toggle("フォグ有効", preset.fogEnabled);
            using (new EditorGUI.DisabledScope(!preset.fogEnabled))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    preset.fogColor = EditorGUILayout.ColorField("フォグ色", preset.fogColor);
                    if (GUILayout.Button("環境光から自動設定", GUILayout.Width(132f)))
                    {
                        preset.fogColor = FogOps.SuggestColorFromEnvironment();
                        FogOps.Apply(preset);
                        EditorUtility.SetDirty(preset);
                    }
                }
                preset.fogMode = (FogMode)EditorGUILayout.EnumPopup("モード", preset.fogMode);
                if (preset.fogMode == FogMode.Linear)
                {
                    preset.fogStartDistance = EditorGUILayout.FloatField("開始距離 (m)", preset.fogStartDistance);
                    preset.fogEndDistance = EditorGUILayout.FloatField("終了距離 (m)", preset.fogEndDistance);
                }
                else
                {
                    preset.fogDensity = EditorGUILayout.Slider("濃度", preset.fogDensity, 0f, 0.15f);
                }
            }
            if (EditorGUI.EndChangeCheck())
            {
                FogOps.Apply(preset);
                EditorUtility.SetDirty(preset);
            }

            EditorGUILayout.HelpBox(
                "AR納品ではカメラ映像にフォグが乗ると不自然になる場合があります。実機確認のうえ、不要なら「フォグ有効」を OFF に。",
                MessageType.None);
        }

        // ---------------- Module E: Look Lint ----------------

        private List<LintIssue> lintResults;
        private bool lintRan;

        private void DrawModuleE()
        {
            if (!RequirePreset()) return;

            EditorGUILayout.LabelField("ルック診断", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "ルックを壊しがちな設定を一括チェックします: Gamma色空間 / 純白アルベド / UV2欠落 / 強すぎEmission / ミップマップ無効 / HDR無効 / プローブ未生成 / 色温度無効。",
                MessageType.Info);

            if (ColoredButton("🔍 診断を実行", ColorAuto, GUILayout.Height(28f)))
            {
                lintResults = LintOps.RunAll(preset);
                lintRan = true;
            }

            if (!lintRan || lintResults == null) return;

            EditorGUILayout.Space(4);
            if (lintResults.Count == 0)
            {
                EditorGUILayout.HelpBox("問題は見つかりませんでした ✨", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"検出: {lintResults.Count} 件", EditorStyles.boldLabel);
            foreach (var issue in lintResults)
            {
                var type = issue.severity == LintSeverity.Error ? MessageType.Error
                    : issue.severity == LintSeverity.Warning ? MessageType.Warning
                    : MessageType.Info;
                EditorGUILayout.HelpBox(issue.message, type);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (issue.targets != null && issue.targets.Count > 0 &&
                        GUILayout.Button($"対象を選択 ({issue.targets.Count})", GUILayout.Width(140f)))
                    {
                        Selection.objects = issue.targets.ToArray();
                    }
                    if (issue.fix != null && ColoredButton(issue.fixLabel, ColorGenerate))
                    {
                        issue.fix();
                        lintResults = LintOps.RunAll(preset);
                        GUIUtility.ExitGUI(); // the list we're iterating just changed
                    }
                }
                EditorGUILayout.Space(2);
            }
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
