# kral-update

- **Type:** feature
- **Status:** done
- **Branch:** kral-update
- **Created:** 2026-05-21
- **Completed:** 2026-05-21

## Final result

- **4 challenger rounds** → Proceed verdict (Round 4: 0/0/0)
- **88 plan steps** across 12 sections (Pre-defaults → Foundation → A1 → A2 → X → B → C → D1 → D2 → D3 → E + Acknowledged risks)
- **Code-level changes:** 11 files modified, 6 new files (Dialogs/SpeakerRenameDialogView, Dialogs/MarkdigSpikeWindow, Services/MapReduceModels, Services/SummarizationFailedException, docs/task scaffolding)
- **Diff stat:** +835 / -76 across C# and Python
- **Build:** `dotnet build /p:EnableWindowsTargeting=true` → 0 errors, 0 warnings
- **Review:** approved-with-comments — 0 Blocker, 0 Major, 5 Minor (2 fixed: unused field + NRE guards; 3 deferred Windows runtime)

## Deferred to Windows runtime

- A1.8–A1.10 settings migration scenarios (legacy v0, customised prompt preserved, idempotency)
- A2.1a Markdig.Wpf spike (6 visual acceptance criteria)
- A2.6/A2.7 Markdown render + IsStreaming error path
- X.0 OllamaSharp cancellation latency (≤ 1500ms / ≤ 500ms thresholds)
- B.1–B.11 Python diarization merge + integration
- C.8–C.11 speaker rename UI flows + C1 race protection
- D2.6/D2.7 Map/Reduce live runs
- D3.7–D3.9 action items table render + diacritic quote validation
- E.6–E.9 ctx overflow warning Snackbar

These tests verify behavior at runtime; the code paths are in place.

## Request

Comprehensive readability improvements for meeting transcripts and summarization output, split into five logical areas:

A) Quick wins: change default OllamaModel to `gemma3:12b`, add Markdown renderer for chat messages (Markdig.Wpf or Neo.Markdig.Xaml), rewrite DefaultSystemPrompt to new structured format (Executive summary → Participants → Topics with sub-sections → Action items as Markdown table → Open questions).

B) Transcript cleanup (LLM input): merge adjacent diarization turns from the same speaker when gap < 1.5s, lower MIN_DUR from 0.3 to 0.15 and merge short segments into previous turn instead of dropping, align chunking boundaries in SummarizationService.ChunkTranscript to nearest speaker-turn change with 1-2 turn overlap between chunks, generate a timestamp-free transcript version for LLM input (UI continues showing timestamps).

C) Speaker rename UI: after transcription completes show a dialog listing detected SPEAKER_XX identifiers with a textbox per speaker for a real name; on confirm, replace throughout the transcript in memory and in TranscriptBox; Skip/Cancel keeps anonymous labels.

D) Map-reduce summarization + action-items extraction pass: refactor SummarizationService to a two-pass model (Map: each chunk -> structured JSON via LLM; Reduce: final LLM call over merged JSON -> Markdown output in new prompt structure), plus a separate action-items extraction pass with a dedicated prompt producing JSON `[{task, owner, due, source_quote}]` rendered as a Markdown table.

E) Context overflow warning: at summarization start estimate transcript token count (chars/4) and compare to model context limit; if near the limit show a UI warning toast/banner recommending a larger model.

## Context / Why

The current output produces hard-to-read plain-text minutes with speaker labels like SPEAKER_00, no visual hierarchy, and long monolithic LLM calls that lose coherence on long meetings. The improvements target all three pain points: input quality (transcript cleanup), LLM processing quality (map-reduce, better prompt), and UI output quality (Markdown rendering, speaker names, structured sections).

## Acceptance criteria

### A — Quick wins
- `UserSettings.OllamaModel` default value is `gemma3:12b`.
- Chat message content renders Markdown (headers, bullet lists, tables) in the WPF chat UI.
- `DefaultSystemPrompt` produces output with the sections: Executive summary (2-3 sentences), Participants, Topics (each with summary + key points + decisions sub-sections), Action items as a `| # | Task | Owner | Due | Priority |` Markdown table, Open questions.

### B — Transcript cleanup
- Adjacent diarization turns from the same speaker with a gap below 1.5 s are merged into one turn in `transcription_service.py`.
- Segments shorter than MIN_DUR (0.15 s) are merged into the preceding turn rather than dropped.
- `SummarizationService.ChunkTranscript` splits only at speaker-turn boundaries; chunks share a 1-2 turn overlap with the next chunk.
- The string passed to the LLM for summarization contains no `[mm:ss]` timestamps; the `TranscriptBox` display string is unchanged.

### C — Speaker rename UI
- A dialog appears after transcription completes, showing each unique `SPEAKER_XX` label with an editable name field.
- Confirming the dialog replaces all occurrences of each label in the in-memory transcript and in the `TranscriptBox` text.
- Pressing Skip or Cancel leaves all `SPEAKER_XX` labels unchanged.

### D — Map-reduce + action items
- `SummarizationService` sends one LLM call per chunk (Map) and one final call over the merged JSON results (Reduce); no single call processes the full transcript.
- The Map LLM call returns structured JSON containing topics, decisions, tasks, and participants.
- A separate action-items extraction pass produces JSON `[{task, owner, due, source_quote}]` and the final summary contains a rendered Markdown table from that data.

### E — Context overflow warning
- Before summarization starts, estimated token count (transcript length in chars / 4) is compared to the selected model's known context limit.
- If the estimate exceeds 80 % of the context limit, a visible UI warning (toast or banner) appears recommending a model with a larger context window.

## Routing

| Phase | Read | Skills |
|---|---|---|
| explorer | `MeetingMinutes.Client/Services/SummarizationService.cs`, `OllamaLlmService.cs`, `ISummarizationService.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `python/services/transcription_service.py`, `MeetingMinutes.Client/Models/UserSettings.cs`, `*.csproj` (Client) | Grep, Glob |
| planner | index.md, research.md | - |
| executor | `UserSettings.cs`, `SummarizationService.cs`, `OllamaLlmService.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `python/services/transcription_service.py`, new dialog XAML/CS for speaker rename | Edit, Write |
| reviewer | all changed files, plan.md | - |

_(Routing is a best-guess starting point — explorer may refine it.)_

## Files

- [research.md](research.md) — _(to be created by explorer)_
- [plan.md](plan.md) — _(to be created by planner)_
- [review.md](review.md) — _(to be created by reviewer)_

## Open questions — resolved

1. **Markdown library (area A):** Explorer evaluated both → **Markdig.Wpf 0.5.0.1** (closer TFM `net5.0-windows7.0` vs Neo's `netcoreapp3.1`). Streaming flicker risk noted — planner addresses.
2. **Ctx limit source (area E):** **Runtime fetch via `OllamaApiClient.ShowModelAsync`**, scan `ModelInfo.ExtraInfo` for keys ending `.context_length` / `context_length`. Hardcoded fallback (8192) for unknown architectures.
3. **Action items destination (area D):** **Final chat bubble only** (Markdown table inline ve shrnutí).
