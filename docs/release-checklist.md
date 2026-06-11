# Release Checklist

## Validation Results

- [PASS] `dotnet restore .\YMMResourceUnpackerApp.sln`
- [PASS] `dotnet build .\YMMResourceUnpackerApp.sln -c Release`
- [PASS] `dotnet test .\YMMResourceUnpackerApp.sln -c Release`
- [PASS] 27 tests passed
- [PASS] GitHub Actions are pinned to commit SHAs
- [PASS] YMM4 build libraries are pinned to `v4.53.0.2` and SHA-256 verified
- [PASS] `release.yml` keeps `draft: true`
- [PASS] `release.yml` sets `prerelease: false`
- [PASS] `release.yml` artifact name is `YMMResourcePackager.ymme`
- [PASS] `release.yml` copies `THIRD_PARTY_NOTICES.md` into the release package
- [PASS] Local release package contents were enumerated
- [PASS] `YmmpxLib.dll` is not included in the release package
- [PASS] `YmmpxLib.deps.json` is not included in the release package
- [PASS] `LICENSE` is included in the release package
- [PASS] `readme.txt` is included in the release package
- [PASS] `THIRD_PARTY_NOTICES.md` is included in the release package

## Release Package Contents

Validated archive contents for `YMMResourcePackager.ymme`:

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

## YMM4 実機確認項目

These require manual verification inside a running YMM4 environment. They are intentionally skipped for the current code-only release review and still need to be executed on actual YMM4 before shipping.

- [SKIPPED] `.ymme` のインストールが `YMMResourcePackager.ymme` で問題なく進むこと
- [SKIPPED] `.ymme` インストール後にプラグインが起動できること
- [SKIPPED] `YmmpxLibPlugin` 未導入時に導入案内が出ること
- [SKIPPED] `YmmpxLib` 旧フォルダー検出の警告が表示されること
- [SKIPPED] パッケージ化と展開が既存挙動を壊さずに動くこと
- [SKIPPED] `.ymmpx` 関連付けが `YMMResourceUnpackerApp` と連動すること

## Notes

- Local build/test succeeded with warnings from `CopyToYMM4` because the configured YMM4 installation path was in use by running desktop applications.
- Those warnings did not block `build` or `test`.
