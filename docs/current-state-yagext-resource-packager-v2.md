# YMMResourcePackager 現状調査 v2

## 1. 概要
YMMResourcePackager は、YMM4 向けの「同梱展開プラグイン」である。
.ymmp プロジェクトを .ymmpx に同梱展開し、必要に応じて YmmpxLib の導入も案内・実行する。

現在の実装は、改修前の一枚岩構造ではなく、以下のように責務分離済みの完成状態になっている。

- `PackagingRules` はオーケストレーター
- `PackagingDetector` は検出
- `PackagingExcluder` は除外
- `PackagingValidator` は検証
- `PackagingOutputHelper` は出力補助
- `ToolViewModel` は UI と実行フローの制御
- `AppLogger` はログ記録

## 2. リポジトリ構成
主要ディレクトリと役割は次のとおり。

- `YMMResourcePackager/`
  - WPF ベースのプラグイン UI と操作ロジック
- `YMMResourcePackager.Shared/`
  - 同梱展開の中核ロジック、設定、ログ、補助関数
- `YMMResourcePackager.Features/`
  - YmmpxLib を反射経由で呼び出す実処理ラッパー
- `YMMResourceUnpackerApp/`
  - `.ymmpx` 展開側の補助アプリ
- `YMMResourcePackager.Tests/`
  - `PackagingRules` と関連機能のテスト
- `docs/`
  - 仕様ドラフト、現状資料、制約メモ

## 3. プロジェクト構成
ソリューションは `YMMResourceUnpackerApp.sln`。

主なプロジェクトは次のとおり。

- `YMMResourcePackager`
  - WPF プラグイン本体
- `YMMResourcePackager.Shared`
  - 共通ライブラリ
- `YMMResourcePackager.Features`
  - YmmpxLib 連携の実行ラッパー
- `YMMResourceUnpackerApp`
  - 展開用アプリ
- `YMMResourcePackager.Tests`
  - テストプロジェクト

`YMMResourcePackager.csproj` は、ビルド時に `Shared`、`Features`、`Unpacker` を出力へ集約する構成になっている。

## 4. 同梱展開プラグイン機能の場所
中心ファイルと責務は次のとおり。

- [`YMMResourcePackager.Shared/PackagingRules.cs`](C:/Users/yu-za-hazimeyou/source/repos/hazimeyou/YMMResourcePackager/YMMResourcePackager.Shared/PackagingRules.cs)
  - 全体の窓口
- [`YMMResourcePackager.Shared/PackagingDetector.cs`](C:/Users/yu-za-hazimeyou/source/repos/hazimeyou/YMMResourcePackager/YMMResourcePackager.Shared/PackagingDetector.cs)
  - `.ymmp` から候補を抽出する
- [`YMMResourcePackager.Shared/PackagingExcluder.cs`](C:/Users/yu-za-hazimeyou/source/repos/hazimeyou/YMMResourcePackager/YMMResourcePackager.Shared/PackagingExcluder.cs)
  - 除外ルールを適用する
- [`YMMResourcePackager.Shared/PackagingValidator.cs`](C:/Users/yu-za-hazimeyou/source/repos/hazimeyou/YMMResourcePackager/YMMResourcePackager.Shared/PackagingValidator.cs)
  - 事前検証を行う
- [`YMMResourcePackager.Shared/PackagingOutputHelper.cs`](C:/Users/yu-za-hazimeyou/source/repos/hazimeyou/YMMResourcePackager/YMMResourcePackager.Shared/PackagingOutputHelper.cs)
  - 出力パス生成と移動を扱う
- [`YMMResourcePackager/ToolViewModel.cs`](C:/Users/yu-za-hazimeyou/source/repos/hazimeyou/YMMResourcePackager/YMMResourcePackager/ToolViewModel.cs)
  - UI から各処理を呼び出す
- [`YMMResourcePackager.Features/EntryPoint.cs`](C:/Users/yu-za-hazimeyou/source/repos/hazimeyou/YMMResourcePackager/YMMResourcePackager.Features/EntryPoint.cs)
  - YmmpxLib の反射呼び出し境界

## 5. 現在の処理フロー
### 5.1 全体フロー
```text
.ymmp入力
↓
Detector（候補抽出）
↓
Excluder（除外）
↓
Validator（検証）
↓
YmmpxLib（実梱包）
↓
OutputHelper（出力）
```

`PackagingRules` はこの流れを束ねるだけで、実処理の中心ではない。

