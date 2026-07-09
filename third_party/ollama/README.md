# third_party/ollama — dev staging only

This directory is used **only for local dev/CI testing** of Ollama runtime downloads. Files here are **not committed** and are **not bundled** in `UnifiedMessengerSetup.exe` as of v3.7.1.

## Populate (optional, for manual tests)

```powershell
./scripts/fetch-ollama-runtime.ps1              # both architectures
./scripts/fetch-ollama-runtime.ps1 -Architecture amd64
./scripts/fetch-ollama-runtime.ps1 -Architecture arm64
```

Pinned release: **v0.30.8** with SHA256 verification (see script for hashes).

## Production path

End users download the same pinned zip at runtime via **Settings › AI** (`OllamaRuntimeService.DownloadRuntimeAsync`), verified with the same SHA256 constants in `OllamaOptions`.
