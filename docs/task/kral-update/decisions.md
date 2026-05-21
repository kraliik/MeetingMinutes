# Decisions — kral-update

Architectural decisions referenced by `plan.md`. Updated in round 3 to address `challenge.md` round-2 findings (M15, M16, M17, M18, M19, m12).

---

## ADR-1: Transcript segment storage — single source of truth + thread-safety guard

- **Problem:** Area C (speaker rename) and area B (chunking by turn boundary) both need to operate on structured segments after transcription, but today the result is materialised as a single display string in `TranscriptBox.Text` and the underlying `IReadOnlyList<TranscriptSegment>` is discarded the moment `FormatTranscript` returns. **Round 2 addition:** the rename dialog runs asynchronously and the user can click Start/Import while it is open, racing the field swap (C1).
- **Options:**
  - **A.** Keep `_lastSegments: List<TranscriptSegment>` (or a richer wrapper) as a field on `MainWindow`. On every rename or any mutation, regenerate both the display string (with `[mm:ss]`) and the LLM string (timestamp-free) from the list.
  - **B.** Keep a parallel `_llmTranscript: string` and apply regex `Replace` for renames; never materialise segments after transcription.
- **Decision:** **A** — segments are the single source of truth. **Thread-safety:** disable Start/Import buttons while the rename dialog is open AND snapshot `_lastSegments` reference at dialog-open time; if the reference changed by the time OK is clicked, abort the rename apply with a debug log. Also cancel the dialog programmatically if the user finds a way to start a new transcription (defence in depth).
- **Why:**
  - Eliminates duplicate state (one list, two derived strings).
  - Trivially extends to future features (export, jump-to-time, per-segment search).
  - Avoids regex pitfalls (e.g. `SPEAKER_01` is a prefix of `SPEAKER_010` if pyannote ever scales — unlikely but free to avoid).
  - Renames mutate `Speaker` field on the matching items; both strings rebuild deterministically.
  - Reference-equality guard plus button disable closes the race (C1) without locking — UI-thread only, no `lock` needed.
- **Trade-offs:**
  - `MainWindow` carries one more piece of mutable state; the field must be cleared on `StartButton_Click` / `ImportButton_Click` to avoid stale renames being re-applied.
  - When the user manually edits `TranscriptBox` (the box is editable), segments and box can desynchronise. We accept this and additionally invalidate `_lastSegments` whenever the user edits the box manually (TextChanged-driven, guarded by a `_suppressTextChanged` flag — see ADR-5).

---

## ADR-2: Markdown rendering during streaming — defer to completion + guaranteed reset + spike verification + fallback chain

- **Problem:** `Markdig.Wpf` renders a `FlowDocument` from a Markdown string. Streaming token-by-token updates would re-parse and re-render the entire document on every `Content` `PropertyChanged`, causing visible flicker and CPU spikes on long assistant replies. The competing concerns: users want live feedback during streaming AND polished Markdown (tables, headers, bullets) once the answer is complete. **Round 2 addition:** the `IsStreaming` flag must reset on every exit path (success, exception, cancellation) — see C2. **Round 3 addition (M17):** the Markdig.Wpf spike A2.1a had no concrete pass/fail criterion and no defined fallback path; we now spell out the acceptance criteria and a deterministic three-stage fallback chain.
- **Options:**
  - **A.** Stream into the Markdown control directly; accept flicker.
  - **B.** Stream into a plain `TextBox` (current behaviour); on completion, swap the `DataTemplate` content to the Markdown control.
  - **C.** Debounce Markdown re-renders (every N ms) — extra moving parts, still flickers, partial Markdown often renders broken.
