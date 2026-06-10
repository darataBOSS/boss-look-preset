# Changelog

All notable changes to this project will be documented in this file.

## [0.2.0] - 2026-06-09

### Added
- URP (Universal Render Pipeline) support for Module C (Post Processing).
  Module C now auto-detects the active render pipeline and dispatches to either
  PPv2 (Built-in RP) or the URP Volume framework. No user toggle required.
- `RenderPipelineDetector` helper that reports Built-in / URP / HDRP / Unknown.
- URP Volume + VolumeProfile generation in Module C, with Bloom, Tonemapping (ACES),
  ColorAdjustments, WhiteBalance, Vignette, DepthOfField, MotionBlur as
  VolumeComponents matching the existing shared effect toggles.
- Active render pipeline name displayed in the wizard header.

### Changed
- `PostProcessOps` is now a dispatcher; PPv2 implementation moved to
  `PostProcessOpsBuiltin`, URP implementation lives in `PostProcessOpsURP`.
- `BOSSLookPreset` SO gained `urpVolume` / `urpVolumeProfile` fields.
- Module C tab labels itself with the active backend ("PPv2" or "URP Volume").

### Notes
- URP AO is a Renderer Feature (Screen Space Ambient Occlusion), not a
  VolumeComponent. The wizard now shows a warning and the user must add the
  feature to their URP Renderer Asset manually.
- Module A (HDRI / bake) and Module B (light rig) work on both Built-in and URP
  unchanged — the Unity APIs they use are pipeline-agnostic.
- HDRP is detected but not supported in this version.

## [0.1.1] - 2026-06-09

### Changed
- Renamed package id from `com.oborokurage.boss-look-preset` to `com.darataboss.boss-look-preset`.
- Renamed C# root namespace from `Oborokurage.BOSSLookPreset` to `DarataBOSS.BOSSLookPreset`.

### Fixed
- Added `.meta` files for all package assets so Unity no longer warns "has no meta file, but it's in an immutable folder" when the package is installed via Package Manager.

## [0.1.0] - 2026-06-09

### Added
- Initial release.
- Module A: HDRI / environment lighting bake wizard (skybox, lighting settings, light probes, reflection probes, bake, AR-ize).
- Module B: 3-point light rig (Spot / Area, Key / Fill / Back) with existing-light stashing.
- Module C: Post Processing Stack v2 setup (profile / volume / layer) with toggles.
- Single ScriptableObject preset acting as the anchor for all settings and phase state.
