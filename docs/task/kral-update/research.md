# Research — kral-update

## Scope

Full read-only recon of all five change areas (A–E) across the WPF Client C# codebase and the Python transcription pipeline. Covers model/settings, chat bubble UI, transcript formatting, diarization pipeline, dialog infrastructure, LLM service API, and context-limit detection.

---

## Affected files

| File | Role | Likely change |
|---|---|---|
| `MeetingMinutes.Client/Settings/UserSettings.cs:9` | `UserSettingsData` with property defaults and `DefaultSystemPrompt` const | Change `OllamaModel` default to `gemma3:12b`; rewrite `DefaultSystemPrompt` |
| `MeetingMinutes.Client/MainWindow.xaml:139–178` | `ItemsControl` DataTemplate with `TextBox` binding `ChatMessage.Content` | Replace `TextBox` with Markdown control |
| `MeetingMinutes.Client/MainWindow.xaml.cs:303–338` | `RunTranscriptionAsync` + `FormatTranscript` — assembles string from segments | Split into `FormatTranscriptForDisplay` (with timestamps) and `FormatTranscriptForLlm` (without); store segments in a field; add speaker-rename dialog trigger |
| `MeetingMinutes.Client/MainWindow.xaml.cs:352–414` | `SummarizeButton_Click` — first call triggers `SummarizationService.SummarizeAsync` | Add context-overflow estimation + toast/banner before summarization |
| `MeetingMinutes.Client/ViewModels/ChatMessage.cs` | Chat bubble model with `IsUser` / `Content` | No structural change needed; Markdown control binds to `Content` |
| `MeetingMinutes.Client/Services/SummarizationService.cs:48–69` | `ChunkTranscript` — char-count split on `\n` lines | Rewrite to split at speaker-turn boundaries with overlap; add `SummarizeAsync` Map/Reduce pass |
| `MeetingMinutes.Client/Services/ISummarizationService.cs` | `SummarizationRequest` record + interface | Possibly extend `SummarizationRequest` with `LlmTranscript` field (no-timestamp version) |
| `MeetingMinutes.Client/Services/OllamaLlmService.cs` | Wraps `OllamaApiClient.ChatAsync` + `_http` client | Add `ShowModelAsync` call for context-limit fetch; add `Format = "json"` path for Map LLM calls |
| `MeetingMinutes.Client/Services/ILlmService.cs` | `LlmMessage` record + `ILlmService` interface | Possibly add `CompleteStructuredAsync` overload for JSON output |
| `MeetingMinutes.Client/Dialogs/SettingsDialogView.xaml:27` | ComboBox hardcodes three model items | Add `gemma3:12b` as selected default (already listed) |
| `MeetingMinutes.Client/Dialogs/SettingsDialogView.xaml.cs:17` | Sets `SystemPromptBox.Text = current.SystemPrompt` | No structural change needed; gets new default from `UserSettingsData` |
| `MeetingMinutes.Client/Dialogs/` _(new file)_ | Speaker-rename dialog | New `SpeakerRenameDialogView.xaml` + `.xaml.cs` |
| `MeetingMinutes.Client/MeetingMinutes.Client.csproj` | Package references | Add Markdown library reference |
| `MeetingMinutes.Client/python/services/transcription_service.py:16,69` | `MIN_DUR = 0.3`, filter at line 69 | Lower `MIN_DUR` to 0.15; merge short segments into previous turn instead of dropping |
| `MeetingMinutes.Client/python/services/transcription_service.py:59` | `diarizer.get_speaker_turns(audio_path)` — receives raw turns | Add adjacent-speaker merging pass (gap < 1.5 s, same speaker) before `valid_turns` filter |

---

## Dependencies & callers

**`UserSettingsData.DefaultSystemPrompt`** consumed at:
- `UserSettings.cs:8` — property initializer default (only used when no `user-settings.json` exists or when json deserialization produces null)
- `SettingsDialogView.xaml.cs:16` — displays in SystemPromptBox on dialog open
- `MainWindow.xaml.cs:366,381` — `_userSettings.SystemPrompt` passed to `SummarizationRequest` and as `ChatRole.System` message