- **Decision:** **B** — stream as plain text, render Markdown on completion. **Guaranteed-reset rule:** every code path that sets `IsStreaming = true` must reset it in a `finally` block. No exceptions. Exception case still gets Markdown render of the `[Chyba: …]` text, which is harmless.
- **M17 — Spike acceptance criteria + fallback chain:**
  - **Spike test input:** `# Nadpis H1\n\n- bod jedna\n- bod dva\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\n**tučně** a *kurzíva*.`
  - **Pass criteria (all must hold):**
    1. `dotnet build` of `MeetingMinutes.Client.csproj` succeeds with zero NU* / CS* warnings introduced by `Markdig.Wpf`.
    2. Throw-away test window instantiates a `MarkdownViewer`, sets `Markdown` property to the spike input string, renders inside a visible `Window` on net10.0-windows.
    3. The H1 is visibly larger than body text (FontSize ≥ 20 per the bundled `Markdown.xaml` styles).
    4. Both bullet items are visible and indented.
    5. The 2×2 table renders as a grid (visible row/column structure), not as raw `|---|---|`.
    6. Bold and italic spans visibly differ from surrounding text.
    7. No `XamlParseException`, no resource-not-found, no `PackUriException` at runtime.
  - **Fail = any one of those criteria not met OR any exception during the test.**
  - **Fallback chain (deterministic, no orchestrator consult required for stage 1 / stage 2):**
    - **Stage 0 — primary:** `Markdig.Wpf` 0.5.0.1. Steps A2.1 → A2.1a.
    - **Stage 1 — secondary (auto on Stage 0 fail):** swap to `Neo.Markdig.Xaml` (NuGet `Neo.Markdig.Xaml` ≥ 0.2.0). Replace the namespace, replace `md:MarkdownViewer` with `neo:MarkdownXaml.SetMarkdown` attached property on a `FlowDocumentScrollViewer`, drop the `Markdown.xaml` resource dictionary include. Re-run the same spike test. Steps A2.1c → A2.1d.
    - **Stage 2 — tertiary (auto on Stage 1 fail):** abort Markdown rendering for this task; leave assistant bubbles as the existing read-only `TextBox`. Remove the NuGet reference. D2/D3 still ship — action-items table will render as raw Markdown pipes, which is a functional degradation but not a blocker. Step A2.1e. Surface a flag in `Acknowledged risks` and prompt the user via the orchestrator for whether to land the rest of the plan without rich rendering.
- **Why:**
  - Keeps the streaming UX identical to today (no regression on perceived latency).
  - Final state is the only one users read carefully; that's where polish matters.
  - Cheapest implementation: one extra `bool IsStreaming` flag on `ChatMessage` + a `DataTrigger` in the existing `DataTemplate`.
  - `finally`-block reset eliminates orphaned "still streaming" bubbles after errors (C2).
  - Explicit pass/fail criteria + automatic Stage 1 fallback removes the "consult orchestrator" stall point M17 flagged.
- **Trade-offs:**
  - Brief visual "snap" at the end of streaming when text re-flows as Markdown. Acceptable — it signals "done" to the user.
  - Two visual representations of the same content in XAML (TextBox vs Markdown viewer); we hide one with the trigger rather than swap controls, keeping XAML readable.
  - Stage 2 (no Markdown render) keeps the rest of the plan unblocked at the cost of a raw-pipe table — the orchestrator can decide whether to merge.

---

## ADR-3: Map-phase output format — JSON string mode with one retry; empty-results abort; deterministic dedup; deterministic strip of stray action-items table; diacritic-aware source-quote validation

- **Problem:** Area D's Map phase needs structured JSON from each chunk for the Reduce phase to merge. OllamaSharp's `ChatRequest.Format` accepts either `"json"` or a `JsonSchema` object. **Round 2 additions:**
  - if every chunk fails JSON parsing, Reduce must NOT proceed with `[]` (M1)
  - chunk overlap (from B.9) means adjacent Map results contain duplicated items; dedup must be deterministic in C#, not left to the LLM (M3)
  - action-items table is rendered once, by the structured extraction pass — never by the Reduce-phase Markdown (M14)
  
  **Round 3 additions:**
  - the Reduce LLM may emit an action-items table anyway despite the prompt instruction (M15) — we need a deterministic C# strip pass BEFORE appending the canonical table
  - source-quote validation must handle Czech diacritics + LLM normalisation (M16) — case-insensitive substring is insufficient
