using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarataBOSS.BOSSLookPreset.Editor.Ops
{
    /// <summary>
    /// Module D (ground shadow): generates a transparent shadow-catcher plane
    /// under the subject so AR content doesn't look like it's floating.
    ///
    /// The shader is written into the preset folder as a project asset rather
    /// than shipped inside this package: the URP variant includes URP shader
    /// library files, which would fail to import in Built-in-only projects if
    /// it lived in the (always-imported) package. Generating per active RP
    /// sidesteps that entirely, and the asset travels with the scene upload.
    /// </summary>
    public static class GroundShadowOps
    {
        private const string ShadowColorProp = "_ShadowColor";

        public static void CreateOrUpdate(BOSSLookPreset preset)
        {
            if (preset == null) return;
            SceneRelinkOps.RelinkAll(preset);

            var shader = EnsureShaderAsset(preset);
            if (shader == null)
            {
                Debug.LogError("[BOSS Look] シャドウキャッチャーシェーダの生成に失敗しました。");
                return;
            }

            // Material
            string matPath = $"{preset.folderPath}/{preset.baseName}_GroundShadow.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            mat.SetColor(ShadowColorProp, new Color(0f, 0f, 0f, preset.groundShadowOpacity));
            EditorUtility.SetDirty(mat);
            preset.groundShadowMaterial = mat;

            // Plane
            var plane = preset.groundShadowPlane;
            if (plane == null)
            {
                plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                plane.name = $"{preset.baseName}_GroundShadow";
                Undo.RegisterCreatedObjectUndo(plane, "Create Ground Shadow");
                var col = plane.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
                preset.groundShadowPlane = plane;
            }

            // Place at the subject's feet, sized relative to its footprint.
            Bounds? subjectBounds = ComputeSubjectBounds(preset);
            Vector3 position;
            float footprint;
            if (subjectBounds.HasValue)
            {
                var b = subjectBounds.Value;
                position = new Vector3(b.center.x, b.min.y + 0.001f, b.center.z);
                footprint = Mathf.Max(b.size.x, b.size.z, 0.1f);
            }
            else
            {
                position = Vector3.zero;
                footprint = 1f;
            }
            Undo.RecordObject(plane.transform, "Place Ground Shadow");
            plane.transform.position = position;
            // Unity's Plane primitive is 10x10 units at scale 1.
            float scale = footprint * Mathf.Max(1f, preset.groundShadowSizeMultiplier) / 10f;
            plane.transform.localScale = new Vector3(scale, 1f, scale);

            var mr = plane.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;
            // Keep it out of the bake — it's a runtime compositing helper.
            GameObjectUtility.SetStaticEditorFlags(plane, 0);

            EditorUtility.SetDirty(preset);
        }

        public static void ApplyOpacity(BOSSLookPreset preset)
        {
            if (preset == null || preset.groundShadowMaterial == null) return;
            preset.groundShadowMaterial.SetColor(ShadowColorProp,
                new Color(0f, 0f, 0f, preset.groundShadowOpacity));
            EditorUtility.SetDirty(preset.groundShadowMaterial);
        }

        public static void Remove(BOSSLookPreset preset)
        {
            if (preset == null) return;
            SceneRelinkOps.RelinkAll(preset);
            if (preset.groundShadowPlane != null)
            {
                Undo.DestroyObjectImmediate(preset.groundShadowPlane);
            }
            preset.groundShadowPlane = null;
            EditorUtility.SetDirty(preset);
        }

        private static Bounds? ComputeSubjectBounds(BOSSLookPreset preset)
        {
            if (preset.subject == null) return null;
            Bounds? bounds = null;
            foreach (var r in preset.subject.GetComponentsInChildren<Renderer>())
            {
                if (r == null) continue;
                if (bounds == null) bounds = r.bounds;
                else { var b = bounds.Value; b.Encapsulate(r.bounds); bounds = b; }
            }
            return bounds;
        }

        private static Shader EnsureShaderAsset(BOSSLookPreset preset)
        {
            string shaderPath = $"{preset.folderPath}/{preset.baseName}_ShadowCatcher.shader";
            string source = RenderPipelineDetector.IsURP ? UrpShaderSource : BuiltinShaderSource;

            bool needsWrite = !File.Exists(shaderPath) || File.ReadAllText(shaderPath) != source;
            if (needsWrite)
            {
                File.WriteAllText(shaderPath, source);
                AssetDatabase.ImportAsset(shaderPath);
            }
            return AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
        }

        // Catches realtime shadows of the main directional light.
        private const string BuiltinShaderSource = @"// Auto-generated by BOSS Look Preset (Built-in RP variant). Re-created on update.
Shader ""BOSS/ShadowCatcher""
{
    Properties
    {
        _ShadowColor (""Shadow Color"", Color) = (0, 0, 0, 0.6)
    }
    SubShader
    {
        Tags { ""Queue"" = ""Transparent-1"" ""RenderType"" = ""Transparent"" ""IgnoreProjector"" = ""True"" }
        Pass
        {
            Tags { ""LightMode"" = ""ForwardBase"" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include ""UnityCG.cginc""
            #include ""AutoLight.cginc""

            struct v2f
            {
                float4 pos : SV_POSITION;
                SHADOW_COORDS(0)
            };

            fixed4 _ShadowColor;

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                TRANSFER_SHADOW(o);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed shadow = SHADOW_ATTENUATION(i);
                return fixed4(_ShadowColor.rgb, (1.0 - shadow) * _ShadowColor.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
";

        // Catches main light + additional light (spot rig) realtime shadows.
        private const string UrpShaderSource = @"// Auto-generated by BOSS Look Preset (URP variant). Re-created on update.
Shader ""BOSS/ShadowCatcher""
{
    Properties
    {
        _ShadowColor (""Shadow Color"", Color) = (0, 0, 0, 0.6)
    }
    SubShader
    {
        Tags { ""RenderType"" = ""Transparent"" ""Queue"" = ""Transparent-1"" ""RenderPipeline"" = ""UniversalPipeline"" }
        Pass
        {
            Name ""GroundShadow""
            Tags { ""LightMode"" = ""UniversalForward"" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl""

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            half4 _ShadowColor;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half shadow = mainLight.shadowAttenuation;

                #if defined(_ADDITIONAL_LIGHTS) && defined(_ADDITIONAL_LIGHT_SHADOWS)
                uint lightCount = GetAdditionalLightsCount();
                for (uint li = 0u; li < lightCount; li++)
                {
                    Light l = GetAdditionalLight(li, IN.positionWS, half4(1, 1, 1, 1));
                    shadow = min(shadow, l.shadowAttenuation);
                }
                #endif

                return half4(_ShadowColor.rgb, (1.0 - shadow) * _ShadowColor.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
";
    }
}
