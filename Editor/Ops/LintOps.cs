using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DarataBOSS.BOSSLookPreset.Editor.Ops
{
    public enum LintSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
    }

    public class LintIssue
    {
        public LintSeverity severity;
        public string message;
        public List<UnityEngine.Object> targets = new List<UnityEngine.Object>();
        public string fixLabel;
        public Action fix;
    }

    /// <summary>
    /// Module E (look lint): one-button diagnosis of the usual look-killers.
    /// Each check returns an issue with selectable offenders and, where it is
    /// safe to automate, a fix action (destructive ones confirm via dialog).
    /// </summary>
    public static class LintOps
    {
        private static readonly string[] AlbedoProps = { "_BaseColor", "_Color" };
        private static readonly string[] MainTexProps = { "_BaseMap", "_MainTex" };

        public static List<LintIssue> RunAll(BOSSLookPreset preset)
        {
            var issues = new List<LintIssue>();

            CheckColorSpace(issues);
            CheckCameraHdr(preset, issues);
            CheckColorTemperature(preset, issues);
            CheckProbes(preset, issues);

            var materials = CollectSceneMaterials();
            CheckWhiteAlbedo(materials, issues);
            CheckStrongEmission(materials, issues);
            CheckMipmaps(materials, issues);
            CheckMissingUv2(issues);

            return issues;
        }

        // ---------------- Project settings ----------------

        private static void CheckColorSpace(List<LintIssue> issues)
        {
            if (PlayerSettings.colorSpace == ColorSpace.Linear) return;
            issues.Add(new LintIssue
            {
                severity = LintSeverity.Error,
                message = "Color Space が Gamma です。Tonemapping / Color Grading が実質使えず、ライティングの質も落ちます。",
                fixLabel = "Linear に変更 (全テクスチャ再インポート発生)",
                fix = () =>
                {
                    if (EditorUtility.DisplayDialog("BOSS Look Preset",
                            "Color Space を Linear に変更します。テクスチャの再インポートが走り、" +
                            "既存マテリアルの見た目が変わる可能性があります。よろしいですか?",
                            "変更", "キャンセル"))
                    {
                        PlayerSettings.colorSpace = ColorSpace.Linear;
                    }
                },
            });
        }

        private static void CheckCameraHdr(BOSSLookPreset preset, List<LintIssue> issues)
        {
            if (preset == null || !preset.postBloomEnabled) return;
            var cam = Camera.main;
            if (cam == null || cam.allowHDR) return;
            issues.Add(new LintIssue
            {
                severity = LintSeverity.Warning,
                message = "Bloom が有効なのに Main Camera の HDR が OFF です。Bloom がほぼ効きません。",
                targets = { cam },
                fixLabel = "HDR を ON にする",
                fix = () =>
                {
                    Undo.RecordObject(cam, "Enable HDR");
                    cam.allowHDR = true;
                    EditorUtility.SetDirty(cam);
                },
            });
        }

        private static void CheckColorTemperature(BOSSLookPreset preset, List<LintIssue> issues)
        {
            if (preset == null || preset.keyLight == null) return;
            if (LightRigOps.ColorTemperatureSupported) return;
            issues.Add(new LintIssue
            {
                severity = LintSeverity.Warning,
                message = "ライトリグがあるのに Graphics Settings の色温度が無効です。Kelvin 指定が効いていません。",
                fixLabel = "色温度を有効化",
                fix = () =>
                {
                    if (EditorUtility.DisplayDialog("BOSS Look Preset",
                            "lightsUseLinearIntensity / lightsUseColorTemperature を ON にします。" +
                            "既存ライトの見え方が変わる可能性があります。よろしいですか?",
                            "有効化", "キャンセル"))
                    {
                        LightRigOps.EnableColorTemperature();
                    }
                },
            });
        }

        private static void CheckProbes(BOSSLookPreset preset, List<LintIssue> issues)
        {
            if (preset == null) return;
            if (preset.lightProbeGroup == null)
            {
                issues.Add(new LintIssue
                {
                    severity = LintSeverity.Info,
                    message = "ライトプローブ未生成です。動くオブジェクトがベイク照明を受け取れません (Step 4)。",
                });
            }
            bool hasReflection = preset.reflectionProbes != null &&
                                 preset.reflectionProbes.Count > 0 &&
                                 preset.reflectionProbes[0] != null;
            if (!hasReflection)
            {
                issues.Add(new LintIssue
                {
                    severity = LintSeverity.Info,
                    message = "リフレクションプローブ未生成です。映り込みがスカイボックス頼みになります (Step 5)。",
                });
            }
        }

        // ---------------- Materials / textures ----------------

        private static List<Material> CollectSceneMaterials()
        {
            var set = new HashSet<Material>();
            foreach (var mr in SceneRelinkOps.FindAllInScene<MeshRenderer>())
            {
                foreach (var m in mr.sharedMaterials)
                {
                    if (m == null) continue;
                    // Only project-owned materials: package/built-in ones are
                    // read-only and shouldn't be reported as fixable.
                    string path = AssetDatabase.GetAssetPath(m);
                    if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets")) continue;
                    set.Add(m);
                }
            }
            return new List<Material>(set);
        }

        private static void CheckWhiteAlbedo(List<Material> materials, List<LintIssue> issues)
        {
            var offenders = new List<Material>();
            foreach (var m in materials)
            {
                foreach (var prop in AlbedoProps)
                {
                    if (!m.HasProperty(prop)) continue;
                    var c = m.GetColor(prop);
                    if (c.r > 0.95f && c.g > 0.95f && c.b > 0.95f) offenders.Add(m);
                    break;
                }
            }
            if (offenders.Count == 0) return;
            issues.Add(new LintIssue
            {
                severity = LintSeverity.Warning,
                message = $"アルベドがほぼ純白のマテリアルが {offenders.Count} 個あります。GIで光が増幅し白飛びの原因になります (現実の白い壁は反射率 0.8 程度)。",
                targets = new List<UnityEngine.Object>(offenders),
                fixLabel = "アルベドを 0.85 に下げる",
                fix = () =>
                {
                    foreach (var m in offenders)
                    {
                        Undo.RecordObject(m, "Clamp Albedo");
                        foreach (var prop in AlbedoProps)
                        {
                            if (!m.HasProperty(prop)) continue;
                            var c = m.GetColor(prop);
                            m.SetColor(prop, new Color(0.85f, 0.85f, 0.85f, c.a));
                            break;
                        }
                        EditorUtility.SetDirty(m);
                    }
                },
            });
        }

        private static void CheckStrongEmission(List<Material> materials, List<LintIssue> issues)
        {
            var offenders = new List<Material>();
            foreach (var m in materials)
            {
                if (!m.HasProperty("_EmissionColor")) continue;
                if (m.GetColor("_EmissionColor").maxColorComponent > 3f) offenders.Add(m);
            }
            if (offenders.Count == 0) return;
            issues.Add(new LintIssue
            {
                severity = LintSeverity.Warning,
                message = $"Emission が非常に強いマテリアルが {offenders.Count} 個あります。ベイクで周囲が焼けたり Bloom が暴れる原因になります。意図したものか確認してください。",
                targets = new List<UnityEngine.Object>(offenders),
            });
        }

        private static void CheckMipmaps(List<Material> materials, List<LintIssue> issues)
        {
            var offenders = new List<Texture>();
            var seen = new HashSet<Texture>();
            var importers = new List<TextureImporter>();
            foreach (var m in materials)
            {
                foreach (var prop in MainTexProps)
                {
                    if (!m.HasProperty(prop)) continue;
                    var tex = m.GetTexture(prop);
                    if (tex == null || !seen.Add(tex)) continue;
                    string path = AssetDatabase.GetAssetPath(tex);
                    if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets")) continue;
                    if (AssetImporter.GetAtPath(path) is TextureImporter ti && !ti.mipmapEnabled)
                    {
                        offenders.Add(tex);
                        importers.Add(ti);
                    }
                    break;
                }
            }
            if (offenders.Count == 0) return;
            issues.Add(new LintIssue
            {
                severity = LintSeverity.Warning,
                message = $"ミップマップ無効のテクスチャが {offenders.Count} 個あります。遠景でチラつき (エイリアシング) の原因になります。",
                targets = new List<UnityEngine.Object>(offenders),
                fixLabel = "ミップマップを有効化 (再インポート)",
                fix = () =>
                {
                    foreach (var ti in importers)
                    {
                        ti.mipmapEnabled = true;
                        ti.SaveAndReimport();
                    }
                },
            });
        }

        private static void CheckMissingUv2(List<LintIssue> issues)
        {
            var offenderGOs = new List<UnityEngine.Object>();
            var importers = new HashSet<ModelImporter>();
            foreach (var mf in SceneRelinkOps.FindAllInScene<MeshFilter>())
            {
                if (mf == null || mf.sharedMesh == null) continue;
                var flags = GameObjectUtility.GetStaticEditorFlags(mf.gameObject);
                if ((flags & StaticEditorFlags.ContributeGI) == 0) continue;
                if (mf.sharedMesh.uv2 != null && mf.sharedMesh.uv2.Length > 0) continue;

                offenderGOs.Add(mf.gameObject);
                string path = AssetDatabase.GetAssetPath(mf.sharedMesh);
                if (AssetImporter.GetAtPath(path) is ModelImporter mi)
                {
                    importers.Add(mi);
                }
            }
            if (offenderGOs.Count == 0) return;

            var issue = new LintIssue
            {
                severity = LintSeverity.Error,
                message = $"ContributeGI なのに UV2 (ライトマップUV) が無いメッシュが {offenderGOs.Count} 個あります。ベイク結果が壊れます。",
                targets = offenderGOs,
            };
            if (importers.Count > 0)
            {
                issue.fixLabel = $"Generate Lightmap UVs を有効化 ({importers.Count} モデル, 再インポート)";
                issue.fix = () =>
                {
                    foreach (var mi in importers)
                    {
                        mi.generateSecondaryUV = true;
                        mi.SaveAndReimport();
                    }
                };
            }
            issues.Add(issue);
        }
    }
}
