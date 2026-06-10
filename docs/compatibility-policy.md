# Compatibility Policy

## Scope

- このリポジトリは YMM4 向けの素材パッケージング補助に限定します。
- `YmmpxLibPlugin` が提供する共有 DLL 方式を維持します。
- `YmmpxLib.dll` の再配布は行いません。

## Versioning

- `v1.0.0` は現行挙動を壊さない土台整備を優先します。
- 既存の `.ymme` インストール導線は維持します。
- 破壊的変更が必要な場合のみ次のメジャー番号を使います。

## Supported Behavior

- 旧 `YmmpxLib` フォルダー検出は警告扱いのままにします。
- 出力先同名ファイルの扱いは既存の挙動を維持します。
- `YMMResourceUnpackerApp` は関連付けと展開の補助ツールとして維持します。

## Non-Goals

- `YmmpxLib` の内製化はしません。
- YMM4 の DLL を同梱しません。
- UI の大幅な再設計は行いません。
