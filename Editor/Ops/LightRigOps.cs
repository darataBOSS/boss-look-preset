using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Oborokurage.BOSSLookPreset.Editor.Ops
{
    public static class LightRigOps
    {
        public static void StashExistingDirectionalLights(BOSSLookPreset preset)
        {
            if (preset == null) return;
            if (preset.stashedDirectionalLights == null)
            {
                preset.stashedDirectionalLights = new List<StashedLightInfo>();
            }

            foreach (var l in Object.FindObjectsOfType<Light>())
            {
                if (l == null) continue;
                if (l.type != LightType.Directional) continue;
                if (preset.keyLight == l || preset.fillLight == l || preset.backLight == l) continue;

                bool alreadyStashed = false;
                foreach (var info in preset.stashedDirectionalLights)
                {
                    if (info != null && info.light == l) { alreadyStashed = true; break; }
                }
                if (alreadyStashed) continue;

                Undo.RecordObject(l.gameObject, "Stash Directional Light");
                var stash = new StashedLightInfo { light = l, wasEnabled = l.enabled };
                preset.stashedDirectionalLights.Add(stash);
                l.enabled = false;
            }
            EditorUtility.SetDirty(preset);
        }

        public static void RestoreStashedLights(BOSSLookPreset preset)
        {
            if (preset == null || preset.stashedDirectionalLights == null) return;
            foreach (var info in preset.stashedDirectionalLights)
            {
                if (info != null && info.light != null)
                {
                    Undo.RecordObject(info.light, "Restore Stashed Light");
                    info.light.enabled = info.wasEnabled;
                }
            }
            preset.stashedDirectionalLights.Clear();
            EditorUtility.SetDirty(preset);
        }

        public static void DeleteStashedLights(BOSSLookPreset preset)
        {
            if (preset == null || preset.stashedDirectionalLights == null) return;
            foreach (var info in preset.stashedDirectionalLights)
            {
                if (info != null && info.light != null)
                {
                    Undo.DestroyObjectImmediate(info.light.gameObject);
                }
            }
            preset.stashedDirectionalLights.Clear();
            EditorUtility.SetDirty(preset);
        }

        public static void CreateOrUpdateRig(BOSSLookPreset preset)
        {
            if (preset == null) return;

            var rigRoot = preset.rigRoot;
            if (rigRoot == null)
            {
                rigRoot = new GameObject(BOSSLookDefaults.RigRootName);
                Undo.RegisterCreatedObjectUndo(rigRoot, "Create BOSS Light Rig");
                preset.rigRoot = rigRoot;
            }

            Vector3 subjectPos = preset.subject != null
                ? preset.subject.position
                : Vector3.zero;
            float radius = 4f;

            if (preset.keyLight == null)
            {
                preset.keyLight = MakeRigLight(rigRoot, "Key", preset);
            }
            if (preset.fillLight == null)
            {
                preset.fillLight = MakeRigLight(rigRoot, "Fill", preset);
            }
            if (preset.backLight == null)
            {
                preset.backLight = MakeRigLight(rigRoot, "Back", preset);
            }

            ConfigureLight(preset.keyLight, preset, subjectPos,
                AngleOffset(45f, 30f, radius), preset.keyIntensity, preset.keyColorTemperatureKelvin,
                shadows: LightShadows.Soft);

            float fillIntensity = Mathf.Max(0.01f, preset.keyIntensity / Mathf.Max(0.1f, preset.keyFillRatio));
            ConfigureLight(preset.fillLight, preset, subjectPos,
                AngleOffset(-60f, 15f, radius), fillIntensity, preset.fillColorTemperatureKelvin,
                shadows: LightShadows.None);

            ConfigureLight(preset.backLight, preset, subjectPos,
                AngleOffset(180f, 45f, radius), preset.keyIntensity * 0.6f, preset.backColorTemperatureKelvin,
                shadows: LightShadows.None);

            EditorUtility.SetDirty(preset);
        }

        private static Light MakeRigLight(GameObject parent, string label, BOSSLookPreset preset)
        {
            var go = new GameObject($"{BOSSLookDefaults.RigRootName} - {label}");
            Undo.RegisterCreatedObjectUndo(go, $"Create {label} Light");
            go.transform.SetParent(parent.transform, worldPositionStays: true);
            return go.AddComponent<Light>();
        }

        private static Vector3 AngleOffset(float yawDeg, float pitchDeg, float radius)
        {
            float yaw = yawDeg * Mathf.Deg2Rad;
            float pitch = pitchDeg * Mathf.Deg2Rad;
            return new Vector3(
                Mathf.Sin(yaw) * Mathf.Cos(pitch),
                Mathf.Sin(pitch),
                Mathf.Cos(yaw) * Mathf.Cos(pitch)
            ) * radius;
        }

        private static void ConfigureLight(Light light, BOSSLookPreset preset, Vector3 subjectPos,
            Vector3 offset, float intensity, float kelvin, LightShadows shadows)
        {
            if (light == null) return;
            Undo.RecordObject(light.transform, "Configure Light Transform");
            Undo.RecordObject(light, "Configure Light");

            light.transform.position = subjectPos + offset;
            light.transform.LookAt(subjectPos);

            light.type = preset.rigLightKind == RigLightKind.Spot ? LightType.Spot : LightType.Area;
            light.lightmapBakeType = LightmapBakeType.Mixed;
            light.shadows = shadows;
            light.intensity = intensity;
            light.useColorTemperature = true;
            light.colorTemperature = kelvin;
            light.color = Color.white;

            if (light.type == LightType.Spot)
            {
                light.spotAngle = preset.spotAngleDeg;
                light.range = Mathf.Max(1f, offset.magnitude * 2f);
            }
            else
            {
                light.areaSize = preset.areaSize;
            }
        }

        public static void DeleteRig(BOSSLookPreset preset)
        {
            if (preset == null) return;
            if (preset.rigRoot != null)
            {
                Undo.DestroyObjectImmediate(preset.rigRoot);
            }
            preset.rigRoot = null;
            preset.keyLight = null;
            preset.fillLight = null;
            preset.backLight = null;
            EditorUtility.SetDirty(preset);
        }
    }
}