**No migration exists.** `UserSettings.Load()` at line 66–76 uses `JsonSerializer.Deserialize<UserSettingsData>`. If a `user-settings.json` already exists on disk with `OllamaModel: "gemma3:4b"`, that value is deserialized and overrides the new `"gemma3:12b"` default. There is no migration/version field.

**`FormatTranscript`** (MainWindow.xaml.cs:341) is called once in `RunTranscriptionAsync` (line 318). Segments are NOT stored in a field after this — only the formatted string lives in `TranscriptBox.Text`. For area C (speaker rename), segments must either be stored in a `_lastSegments` field or the rename must operate on the text in `TranscriptBox` by regex replacement of `SPEAKER_XX` patterns.

**`TranscriptBox.Text`** is read at `SummarizeButton_Click` line 354 and passed directly to `SummarizationRequest`. For area B's no-timestamp LLM version, a second field or transformation at call time is needed.

**`SummarizationService.ChunkTranscript`** is called only from `SummarizeAsync` (line 16). Its return value is `List<string>` — no callers outside of `SummarizationService.cs`.

**`ILlmService.CompleteAsync`** is called from:
- `SummarizationService.SummarizeAsync` (line 34) — main summarization
- `SummarizationService.ContinueAsync` (line 46) — delegated directly

**`OllamaApiClient`** instance in `OllamaLlmService` is `_client` (line 16), constructed from the existing `_http` static field. `ShowModelAsync` is available on `IOllamaApiClient` interface as `ShowModelAsync(ShowModelRequest, CancellationToken)`.

**`DialogHost`** identifier `"RootDialog"` is declared in `MainWindow.xaml:21`. `ShowSettingsDialogAsync` (line 206) uses `MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "RootDialog")`. The pattern for passing data back is `DialogHost.CloseDialogCommand.Execute(result, this)` — result is cast from the dialog's return value.

---

## Side effects & contracts

- **DB:** no
- **API contract:** no external API changes; Python `app.py` output JSON schema (`{"segments": [...]}`) is unchanged
- **Shared state:** `TranscriptBox.Text` is the only in-memory transcript — no separate `_segments` field exists today. Area C and area B both require a second representation to be stored.
- **External integrations:** Ollama on `localhost:11434` — new `ShowModelAsync` call added for area E
- **Feature flags:** none

---

## Tests

- Existing tests: none (confirmed — `AGENTS.md` states "No test project detected in the solution"; `find` confirms only `TestController.cs` in API which is a HTTP test endpoint, not a unit test)
- Coverage assessment: **none**

---

## Area-by-area technical notes

### Area A — Quick wins

**Default model migration gap:** `UserSettings.Load()` blindly deserializes existing JSON; users with existing `user-settings.json` at `%AppData%\MeetingMinutes\user-settings.json` will keep `gemma3:4b`. No migration mechanism exists.

**Chat bubble Markdown rendering:** `MainWindow.xaml:159–174` uses a `TextBox` with `IsReadOnly=True` and `Text="{Binding Content}"`. The `DataTemplate` at lines 141–177 is the exact replacement point.

**Markdown library — Markdig.Wpf vs Neo.Markdig.Xaml:**

| | Markdig.Wpf | Neo.Markdig.Xaml |
|---|---|---|
| Latest version | 0.5.0.1 | 1.0.10 |
| Published | 2021-01-15 | 2021-07-25 |
| TFM support | `net5.0-windows7.0`, `.NETCoreApp3.1`, `.NETFramework4.5.2` | `.NETCoreApp3.1`, `.NETFramework4.7` |
| net10.0-windows | NOT listed | NOT listed |

**Neither package declares `net10.0-windows` or `net6/7/8/9` TFMs.** Both will resolve via compatibility fallback. Both repos appear abandoned since 2021.

