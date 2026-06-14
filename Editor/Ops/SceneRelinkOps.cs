using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
#if BOSS_LOOK_PRESET_HAS_PPV2
using UnityEngine.Rendering.PostProcessing;
#endif
#if BOSS_LOOK_PRESET_HAS_SRPCORE
using UnityEngine.Rendering;
#endif

namespace DarataBOSS.BOSSLookPreset.Editor.Ops
{
    /// <summary>
    /// The preset is a project asset, but it tracks objects that live in the
    /// scene (probe group, rig lights, volumes...). Unity cannot persist
    /// asset-to-scene references across editor sessions, so they all come back
    /// null after a restart. This class re-finds them by the tool's naming
    /// convention so "create or update" keeps updating instead of duplicating.
    /// </summary>
    public static class SceneRelinkOps
    {
        public static T[] FindAllInScene<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>();
#endif
        }

        /// <summary>Fills null scene references back in. Never overwrites a
        /// live reference. Safe to call often (wizard open / preset switch /
        /// after domain reload).</summary>
        public static void RelinkAll(BOSSLookPreset preset)
        {
            if (preset == null) return;

            // Container
            if (preset.containerRoot == null)
            {
                preset.containerRoot = GameObject.Find(ContainerName(preset));
            }

            // Light probe group
            if (preset.lightProbeGroup == null)
            {
                var go = GameObject.Find($"{preset.baseName}_LightProbeGroup");
                if (go != null) preset.lightProbeGroup = go.GetComponent<LightProbeGroup>();
            }

            // Reflection probes: drop dead entries, then re-find by name
            if (preset.reflectionProbes == null)
            {
                preset.reflectionProbes = new List<ReflectionProbe>();
            }
            preset.reflectionProbes.RemoveAll(p => p == null);
            if (preset.reflectionProbes.Count == 0)
            {
                var go = GameObject.Find($"{preset.baseName}_ReflectionProbe");
                if (go != null)
                {
                    var probe = go.GetComponent<ReflectionProbe>();
                    if (probe != null) preset.reflectionProbes.Add(probe);
                }
            }

            // Light rig
            if (preset.rigRoot == null)
            {
                preset.rigRoot = GameObject.Find(BOSSLookDefaults.RigRootName);
            }
            if (preset.rigRoot != null)
            {
                if (preset.keyLight == null) preset.keyLight = FindRigLight(preset.rigRoot, "Key");
                if (preset.fillLight == null) preset.fillLight = FindRigLight(preset.rigRoot, "Fill");
                if (preset.backLight == null) preset.backLight = FindRigLight(preset.rigRoot, "Back");
                if (preset.sunLight == null) preset.sunLight = FindRigLight(preset.rigRoot, "Sun");
                if (preset.skyFillLight == null) preset.skyFillLight = FindRigLight(preset.rigRoot, "Sky Fill");

                if (preset.gridLights == null) preset.gridLights = new List<Light>();
                preset.gridLights.RemoveAll(l => l == null);
                if (preset.gridLights.Count == 0)
                {
                    foreach (Transform child in preset.rigRoot.transform)
                    {
                        if (!child.name.Contains(" - Grid ")) continue;
                        var l = child.GetComponent<Light>();
                        if (l != null) preset.gridLights.Add(l);
                    }
                }
            }

            // Stashed directional lights, by recorded hierarchy path
            if (preset.stashedDirectionalLights != null)
            {
                foreach (var info in preset.stashedDirectionalLights)
                {
                    if (info == null || info.light != null || string.IsNullOrEmpty(info.scenePath)) continue;
                    var go = GameObject.Find(info.scenePath);
                    if (go != null) info.light = go.GetComponent<Light>();
                }
            }

            // Ground shadow plane
            if (preset.groundShadowPlane == null)
            {
                preset.groundShadowPlane = GameObject.Find($"{preset.baseName}_GroundShadow");
            }

            // Post processing (Built-in / PPv2)
#if BOSS_LOOK_PRESET_HAS_PPV2
            if (preset.postVolume == null)
            {
                var go = GameObject.Find($"{preset.baseName} Post Process Volume");
                if (go != null) preset.postVolume = go.GetComponent<PostProcessVolume>();
            }
            if (preset.postLayer == null && Camera.main != null)
            {
                preset.postLayer = Camera.main.GetComponent<PostProcessLayer>();
            }
#endif

            // Post processing (URP)
#if BOSS_LOOK_PRESET_HAS_SRPCORE
            if (preset.urpVolume == null)
            {
                var go = GameObject.Find($"{preset.baseName} Volume");
                if (go != null) preset.urpVolume = go.GetComponent<Volume>();
            }
#endif
        }

        private static Light FindRigLight(GameObject rigRoot, string label)
        {
            var t = rigRoot.transform.Find($"{BOSSLookDefaults.RigRootName} - {label}");
            return t != null ? t.GetComponent<Light>() : null;
        }

        // ---------------- Scene organization (container) ----------------

        public static string ContainerName(BOSSLookPreset preset) => $"BOSS Look [{preset.baseName}]";

        /// <summary>Finds or creates the empty parent that all generated scene
        /// objects are gathered under. Kept at the world origin so children keep
        /// their world positions when re-parented.</summary>
        public static Transform GetOrCreateContainer(BOSSLookPreset preset)
        {
            if (preset == null) return null;
            if (preset.containerRoot == null)
            {
                preset.containerRoot = GameObject.Find(ContainerName(preset));
            }
            if (preset.containerRoot == null)
            {
                var go = new GameObject(ContainerName(preset));
                Undo.RegisterCreatedObjectUndo(go, "Create BOSS Look Container");
                preset.containerRoot = go;
                EditorUtility.SetDirty(preset);
            }
            return preset.containerRoot.transform;
        }

        /// <summary>Re-parents a generated object under the container, preserving
        /// its world position so nothing visually shifts.</summary>
        public static void Parent(GameObject go, BOSSLookPreset preset)
        {
            if (go == null || preset == null) return;
            if (go == preset.containerRoot) return;
            var container = GetOrCreateContainer(preset);
            if (container == null || go.transform.parent == container) return;
            Undo.SetTransformParent(go.transform, container, "Organize under BOSS Look container");
        }

        /// <summary>Relinks everything, then gathers all currently-known generated
        /// objects under the container. Use for scenes built before this existed.</summary>
        public static void OrganizeUnderContainer(BOSSLookPreset preset)
        {
            if (preset == null) return;
            RelinkAll(preset);
            GetOrCreateContainer(preset);

            if (preset.lightProbeGroup != null) Parent(preset.lightProbeGroup.gameObject, preset);
            if (preset.reflectionProbes != null)
            {
                foreach (var p in preset.reflectionProbes)
                {
                    if (p != null) Parent(p.gameObject, preset);
                }
            }
            if (preset.rigRoot != null) Parent(preset.rigRoot, preset);
            if (preset.groundShadowPlane != null) Parent(preset.groundShadowPlane, preset);

            // Volumes are scene objects; the post LAYER lives on the camera and
            // must NOT be reparented.
            var postVol = preset.postVolume as Component;
            if (postVol != null) Parent(postVol.gameObject, preset);
            var urpVol = preset.urpVolume as Component;
            if (urpVol != null) Parent(urpVol.gameObject, preset);

            EditorUtility.SetDirty(preset);
        }

        /// <summary>"Parent/Child/Leaf" path usable with GameObject.Find.
        /// Stashed lights keep their GameObject active (only the Light
        /// component is disabled), so Find can locate them.</summary>
        public static string GetHierarchyPath(Transform t)
        {
            if (t == null) return null;
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return "/" + path;
        }
    }
}
