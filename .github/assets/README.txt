YMM4素材同梱プラグイン

YMM4素材同梱プラグインは、YukkuriMovieMaker4 のプロジェクト素材をまとめて管理・共有するためのプラグインです。

インストール方法

1. GitHub Releases から `YMMResourcePackager.ymme` をダウンロードします。
2. ダウンロードした `.ymme` を起動してインストールします。
3. YMM4 のツールから「素材同梱プラグイン」を起動します。

YmmpxLibPlugin が必要な場合

・素材のパッケージ化と展開は、`YmmpxLibPlugin` が提供する共有 DLL を利用します。
・`YmmpxLibPlugin` が未導入の場合、アプリ側で導入案内を表示します。
・このリポジトリとリリース成果物には `YmmpxLib.dll` は同梱しません。

v1.0.0 移行注意

・旧構成の `YmmpxLib` フォルダーがプラグインルート配下に残っている場合は、削除案内が表示されます。
・旧フォルダーは自動削除しません。削除後に YMM を再起動してください。

ライセンスと通知

・このプロジェクトは MIT License のもとで公開されています。
・第三者表記は THIRD_PARTY_NOTICES.md を参照してください。
