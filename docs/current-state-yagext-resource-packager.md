# YMMResourcePackager 現状調査

## 1. 概要
YMMResourcePackager は、YMM4 向けのプラグインとして動作する素材同梱・展開ツールです。

主な役割は次の 3 つです。

- `.ymmp` プロジェクトを素材同梱用の `.ymmpx` にパッケージ化する
- `.ymmpx` を展開して `.ymmp` に戻す
- `YmmpxLibPlugin` の導入案内、関連付け、ログ、除外設定をまとめて扱う

実装上は、YMM4 本体に読み込まれる WPF プラグイン本体、共通ロジック、機能ブリッジ、単体実行の展開アプリの 4 層に分かれています。

## 2. リポジトリ構成
主要ディレクトリと役割は次の通りです。

- `YMMResourcePackager/`
  - YMM4 に読み込まれる WPF プラグイン本体
  - UI、ViewModel、プラグイン登録、設定画面
- `YMMResourcePackager.Shared/`
  - UI なしの共通ロジック
  - 設定、ログ、除外ルール、パッケージング補助関数
- `YMMResourcePackager.Features/`
  - リフレクションで呼び出される機能ブリッジ
  - `YmmpxLib` 依存の pack / unpack を中継する
- `YMMResourceUnpackerApp/`
  - 単独実行可能な展開アプリ
  - `.ymmpx` の関連付けや起動補助を含む
- `YMMResourcePackager.Tests/`
  - `Shared` 層の単体テスト
- `docs/`
  - 互換性、既知制限、リリースチェックリスト
- `.github/workflows/`
  - CI と Release ワークフロー
- `scripts/`
  - YMM4 共有 DLL の取得スクリプト
- `libs/`
  - ローカルビルド用の YMM4 DLL 置き場

## 3. プロジェクト構成
ソリューションは `YMMResourceUnpackerApp.sln` で、次の 4 プロジェクトを含みます。

- `YMMResourcePackager`
  - `net10.0-windows`
  - `OutputType` は明示されていないが、WPF プラグイン DLL として動作
  - `UseWPF=true`
- `YMMResourcePackager.Shared`
  - `net10.0`
  - UI なしの共有ライブラリ
- `YMMResourcePackager.Features`
  - `net10.0-windows`
  - `Shared` 参照あり
- `YMMResourceUnpackerApp`
  - `net10.0`
  - `OutputType=Exe`
  - `win-x64` の self-contained / single-file publish 設定あり
- `YMMResourcePackager.Tests`
  - `net10.0`
  - xUnit テスト

ビルド時の特徴として、`YMMResourcePackager.csproj` から `Features` と `UnpackerApp` を MSBuild で先にビルドし、出力 DLL / EXE をプラグイン出力へコピーします。

## 4. 同梱展開プラグイン機能の場所
中心ファイルは次の通りです。

- [`YMMResourcePackager/ToolViewModel.cs`](../YMMResourcePackager/ToolViewModel.cs)
  - UI からの操作を受ける中心
  - pack / unpack / 設定 / ログ / 導入案内の制御を担う
- [`YMMResourcePackager/ToolView.xaml`](../YMMResourcePackager/ToolView.xaml)
  - 画面上の操作部
- [`YMMResourcePackager/YMMResourcePackagerPluginToolPlugin.cs`](../YMMResourcePackager/YMMResourcePackagerPluginToolPlugin.cs)
  - YMM4 の `IToolPlugin` 登録
- [`YMMResourcePackager.Features/EntryPoint.cs`](../YMMResourcePackager.Features/EntryPoint.cs)
  - `RunPackAsync` / `RunUnpack`
  - `YmmpxLib` をリフレクションで呼び出す
- [`YMMResourcePackager.Shared/PackagingRules.cs`](../YMMResourcePackager.Shared/PackagingRules.cs)
  - 素材検出、除外判定、出力ファイル名、移動処理
- [`YMMResourcePackager.Shared/ExcludeRuleStore.cs`](../YMMResourcePackager.Shared/ExcludeRuleStore.cs)
  - 除外ルールの保存 / 読み込み
- [`YMMResourcePackager.Shared/AppPaths.cs`](../YMMResourcePackager.Shared/AppPaths.cs)
  - 設定、ログ、一時ディレクトリの基点
- [`YMMResourcePackager.Shared/AppSettingsStore.cs`](../YMMResourcePackager.Shared/AppSettingsStore.cs)
  - `settings.json` の保存
- [`YMMResourcePackager.Shared/AppLogger.cs`](../YMMResourcePackager.Shared/AppLogger.cs)
  - ログ出力