### 5.2 UI からの流れ
1. `ToolViewModel.PackageProjectAsync()` が開始される
2. UI で設定済みの除外ルールと出力先が読み込まれる
3. `PackagingRules.ResolveExcludedFiles()` が候補から除外対象を求める
4. `PackagingRules.ValidateProjectBeforePack()` が件数と欠落を確認する
5. `YMMResourcePackager.Features.EntryPoint.RunPackAsync()` が呼ばれる
6. `PackagingRules.CreateTemporaryPackagePath()` で一時出力を作る
7. `PackagingRules.MoveGeneratedPackage()` で最終出力へ移す

## 6. 入出力仕様
### 入力
- `.ymmp` プロジェクト
- 設定ファイル
  - `settings.json`
  - `packaging_options.json`
- 除外ルール
  - グローバル除外
  - ローカル除外

### 出力
- `.ymmpx`
- 配布 zip
- ログファイル

### 中間
- 一時パス
- 検出済み候補ファイル
- 除外後ファイル一覧

### ファイル扱い
- `Dll`
  - `.ymmpx` 実梱包に必要な主要成果物
- `pdb`
  - 任意
- `README` / `LICENSE`
  - 配布 zip には含める
  - `.ymmpx` には含めない

## 7. UI / ログ
### ToolViewModel の役割
`ToolViewModel` は、画面表示・ボタン操作・警告ダイアログ・進捗表示・実行順序の制御を担当する。

### ログ ON/OFF
- `IsLoggingEnabled` が UI 側の切替フラグ
- 既存設定 `AppLoggingSettings.EnableLogging` に保存される
- `AppLogger.RefreshSettingsCache()` によって反映される
- ログは ON のときだけファイルへ出る

### AppLogger の使われ方
- `LogInfo`
- `LogWarning`
- `LogError`
- `LogException`

### ユーザーに見えるメッセージ
- 同梱展開開始
- 検証結果
- 事前チェック警告
- YmmpxLib 未導入案内
- 完了・キャンセル・エラー

## 8. 依存関係
### YmmpxLib
最重要依存。実梱包の最終処理は `YMMResourcePackager.Features.EntryPoint` 経由で YmmpxLib の型とメソッドを反射呼び出ししている。

### NuGet / 外部ライブラリ
実装上の中心は .NET/WPF 標準 API であり、特定の大きな外部基盤は見当たらない。
詳細な NuGet の固定一覧は csproj から確認できるが、現調査では大きな外部依存は限定的。

### YMM4 プラグイン API
`YukkuriMovieMaker.Plugin` と `YukkuriMovieMaker.Controls` に依存する。

## 9. docs と実装の一致確認
以下の docs は、現在の実装と概ね整合している。

- [`docs/packaging-rules-refactor-plan.md`](C:/Users/yu-za-hazimeyou/source/repos/hazimeyou/YMMResourcePackager/docs/packaging-rules-refactor-plan.md)
- [`docs/bundled-plugin-spec-draft.md`](C:/Users/yu-za-hazimeyou/source/repos/hazimeyou/YMMResourcePackager/docs/bundled-plugin-spec-draft.md)

一致している点:
- 責務分離後の構造
- UI 表記の「同梱展開プラグイン」統一
- ログ機能の ON/OFF
- `.ymmpx` と配布 zip の役割分離

古い記述が残る可能性がある箇所:
- 文字化けした過去資料
- `YmmpxLib` 境界の今後の扱い
- UI 導線の微調整

## 10. 現在の状態評価
### 完了していること
- 責務分離
- ログ機能
- UI 統一
- 処理フロー確定
- `PackagingRules` のオーケストレーター化

### 残課題
- `YmmpxLib` 境界の将来改善
- エラーメッセージの細部調整
- UI 導線の微調整
- ログの詳細化

## 11. 問題点
ここでいう問題点は、未解決または今後の改善候補のみ。

- `YmmpxLib` 境界が反射依存のまま
- `FilePath` ベースの検出仕様への依存が残る
- ログはあるが、粒度はまだ最小限
- エラー文言は一部が簡潔で、原因把握に追加情報が欲しい場面がある

## 12. 改修候補
### High
- `YmmpxLib` 境界の整理方針を明文化する
- 失敗時メッセージの詳細をそろえる

### Medium
- ログの粒度を少し増やす
- UI の案内文をさらにわかりやすくする

### Low
- 将来の構造改善案を docs に追記する
- 例外メッセージの表現を少し整える

## 13. リスク
- `YmmpxLib` 変更時の影響が大きい
- `FilePath` の仕様変更に弱い
- UI と内部ロジックがまだ完全分離ではない
- 反射呼び出し先の型名・メソッド名変更に脆い

## 14. 次にやるべきこと
実装ではなく方向性として、次の順が自然。

1. エラーメッセージ改善
2. ログ詳細化
3. UI 微調整
4. `YmmpxLib` 境界の再整理
5. 将来の構造改善案を docs に固定
