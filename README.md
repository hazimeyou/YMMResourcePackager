# YMM4素材同梱プラグイン

YMM4素材同梱プラグインは、YukkuriMovieMaker4 のプロジェクト素材をまとめて管理・共有するためのプラグインです。

## インストール方法

1. GitHub Releases から `YMMResourcePackager.ymme` をダウンロードします。
2. ダウンロードした `.ymme` を起動してインストールします。
3. YMM4 のツールから「素材同梱プラグイン」を起動します。

## YmmpxLibPlugin が必要な場合

- 素材のパッケージ化と展開は、`YmmpxLibPlugin` が提供する共有 DLL を利用します。
- `YmmpxLibPlugin` が未導入の場合、アプリ側で導入案内を表示します。
- このリポジトリとリリース成果物には `YmmpxLib.dll` は同梱しません。

## v1.0.0 移行注意

- 旧構成の `YmmpxLib` フォルダーがプラグインルート配下に残っている場合は、削除案内が表示されます。
- 旧フォルダーは自動削除しません。削除後に YMM を再起動してください。
- 互換性と既知の制限は [docs/known-limitations.md](./docs/known-limitations.md) を参照してください。

## 主要な機能

- プロジェクト内の素材を一括でパッケージ化
- 除外設定で不要なファイルを選択的に除外
- `.ymmpx` の関連付けと展開
- 出力先同名ファイルの扱いの調整

## ドキュメント

- [CHANGELOG.md](./CHANGELOG.md)
- [docs/release-checklist.md](./docs/release-checklist.md)
- [docs/compatibility-policy.md](./docs/compatibility-policy.md)
- [docs/known-limitations.md](./docs/known-limitations.md)
- [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md)

## ライセンス

このプロジェクトは [MIT License](./LICENSE) のもとで公開されています。
