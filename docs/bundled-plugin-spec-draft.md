# 同梱展開プラグイン仕様ドラフト

## 1. 同梱展開プラグイン機能の目的
この機能の目的は、YMM4 の `.ymmp` プロジェクトを、配布・共有しやすい `.ymmpx` 形式へまとめることです。

機能の役割は次の 4 つに整理できます。

- プロジェクト内の参照素材を同梱する
- 除外設定を反映して、同梱対象を調整する
- `.ymmpx` から `.ymmp` への展開を維持する
- YMM4 の配布フローで実用的な構成を保つ

現状の実装では、同梱機能は `YMMResourcePackager` を中心に、`YmmpxLibPlugin` と `YMMResourceUnpackerApp` にまたがって構成されています。

## 2. 同梱対象にするファイル
### 仕様として固定する考え方
同梱対象の最終判定は `YmmpxLib` 側で行うものとします。

つまり、仕様上の同梱対象は「`YmmpxLib` が同梱対象として扱うもの」に従います。

### 現状実装として記録する内容
現状のコードでは、`PackagingRules.FindFilePaths()` が `.ymmp` の JSON を再帰走査し、`FilePath` を拾っています。

このため、現状実装の観察としては次を記録します。

- `.ymmp` 内の `FilePath`
- ネストしたオブジェクト内の `FilePath`
- 相対パスの素材
- 絶対パスの素材
- `file://` 形式の URI

ただし、これらは「現状の検出方法」であり、仕様上の最終判定そのものではありません。

### 仕様上の扱い
- 同梱対象の最終判断は `YmmpxLib` に従う
- `FilePath` 再帰走査は現状実装として記録する
- `FilePath` 以外を同梱対象に含めるかどうかは、`YmmpxLib` の判定結果に従う

## 3. 同梱対象から除外するファイル
現状確認できる除外対象は次の通りです。

- グローバル除外設定で除外された素材
- ローカル除外設定で除外された素材
- `YmmpxLib.dll`
- `YmmpxLib.deps.json`

補足として、`YMMResourcePackager.csproj` と YMM4 配置処理では `YmmpxLib.dll` と `YmmpxLib.deps.json` を削除しています。

また、旧 `YmmpxLib` フォルダーについては自動削除しない方針に固定します。

- 自動削除はしない
- 警告または案内に留める

## 4. 必須ファイル
### 配布 zip の必須ファイル
配布 zip には次のファイルを含めます。

- `LICENSE`
- `readme.txt`
- `YMMResourcePackager.dll`
- `YMMResourcePackager.deps.json`
- `YMMResourcePackager.Features.dll`
- `YMMResourcePackager.Shared.dll`
- `YMMResourceUnpackerApp.exe`
- `YMMResourceUnpackerApp.dll`
- `YMMResourceUnpackerApp.runtimeconfig.json`

### 配布 zip で必須ではないもの
- `YMMResourcePackager.pdb`
  - 任意
  - 存在すれば含めてもよい
  - 必須ではない

### `.ymmpx` 内に入れないもの
次のものは配布 zip には含めるが、`.ymmpx` の中には含めません。

- `LICENSE`
- `readme.txt`

## 5. 任意ファイル
任意ファイルは次の通りです。

- `YMMResourcePackager.pdb`
  - 生成されていれば入れてよい
- `settings.json`
  - 実行時設定
- `logs/*.log`
  - ログ有効時のみ生成
- `temp/*`
  - 一時生成物
- `YmmpxLibPlugin.ymme`
  - 導入確認やダウンロード時の一時利用物

## 6. 出力 zip の理想構成
配布 zip の理想構成は、ルート直下に必要ファイルを置く形です。

```text
YMMResourcePackager.ymme
├─ LICENSE
├─ readme.txt
├─ YMMResourcePackager.deps.json
├─ YMMResourcePackager.dll
├─ YMMResourcePackager.Features.dll
├─ YMMResourcePackager.pdb            # 任意
├─ YMMResourcePackager.Shared.dll
├─ YMMResourceUnpackerApp.dll
├─ YMMResourceUnpackerApp.exe
└─ YMMResourceUnpackerApp.runtimeconfig.json
```

現時点では、サブフォルダーを増やさず、既存の release 形式を保つのが前提です。

## 7. 現在の実装との差分
仕様判断を反映した場合、現在の実装との差分は次の通りです。

- 現在は UI 表記が「同梱展開プラグイン」
  - 仕様上は「同梱展開プラグイン」に統一する
- 現在は `YmmpxLibPlugin` 導入案内がある
  - 仕様上はダウンロード確認を必ず挟む
- 現在は旧 `YmmpxLib` フォルダーの警告がある
  - 仕様上も自動削除せず、案内に留める
- 現在は同梱対象の検出を `FilePath` 再帰走査で行っている
  - 仕様上の判定は `YmmpxLib` に従う
- 現在は配布物に `pdb` があればコピーされる
  - 仕様上は任意でよい
- 現在は `LICENSE` / `readme.txt` を配布 zip に含める
  - 仕様上も維持する

## 8. 互換性を維持すべき点
互換性を維持すべき点は次の通りです。

- 既存の `.ymmpx` 展開フロー
- 既存の `.ymmpx` 関連付け
- 既存の除外設定 JSON
- 既存の `settings.json`
- 既存の release package 名 `YMMResourcePackager.ymme`
- `YmmpxLib.dll` をリポジトリや release package に同梱しない方針
- 既存の `--enable-logging` / `--disable-logging`
- 既存の release workflow の大枠

特に、既存ユーザーが保存済み設定と除外設定をそのまま使えることは維持対象です。

## 9. 破壊的変更になりそうな点
破壊的変更になる可能性があるのは次の変更です。

- 同梱対象の最終判定を `FilePath` ベースに固定すること
- 除外設定の JSON 形式を変えること
- 配布 zip の構成を変更すること
- `YmmpxLib` 取得元や asset 名を変えること
- `YMMResourcePackager.Features` の反射 API を変えること
- `YMMResourceUnpackerApp` の引数仕様を変えること
- `settings.json` のキー名や意味を変えること

今回の判断では、`YmmpxLib` 側判定を仕様に採用するため、`FilePath` 再帰走査は「実装上の検出手段」として残す整理になります。

## 10. 実装前に人間が判断すべき未確定事項
現時点で不明、または今後の実装前確認が必要な点は次の通りです。

- `YmmpxLib` が最終的にどの範囲を同梱対象と判定するかの詳細
- `FilePath` 以外のキーを将来拾う必要があるか
- `YmmpxLibPlugin` 導入確認の UI 文言をどこまで変えるか
- 「同梱展開プラグイン」の略称や正式表記をどう固定するか
- 旧 `YmmpxLib` フォルダー警告の表示タイミング
- `pdb` の扱いを release とローカルビルドで分けるか
- `readme.txt` の内容を現状維持するか、表記を更新するか
- `YmmpxLib` 連携の反射 API をいつ置き換えるか

## 11. 次に実装へ進めるための最小改修単位
次に実装する場合は、以下の順で最小単位に分けるのがよいです。

1. UI 表記を「同梱展開プラグイン」に統一する
2. `LICENSE` / `readme.txt` を `.ymmpx` に入れない仕様を明文化する
3. `YmmpxLibPlugin` 導入案内で、ダウンロード確認を必ず挟む仕様に整理する
4. 文字化けしている文言・例外メッセージを修正する
5. `PackagingRules` と `YmmpxLib` の境界を整理する
6. `ToolViewModel` の分割は後回しにする

この順番にすると、UI と配布仕様を先に揃えたうえで、内部実装の整理に進めます。