- **Options (round 1):**
  - **A.** `Format = "json"` string mode + `JsonSerializer.Deserialize` with a single retry on parse failure ("respond with valid JSON only — your previous output failed to parse").
  - **B.** `JsonSchema` object mode from day one — strict but more boilerplate.
- **Decision:** **A** — `"json"` string mode, retry once on parse failure. **Empty-results handling:** if `mapResults.Count == 0` after all retries, abort summarisation with a chat-bubble error "Nepodařilo se zpracovat žádný chunk transkripce" and do NOT invoke Reduce. If `mapResults.Count < chunks.Count * 0.5`, surface a non-fatal Snackbar warning and proceed. **Deterministic dedup (in C#, in `SummarizationService.Reduce` pre-processing):**
  - `participants`: union by case-insensitive trimmed string
  - `topics`: dedup by `topic.Title.Trim().ToLowerInvariant()` (keep first occurrence; merge `Points` arrays)
  - `decisions`: dedup by `decision.Decision.Trim().ToLowerInvariant()`
  - `tasks`: dedup by tuple `(task.Task.Trim().ToLowerInvariant(), (task.Owner ?? "").Trim().ToLowerInvariant())`

  **M15 — Deterministic strip of stray action-items table from Reduce output:**
  - After the Reduce stream completes and BEFORE appending the canonical table, run `private static string RemoveActionItemsTable(string md)` over the captured Markdown.
  - Algorithm: case-insensitive regex search for a Markdown heading line matching `^\s{0,3}#{1,4}\s+(Akční položky|Akční úkoly|Akce|Action items|Action Items|Tasks|Úkoly)\s*$` (multiline). For each match, find the byte range from the match's line start through the next heading of equal-or-higher level (`^\s{0,3}#{1,N}\s+` where N = matched-level) OR end-of-string, whichever comes first. Remove that range. Repeat until no further matches. Also strip any orphan Markdown table (`|...|...|` header line immediately followed by `|---|---|...` separator and at least one body row) whose header row contains BOTH a column header matching `^\s*(úkol|task|akce|action)\s*$` (case-insensitive, diacritic-stripped) AND a column header matching `^\s*(vlastník|owner|odpovědný|assignee)\s*$` (case-insensitive, diacritic-stripped) — strip header + separator + all consecutive body rows. Diacritic stripping uses the helper from M16 below.
  - The strip is best-effort. Log via `Debug.WriteLine($"RemoveActionItemsTable: stripped N chars")` if anything was removed (so the executor can spot prompt-non-compliance in real-world runs).
  - The strip runs unconditionally; if the LLM complied it is a no-op.

  **M16 — Diacritic-aware source-quote validation (D3.3):**
  - Implement `private static string NormalizeForQuoteMatch(string s)`:
    1. `s.Normalize(NormalizationForm.FormD)`
    2. Iterate chars, keep only those where `CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark` (this drops combining marks → strips diacritics).
    3. Map smart quotes (`„`, `"`, `'`, `'`, `'`, `'`) → straight `"` or `'` as appropriate.
    4. Map en/em dash (`–`, `—`) → hyphen `-`.
    5. `ToLowerInvariant()`.
    6. Collapse all runs of whitespace (any Unicode whitespace via `char.IsWhiteSpace`) to a single space; trim ends.
  - Apply the helper identically to BOTH the transcript and the candidate quote BEFORE the substring check.
  - If `normalizedTranscript.Contains(normalizedQuote, StringComparison.Ordinal)` → quote is verified, keep `SourceQuote` as the original (un-normalised) string.
  - If no match: return `item with { SourceQuote = null, Task = "⚠ " + item.Task }`. The token-overlap fallback heuristic the challenger suggested is intentionally NOT added — keep the strip pass deterministic; high false-positive rate is acceptable in round 3 as long as the false-negative rate (verified quotes that were actually fabricated) stays near zero.

  **Action-items table is rendered ONLY from the structured extraction pass output** — the Reduce phase prompt instructs the LLM NOT to emit an action-items table (sections it must emit: Executive summary, Participants, Topics, Open questions). Final assembly: `RemoveActionItemsTable(streamedReduceMarkdown) + "\n\n## Akční položky\n\n" + renderedTable`. This eliminates the duplicate-table problem in M14 even if the LLM ignores the prompt instruction (M15).
- **Why:**
  - Simpler to ship; we can observe real-world retry rates on the user's actual models.
  - Retry-once gives us a graceful failure mode without burying errors.
  - Deterministic C# dedup gives reproducible behaviour the reviewer can read; LLM-side dedup varies per call.
  - Deterministic C# strip closes the M15 gap regardless of LLM prompt-adherence variance.
  - Single canonical table source eliminates user confusion.
  - Diacritic-aware normalisation makes M2's `⚠` marker meaningful for Czech transcripts.
- **Trade-offs:**
  - Map step can fail twice → chunk dropped. Acceptable as long as we abort when ALL chunks fail (handled above).
  - Reduce prompt slightly diverges from the user-visible `DefaultSystemPrompt`: it must NOT emit the action-items table. We document this in `ReduceSystemPrompt` itself and on the spec for the Markdown rendered to the user (which DOES end up containing the table, just appended).
  - `RemoveActionItemsTable` regex can theoretically false-positive on a legitimate `## Úkoly` section the user wants in the summary — accepted; D3 owns "úkoly" as a concept.
  - The diacritic-strip approach loses semantic information (e.g. `více` vs `vice`); acceptable for substring containment check since we're verifying the quote came from the transcript, not preserving the original casing/diacritics in the rendered table.

---

## ADR-4: Settings migration — chained version migrations + atomic write + new-file detection

- **Problem:** Existing users have a `user-settings.json` on disk with `OllamaModel: "gemma3:4b"` and the legacy `SystemPrompt`. Area A changes both defaults. We need to upgrade existing users without trampling user customisations. **Round 2 additions:**
  - one-shot migration creates a scaling cliff when v2 is needed (M9)
  - `File.WriteAllText` is not atomic — a crash mid-write loses settings entirely (C5)

  **Round 3 addition (m12):**
  - if `SettingsVersion` defaults to `1` in the C# initialiser, then `JsonSerializer.Deserialize` on a v0 file without that field yields an object with `SettingsVersion == 1` → migration is SILENTLY SKIPPED for every existing user. This is a correctness bug masked by the initialiser. Resolution: keep the C# default at `0` and detect "new install" separately via `File.Exists`.
- **Options:**
  - **A.** Force-overwrite both fields with new defaults on load → trampling user choices.
  - **B.** Leave existing files untouched → new structured output never reaches existing users.
  - **C.** Add `SettingsVersion: int` field; on `Load`, run a migration chain via `while (data.SettingsVersion < CurrentVersion)`; each step is its own function (`Migrate_v0_to_v1`, future `Migrate_v1_to_v2` …).
- **Decision:** **C** — chained migrations + atomic write via temp-file + `File.Replace` + explicit new-install short-circuit.
- **m12 — New-install detection (correctness fix):**
  - `UserSettingsData.SettingsVersion` C# default initialiser STAYS at `0`. Any deserialised JSON without the field will yield `SettingsVersion == 0` → migration runs as intended for existing v0 users.
  - `UserSettings.Load()` detects "new install" BEFORE deserialising: `if (!File.Exists(FilePath) && !File.Exists(FilePath + ".bak")) { var fresh = new UserSettingsData { SettingsVersion = CurrentSettingsVersion }; Save(fresh); return fresh; }`. New users skip migration entirely and persist at the current version.
  - All other paths fall through to deserialise → run the migration chain → save once if anything migrated.
  - Step A1.7 (which previously bumped the default initialiser to `1`) is REPLACED with a step that adds the `!File.Exists` short-circuit. The initialiser stays at `0`.
- **Migration chain pattern:**
  ```csharp
  const int CurrentVersion = 1;
  while (data.SettingsVersion < CurrentVersion)
  {
      data = data.SettingsVersion switch
      {
          0 => Migrate_v0_to_v1(data),
          // future: 1 => Migrate_v1_to_v2(data),
          _ => throw new InvalidOperationException(
                   $"Unknown settings version {data.SettingsVersion}")
      };
  }
  ```
  Each `Migrate_vN_to_vN+1` is a pure function: take old data, return new data with `SettingsVersion = N+1`. Save runs once at the end (if any migration ran). Throwing on unknown version is intentional — better to surface a corrupted/forward-version file than silently mangle it.
- **Atomic write:** `UserSettings.Save(data)` is changed to:
  1. Serialize JSON to a `string json`.
  2. Write to `FilePath + ".tmp"` via `File.WriteAllText`.
  3. If `File.Exists(FilePath)`: call `File.Replace(tmpPath, FilePath, FilePath + ".bak")` (atomic on Windows NTFS; produces a `.bak` of the previous contents as a free side-effect).
  4. Else: `File.Move(tmpPath, FilePath)`.
  5. Any exception is rethrown to the caller; `UserSettings.Load` catches and falls back to `new UserSettingsData()` only when the file is unreadable, not when the migration's Save threw.
- **v0→v1 migration body:** if `SystemPrompt == LegacyDefaultSystemPromptV0` → `SystemPrompt = DefaultSystemPrompt` (new structured). Else keep custom. `OllamaModel` untouched (different VRAM / speed — user choice). Bump `SettingsVersion` to 1.
- **Why:**
  - The chain shape is in place now → adding v2 is one function + a `case`, no risk of "skip v0→v1" bug.
  - Atomic write via `File.Replace` is the WinAPI MoveFileEx semantics; ships a `.bak` for free on every save.
  - Same load-time fallback if the `.bak` exists and the main file is missing/empty (defence in depth — see plan step A1.6).
  - Keeping the C# initialiser at `0` is the only correct value for "missing field in JSON" semantics; the new-install path is handled by a file-existence check, not by mutating the C# default.
- **Trade-offs:**
  - One extra disk file (`user-settings.json.bak`); acceptable, < 5 KB.
  - `File.Replace` requires both files on the same volume; standard for `%AppData%`.
  - New users incur one extra disk write on first launch (the `Save(fresh)` call); negligible.

---

## ADR-5: Cancellation propagation through map-reduce + UI rename protection + verified mid-stream cancellation

- **Problem:** Map-reduce + action-items is up to N+2 sequential LLM calls on `gemma3:12b`. A user who realises mid-flow they want to cancel (wrong model, transcript too long, system slow) currently has no escape — only killing the process. Separately, `TranscriptBox.TextChanged` (lines 188-192 of MainWindow.xaml.cs) fires on every programmatic mutation, including the rename-driven `Clear()`/`AppendText()` round-trip from C.6, which clobbers `_systemMessage` and the button-enabled state. (Findings C3 + C4.)

  **Round 3 addition (M18):** OllamaSharp's `_client.ChatAsync(request, ct)` may or may not propagate the token to the underlying HTTP stream; if it does not, cancellation is observed only between awaits (i.e. between chunks), not mid-stream. The plan now includes a pre-implementation spike that measures cancel latency against a long Reduce stream; if the OllamaSharp path fails, we fall back to a raw `HttpClient` integration with explicit `HttpCompletionOption.ResponseHeadersRead` so `ct` reaches the socket read loop.
- **Options for cancellation:**
  - **A.** Add a `CancellationTokenSource` field on `MainWindow`, repurpose `SummarizeButton` to "Zrušit" while streaming (toggle by `IsStreaming`), thread the token from the click handler through `ISummarizationService.SummarizeAsync` and into every `ILlmService.Complete*Async` call.
  - **B.** Leave as-is.
- **Decision (cancellation):** **A** — `_summarizeCts` field, button label flip while running, token plumbed end-to-end. On cancel: `_summarizeCts.Cancel()`, `reply.Content += "\n\n[Zrušeno uživatelem]"`, `reply.IsStreaming = false` (via the global finally-rule from ADR-2).

  **M18 — Cancellation verification spike + fallback:**
  - **Spike (Step X.0):** before doing any of X.1–X.7, write a 30-line throw-away console block inside `MainWindow.xaml.cs` (or a temporary debug command) that:
    1. Builds a `ChatRequest` with a deliberately long system prompt + a user message asking the model to produce a 2000-token response.
    2. Starts `Task.Run(async () => { await foreach (var c in _client.ChatAsync(req, cts.Token)) { Debug.WriteLine(c.Message?.Content); } })`.
    3. `await Task.Delay(500); cts.Cancel(); var sw = Stopwatch.StartNew(); try { await task; } catch (OperationCanceledException) { } sw.Stop();`
    4. Log `sw.ElapsedMilliseconds`.
  - **Pass:** elapsed ≤ 1500ms and the stream actually stopped emitting tokens before the timer (visual inspection via `Debug.WriteLine`). → Proceed to X.1–X.7 with OllamaSharp as-is.
  - **Fail:** elapsed > 1500ms OR tokens continue past `Cancel()` for > 500ms. → Switch to **Stage 1 fallback (Step X.0a):** replace the OllamaSharp `ChatAsync` call inside `OllamaLlmService` with a raw `HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)` that posts to `http://localhost:11434/api/chat` with `{"model":..., "messages":[...], "stream":true}` and reads the response body as NDJSON line-by-line via `await foreach (var line in ReadLinesAsync(stream, ct))`. The `ct` reaches the socket read on each line; mid-stream cancel works deterministically.
  - **Stage 2 fallback (Step X.0b, only if Stage 1 also fails for some other reason):** document mid-stream cancel as a known limitation in `Acknowledged risks`; keep Section X otherwise — cancel between Map iterations still works, which is the most common case.
- **Options for TextChanged protection:**
  - **A.** `private bool _suppressTextChanged` flag; rename code sets to true before `Clear()`/`AppendText()`, resets after. Handler short-circuits on true.
  - **B.** Unsubscribe / resubscribe handler around the mutation.
  - **C.** Refuse to apply rename if `TranscriptBox.Text` differs from `FormatForDisplay(_lastSegments)` (user has manual edits pending).
- **Decision (TextChanged protection):** **A** (flag) + **C** (refuse-on-divergence as a secondary guard). The flag handles the common case (no manual edits). The divergence check handles the rare case where the user did edit between transcription end and dialog OK — we abort with a Snackbar "Manuální úpravy v boxu — přejmenování zrušeno" so the user is never silently overwritten.
- **Why:**
  - `CancellationToken` is the .NET-idiomatic plumbing; `OllamaApiClient` already accepts it on `ChatAsync` / `ShowModelAsync`, so the work is parameter-threading, not new control-flow.
  - `_suppressTextChanged` is a one-field, two-line change; subscribing/unsubscribing is more code and event-leak risk.
  - Divergence-refusal protects user data; in practice it almost never fires because the rename dialog is shown immediately after transcription.
  - The pre-implementation spike X.0 catches the "OllamaSharp doesn't propagate ct" failure mode upfront instead of leaving it for the executor to discover during X.6 manual testing.
- **Trade-offs:**
  - The Cancel UX consumes the "Zrušit" semantic during streaming; the button does not re-enable until streaming truly stops (Ollama may take a beat to respond to cancellation).
  - Refuse-on-divergence is a soft block — user re-runs by re-importing or re-recording.
  - Stage 1 raw-HttpClient fallback duplicates a thin slice of OllamaSharp; acceptable as the only path that guarantees mid-stream cancel.

---

## ADR-6: Context-limit lookup — typed fallback map, no false-positive warnings

- **Problem:** Plan E.1's fallback to `8192` triggers false-positive Snackbar warnings on transcripts > ~6500 chars when the user is already on the largest sensible local model (e.g. `gemma3:12b`, advertised 128k). (Finding M4.)
- **Options:**
  - **A.** Hardcoded model→context map as a fallback layer between the live `ShowModelAsync` lookup and the 8192 last-resort.
  - **B.** Silent degrade — never warn when `ctxLimit` is the fallback value.
  - **C.** Keep 8192, accept noise.
- **Decision:** **A + B** combined. Lookup order in `OllamaLlmService.GetContextLengthAsync`:
  1. Call `ShowModelAsync`; scan `ModelInfo.ExtraInfo` for keys ending in `.context_length` or equalling `context_length`. If found and parseable, return.
  2. Else, consult a small static map: `gemma3:* → 131072`, `qwen2.5:* → 32768`, `llama3.1:* → 131072`, `mistral:* → 32768`, `phi3:* → 4096`, `phi3.5:* → 131072`. Prefix match (`model.StartsWith(prefix, OrdinalIgnoreCase)`). If found, return.
  3. Else return a sentinel `int.MinValue` (meaning "unknown"). Caller (`SummarizeButton_Click`) treats sentinel as "do not warn"; log to `Debug.WriteLine` for diagnostics.
- **Why:**
  - Two-layer fallback covers the common-models case (which is everyone, given Ollama's catalogue is small).
  - Sentinel-instead-of-8192 stops the false-positive Snackbar without silently lying about context size to other parts of the app.
  - Map lives in a single static array, easy to extend.
- **Trade-offs:**
  - Map will go stale as Ollama adds models; acceptable maintenance cost (one line per model).
  - "Unknown model" users get no warning at all — acceptable; better than crying wolf.

---

## ADR-7: Centralised Snackbar surface — Foundation owns the XAML element (round 3, M19)

- **Problem:** Multiple plan steps (C.5b, D2.4 partial-warning, E.5 ctx-overflow) need to enqueue user-visible warnings. Round-2 plan landed the `<materialDesign:Snackbar x:Name="WarningSnackbar"/>` element in Section E (last). Sections C and D had to fall back to `Debug.WriteLine` until E shipped, OR carry a forward reference to a XAML element that did not yet exist (m10). M19 specifically called out that D2.4's "prepend marker to Markdown" workaround for the partial-warning is a low-visibility error surface compared to the Snackbar pattern used elsewhere.
- **Options:**
  - **A.** Move the `<Snackbar>` element into the Pre-defaults or Foundation section so every later section can call `WarningSnackbar.MessageQueue?.Enqueue(...)` directly.
  - **B.** Keep the element in Section E; later sections continue to use `Debug.WriteLine` placeholders and the executor remembers to swap them in during Section E.
  - **C.** Run two passes: ship the element with Foundation, ship the styling/positioning with Section E.
- **Decision:** **A** — move the `<materialDesign:Snackbar x:Name="WarningSnackbar" MessageQueue="{materialDesign:MessageQueue}"/>` XAML element into a new step in **Section 0 (Foundation)** as step **0.7**. The Snackbar is pure presentational XAML with no service dependency; it is safe to land before any feature that uses it. Section E.3 is then a no-op (already done in Foundation) and only the consumer side (E.5) remains in Section E.
- **Why:**
  - Removes the "use `Debug.WriteLine` for now, switch later" footgun from C.5b, D2.4 and m10.
  - D2.4's partial-warning surface (M19) can now be a Snackbar enqueue from the UI catch site, identical to the ctx-overflow warning, without ordering gymnastics.
  - No code dependency on Section E; the Snackbar is just a passive XAML element until something Enqueues on it.
- **Trade-offs:**
  - Section E becomes slightly thinner (one less XAML step). Acceptable.
  - The Snackbar appears in the UI from Pre-defaults onward but is inert until consumers wire up. Visually a no-op since it has no queued messages.

---
