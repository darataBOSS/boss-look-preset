# Changelog

All notable changes to this project will be documented in this file.

## [0.9.2] - 2026-06-14

### Fixed — 別ルックへ切り替えると例外
- ルックを切り替える (= プロファイル再適用) 際に
  `UnityException: Adding asset to object ... failed` で落ちる問題を修正。
  リインポート後はエフェクトの「メモリ上インスタンス」と「ディスク表現」が
  別物になり `IsSubAsset` が誤判定 → 既に保存済みのエフェクトを二重に
  `AddObjectToAsset` して例外になっていた。
- ガードを `EditorUtility.IsPersistent` に変更し、`AddObjectToAsset` を
  try/catch で保護。プロファイル掃除も、ディスク表現のオーファン破棄
  (インスタンス不一致で生きたサブアセットを消す恐れ) をやめ、リストの
  null / 重複除去のみに限定。
- Built-in (PPv2) / URP 両方に適用。

## [0.9.1] - 2026-06-14

### Fixed — ポストプロセスが保存されない致命バグ
- Post Process Profile / URP Volume Profile に追加したエフェクトが **ディスクに
  サブアセットとして保存されていなかった** 問題を修正。`AddSettings` / `Add` は
  メモリ上のリストに足すだけで、`AssetDatabase.AddObjectToAsset` を呼ばないと
  永続化されず、保存・アップロード・ドメインリロードで全エフェクトが null 化して
  いた (プロファイルが空 = ポストが何も効かない)。STYLY アップ時に「ルックが
  効かずベイクの見た目のまま」だった主因。
- 旧ビルドで壊れたプロファイル (null/重複エントリ) を再セットアップ時に自動で
  掃除して作り直すように。
- Built-in (PPv2) と URP の両方に適用。`ReapplyProfile` も保存するように変更。

## [0.9.0] - 2026-06-13

### Added — 出力ターゲット (STYLY モバイル対応)
- ルックタブ先頭に **出力ターゲット**選択を追加: **汎用 / PC (Linear)** と
  **STYLY モバイル (Gamma)**。汎用は従来通り (ACES + HDR グレード)。
- **STYLY モバイルモード**: STYLY モバイルは Linear 不可・自前カメラで描画する
  ため、ルックを2層で効かせる:
  1. **ライティング/環境光への焼き込み** (`LookBakeOps`) — スカイボックス Tint/
     Exposure と Ambient 強度に色の意図を反映。標準シーンデータなのでポストが
     落ちても残る。
  2. **LDR モードのポストグレード** — `ColorGrading` を `LowDefinitionRange` に
     切り替え、ACES なしで明るさ/コントラスト/彩度/色温度/ティント/フィルターを
     Gamma のまま反映。Bloom 閾値も HDR 非依存に調整。
- 診断 (Lint) がターゲット連動: STYLY モバイル時は **Gamma を正常扱い**、逆に
  Linear を警告。汎用時は従来通り Gamma を警告。
- ウィザードヘッダーに現在の出力ターゲットを常時表示。

## [0.8.0] - 2026-06-13

### Added — ルックタブ (Module G)
- ワンクリックで「カラーグレード + Bloom/Vignette + フォグ + ライト比」を
  まとめて設定する **ルックプリセット集**。組み込み4種:
  - **シネマティック** — 沈めた露出・高コントラスト・寒色寄り
  - **明るい物販** — 均一でクリーンな明るさ、フラットな影
  - **ムーディ** — 暗部を締めた高コントラスト・濃いめの空気感
  - **ビビッド** — 高彩度・やや暖色で華やか
- **適用の強さ** スライダ (0=ニュートラル 〜 1=最大) でニュートラルからブレンド適用。
- 適用するとポストプロファイル・フォグ・ライトリグへ即反映 (ベイクや生成物は不変)。
- **カスタムルックの保存 / 適用 / 削除**: 現在の見た目を `<preset>/Looks/*.asset` に
  保存し、ギャラリーから呼び出せます。
- ルックタブを先頭に配置。タブ構成: ★ルック / 環境光 / ライト / ポスト / 仕上げ / 品質 / 診断。

## [0.7.0] - 2026-06-13

「整える三本柱」+ 環境系の品質レバーを追加し、ウィザードUIを刷新。

