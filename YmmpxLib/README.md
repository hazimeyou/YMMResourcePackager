# YmmpxLib

`YmmpxLib` は `.ymmpx` の作成・展開・プロジェクト JSON 内 `FilePath` 復元を提供する再利用向けライブラリです。

## 主な API

- `YmmpxPackageService.CreatePackageAsync(...)`
- `YmmpxPackageService.ExtractAndRestoreProject(...)`
- `YmmpxProjectJson.FindFilePaths(...)`
- `YmmpxProjectJson.ReplaceFilePaths(...)`

## 例

```csharp
var result = await YmmpxPackageService.CreatePackageAsync(
    projectFilePath: @"C:\work\sample.ymmp",
    outputPath: @"C:\work\sample.ymmpx");

var unpack = YmmpxPackageService.ExtractAndRestoreProject(
    ymmpxPath: @"C:\work\sample.ymmpx",
    extractDirectory: @"C:\work\sample");
```
