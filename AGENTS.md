# AGENTS.md

Guidance for AI agents working in this repository.

## Cursor Cloud specific instructions

### Platform reality (read first)

Unified Messenger is a **native WinUI 3** desktop app (`net8.0-windows10.0.19041.0`). Full **build, test, and run** require **Windows** with the .NET 8 SDK and WebView2 Runtime. Cloud Agent VMs are **Linux**; they cannot execute `dotnet run`, the WinUI XAML compiler (`XamlCompiler.exe`), or the GUI.

| Task | Linux cloud VM | Windows (local / `windows-latest` CI) |
|------|----------------|----------------------------------------|
| `dotnet restore` | Yes, with `-p:EnableWindowsTargeting=true` | Yes |
| `dotnet build` / `dotnet test` | **No** (XAML compiler is Windows-only) | Yes |
| `dotnet run` (app) | **No** | Yes |
| Optional: Inno Setup installers | **No** | Yes |

Treat **GitHub Actions** (`.github/workflows/build.yml`) as the authoritative automated gate when developing from Linux: the `verify` job runs restore → build → **501** unit tests on `windows-latest`.

### Services

No background daemons are required for build or unit tests. Optional at runtime on Windows:

- **WebView2 Runtime** — embedded messaging UIs
- **Ollama** (`http://127.0.0.1:11434/`) — only if the user enables **Settings → Local AI** (tests mock HTTP)

### Standard commands (Windows)

From repo root, matching CI:

```powershell
dotnet restore UnifiedMessenger.sln
dotnet build UnifiedMessenger.sln --configuration Release -p:Platform=x64
dotnet test UnifiedMessenger.sln --configuration Release --no-build -p:Platform=x64
```

Run the app from `UnifiedMessenger/`:

```powershell
dotnet run -c Release -p:Platform=x64
```

See [README.md](README.md) for publish and Inno Setup installer steps.

### Linux cloud VM (limited)

After the VM update script runs, `dotnet` is on `PATH` via `$HOME/.dotnet` (also added to `~/.bashrc` during initial setup).

```bash
dotnet restore UnifiedMessenger.sln -p:EnableWindowsTargeting=true
```

Do **not** expect `dotnet build` or `dotnet test` to succeed on Linux; use `gh run list --workflow=build.yml` to inspect CI, or develop on a Windows machine / VS 2022.

Cross-platform smoke checks that *do* work on Linux: `node --check` on files under `UnifiedMessenger/Assets/Scripts/`.

### Lint / format

There is no repo-enforced `dotnet format` or StyleCop step in CI. Compiler warnings and nullable reference types are the main static checks. On Windows, optional: `dotnet format UnifiedMessenger.sln`.

### Gotchas

- Always pass **`-p:Platform=x64`** (or `ARM64`) for solution build/test; the solution is platform-aware.
- **ReadyToRun** is intentionally disabled for publish (breaks self-contained WinUI).
- Version strings must stay in sync across `UnifiedMessenger.csproj`, `app.manifest`, and `installer-shared.iss` before releases (see README).
- Pre-commit / husky hooks are **not** configured in this repo.
