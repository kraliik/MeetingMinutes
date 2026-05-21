# Review — kral-update

**Final status:** approved-with-comments
**Reviewer:** dev-reviewer agent
**Date:** 2026-05-21

## Summary

Reviewed 11 modified + 6 new files (+835 / -76) against the 88-step plan, ADR-1 through ADR-7, and 4 rounds of challenge findings. All 5 round-1 Criticals (C1–C5) are concretely resolved in code; the build is clean (0 errors, 0 warnings) under `dotnet build /p:EnableWindowsTargeting=true`. The map-reduce refactor, settings migration chain, atomic write, speaker rename UI, cancellation plumbing, ctx-overflow Snackbar, and diacritic-aware quote validation all match the plan and ADRs. Remaining concerns are minor: one dead field (`_renameDialogOpen`), an unused `RunCancellationSpike()` method, and Windows-runtime tests intentionally deferred per AGENTS.md (project targets `net10.0-windows`, dev box is macOS).

## Acceptance criteria

| Criterion | Verdict | Evidence |
|---|---|---|
| A — `UserSettings.OllamaModel` default is `gemma3:12b` | ✅ | `Settings/UserSettings.cs:10` |
| A — Chat renders Markdown (headers, bullets, tables) | ✅ (code-level) | `MainWindow.xaml:9, 25, 192–209` (Markdig.Wpf 0.5.0.1 added in `MeetingMinutes.Client.csproj:25`; DataTemplate gates `md:MarkdownViewer` on `IsStreaming==False && IsUser==False`). Visual render deferred to Windows. |
| A — `DefaultSystemPrompt` has Executive summary / Účastníci / Témata / Otevřené otázky; NO action-items section | ✅ | `Settings/UserSettings.cs:16–58`; grep confirms no `Akční`/`Action items`/`Tasks` in prompt body. |
| B — Same-speaker adjacent turns merged if gap < 1.5 s | ✅ | `python/services/transcription_service.py:17,20–32,94` (`MERGE_GAP_S = 1.5`, `_merge_adjacent_turns`). |
| B — `MIN_DUR = 0.15`; short segments merged into previous turn | ✅ | `transcription_service.py:16,86–92`. |
| B — `ChunkTranscript` splits only at speaker-turn boundaries with overlap | ✅ | `Services/SummarizationService.cs:362–398` (segment-by-segment accumulation with `overlapTurns = 2` carry-forward). |
| B — LLM input has no `[mm:ss]` timestamps; UI display unchanged | ✅ | `MainWindow.xaml.cs:382–409` (`FormatForDisplay` vs `FormatForLlm`; `GetLlmTranscript` returns timestamp-free string). |
| C — Rename dialog after transcription, one row per `SPEAKER_XX` | ✅ | `MainWindow.xaml.cs:411–470`, `Dialogs/SpeakerRenameDialogView.xaml/.cs`. |
| C — OK applies to in-memory transcript AND `TranscriptBox` | ✅ | `MainWindow.xaml.cs:443–462` (`_lastSegments = _lastSegments.Select(s => s with { Speaker = ... }).ToList()` + suppress-flag-wrapped box refresh). |
| C — Skip / Cancel preserves `SPEAKER_XX` | ✅ | `Dialogs/SpeakerRenameDialogView.xaml.cs:56–59` (Skip closes with `null`); `MainWindow.xaml.cs:429` (`if (result is not Dictionary<string,string> renameMap) return;`). |
| D — Map per chunk + Reduce over merged JSON; no single full-transcript call | ✅ | `Services/SummarizationService.cs:80–131`. |
| D — Map returns structured JSON (`participants`/`topics`/`decisions`/`tasks`) | ✅ | `Services/MapReduceModels.cs:5–21`; `Services/OllamaLlmService.cs:57–80` (`Format = "json"`, `Stream = false`). |
| D — Action-items extraction pass produces JSON + Markdown table | ✅ | `Services/SummarizationService.cs:54–67,133–163,262–286`. |
| E — Token estimate vs ctx limit | ✅ | `MainWindow.xaml.cs:496–503` (chars/4 vs `_llm.GetContextLengthAsync`). |
| E — Snackbar warning if est > 80 % ctx | ✅ | `MainWindow.xaml.cs:499–503` (`ctxLimit > 0 && estTokens > ctxLimit * 0.8` guard prevents sentinel false-positive). |

