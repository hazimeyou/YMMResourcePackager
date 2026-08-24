# YMMPX v2 移行方針

YMMResourcePackager の通常経路は `YmmpxLibV2` を型安全に参照します。新規パッケージは `YmmpxV2Writer` により YMMPX Format 2.0 として作成し、除外素材、プロジェクトUI設定、進捗は `YmmpxV2WriteOptions` を通じて渡します。

展開は必ず `YmmpxFormatDetector` から始めます。v1形式は `LegacyV1Reader`、Format 2.0は `YmmpxV2Reader` を選び、共通の `YmmpxProjectReferenceResolver` と `YmmpxPackageExtractor` を使います。Consumer側はZIP、manifest、links、FilePath、連番素材を実装しません。

## runtime と package形式

- v1 package形式: 古い `.ymmpx`。YmmpxLibV2だけで読めるため、旧runtimeは不要です。
- v1 runtime: `YmmpxLib` / `YmmpxLibPlugin`。通常経路では使用しません。旧runtime専用機能を将来維持する場合だけ、独立したLegacy互換層に閉じ込めます。
- v2 runtime: `YmmpxLibV2` / `YmmpxLibV2Plugin`。正式な依存です。
- v2 package形式: YMMPX Format 2.0。

v2 APIへのReflectionは禁止です。旧v1 runtime APIのReflectionが必要になった場合も、Legacy互換層以外には配置しません。この移行時点の通常経路には旧v1 runtime呼出がないため、Legacy層は作成していません。将来v1 runtimeを削除しても、v2 Writer、Reader、Detector、Resolver、Extractor、通常UIを変更する必要はありません。