- [`YMMResourceUnpackerApp/Program.cs`](../YMMResourceUnpackerApp/Program.cs)
  - `.ymmpx` 展開、ファイル関連付け、起動補助

## 5. 現在の処理フロー
### 入力
主な入力は次の通りです。

- `.ymmp` プロジェクトファイル
- 除外設定
  - グローバル除外
  - ローカル除外
- `IncludeProjectUiSettings` の有無
- `YmmpxLibPlugin` の導入状態
- `YMM4_PATH` または `libs/` の YMM4 DLL

### 検出
`PackagingRules.GetProjectFilePaths()` が `.ymmp` 内の `FilePath` を再帰的に拾います。

`PackagingRules.ResolveExcludedFiles()` と `ValidateProjectBeforePack()` が、除外対象と欠損素材を計算します。

### コピー / 梱包
`ToolViewModel.PackageProjectAsync()` が全体の起点です。

1. すでに `.ymmpx` を選んでいる場合は、梱包ではなく展開に回します
2. `YmmpxLib` の導入確認を行います
3. 素材数、除外数、欠損数を事前チェックします
4. 出力先の重複があれば確認し、必要に応じて連番サフィックスを付けます
5. 一時ファイルを作成します
6. `YMMResourcePackager.Features.EntryPoint.RunPackAsync()` を反射で呼びます
7. 完了後に一時ファイルを最終出力へ移動します
8. エクスプローラーで出力先を開きます

### 展開
`.ymmpx` の展開は `ToolViewModel.InvokeFeatureUnpack()` から `YMMResourcePackager.Features.EntryPoint.RunUnpack()` を呼びます。

その先では `YMMResourceUnpackerApp.Program.Main()` が

- `--associate` による `.ymmpx` 関連付け
- 通常展開
- `YmmpxLib` 欠如時の案内
- 起動後の `.ymmp` 再オープン補助

を担当します。

## 6. 入出力仕様
### 入力されるファイル・設定
- `.ymmp` プロジェクト
- `.ymmpx` パッケージ
- `exclude` ルール JSON
- `settings.json`
- `YMM4_PATH` に配置された YMM4 開発用 DLL
- `libs/YMM4/*.dll`
- `YmmpxLibPlugin.ymme`

### 出力される成果物
- `.ymmpx` 出力
- 展開後の `.ymmp`
- `settings.json`
- `logs/*.log`
- `temp/*.ymme`
- release 時の `YMMResourcePackager.ymme`

### 一時ファイル / 中間生成物
- `.<name>.<guid>.tmp.ymmpx`
- `temp/YmmpxLibPlugin.ymme`
- `YMMResourceUnpackerApp` の publish 出力
- `YMMResourcePackager.Features.dll`
- `YMMResourcePackager.Shared.dll`

### zip / release 構成
`release.yml` では次の内容を最終アーカイブに入れます。

- `LICENSE`
- `readme.txt`
- `THIRD_PARTY_NOTICES.md`
- `YMMResourcePackager.deps.json`
- `YMMResourcePackager.dll`
- `YMMResourcePackager.Features.dll`
- `YMMResourcePackager.pdb`
- `YMMResourcePackager.Shared.dll`
- `YMMResourceUnpackerApp.dll`
- `YMMResourceUnpackerApp.exe`
- `YMMResourceUnpackerApp.runtimeconfig.json`

`YmmpxLib.dll` と `YmmpxLib.deps.json` は release package から除外されています。

## 7. UI / CLI / 自動化の関係
### UI
`ToolView.xaml` が YMM4 のツール画面を構成します。

主要な操作は次の通りです。

- `同梱 / 展開`
- `除外素材を設定`
- `YmmpxLib をインストール`
- `.ymmpx 展開先`
- `ログを有効化`
- `ログフォルダを開く`
- `最新ログを開く`

### CLI
`YMMResourceUnpackerApp` はコンソールアプリとしても動きます。

- 引数に `.ymmpx` を渡して展開
- `--associate` で `.ymmpx` 関連付け
- `--enable-logging` / `--disable-logging` でログ設定

### GitHub Actions
- `ci.yml`
  - `master` への push と pull request で build / test
- `release.yml`
  - `v*` タグで release
  - build / test / publish / package / draft release を実施

### release workflow
release は `YMMResourceUnpackerApp` の publish をベースにし、そこへプラグイン DLL 群と同梱物を足して `.ymme` にまとめます。

## 8. 依存関係
### NuGet
確認できた主な参照は次の通りです。

- `Microsoft.NET.Test.Sdk`
- `xunit`
- `xunit.runner.visualstudio`
- `coverlet.collector`

