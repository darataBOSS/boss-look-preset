using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarataBOSS.BOSSLookPreset.Editor.Ops
{
    /// <summary>
    /// Module B: light rigs. Three interchangeable types share one rig root —
    /// ThreePoint (subject focus), Sun (outdoor directional + sky fill), and
    /// CeilingGrid (rows of downward lights covering the probe area for large
    /// interiors). Switching type rebuilds only the lights that don't belong.
    /// </summary>
    public static class LightRigOps
    {
        /// <summary>Built-in RP ignores Light.colorTemperature unless these
        /// project-wide flags are on, so the rig's Kelvin values would
        /// silently do nothing.</summary>
        public static bool ColorTemperatureSupported =>
            GraphicsSettings.lightsUseColorTemperature && GraphicsSettings.lightsUseLinearIntensity;

        public static void EnableColorTemperature()
        {
            GraphicsSettings.lightsUseLinearIntensity = true;
            GraphicsSettings.lightsUseColorTemperature = true;
        }

        public static bool RigExists(BOSSLookPreset preset)
        {
            if (preset == null) return false;
            switch (preset.rigType)
            {
                case RigType.ThreePoint: return preset.keyLight != null;
                case RigType.Sun: return preset.sunLight != null;
                case RigType.CeilingGrid:
                    return preset.gridLights != null && preset.gridLights.Count > 0 && preset.gridLights[0] != null;
                default: return false;
            }
        }

        // ---------------- Stash / restore existing lights ----------------

        public static void StashExistingDirectionalLights(BOSSLookPreset preset)
        {
            if (preset == null) return;
            if (preset.stashedDirectionalLights == null)
            {
                preset.stashedDirectionalLights = new List<StashedLightInfo>();
            }

            foreach (var l in SceneRelinkOps.FindAllInScene<Light>())
            {
                if (l == null) continue;
                if (l.type != LightType.Directional) continue;
                // Never stash the rig's own lights (e.g. the Sun).
                if (preset.rigRoot != null && l.transform.IsChildOf(preset.rigRoot.transform)) continue;

                bool alreadyStashed = false;
                foreach (var info in preset.stashedDirectionalLights)
                {
                    if (info != null && info.light == l) { alreadyStashed = true; break; }
                }
                if (alreadyStashed) continue;

                Undo.RecordObject(l, "Stash Directional Light");
                var stash = new StashedLightInfo
                {
                    light = l,
                    wasEnabled = l.enabled,
                    scenePath = SceneRelinkOps.GetHierarchyPath(l.transform),
                };
                preset.stashedDirectionalLights.Add(stash);
                l.enabled = false;
            }
            EditorUtility.SetDirty(preset);
        }

        public static void RestoreStashedLights(BOSSLookPreset preset)
        {
            if (preset == null || preset.stashedDirectionalLights == null) return;
            SceneRelinkOps.RelinkAll(preset);

            var unresolved = new List<string>();
            foreach (var info in preset.stashedDirectionalLights)
            {
                if (info == null) continue;
                if (info.light != null)
                {
                    Undo.RecordObject(info.light, "Restore Stashed Light");
                    info.light.enabled = info.wasEnabled;
                }
                else if (!string.IsNullOrEmpty(info.scenePath))
                {
                    unresolved.Add(info.scenePath);
                }
            }
            if (unresolved.Count > 0)
            {
                Debug.LogWarning("[BOSS Look] 次の退避ライトが見つからず復元できませんでした (リネーム/削除/別シーン?):\n" +
                                 string.Join("\n", unresolved));
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

        // ---------------- Rig build ----------------

        public static void CreateOrUpdateRig(BOSSLookPreset preset)
        {
            if (preset == null) return;
            SceneRelinkOps.RelinkAll(preset);

            if (preset.rigRoot == null)
            {
                preset.rigRoot = new GameObject(BOSSLookDefaults.RigRootName);
                Undo.RegisterCreatedObjectUndo(preset.rigRoot, "Create BOSS Light Rig");
            }

            ClearMismatchedLights(preset);

            switch (preset.rigType)
            {
                case RigType.ThreePoint: BuildThreePoint(preset); break;
                case RigType.Sun: BuildSun(preset); break;
                case RigType.CeilingGrid: BuildCeilingGrid(preset); break;
            }

            EditorUtility.SetDirty(preset);
        }

        /// <summary>Removes rig children that belong to a different rig type,
        /// so switching types doesn't leave orphan lights behind.</summary>
        private static void ClearMismatchedLights(BOSSLookPreset preset)
        {
            var root = preset.rigRoot;
            if (root == null) return;

            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                var child = root.transform.GetChild(i).gameObject;
                string n = child.name;
                bool keep;
                switch (preset.rigType)
                {
                    case RigType.ThreePoint:
                        keep = n.EndsWith(" - Key") || n.EndsWith(" - Fill") || n.EndsWith(" - Back");
                        break;
                    case RigType.Sun:
                        keep = n.EndsWith(" - Sun") || n.EndsWith(" - Sky Fill");
                        break;
                    case RigType.CeilingGrid:
                        keep = n.Contains(" - Grid ");
                        break;
                    default:
                        keep = false;
                        break;
                }
                if (!keep)
                {
                    Undo.DestroyObjectImmediate(child);
                }
            }

            if (preset.rigType != RigType.ThreePoint)
            {
                preset.keyLight = null;
                preset.fillLight = null;
                preset.backLight = null;
            }
            if (preset.rigType != RigType.Sun)
            {
                preset.sunLight = null;
                preset.skyFillLight = null;
            }
            if (preset.rigType != RigType.CeilingGrid && preset.gridLights != null)
            {
                preset.gridLights.Clear();
            }
        }

        private static Light MakeRigLight(GameObject parent, string label)
        {
            var go = new GameObject($"{BOSSLookDefaults.RigRootName} - {label}");
            Undo.RegisterCreatedObjectUndo(go, $"Create {label} Light");
            go.transform.SetParent(parent.transform, worldPositionStays: true);
            return go.AddComponent<Light>();
        }

        // ---- Type 1: three-point ----

        private static void BuildThreePoint(BOSSLookPreset preset)
        {
            Vector3 subjectPos = preset.subject != null ? preset.subject.position : Vector3.zero;
            float radius = 4f;

            if (preset.keyLight == null) preset.keyLight = MakeRigLight(preset.rigRoot, "Key");
            if (preset.fillLight == null) preset.fillLight = MakeRigLight(preset.rigRoot, "Fill");
            if (preset.backLight == null) preset.backLight = MakeRigLight(preset.rigRoot, "Back");

            ConfigureFocusLight(preset.keyLight, preset, subjectPos,
                AngleOffset(45f, 30f, radius), preset.keyIntensity, preset.keyColorTemperatureKelvin,
                shadows: LightShadows.Soft);

            float fillIntensity = Mathf.Max(0.01f, preset.keyIntensity / Mathf.Max(0.1f, preset.keyFillRatio));
            ConfigureFocusLight(preset.fillLight, preset, subjectPos,
                AngleOffset(-60f, 15f, radius), fillIntensity, preset.fillColorTemperatureKelvin,
                shadows: LightShadows.None);

            ConfigureFocusLight(preset.backLight, preset, subjectPos,
                AngleOffset(180f, 45f, radius), preset.keyIntensity * 0.6f, preset.backColorTemperatureKelvin,
                shadows: LightShadows.None);
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

        private static void ConfigureFocusLight(Light light, BOSSLookPreset preset, Vector3 subjectPos,
            Vector3 offset, float intensity, float kelvin, LightShadows shadows)
        {
            if (light == null) return;
            Undo.RecordObject(light.transform, "Configure Light Transform");
            Undo.RecordObject(light, "Configure Light");

            light.transform.position = subjectPos + offset;
            light.transform.LookAt(subjectPos);

            light.type = preset.rigLightKind == RigLightKind.Spot ? LightType.Spot : LightType.Area;
            // Area lights only support Baked; Mixed on an Area light is invalid.
            light.lightmapBakeType = light.type == LightType.Spot
                ? LightmapBakeType.Mixed
                : LightmapBakeType.Baked;
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

        // ---- Type 2: sun (outdoor) ----

        private static void BuildSun(BOSSLookPreset preset)
        {
            Vector3 anchor = preset.subject != null ? preset.subject.position : Vector3.zero;

            if (preset.sunLight == null) preset.sunLight = MakeRigLight(preset.rigRoot, "Sun");
            var sun = preset.sunLight;
            Undo.RecordObject(sun.transform, "Configure Sun");
            Undo.RecordObject(sun, "Configure Sun");
            sun.transform.position = anchor + Vector3.up * 8f; // cosmetic; directionals ignore position
            sun.transform.rotation = Quaternion.Euler(preset.sunElevationDeg, preset.sunAzimuthDeg, 0f);
            sun.type = LightType.Directional;
            // Mixed keeps a realtime directional alive, which is exactly what the
            // Built-in ground-shadow catcher needs.
            sun.lightmapBakeType = LightmapBakeType.Mixed;
            sun.shadows = LightShadows.Soft;
            sun.intensity = preset.sunIntensity;
            sun.useColorTemperature = true;
            sun.colorTemperature = preset.sunColorTemperatureKelvin;
            sun.color = Color.white;

            if (preset.skyFillEnabled)
            {
                if (preset.skyFillLight == null) preset.skyFillLight = MakeRigLight(preset.rigRoot, "Sky Fill");
                var fill = preset.skyFillLight;
                Undo.RecordObject(fill.transform, "Configure Sky Fill");
                Undo.RecordObject(fill, "Configure Sky Fill");
                fill.transform.position = anchor + Vector3.up * 8f;
                fill.transform.rotation = Quaternion.Euler(45f, preset.sunAzimuthDeg + 180f, 0f);
                fill.type = LightType.Directional;
                // Baked only: it's a soft ambient bounce, no need for realtime cost.
                fill.lightmapBakeType = LightmapBakeType.Baked;
                fill.shadows = LightShadows.None;
                fill.intensity = preset.sunIntensity / Mathf.Max(1f, preset.skyFillRatio);
                fill.useColorTemperature = true;
                fill.colorTemperature = preset.skyFillColorTemperatureKelvin;
                fill.color = Color.white;
            }
            else if (preset.skyFillLight != null)
            {
                Undo.DestroyObjectImmediate(preset.skyFillLight.gameObject);
                preset.skyFillLight = null;
            }
        }

        // ---- Type 3: ceiling grid (large interior) ----

        private static void BuildCeilingGrid(BOSSLookPreset preset)
        {
            var bounds = preset.probeArea;
            int rows = Mathf.Clamp(preset.gridRows, 1, 8);
            int cols = Mathf.Clamp(preset.gridColumns, 1, 8);
            int desired = rows * cols;

            if (preset.gridLights == null) preset.gridLights = new List<Light>();
            preset.gridLights.RemoveAll(l => l == null);

            while (preset.gridLights.Count > desired)
            {
                var last = preset.gridLights[preset.gridLights.Count - 1];
                preset.gridLights.RemoveAt(preset.gridLights.Count - 1);
                if (last != null) Undo.DestroyObjectImmediate(last.gameObject);
            }
            while (preset.gridLights.Count < desired)
            {
                preset.gridLights.Add(MakeRigLight(preset.rigRoot, "Grid X"));
            }

            float ceilingY = bounds.max.y - 0.05f;
            float cellX = bounds.size.x / cols;
            float cellZ = bounds.size.z / rows;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    var light = preset.gridLights[r * cols + c];
                    if (light == null) continue;
                    Undo.RecordObject(light.transform, "Configure Grid Light");
                    Undo.RecordObject(light, "Configure Grid Light");

                    light.gameObject.name = $"{BOSSLookDefaults.RigRootName} - Grid R{r}C{c}";
                    light.transform.position = new Vector3(
                        bounds.min.x + (c + 0.5f) * cellX,
                        ceilingY,
                        bounds.min.z + (r + 0.5f) * cellZ);
                    light.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // straight down

                    // All-Baked: a whole array of Mixed lights would be a realtime
                    // cost disaster on WebAR. Dynamic objects get lit via probes.
                    light.lightmapBakeType = LightmapBakeType.Baked;
                    light.shadows = LightShadows.Soft;
                    light.intensity = preset.gridIntensity;
                    light.useColorTemperature = true;
                    light.colorTemperature = preset.gridColorTemperatureKelvin;
                    light.color = Color.white;

                    switch (preset.gridLightKind)
                    {
                        case GridLightKind.Spot:
                            light.type = LightType.Spot;
                            light.spotAngle = preset.gridSpotAngle;
                            light.range = Mathf.Max(2f, bounds.size.y * 2f);
                            break;
                        case GridLightKind.Point:
                            light.type = LightType.Point;
                            light.range = Mathf.Max(2f, bounds.size.y * 1.5f);
                            break;
                        case GridLightKind.Area:
                            light.type = LightType.Area;
                            light.areaSize = new Vector2(cellX * 0.6f, cellZ * 0.6f);
                            break;
                    }
                }
            }
        }

        // ---------------- Delete ----------------

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
            preset.sunLight = null;
            preset.skyFillLight = null;
            if (preset.gridLights != null) preset.gridLights.Clear();
            EditorUtility.SetDirty(preset);
        }
    }
}
