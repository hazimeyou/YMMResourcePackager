# Known Limitations

- `YmmpxLib.dll` is not bundled in this repository or its release artifacts.
- `YmmpxLibPlugin` is still required at runtime for packing and unpacking flows that depend on the shared DLL.
- The legacy `YmmpxLib` folder is not deleted automatically; the user must remove it manually.
- `.ymmpx` association support only works on Windows.
- Output-name collision handling still follows the existing confirmation / suffix behavior.
- When the configured YMM4 installation path is currently locked by running applications, local `build` can emit `MSB3026` copy warnings from the `CopyToYMM4` target even though the build completes successfully.

See [docs/compatibility-policy.md](./compatibility-policy.md) for the release compatibility stance.