**Recommendation: Markdig.Wpf 0.5.0.1** — explicitly ships a `net5.0-windows7.0` TFM (closer match to `net10.0-windows`). Larger NuGet download base. Fallback if either fails: local FlowDocument renderer using Markdig + manual XAML generation.

**`DefaultSystemPrompt`** is a `const string` — value baked into assembly. Changing it updates default for new installs only.

### Area B — Transcript cleanup (Python)

**`MIN_DUR` at `transcription_service.py:16,69`:** current 0.3 s filter drops segments before transcription. New behavior (merge into previous) requires tracking a "current accumulator turn" per speaker.

**Adjacent-turn merging:** `diarizer.get_speaker_turns()` returns sorted list `[(start, end, speaker)]` (pyannote `itertracks` returns chronological order). Same-speaker turns can be non-adjacent in list (S0, S1, S0). Merge only consecutive same-speaker turns where `turn[i+1].start - turn[i].end < 1.5`. New helper `_merge_turns()` called before filter.

**Overlap in diarization output:** pyannote `itertracks` CAN produce overlapping segments. Current code does not handle this. After same-speaker merge, overlapping segments from different speakers remain — chunking on C# side must be aware.

**`SummarizationService.ChunkTranscript` (C#):** current splits on `\n` with no regard for speaker boundaries. Lines: `[mm:ss] SPEAKER_XX: text`. Boundary detection regex: `^\[\d{2}:\d{2}\] SPEAKER_\d{2}:`. Overlap of 1–2 turns = last 1–2 lines of chunk N also appear at start of chunk N+1.

**Two transcript representations:** `FormatTranscript` at `MainWindow.xaml.cs:341–349` builds display string. For LLM, strip `[mm:ss] ` prefix while keeping speaker labels. `SummarizationRequest` record needs second field, or transformation at call time.

### Area C — Speaker rename UI

**Dialog infrastructure:** `MaterialDesignThemes.Wpf.DialogHost` with identifier `"RootDialog"` is existing pattern. `ShowSettingsDialogAsync` (line 206) is call pattern — `DialogHost.Show(userControl, "RootDialog")` returns `object?`. New `SpeakerRenameDialogView : UserControl` follows same pattern as `SettingsDialogView`.

**Data passed to dialog:** unique speaker labels extracted from `TranscriptBox.Text` by regex `SPEAKER_\d{2}` (or from `_lastSegments`). Dialog returns `Dictionary<string, string>` mapping original label to user-entered name. `null` return = cancel/skip.

**Rename consistency problem:** today segments not kept in memory (`RunTranscriptionAsync` at line 318–329 stores only formatted string). No separate LLM transcript stored. Two options:
- **Option 1:** Store `_lastSegments` as `List<TranscriptSegment>` field and regenerate both representations from it on rename. CLEANER.
- **Option 2:** Store `_llmTranscript` as separate field alongside `TranscriptBox.Text` and do regex replacement in both strings. SIMPLER but duplicates state.

**Rename trigger point:** `RunTranscriptionAsync` line 320–329 — after `FormatTranscript` and before clearing `ChatMessages`. Must run on UI thread.

### Area D — Map-reduce summarization + action items

**`LlmMessage` / `ChatRole`:** `LlmMessage` record at `ILlmService.cs:5`. `ChatRole` from `OllamaSharp.Models.Chat`: `System`, `User`, `Assistant`. No structured output type — Map pass needs `ChatRequest.Format` field (OllamaSharp 5.4.23: accepts `"json"` string or `JsonSchema` object).

**Structured JSON output path:** `OllamaLlmService.CompleteAsync` constructs `ChatRequest` at line 25 without `Format` field. Need new overload or parameter on `ILlmService`.

**Map phase JSON schema:**
```json
{"topics": [...], "decisions": [...], "tasks": [...], "participants": [...]}
```

**Reduce phase:** collects all chunk JSONs, merges, calls LLM once with merged JSON + final Markdown output prompt. Action-items pass = third LLM call over merged participants/tasks JSON.

**Current `SummarizeAsync` is incremental accumulation** (previous summary concatenated into next prompt) — fundamentally different from Map/Reduce. Entire method needs replacement.

### Area E — Context overflow warning

**`OllamaApiClient.ShowModelAsync`** confirmed available in OllamaSharp 5.4.23. `ShowModelResponse.Info` typed as `ModelInfo`. `ModelInfo.ExtraInfo` = `Dictionary<string, object>`.

**`context_length` key in `ExtraInfo`** is architecture-prefixed (e.g. `llama.context_length`, `gemma3.context_length`). Safe approach: scan keys ending in `.context_length` or just `context_length`. Fallback to hardcoded default (8192) for unknown architectures.

**`RunningModel.ContextLength`** = separate property on running-models list response (`/api/ps`) — only when model loaded. For pre-check, `ShowModelAsync` is right path.

**Estimation:** `transcript.Length / 4` approximates tokens. Compare to `contextLimit * 0.8`. Warning threshold = 80%.

**UI warning mechanism:** no existing toast/snackbar in `MainWindow.xaml`. MaterialDesignThemes provides `Snackbar`/`SnackbarMessageQueue`. Alternative: `TextBlock`/`Border` collapsing.

---

## Risks

1. **Segments not stored in memory** — single highest-impact structural gap. Areas B, C, D all need access to segments or derived LLM string. Must be resolved before C and B can be independently implemented.

2. **`context_length` key is architecture-specific in Ollama** — `ExtraInfo` keys are `<arch>.context_length`. Safe: scan all keys for suffix match. Fallback hardcoded default (e.g. 8192) needed.

3. **No migration for `UserSettings.OllamaModel` default change** — existing users keep `gemma3:4b`. New `DefaultSystemPrompt` only applies to fresh installs unless migration logic added.

4. **Markdig.Wpf streaming flicker** — re-renders `FlowDocument` on every PropertyChanged. Token-level streaming (`reply.Content += token`) will flicker. Need buffered updates or render after streaming completes.

5. **Diarization turn overlap** — pyannote can emit overlapping segments. Merge logic must not merge turns that overlap in time.

---

## Complexity estimate

**L** — 10+ files across C# and Python, cross-cutting structural change (segments in memory, dual transcript representations, new dialog, Map/Reduce LLM refactor, new streaming context check), no test coverage, multiple new UI components.

---

## Git state

- Branch: `kral-update`
- Uncommitted changes: scout artifacts (AGENTS.md, ai.txt, docs/)
- Recent activity in affected area: 3 recent commits touch `SummarizationService.cs`, `OllamaLlmService.cs`, `MainWindow.xaml.cs`. Active churn area.

---

## Open technical questions

1. **`context_length` key in `ExtraInfo`:** Empirically architecture-prefixed (e.g. `llama.context_length`). Executor must scan keys, not use fixed name. Needs real Ollama call to confirm gemma3 prefix.

2. **Markdig streaming flicker:** Rebuilds FlowDocument on every bind update. Executor should batch (DispatcherTimer 50ms) or wait until streaming completes before Markdown render.

3. **Map phase `Format` field type:** `"json"` string vs `JsonSchema` object. Planner specifies which (decisions.md ADR-3).

4. **Speaker rename trigger timing:** Dialog before TranscriptBox populated + SummarizeButton enabled. `Dispatcher.Invoke` block at line 320 must become `await Dispatcher.InvokeAsync(...)` to allow async dialog await.

5. **`_segments` field vs. regex replacement:** Both clean — planner decides (decisions.md ADR-1).

---

## Routing update

Add `MeetingMinutes.Client/python/services/diarization_service.py` to executor read list — turn format dependency for B's merge logic. Also `MeetingMinutes.Client/ViewModels/TranscriptionSegment.cs` exists but appears unused (wrong namespace `MeetingMinutes.Client.ViewModels`); executor may delete or repurpose.
