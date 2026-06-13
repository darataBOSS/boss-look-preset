using UnityEditor;
using UnityEngine;

namespace DarataBOSS.BOSSLookPreset.Editor.Ops
{
    /// <summary>
    /// Finds the brightest direction in an equirectangular HDRI and points the
    /// Sun rig at it, so cast shadows line up with the environment's own sun.
    /// Best-effort: only equirectangular Texture2D is supported; the result is a
    /// good starting point the user can fine-tune with the elevation/azimuth
    /// sliders afterwards.
    /// </summary>
    public static class HdriSunOps
    {
        public static bool CanAlign(BOSSLookPreset preset)
        {
            return preset != null
                && preset.hdriTexture is Texture2D
                && preset.rigType == RigType.Sun;
        }

        public static bool AlignSunToHdri(BOSSLookPreset preset, out string message)
        {
            message = null;
            if (preset == null || !(preset.hdriTexture is Texture2D tex))
            {
                message = "equirectangular な Texture2D の HDRI が必要です。";
                return false;
            }

            string path = AssetDatabase.GetAssetPath(tex);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            bool restoreReadable = false;
            bool restoreCompression = false;
            TextureImporterCompression prevCompression = TextureImporterCompression.Compressed;

            try
            {
                if (importer != null)
                {
                    if (!importer.isReadable)
                    {
                        importer.isReadable = true;
                        restoreReadable = true;
                    }
                    if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                    {
                        prevCompression = importer.textureCompression;
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        restoreCompression = true;
                    }
                    if (restoreReadable || restoreCompression)
                    {
                        importer.SaveAndReimport();
                    }
                }

                if (!FindBrightestDirection(tex, preset.skyboxRotation, out Vector3 sunDir))
                {
                    message = "テクスチャを読み取れませんでした (Read/Write を有効化できているか確認してください)。";
                    return false;
                }

                // Directional light forward should point toward the scene, i.e.
                // away from the sun. Decompose into the rig's elevation/azimuth.
                float elevation = Mathf.Asin(Mathf.Clamp(sunDir.y, -1f, 1f)) * Mathf.Rad2Deg;
                float azimuth = Mathf.Atan2(-sunDir.x, -sunDir.z) * Mathf.Rad2Deg;
                if (azimuth < 0f) azimuth += 360f;

                preset.sunElevationDeg = Mathf.Clamp(elevation, 0f, 90f);
                preset.sunAzimuthDeg = azimuth;
                EditorUtility.SetDirty(preset);

                message = $"太陽の向きを HDRI から推定しました (高度 {elevation:F0}° / 方位 {azimuth:F0}°)。スライダで微調整できます。";
                return true;
            }
            finally
            {
                if (importer != null && (restoreReadable || restoreCompression))
                {
                    if (restoreReadable) importer.isReadable = false;
                    if (restoreCompression) importer.textureCompression = prevCompression;
                    importer.SaveAndReimport();
                }
            }
        }

        private static bool FindBrightestDirection(Texture2D tex, float skyboxRotationDeg, out Vector3 dir)
        {
            dir = Vector3.up;
            Color[] pixels;
            try
            {
                pixels = tex.GetPixels();
            }
            catch
            {
                return false;
            }
            if (pixels == null || pixels.Length == 0) return false;

            int w = tex.width;
            int h = tex.height;
            // Subsample for speed on large HDRIs.
            int stride = Mathf.Max(1, Mathf.Max(w, h) / 512);

            float best = -1f;
            int bestX = 0, bestY = 0;
            for (int y = 0; y < h; y += stride)
            {
                for (int x = 0; x < w; x += stride)
                {
                    Color c = pixels[y * w + x];
                    float lum = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
                    if (lum > best)
                    {
                        best = lum;
                        bestX = x;
                        bestY = y;
                    }
                }
            }

            // Equirectangular: u→longitude, v→latitude. Apply the skybox Y
            // rotation so the direction matches what's actually displayed.
            float u = (bestX + 0.5f) / w;
            float v = (bestY + 0.5f) / h;
            float lon = (u - 0.5f) * 2f * Mathf.PI + skyboxRotationDeg * Mathf.Deg2Rad;
            float lat = (v - 0.5f) * Mathf.PI;

            float cosLat = Mathf.Cos(lat);
            dir = new Vector3(cosLat * Mathf.Sin(lon), Mathf.Sin(lat), cosLat * Mathf.Cos(lon)).normalized;
            return true;
        }
    }
}
