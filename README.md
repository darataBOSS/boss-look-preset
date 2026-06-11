# BOSS Look Preset

Unity Editor 拡張パッケージ。HDRI 環境光ベイク・3点ライトリグ・ポストプロセスをウィザードで半自動セットアップし、STYLY の WebAR / アプリ向けに「ルック」を整えるツール。

- レンダーパイプライン: **Built-in RP** および **URP** (自動判定)
- 最低対応 Unity: **2022.3.24f1**
- メニュー: `BOSS > Look Preset`

## インストール

Unity の Package Manager で `+` → **Add package from git URL** に以下を貼り付けます。

```
https://github.com/darataBOSS/boss-look-preset.git
```

バージョン固定したい場合はタグを付けます。

```
https://github.com/darataBOSS/boss-look-preset.git#v0.1.0
```

依存パッケージ (Post Processing Stack v2) は Package Manager が自動で取得します。

## 構成

3つのモジュールを1つのステップ式ウィザードに統合しています。すべての設定は単一の ScriptableObject プリセットに保存され、ウィザードはそれを読み書きします。

- **おまかせセットアップ**: Step 1 で HDRI を指定したら1ボタンで Step 1〜5 (スカイボックス〜リフレクション) を一括実行し、ベイクへ進めます。結果は各ステップで後から調整可能です。
- **再開に強い**: ウィザードは最後に使ったプリセットを覚えており、シーン上の生成物 (プローブ・リグ・Volume) は Unity 再起動後も名前規約で自動再リンクされます。ステップには完了マーク ✓ が付き、開いた時に未完了ステップへ自動ジャンプします。
- **シーンビュー編集**: Step 4 ではプローブ範囲が緑のボックスとしてシーンビューに表示され、ハンドルで直接ドラッグ編集できます。

- **モジュール A: 環境光 / ベイク** — HDRI スカイボックス → ライトプローブ / リフレクション → ベイク → AR 化
- **モジュール B: ライトリグ** — 3タイプ切替: 3点照明 (被写体フォーカス) / 太陽光 (屋外、クイックプリセット付き) / シーリンググリッド (広い室内、全灯Baked)
- **モジュール C: ポストプロセス** — Built-in: PPv2 / URP: Volume (自動判定)
- **モジュール D: 仕上げ** — ARグラウンドシャドウ (シャドウキャッチャー) + 距離フォグ
- **モジュール E: ルック診断** — Gamma色空間・純白アルベド・UV2欠落などを一括検出し、選択 / 自動修正

## 状態 (フェーズ)

| フェーズ | 説明 |
|---|---|
| `NotCreated` | プリセット未生成。初回フローでフォルダとアセットを生成。 |
| `Active` | プリセット有・スカイボックス ON。設定の調整、ベイク実行を繰り返せます。 |
| `Finalized` | スカイボックスを外し AR 化済み。ベイクはブロック / 警告されます。AR 化解除で `Active` に戻れます。 |

モジュール C (ポストプロセス) はランタイムのカメラ効果で、この状態機械の **外** です。いつでも付け外し・調整でき、AR 化後でも触れます。

## Built-in / URP の自動判定

Module C (ポストプロセス) は `GraphicsSettings.defaultRenderPipeline` を見て、Built-in RP なら PPv2 (`PostProcessVolume` + `PostProcessLayer`)、URP なら Volume フレームワーク (`Volume` + `VolumeProfile`) に自動で切り替わります。プロジェクト側でレンダーパイプラインを切り替えたら、ウィザードを開き直すだけで対応する系統が立ち上がります。

| エフェクト | Built-in (PPv2) | URP (Volume) |
|---|---|---|
| Bloom | ○ | ○ |
| Color Grading / Tonemapping | ○ (ColorGrading + ACES) | ○ (Tonemapping + ColorAdjustments + WhiteBalance) |
| Vignette | ○ | ○ |
| Depth of Field | ○ | ○ |
| Motion Blur | ○ | ○ |
| Ambient Occlusion | ○ (VolumeComponent) | △ Renderer Feature の手動追加が必要 |

Module A (HDRI / ベイク) と Module B (ライトリグ) は両 RP 共通で動きます。

## 注意点

- ポストプロセスを正しく効かせるには Player Settings の **Color Space = Linear** が前提です。Gamma だと Tonemapping / Color Grading が実質使えなくなります (Bloom / Vignette は動作可)。Gamma の場合は警告のみ表示し、強制変更はしません。
- URP の場合、Camera 側で `Post Processing` トグルを ON にし、URP Asset 側で `Post Processing` を有効にしておいてください (このツールは Camera Inspector や URP Asset には触りません)。
- WebAR でのポストプロセスの効きについては STYLY 公式の明言が確認できていないため、納品時は実機で要確認です。
- HDRP には現状未対応です。

## ライセンス

MIT
