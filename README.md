# BOSS Look Preset

Unity Editor 拡張パッケージ。HDRI 環境光ベイク・3点ライトリグ・ポストプロセスをウィザードで半自動セットアップし、STYLY の WebAR / アプリ向けに「ルック」を整えるツール。

- レンダーパイプライン: **Built-in RP** 専用
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

- **モジュール A: 環境光 / ベイク** — HDRI スカイボックス → ライトプローブ / リフレクション → ベイク → AR 化
- **モジュール B: ライトリグ** — Spot / Area の 3 点ライト (Key / Fill / Back)
- **モジュール C: ポストプロセス** — Post Processing Stack v2 (Profile / Volume / Layer 一式)

## 状態 (フェーズ)

| フェーズ | 説明 |
|---|---|
| `NotCreated` | プリセット未生成。初回フローでフォルダとアセットを生成。 |
| `Active` | プリセット有・スカイボックス ON。設定の調整、ベイク実行を繰り返せます。 |
| `Finalized` | スカイボックスを外し AR 化済み。ベイクはブロック / 警告されます。AR 化解除で `Active` に戻れます。 |

モジュール C (ポストプロセス) はランタイムのカメラ効果で、この状態機械の **外** です。いつでも付け外し・調整でき、AR 化後でも触れます。

## 注意点

- ポストプロセスを正しく効かせるには Player Settings の **Color Space = Linear** が前提です。Gamma の場合は警告を出しますが強制変更はしません。
- WebAR でのポストプロセスの効きについては STYLY 公式の明言が確認できていないため、納品時は実機で要確認です。
- PPv2 は Built-in RP 専用です。URP は別系統 (Volume フレームワーク) になります。

## ライセンス

MIT
