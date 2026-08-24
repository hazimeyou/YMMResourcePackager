# 展開ストレージ構成

YMMResourcePackagerは、実行時に解決したPlugin rootを基準に保存場所を決めます。YMM4の固定絶対パスやカレントディレクトリには依存しません。

```text
YMMResourcePackager/
├─ ExtractedProjects/
│  └─ <package-name>/
│     ├─ <OriginalFileName>.ymmp
│     └─ resources/
└─ ResourceCache/
```

`ExtractedProjects` はpackageごとの独立した展開先です。同名packageは既存内容を削除せず、既存のsuffixルールで別フォルダーへ展開します。Project名はCoreが返す `LoadedYmmpxProject.OriginalFileName` を使用します。

`ResourceCache` は将来のRecovery向け予約領域です。今回の実装では作成・素材保存・hash検索・再リンクを行いません。現在の単一Project構造は、将来1 packageに複数Projectを追加してもpackage単位directoryを維持できるようにしています。