### 外部ライブラリ / 外部 DLL
- `YukkuriMovieMaker.Plugin.dll`
- `YukkuriMovieMaker.Controls.dll`
- `YmmpxLib.dll`

### 取得 / 配布フロー
- `scripts/fetch-ymm4-libs.ps1` が YMM4 release から DLL を取得し、SHA-256 を検証する
- `ToolViewModel` は `YmmpxLibPlugin.ymme` を GitHub Releases の latest から取得する
- ローカルでの上書き取得は `YMMRESOURCEPACKAGER_YMMPXLIBPLUGIN_PATH` で差し替え可能

### YMM4 依存
- プラグイン本体は YMM4 の `user\plugin\YMMResourcePackager` 配下に配置される前提
- `YMM4_PATH` があると、ビルド後に自動コピーされる
- `libs/` はクリーン環境での代替参照先

## 9. 既存仕様と実装の差分
現時点で大きな仕様崩れは見つかっていません。README / workflow / 実装は概ね一致しています。

ただし、次の差分または注意点があります。

- `README.md` と `.github/assets/README.txt` が両方存在する
  - release で配布されるのは `.github/assets/README.txt`
  - どちらも「同梱展開プラグイン」の案内を意図しているが、実配布物の README は別ファイル
- `README.md` の一部はこの環境では文字化けして見える
  - 内容の意図は概ね把握できるが、実際の文字コード確認は必要
- `release.yml` は `YMMResourcePackager.ymme` を固定名にしている
  - これは README / release checklist と一致している
- `YmmpxLib.dll` は release package に入れない方針が実装・文書とも一致している
- 既知制限にある「旧 `YmmpxLib` フォルダーは手動削除」が、実装でも `ToolViewModel` の起動時警告として出る

## 10. 問題点・改善候補
確認できた問題点は次の通りです。

- 命名がやや不統一
  - `同梱展開プラグイン`
  - `YMM4同梱展開プラグイン`
  - `YMMResourcePackagerPlugin`
  - `YMMResourcePackager`
- 一部の例外メッセージや文字列が文字化けしている
  - `PackagingRules.MoveGeneratedPackage()` の例外メッセージ
  - `README.md` / 一部 XAML の表示文字列
- `EntryPoint` が `YmmpxLib` のメソッド名にリフレクション依存している
  - 外部 DLL の仕様変更に弱い
- `GetProjectFilePaths()` が JSON 内の `FilePath` を広く再帰探索する
  - 意図しない場所の `FilePath` まで拾う可能性がある
- `AppPaths.ResolvePluginDirectory()` と `EntryPoint.ResolvePluginRoot()` に似た判定ロジックがある
  - ただし現段階では変更しない前提
- `ToolViewModel` がかなり多機能
  - pack / unpack / install / settings / logging / association が 1 クラスに集約されている

## 11. 改修候補
### High
- 同梱対象の定義を明文化する
- 除外対象と必須ファイルのルールを整理する
- `ToolViewModel` の責務分離を検討する前提調査を進める
- `YmmpxLib` 取得と起動案内のエラー経路を整理する

### Medium
- UI 表記の統一
- 文字化け文字列の確認と修正候補抽出
- `Features` の反射呼び出しを薄いアダプタに寄せる
- release / publish の成果物一覧をコードと文書で一致させる

### Low
- ログ文言の整備
- `Path` / `FilePath` 探索の意図をコメントまたは docs で補強する
- `README.md` と `.github/assets/README.txt` の役割差を明文化する

## 12. リスク
変更時に壊れやすい箇所は次の通りです。

- `YMMResourcePackager.Features` と `YmmpxLib` の間の反射呼び出し
- release workflow の zip / `.ymme` 生成手順
- `YMMResourceUnpackerApp` の `--associate` と展開処理
- `AppSettingsStore` が使う `settings.json`
- 旧 `YmmpxLib` フォルダー検出と警告フロー
- `CopyToYMM4` / `CopyReleasePublishToYMM4` の自動配置

## 13. 次にやるべきこと
実装前の確認事項と推奨順序は次の通りです。

1. 「同梱展開プラグイン」で同梱したい対象の正式な一覧を確定する
2. 除外対象の優先順位を整理する
3. release package に必須のファイルを明文化する
4. UI 表記を「同梱展開プラグイン」で統一するかどうか決める
5. `YmmpxLib` 取得失敗時の挙動を実機で確認する
6. 旧 `YmmpxLib` フォルダー警告の運用を確認する
7. テストで守る対象と手動確認対象を分ける
8. その後に責務分離やリファクタへ進む
