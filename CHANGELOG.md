# Changelog

All notable changes to this project will be documented in this file.

## [0.3.0] - 2026-06-11

### Fixed (robustness)
- **Scene references now survive editor restarts.** The preset asset cannot
  persist references to scene objects, so they all came back null after
  reopening Unity and "create or update" would duplicate objects. A new
  `SceneRelinkOps` re-finds the probe group, reflection probes, light rig and
  post-process volumes by the tool's naming convention whenever the wizard
  opens or a preset is selected.
- Stashed directional lights record their hierarchy path so they can be
  restored even after a restart; unresolvable ones are reported instead of
  silently lost.
- Area rig lights are now `Baked` (Area + `Mixed` is an invalid combination).
- Wizard warns when `GraphicsSettings.lightsUseColorTemperature` is off (the
  rig's Kelvin values silently do nothing without it) and offers a one-click
  enable.
- The wizard remembers its preset across domain reloads, restores the last
  used preset per project, and auto-loads the preset when it is the only one
  in the project.
- Null entries no longer accumulate in the reflection-probe list.
- Obsolete-API guards for Unity 6 (`FindObjectsOfType`, `giWorkflowMode`).

### Added
- **おまかせセットアップ**: one button on Step 1 runs Steps 1–5 with
  scene-derived bounds (skybox, lighting settings, static flags, light probe
  grid, reflection probe) and jumps to the bake step.
- **Scene-view probe area gizmo**: while Step 4 is open, the probe area is
  drawn as a draggable box (BoxBoundsHandle) in the scene view.
- Step strip shows a check mark on completed steps, and opening a preset
  resumes at the first incomplete step.
- Reflection Mode B is now implemented: the probe renders a neutral gray
  background (SolidColor clear) instead of the skybox, so the HDRI sky no
  longer bakes into reflections.
- URP: the main camera's Post Processing toggle is enabled automatically
  during Module C setup.
- Live preview: rig parameters and Environment Intensity apply to the scene
  immediately once their objects exist (no extra button press).
- Bake progress is shown as a progress bar; "select offending objects" button
  for the UV2 warning; rig subject falls back to the current selection.
- Finalize / Unfinalize record Undo for the preset.

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