### Added — 品質タブ (Module F)
- **アンチエイリアス**: None / FXAA / SMAA / MSAA 2x・4x・8x を選択。Built-in は
  `QualitySettings.antiAliasing` (MSAA) と PostProcessLayer (FXAA/SMAA)、URP は
  URP Asset の `msaaSampleCount` と Camera の AA モードへ自動振り分け。
- **影の品質**: 影距離 (被写体スケールから自動算出ボタン付き) / カスケード /
  解像度 / ソフトシャドウ。Built-in は QualitySettings、URP は URP Asset へ。

### Added — カラーグレード (Module C 拡張)
- これまで実質ノーオーバーライドだったグレーディング面を展開。露出 (EV) /
  コントラスト / 彩度 / カラーフィルター / 色温度 / ティントをスライダ化し、
  PPv2 ColorGrading と URP ColorAdjustments+WhiteBalance の両方へライブ反映。
  Bloom / Vignette も強度スライダを追加。
- ポストの AO を軽め設定で既定 ON に変更。

### Added — 環境 (Module A 拡張)
- **スカイボックスの回転 / 露出** スライダ (ライブ反映)。HDRI の太陽の向きを回せます。
- **HDRI 太陽整列**: 太陽リグ選択時、equirectangular HDRI の最輝点を太陽とみなして
  太陽の高度 / 方位を自動推定するボタン (`🧭`)。
- **リフレクションプローブ解像度** の選択 (64〜1024) と HDR 既定 ON。

### Changed — UI 刷新
- 共通 UI ヘルパー `BOSSLookUI` を追加し、アクセントバー付きカード・役割色ボタン・
  補足ヒント表示を全タブで統一。タブは「環境光 / ライト / ポスト / 仕上げ / 品質 / 診断」。

## [0.6.0] - 2026-06-11

### Added
- **Light rig types** (Module B): the rig is now selectable between three
  setups sharing one rig root. Switching types rebuilds in place and removes
  lights that belong to the other types.
  - **3点照明 (ThreePoint)** — the existing subject-focused Key/Fill/Back.
  - **太陽光 (Sun)** — outdoor: a Mixed directional sun (elevation / azimuth /
    intensity / Kelvin) plus an optional Baked "sky fill" from the opposite
    side. Quick presets: 昼 / 朝・夕 / 曇り. The realtime (Mixed) sun also
    makes the Built-in ground-shadow catcher work out of the box.
  - **シーリンググリッド (CeilingGrid)** — large interiors: an N×M grid of
    downward lights (Spot / Point / Area) covering the probe area, placed at
    the box's top face. All Baked so the runtime cost is zero; dynamic objects
    are lit via light probes.
- Scene relink, lint, and the stash logic understand the new rig types
  (the rig's own Sun is never stashed as an "existing" directional).

## [0.5.0] - 2026-06-11

### Added
- **Module D — 仕上げ (Finishing)**:
  - **AR ground shadow (shadow catcher)**: one button creates a transparent
    shadow-receiving plane under the subject so AR content doesn't float.
    The shader is generated into the preset folder per active render pipeline
    (Built-in: main directional shadows; URP: main + additional/spot shadows),
    so the package itself never fails to import in projects without URP.
    Opacity slider applies live; size follows the subject's footprint.
  - **Fog / atmosphere**: RenderSettings distance fog driven by the preset
    with live apply, plus a "suggest color from environment" button that
    samples the ambient probe at the horizon to match the HDRI sky.
- **Module E — ルック診断 (Look Lint)**: one-button diagnosis of common
  look-killers with select / auto-fix actions per issue:
  Gamma color space, near-white albedo (GI blowout), missing UV2 on
  ContributeGI meshes, overly strong emission, disabled mipmaps,
  camera HDR off while Bloom is on, missing light/reflection probes,
  and disabled light color temperature. Reimport-heavy fixes confirm first.

## [0.4.0] - 2026-06-11

### Added
- Step 0 defaults are seeded from the open scene: Base Name = scene name,
  Parent Folder = the scene's directory, so look assets land next to the scene
  they belong to. A "re-read from scene" button refreshes them.
- Step 0 now shows the fields in parent → child order with a live
  "生成先" path preview, making the folder hierarchy obvious.
- Color-coded buttons across the wizard: green = generate/update,
  blue = auto setup, orange = bake, purple = finalize (AR化),
  red = destructive (delete/remove/cancel). Completed steps in the step strip
  are tinted green.

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
