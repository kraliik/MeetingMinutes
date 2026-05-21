# MeetingMinutes

## WHAT — Tech stack & structure

- **Language / runtime:** C# (.NET 10) + Python 3.x (ML pipeline only)
- **Framework:** ASP.NET Core (API) · WPF (Client)
- **Package manager:** dotnet CLI (NuGet) · pip (Python venv)
- **Key libraries:**
  - `OllamaSharp` — LLM communication via Ollama in both projects
  - `MaterialDesignThemes` — WPF UI components in Client
  - `NAudio` — audio recording in Client
  - `NeMo Toolkit` + `faster-whisper` + `pyannote-audio` — ASR & speaker diarization (Python layer)
  - `Scalar.AspNetCore` — OpenAPI docs on the API
- **Top-level dirs:**
  - `MeetingMinutes.Api/` — ASP.NET Core Web API; accepts audio upload, delegates to Python for transcription, exposes results
  - `MeetingMinutes.Api/Controllers/` — `TranscriptionController` (single POST endpoint, 500 MB limit)
  - `MeetingMinutes.Api/Services/` — `TranscriptionService` (spawns Python subprocess)
  - `MeetingMinutes.Client/` — WPF desktop app (Windows-only, net10.0-windows)
  - `MeetingMinutes.Client/Services/` — `LocalPythonTranscriptionService`, `TranscriptionApiService`, `SummarizationService`, `OllamaLlmService`
  - `MeetingMinutes.Client/python/` — Python venv entry point (`app.py`) + `services/` (transcription, diarization)
  - `MeetingMinutes.Client/python/Models/` — local model files (canary-1b-v2.nemo, pyannote, faster-whisper-small); **not tracked in git**

## WHY — Context & purpose

Desktop tool for recording or importing meeting audio, transcribing it (locally via NeMo canary/parakeet or faster-whisper), performing speaker diarization with pyannote, and generating structured meeting minutes via a locally-running Ollama LLM (default model: `gemma3:4b`). Designed for offline/on-premises use; all ML models run locally.

## HOW — Commands & processes

### Run API (dev)
```bash
cd MeetingMinutes.Api
dotnet run
# Default URL: http://localhost:5277
# OpenAPI / Scalar UI available at /scalar in Development
```

### Run Client (dev)
```bash
cd MeetingMinutes.Client
dotnet run
# Requires Ollama running on localhost:11434
# Requires python/ venv bootstrapped (see below)
```

### Python venv setup (one-time, Windows)
```bash
cd MeetingMinutes.Client/python
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
# Also: manually place canary-1b-v2.nemo into python/Models/
```

### Build Client for distribution (Windows self-contained exe)
```bash
cd MeetingMinutes.Client
dotnet publish -c Release -r win-x64 --self-contained true
# Then copy python/ folder next to the exe in bin/Release/net10.0-windows/win-x64/publish/
```

### Build API
```bash
cd MeetingMinutes.Api
dotnet build
```

### Tests
No test project detected in the solution.

### Lint / typecheck
```bash
dotnet build   # compiler errors serve as typecheck
```

## Routing

| Task type | Always read | Contextual |
|---|---|---|
| feature (API) | `MeetingMinutes.Api/Program.cs`, `Controllers/`, `Services/` | `appsettings.json` |
| feature (Client UI) | `MainWindow.xaml`, `MainWindow.xaml.cs`, `ViewModels/` | affected dialog in `Dialogs/` |
| feature (Client LLM/summary) | `Services/SummarizationService.cs`, `Services/OllamaLlmService.cs` | `Services/ISummarizationService.cs` |
| feature (transcription) | `Services/LocalPythonTranscriptionService.cs`, `python/app.py`, `python/services/` | `python/requirements.txt` |
| bugfix | affected service + its interface | `ViewModels/`, logs |
| chore / config | `appsettings.json` (both projects), `*.csproj` | `AppConfig.cs` |

## Conventions

- Services are interface-backed (`ITranscriptionService`, `ILlmService`, `ISummarizationService`); `ServiceFactory` wires them statically (no DI container in Client).
- The Python layer is invoked as a subprocess; stdout JSON is always the **last** JSON line printed (libraries may print noise before it).
- Client reads its API base URL from `MeetingMinutes.Client/appsettings.json` (`ApiBaseUrl`); default is `http://localhost:5277`.
- API reads `OllamaBaseUrl` from `appsettings.json`; default is `http://localhost:11434`.
- WPF project is Windows-only (`net10.0-windows`, `UseWPF=true`); do not add cross-platform targets there.
- Transcript chunking uses a 4000-token ceiling (4 chars ≈ 1 token) with incremental summarization preserving participant section across chunks.
- Supported audio formats in the API: `.wav`, `.mp3`.

## Known gotchas

- The Python venv must be at `MeetingMinutes.Client/python/.venv/Scripts/python.exe` (Windows path hardcoded in `TranscriptionService`); Linux/macOS paths will break it.
- `canary-1b-v2.nemo` must be downloaded manually from Hugging Face and placed in `python/Models/` — it is not in git and not auto-downloaded.
- When publishing the Client, the entire `python/` folder must be copied manually next to the exe; it is not bundled automatically by `dotnet publish`.
- GPU/CUDA support in Docker is not yet implemented (see `TODO.txt` for the planned `docker-compose` snippet and CUDA PyTorch base image).
- `faster-whisper-small` model files inside `python/Models/` are excluded from the build via explicit `<Compile Remove>` / `<None Remove>` entries in the csproj.
