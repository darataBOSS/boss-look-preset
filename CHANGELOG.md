# Changelog

All notable changes to this project will be documented in this file.

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