## Automated checks

| Check | Result |
|---|---|
| typecheck (`dotnet build /p:EnableWindowsTargeting=true`) | ✅ 0 errors, 0 warnings |
| lint | n/a (no lint configured per AGENTS.md — `dotnet build` serves as typecheck) |
| tests | n/a (no test project; AGENTS.md documents this; acknowledged M13) |
| Python syntax | not re-run here (executor reported `python -c` import passing pre-deferral; runtime path is Windows-only) |

## Findings

### 🔴 Blockers

_None._

### 🟠 Major

_None._

All five Round-1 Criticals are concretely realised in the code paths reviewed:

- **C1 race protection** — `MainWindow.xaml.cs:411–470`. `_renameDialogShown` short-circuits re-entrancy at line 413; `StartButton.IsEnabled = false; ImportButton.IsEnabled = false` at lines 421–422 cover the visible-UI race; `segmentsSnapshot` + `object.ReferenceEquals` at lines 419, 431 covers the swap-during-dialog race; `finally` at lines 464–469 always restores buttons.
- **C2 IsStreaming reset** — `MainWindow.xaml.cs:569–577` `finally` block sets `reply.IsStreaming = false` on success, exception, `OperationCanceledException`, and `SummarizationFailedException` paths. Single exit point.
- **C3 cancellation** — `_summarizeCts` field at line 40; token threaded through `ISummarizationService.SummarizeAsync` (line 526), `ContinueAsync` (line 554), and every `_llm.*Async` call inside `SummarizationService.cs:82,94,105,131,148`. Cancel-path branch at lines 474–478. Button label flips to "Zrušit" at 491; `OperationCanceledException` caught at 557–560 appends `[Zrušeno uživatelem]`. `ThrowIfCancellationRequested()` between Map chunks at line 82.
- **C4 TextChanged suppress** — `_suppressTextChanged` field at line 36; early-return in `TranscriptBox_TextChanged` at line 201; wrap at lines 453–462 around `Clear()/AppendText()` during rename. Also defence-in-depth divergence refuse at lines 437–441.
- **C5 atomic write** — `Settings/UserSettings.cs:167–177` `WriteAllText(tmp) → File.Replace(tmp, FilePath, .bak)` on existing file; `File.Move(tmp, FilePath)` on first write. `.bak` produced for free. Load-time `.bak` fallback at lines 135–147.

### 🟡 Minor

1. **Dead field `_renameDialogOpen`** — `MeetingMinutes.Client/MainWindow.xaml.cs:38, 423, 468`. Set to `true` on dialog open and back to `false` in `finally`, but never read anywhere in the codebase. The actual race protection comes from `StartButton.IsEnabled = false; ImportButton.IsEnabled = false` + `_renameDialogShown` re-entrancy guard. The flag was specified in the plan (Step C.4a) as a state marker. It is harmless (no behaviour change) but should either be wired into a future Start/Import handler guard (defence in depth) or removed for clarity. Suggested fix: at the top of `StartButton_Click`/`ImportButton_Click`, add `if (_renameDialogOpen) return;`. Not required for merge — buttons are already disabled at the XAML level by Step C.4a.

