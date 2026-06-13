# PackagingRules 責務整理メモ

## 1. 目的
`PackagingRules` を中心に、同梱対象・除外・出力に関する責務を整理する。

この文書の目的は実装変更ではなく、現状の責務配置を明文化して、将来の分離に向けた境界を決めやすくすることです。

前提は次の通りです。

- 同梱対象の最終判定は `YmmpxLib` 側に従う
- `PackagingRules` はその前後の補助処理を担う
- `ToolViewModel` は UI オーケストレーションに寄せる

## 2. 現在の処理フロー
現状の流れは大きく次の順で動いています。

1. `ToolViewModel.PackageProjectAsync()` がパッケージ化を開始する
2. `ToolViewModel` が global / local の除外ルールを読み込む
3. `PackagingRules.ResolveExcludedFiles()` で除外対象を算出する
4. `PackagingRules.ValidateProjectBeforePack()` で素材数、除外数、欠損数を確認する
5. `ToolViewModel` が出力先 `.ymmpx` を決める
6. `PackagingRules.CreateTemporaryPackagePath()` で一時出力先を作る
7. `YMMResourcePackager.Features.EntryPoint.RunPackAsync()` を呼び、`YmmpxLib` 側で実際の梱包を行う
8. `PackagingRules.MoveGeneratedPackage()` で一時出力を最終出力へ移す
9. `ToolViewModel` が進捗、完了メッセージ、エラー表示を扱う

展開側は別フローですが、`ToolViewModel` が `Features.EntryPoint.RunUnpack()` を呼び、その結果を UI に反映します。

### 2.1 処理フロー図
```text
入力 (.ymmp)
↓
検出 (Detection)
↓
除外 (Exclusion)
↓
検証 (Validation)
↓
YmmpxLib による最終判定
↓
出力補助 (Output Helper)
↓
ファイル生成
```

`PackagingRules` はこの流れのうち、検出・除外・検証・出力補助を前段で支える位置にあります。

## 3. 責務の分解
### 3.1 検出
検出は「プロジェクトから素材候補を集める」責務です。

現状の担当箇所は次の通りです。

- `PackagingRules.FindFilePaths()`
  - JSON を再帰走査して `FilePath` を拾う
- `PackagingRules.LoadProjectFilePaths()`
  - JSON を読み、検出結果を列挙する
- `PackagingRules.GetProjectFilePaths()`
  - 公開入口として検出結果を返す

この部分は「候補の列挙」であり、最終的な同梱判定そのものではありません。

- Detection はファイル候補列挙のみ
- `PackagingRules.FindFilePaths()` と `LoadProjectFilePaths()` は候補を拾うための補助
- `PackagingRules.GetProjectFilePaths()` は検出結果を外へ渡す入口

### 3.2 除外
除外は「候補のうち、どれを外すか」を決める責務です。

現状の担当箇所は次の通りです。

- `PackagingRules.NormalizeExcludedFiles()`
  - 文字列除外を正規化する
- `PackagingRules.NormalizeExcludeRules()`
  - ルールベース除外を正規化する
- `PackagingRules.IsExcludedFile(...)`
  - 文字列またはルールと候補を照合する
- `PackagingRules.ResolveExcludedFiles()`
  - プロジェクト内の候補から除外対象を算出する

ここで混ざっているのは、単純な文字列除外と、`ExcludeRule` によるルール除外が同じクラスにあることです。

- Exclusion は除外ルール適用のみ
- 候補の列挙は行わない
- 最終的に残す / 外すの判断だけを扱う

### 3.3 出力
出力は「どこにどう書き出すか」の責務です。

現状の担当箇所は次の通りです。

- `PackagingRules.CreateTemporaryPackagePath()`
  - 一時出力ファイル名を生成する
- `PackagingRules.MoveGeneratedPackage()`
  - 一時出力を最終出力へ移す
- `ToolViewModel`
  - 最終出力パスの決定
  - 上書き確認
  - 連番付与後の最終出力名決定

このため、出力の責務は `PackagingRules` と `ToolViewModel` に分散しています。

- Output Helper はパス生成・移動のみ
- 実際のファイル生成は `YmmpxLib` 側が担う
- `ToolViewModel` は出力先決定と確認を担当する

### 3.4 検証
検証は「エラーや欠損を見つける」責務です。

現状では `ValidateProjectBeforePack()` がこれを担っています。

- Validation はエラー検出のみ
- 副作用は持たない
- ファイル削除や生成は行わない

### 3.5 旧互換 / 移行対応
旧仕様対応は、移行時の案内や互換性確認の責務です。

- Legacy は旧 `YmmpxLib` フォルダー検出を担う
- 実際の削除はしない
- 旧構成が残っているかを知らせるだけ

## 4. 現状コードの混ざり方
### 4.1 `PackagingRules` の中で混ざっているもの
`PackagingRules` には、少なくとも次の種類の処理が同居しています。