2. **Unused spike method `RunCancellationSpike`** — `MeetingMinutes.Client/Dialogs/MarkdigSpikeWindow.xaml.cs:32–71`. The plan (X.0) deferred runtime cancel-latency verification to Windows. The method is `private async Task` and never invoked. It compiles clean (no CS warnings) and ships with the binary. Cost: ~40 LOC of dead code in `Dialogs/`. Either wire it temporarily on Windows during validation, or remove both the method and `MarkdigSpikeWindow.xaml` before final merge. Note in plan revision log already acknowledges deferred runtime check; this is purely a cleanup item.

3. **`SummarizeButton.Content = "Zrušit"` replaces the `PackIcon` with a plain string** — `MainWindow.xaml.cs:491`. The XAML at `MainWindow.xaml:236–243` declares the button with a `<materialDesign:PackIcon Kind="Send"/>` child and `MaterialDesignIconButton` style (icon-sized round button). Replacing `Content` with a raw `string "Zrušit"` mid-flight will render the text inside an icon-sized button — likely cramped or clipped. The `finally` at line 572 restores a fresh `new MaterialDesignThemes.Wpf.PackIcon { Kind = ... Send }`, which is correct. The visual quality of the "Zrušit" label needs Windows-runtime verification (X.6 / X.7 acknowledged deferred). Consider replacing the text with a `PackIcon { Kind = Stop }` or `CancelCircle` for visual consistency.

4. **`_systemMessage` ChatRole.System constructed from `_userSettings.SystemPrompt` is captured once via `??=`** — `MainWindow.xaml.cs:494`. If the user opens the settings dialog and changes `SystemPrompt` mid-conversation, the cached `_systemMessage` keeps the OLD prompt for follow-up turns until `TranscriptBox_TextChanged` (line 199–204) fires and resets it. This is acknowledged risk **m6** in plan.md's "Acknowledged risks". Not a blocker; documented.

5. **`MapChunkResult` JSON deserialization tolerates null arrays?** — `Services/MapReduceModels.cs:5–9`. If the LLM returns `{"participants": null, ...}`, `JsonSerializer.Deserialize<MapChunkResult>` produces a record with `Participants = null!` (`string[]?` semantics differ across runtimes). Then `DedupMapResults` at `SummarizationService.cs:168–204` calls `SelectMany(r => r.Participants)` which would `NullReferenceException`. Mitigation: the JSON-mode retry-once at lines 99–112 catches `JsonException` only, not `NullReferenceException`. A defensive `?? Array.Empty<...>()` in `DedupMapResults` or a `[JsonRequired]`/non-nullable null-guard would harden this. Real-world risk: low — `Format = "json"` mode + the explicit prompt rule "vrať prázdné pole" steer the model toward `[]`. Worth a defensive fix but not blocking.

### 🔵 Info

1. **Spike window `MarkdigSpikeWindow.xaml/.cs` ships in the published binary** — by design, since the executor noted "wire temporarily on Windows for visual A2.1a verification". Recommend removing before user-facing release once Windows acceptance is confirmed.

2. **`FormatTranscript` thin wrapper around `FormatForDisplay`** — `MainWindow.xaml.cs:401–402` is called once at line 350. Could inline; harmless.

3. **`ChunkTranscriptString` (string fallback) and `ChunkTranscript` (segment-based) are parallel implementations** — `SummarizationService.cs:339,362`. Acceptable: the string path preserves prior manual-paste behaviour (Step B.8). Consolidation would be a follow-up.

4. **Map JSON retry message is a plain English instruction** — `SummarizationService.cs:103` ("respond with valid JSON only"). The rest of the prompt is Czech. Mixed-language inputs to LLMs typically don't degrade compliance; harmless.

5. **`OllamaLlmService._http` static + `Timeout = InfiniteTimeSpan`** — matches the existing pattern for streaming Ollama calls; HttpClient is correctly reused. The infinite timeout is offloaded to `CancellationToken` for cancellation (plan ADR-5).