- 検出
- 除外判定
- 検証
- 出力パス生成
- ファイル移動
- 互換性補助
  - 旧 `YmmpxLib` フォルダー検出

つまり、`PackagingRules` は「同梱対象・除外・出力」の共通ヘルパーである一方、責務の境界がやや広いです。

### 4.2 `ToolViewModel` の中で混ざっているもの
`ToolViewModel` には次の責務が集まっています。

- UI 文言の設定
- 前提プラグイン導入案内
- 除外設定画面の起動
- 出力先の決定
- 上書き確認
- 事前検証の確認ダイアログ
- 一時出力 / 最終出力の切り替え
- `Features.EntryPoint` の反射呼び出し
- 完了メッセージとエラー表示

このため、`ToolViewModel` は「操作のオーケストレーション」と「出力の具体処理の一部」を同時に持っています。

### 4.3 境界が重なっている点
次の境界が重なっています。

- `GetProjectFilePaths()` と `ExcludeRuleStore`
  - どこまでを候補として扱うか
- `ResolveExcludedFiles()` と `ValidateProjectBeforePack()`
  - 除外したものをどう数えるか
- `CreateTemporaryPackagePath()` と `ToolViewModel` の出力命名
  - どこが命名責務を持つか
- `MoveGeneratedPackage()` と `ToolViewModel` の完了フロー
  - 失敗時の巻き戻し責務がどこにあるか

## 5. 問題点
現状の問題点は次の通りです。

- 責務分離は完了している
  - 検出、除外、検証、出力補助は各クラスへ分離済み
- `PackagingRules` はオーケストレーターとして機能している
  - 各処理の呼び出し順をつなぐ役割に整理されている
- 構造改善はロジック変更なしで完了している
  - 実装の振る舞いは変えず、責務の見通しを改善した

### 5.1 図ベースで見た問題点
```text
Detection / Exclusion / Validation / Output が分離済み
↓
PackagingRules がオーケストレーターとして全体を接続
↓
YmmpxLib が実梱包を担当
```

- 処理フローは明確化されている
- 旧来の密結合は解消済み
- 各責務は独立したクラスとして扱える

現在の状態:
- Detection / Exclusion / Validation / Output が分離済み
- 処理フローが明確化されている
- ロジック変更なしで構造改善が完了している

## 6. 最小変更での改善案
この段階ではコードを変えず、将来の最小変更候補を特定するだけに留める。

最小変更として分けやすいのは次の順です。

1. `ToolViewModel` の出力名決定を「UI の責務」として明文化する
2. `PackagingRules` の検出系と出力系を docs 上で分けて整理する
3. `ResolveExcludedFiles()` と `ValidateProjectBeforePack()` を「候補列挙の利用者」として扱う
4. `CreateTemporaryPackagePath()` と `MoveGeneratedPackage()` を「出力補助」に限定して記述する
5. `FindFilePaths()` を「JSON 走査の検出補助」として位置づける

この整理で、コードを変えずに責務の読み取りを揃えられます。

## 7. 将来の理想構造
理想構造は単純に分けると次の形です。

```text
Detector → Excluder → Validator → PackExecutor(YmmpxLib) → OutputHandler
```

- Detector
  - プロジェクト JSON から素材候補を列挙する
- Excluder
  - ルールと候補を照合して、除外対象を決める
- Validator
  - 候補、除外、欠損を数える
- PackExecutor(YmmpxLib)
  - 最終的な同梱判定と梱包を行う
- OutputHandler
  - 一時ファイル生成、最終出力名決定、最終移動を行う
- UI オーケストレーション
  - `ToolViewModel` がダイアログ、状態更新、反射呼び出しを束ねる

理想的には、`PackagingRules` は「検出 / 除外 / 検証」の純粋関数寄りに寄せ、出力補助は別の小さなヘルパーに寄せたいです。

## 8. 現在の最終構造
```text
PackagingRules（オーケストレーター）
├ PackagingDetector（検出）
├ PackagingExcluder（除外）
├ PackagingValidator（検証）
├ PackagingOutputHelper（出力補助）
└ YmmpxLib（実梱包）
```

- `PackagingRules`
  - 全体の呼び出し順を管理するオーケストレーター
- `PackagingDetector`
  - 除外前の候補ファイル一覧を返す
- `PackagingExcluder`
  - 候補に除外ルールを適用する
- `PackagingValidator`
  - 候補、除外、欠損を確認する
- `PackagingOutputHelper`
  - 一時出力パス生成、出力名調整、ファイル移動を担う
- `YmmpxLib`
  - 最終的な同梱判定と実梱包を担う

`PackagingRules` はこの構造の中心であり、各責務の呼び出し順をつなぐ役割として固定する。

## 9. 実装前に確認すること
次に実装へ進む前に、最低限確認したい点は次の通りです。

- YmmpxLib 境界の将来改善
- UI 導線の整理
- ログ出力の改善