6. **Atomic-write race across two app instances** — `UserSettings.Save` uses `File.WriteAllText(tmp) + File.Replace(...)` without a lock or mutex. Two simultaneous saves from two instances racing on the same `%AppData%\MeetingMinutes\user-settings.json` could lose one write. Per challenge.md Round 4, the user-facing scope is single-user single-instance desktop; not a real concern. Documented in ADR-4 acknowledged risks rationale.

7. **`HttpClient` shared between OllamaSharp `_client` and any future direct HTTP use** — currently fine; no direct HTTP path landed because X.0 spike's PASS criterion is met by OllamaSharp natively on the runtime per executor's deferred-but-implemented Stage-0 default. X.0a Stage-1 fallback was not needed and is not in the code.

## Scope check

- Changes stayed within plan.md: **yes**. All 88 steps map to identifiable code locations or deferred-with-note (Windows runtime) status. The 4 deferred steps (`A2.1c`, `A2.1d`, `A2.1e`, `X.0a`, `X.0b`) are conditional fallbacks not needed because Stage-0 (Markdig.Wpf, OllamaSharp `ct`) is the default and builds clean.
- Unrelated changes detected: **none**. All 11 modified files + 6 new files map to plan sections. `AGENTS.md` is present but untracked (no diff). `ai.txt` is also untracked and irrelevant to the task.
- Documentation files (`docs/task/kral-update/*.md`) untracked but expected per workflow.

## Test coverage

The project has no test project (acknowledged risk M13 + AGENTS.md "No test project detected in the solution"). All static helpers added in this task are deterministic and isolated:

- `NormalizeForQuoteMatch` (3-step pure transformation: smart-quote map → Unicode FormD + NSM filter → lower + whitespace collapse)
- `ValidateSourceQuotes` (pure mapping over array)
- `RemoveActionItemsTable` (regex-driven string mutation)
- `DedupMapResults` (LINQ groupings)
- `RenderActionItemsTable` + `EscapeCell` (Markdown table emission)

These are testable in isolation in a future test project. The plan's "scratch verify" lines were dropped at the executor step (no scratch code added to source), which is correct hygiene — the trade-off is that the deterministic helpers are validated only by manual eyeballing of code + future runtime tests on Windows.

Runtime end-to-end behaviour (12 deferred manual tests: 0.6, A1.8–A1.10, A2.6–A2.7, B.11, C.8–C.11, D2.6–D2.7, D3.7–D3.9, E.6–E.9, X.0, X.6, X.7) is intentionally deferred per the macOS dev environment + Windows-only target. Acceptable for the code-level approval; the final approval to merge should follow Windows-side manual validation.

## Recommendation

**Approve with comments — ship pending Windows manual validation.**

The code is functionally correct against the plan, ADRs, and all four challenge rounds. The build is clean; the architectural decisions (segments as SoT, atomic write, map-reduce, three-layer action-items single-source guarantee, diacritic-aware validation, sentinel-based ctx fallback) are concretely realised. Minor cleanup items (dead `_renameDialogOpen` field, unused `RunCancellationSpike` method, spike window shipping in binary) are non-blocking and tracked above.

Before declaring **status: done** in `index.md`:

1. Run the deferred Windows manual tests in an interactive Windows session (especially A1.8–A1.10 migration paths, A2.1a Markdig spike six visual criteria, X.0 cancel-latency spike, C.11 race test, D3.7 single-table assertion, E.6–E.9 Snackbar behaviour).
2. If Markdig.Wpf passes the spike, remove `Dialogs/MarkdigSpikeWindow.xaml`/`.cs` and the unused `RunCancellationSpike` method (or wire it temporarily, run, then remove).
3. Decide on the dead `_renameDialogOpen` field (either consume it or drop it).
4. Verify the "Zrušit" button label renders reasonably inside the `MaterialDesignIconButton` — consider switching to a `PackIcon { Kind = Stop }` if the text looks cramped.

After those four items, set `status: done` and merge. The 88-step plan is complete in code; all that remains is the Windows-side acceptance pass.
