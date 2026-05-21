# Challenge — kral-update

**Round:** 2
**Reviewed plan:** docs/task/kral-update/plan.md (revised, 78 steps, 7 sections)
**Reviewer:** dev-challenger

---

## Round 1 — preserved for history

See git history of this file (round 1 had 5 Critical / 14 Major / 7 Minor, verdict: Loop back to dev-planner). Round 2 below verifies resolution of those findings and probes new risks introduced by the revision.

---

## Round 2 — verification of resolutions + new findings

### Resolved since previous round

- **C1 (race during rename dialog)** — Resolved by Step C.4a (button-disable + `_renameDialogOpen` flag + `finally` restore + `_renameDialogShown` early-return for re-entrancy) and Step C.5a (reference-equality guard via `segmentsSnapshot = _lastSegments` + post-dialog `ReferenceEquals` check + abort log). ADR-1 documents both layers. Both buttons (`StartButton` + `ImportButton`) are explicitly named. Sequence is in-order.
- **C2 (`IsStreaming` leak on exception)** — Resolved by Step A2.5 explicit `try { … } finally { reply.IsStreaming = false; }`. Plan's revision log claims propagation to D.8 / D.11 / D.12, but the renumbered plan now references D2.5 (catch path) and D3.6 (sequencing after action-items append). The `finally` is centralised in `SummarizeButton_Click` (per ADR-2 and Section X.6's outer `try { … } finally { reply.IsStreaming = false; … }`). Single guaranteed exit-point covers Map fail, Reduce stream, Action-items append, exception, and cancellation.
- **C3 (cancellation plumbing)** — Resolved by Section X (X.1–X.7). Token threaded through `ILlmService` (`CompleteAsync`, `ContinueAsync`, new `CompleteJsonAsync`, new `GetContextLengthAsync` — confirmed via X.1/X.2/E.2), `ISummarizationService` (X.3), `SummarizationService.SummarizeAsync` (X.4 + `ThrowIfCancellationRequested` between Map iterations). UI: `_summarizeCts` field (X.5), button repurpose "Shrnout" → "Zrušit" (X.6), `OperationCanceledException` catch with `[Zrušeno uživatelem]` marker (X.6), tooltip (X.7). ADR-5 documents.
- **C4 (TextChanged clobber)** — Resolved by Step 0.1 (adding `_suppressTextChanged` field), Step C.6a (suppress flag wraps `TranscriptBox.Clear()` + `AppendText()` with `try/finally`, modified `TranscriptBox_TextChanged` to early-return on flag), Step C.5b (refuse-on-divergence: compare `TranscriptBox.Text` vs `FormatForDisplay(_lastSegments)` after C.5a's reference check, abort with Snackbar). ADR-5 documents both layers.
- **C5 (atomic write)** — Resolved by Step A1.5 (`File.WriteAllText(tmp) → File.Replace(tmp, FilePath, .bak)` when main exists, `File.Move` otherwise) and A1.6 (load-time `.bak` fallback on missing/corrupt main). `File.Replace` is MoveFileEx semantics on Windows NTFS — atomic per OS conventions on the supported platform. IOException rethrown to caller (no silent swallow).
- **M1 (empty-results abort)** — Resolved by Step D2.4 sub-step 3 (`throw InvalidOperationException` if `mapResults.Count == 0`) and sub-step 4 (warning marker prepended if `mapResults.Count < chunks.Count * 0.5`). D2.5 surfaces the exception in UI.
- **M2 (source-quote validation)** — Resolved by Step D3.3 (`ValidateSourceQuotes` with case-insensitive, whitespace-normalised substring match; on miss → null SourceQuote + ⚠ prefix on Task). See round-2 finding M16 below for diacritics gap.
- **M3 (Map-phase dedup)** — Resolved by Step D2.3 (`DedupMapResults`: participants by case-insensitive trim, topics by lowercased trimmed Title + Points merge, decisions by lowercased trimmed, tasks by `(Task, Owner)` tuple). Called in D2.4 sub-step 5 before Reduce. ADR-3 documents.
- **M4 (context-limit fallback)** — Resolved by Step E.1 (sentinel `int.MinValue` for unknown, prefix-matched fallback map for known) and Step E.5 (`ctxLimit > 0` guard before Snackbar enqueue). ADR-6 documents.
- **M6 (Map participants filtering)** — Resolved by Step D2.1a (explicit MapSystemPrompt rule "vrať prázdné pole" when speakers are anonymous `SPEAKER_XX`).
- **M7 (`IsStreaming` sequencing with action-items append)** — Resolved by Step D3.6 (explicit verification that `finally` block of `SummarizeButton_Click` wraps the entire `await SummarizeAsync`, including append). The flow now is: SummarizeAsync returns the full Markdown with table already appended → `reply.Content = result` → exit `try` → `finally` flips `IsStreaming = false`. Sequencing is correct by construction.
- **M8 (`GetLlmTranscript` stale fallback)** — Resolved by Step 0.5a (divergence check inside `GetLlmTranscript`: if `_lastSegments.Count > 0 && TranscriptBox.Text != FormatForDisplay(_lastSegments)` → `_lastSegments.Clear()`, falls through to TranscriptBox.Text path).
- **M9 (chained migration scaffold)** — Resolved by Step A1.4 (`while (data.SettingsVersion < CurrentSettingsVersion) { switch ... case 0 => Migrate_v0_to_v1 ... }` with `throw InvalidOperationException` on unknown future version). ADR-4 documents.
- **M10 (Markdig fallback)** — Resolved by Step A2.1a (spike-verify resource dictionary load BEFORE proceeding; STOP and escalate if fails) and Step A2.4a (defensive code-behind handler to flip back to TextBox on render exception).
- **M12 (large-commit split)** — Resolved by splitting Section A into A1 (migration) + A2 (Markdig + render); Section D into D1 (API + DTOs) + D2 (Map/Reduce) + D3 (action-items). Each has its own commit boundary line.
- **M14 (duplicate action-items table)** — Resolved by Step D2.2 (`ReduceSystemPrompt` "CRITICAL: MUST instruct LLM to NOT emit action-items section/table") + Step D3.5 (C# appends canonical table). ADR-3 documents single-source rule. See round-2 finding M15 below on enforcement.
- **m1, m2, m7** — Resolved by Steps C.4 (`_renameDialogShown` flag), C.3 (`OriginalLabel { get; }` init-only spelled out), and the pre-flight reword (gemma3:12b required only from A1 onward).

---

## Critical

_None._

All five round-1 Criticals are resolved with cited step IDs and the step bodies are concrete enough for the executor.

---

## Major

- **M15 — Reduce LLM may still emit an action-items table despite ReduceSystemPrompt instruction; no post-process strip** — Step D2.4 sub-step 6 / D3.5
  - **Concern:** ADR-3 and D2.2 instruct the LLM to omit the action-items section, but the reduce phase is a streaming Markdown completion — there is no deterministic check that the LLM complied. Real-world prompt adherence on `gemma3:12b` against a strongly-implied default structure (the SystemPrompt the user customised, plus all Map results containing `tasks` arrays) is NOT 100%. If the model emits a `## Akční položky` heading mid-stream, D3.5's unconditional `+ "\n\n## Akční položky\n\n" + table` append produces exactly the duplicate-table bug M14 was supposed to eliminate — now hidden behind a "but the prompt said not to" assumption. No post-process step strips a stray action-items section from the reduced Markdown before append.
  - **Mitigation:** In D3.5, before appending, strip any block whose heading matches `^## Akční položky` (or variants — "Akční", "Action items") through the next `## ` heading or end-of-string. Use a deterministic C# regex pass. Document that the LLM's compliance with "omit" is best-effort.

- **M16 — Source-quote validation D3.3 ignores Czech diacritics + LLM normalisation** — Step D3.3
  - **Concern:** D3.3 normalises by `ToLowerInvariant` + whitespace collapse, but does NOT strip diacritics. LLMs frequently emit slightly normalised text when "quoting" — `"řekneme to v pátek"` from transcript might come back as `"rekneme to v patek"` (diacritic-stripped) or with subtle differences in punctuation. The validator marks both cases as ⚠ unverified even though the quote is genuinely from the transcript. Conversely, a valid quote with a smart-quote vs straight-quote mismatch (`„úkol"` vs `"úkol"`) fails the substring check. Result: high false-positive ⚠ rate, devaluing the marker. Worse: users will train to ignore the warning, defeating M2's whole purpose.
  - **Mitigation:** Two layers of normalisation: (a) strip diacritics via `String.Normalize(NormalizationForm.FormD) + IsNonSpacingMark filter` before substring match; (b) normalise punctuation (smart quotes → straight, en/em dash → hyphen). Apply identical normalisation to both transcript and quote. If still no match, fall back to a token-overlap heuristic (e.g. ≥ 70% of quote tokens appear in transcript window) before declaring ⚠.

- **M17 — Markdig spike A2.1a "STOP and consult orchestrator" lacks verification criterion + abort consequence is undefined** — Step A2.1a
  - **Concern:** A2.1a says "If this fails, STOP and consult orchestrator — likely need to escalate to a different package or roll our own renderer." There is no concrete pass/fail criterion beyond "Hello appears as an H1". What counts as failure: build error? XAML parse exception at runtime? Visible-but-wrong rendering (text appears but not as H1)? Empty render? More importantly, the plan does NOT specify what happens to the rest of Section A2 (A2.2–A2.7) and downstream sections (D2/D3 which expect Markdown-rendered action-items tables to actually render as tables) if A2.1a fails. Does the entire plan abort? Does only Section A2 abort but Foundation + B + C + D proceed (producing summaries that never render as Markdown)? Executor is left with "consult orchestrator" — a hand-wave that produces a stalled execution.
  - **Mitigation:** Spell out the verification criterion explicitly (e.g. "MarkdownViewer renders 'Hello' as `FontSize >= 24` and bold via the Markdown.xaml styles — visual inspection sufficient"). Add a contingency plan: if A2.1a fails, Section A2 is aborted, the `Markdig.Wpf` NuGet reference is reverted, the DataTemplate is left as the existing plain `TextBox`, and B/C/D/E proceed unchanged but action-items tables render as raw Markdown (functional regression noted). Document the fallback NuGet candidate (Neo.Markdig.Xaml).

- **M18 — Section X cancellation depends on `await Task` semantics that may not propagate `OperationCanceledException` from Ollama mid-stream** — Step X.4 / X.6
  - **Concern:** X.4 says "propagate `ct` to every `_llm.*` call AND `ct.ThrowIfCancellationRequested()` between chunks." But OllamaSharp's `ChatAsync` with streaming returns an `IAsyncEnumerable<ChatResponseStream>` — cancellation of an in-flight HTTP stream is implementation-dependent. If `_client.ChatAsync(request, ct)` does not internally honour the token (older OllamaSharp versions had partial support; the project pins 5.4.23), then `_summarizeCts.Cancel()` flips the source but the running Map call completes to its natural end before the next `await` yields. Worst case: cancel during a 30-second Reduce stream → user clicks "Zrušit" → button label flips but stream continues for the full 30s, content keeps appearing, then `[Zrušeno uživatelem]` appends after the (already-complete) stream. Plan has no verification that the streaming enumerator actually respects `ct`. X.6's manual test "click Zrušit mid-stream; bubble shows partial content + cancel marker" is performed by the executor and depends on observation, not test infrastructure.
  - **Mitigation:** Before claiming X.6 verified, executor must measure the time-to-stop on cancel against a long-running prompt (e.g. multi-thousand-token Reduce). If cancellation latency > 2 seconds, treat as defective and either (a) wrap the streaming enumerator in `WithCancellation(ct)` if available, (b) check `ct.IsCancellationRequested` inside the per-token foreach loop and `break`, or (c) document the latency as a known limitation in `Acknowledged risks`. The plan should pick one upfront, not leave to executor discretion.

- **M19 — D2.4 sub-step 4 partial-warning marker prepended to Markdown breaks Markdig structured render** — Step D2.4 sub-step 4
  - **Concern:** Sub-step 4 says "expose a non-fatal warning by appending a marker to the return value (e.g. `[Upozornění: zpracováno X z Y chunků]` prepended to the Markdown)." Prepending a bracketed plain-text line to a Markdown document starting with `# Executive summary` produces output like `[Upozornění: …]\n# Executive summary`. In Markdig.Wpf this renders as a paragraph followed by an H1 — visually awkward but functional. Worse: this is a low-visibility surface for an important error condition. A user who actually got half-bad data sees a small grey line above the headline. Compare to the strong-signal Snackbar pattern used everywhere else in the plan (E.5, C.5b).
  - **Mitigation:** Replace the prepended-marker mechanism with a Snackbar enqueue from the UI layer (D2.5 already has the catch site). The service can throw a non-fatal exception type or return a tuple `(string markdown, double mapSuccessRatio)`; if `ratio < 0.5`, UI enqueues a Snackbar.

## Minor

- **m8 — Pre-defaults PD.2 ships a prompt without action-items section, but section D3 won't land for many steps; intermediate states produce summaries with no action items at all** — Step PD.2 / D3
  - **Concern:** PD.2 says "do NOT include an action-items table in this default prompt anymore — D3 will render it deterministically." Between commit PD (step PD.2) and commit D3 (step D3.5), the default system prompt produces summaries with no action items section and no C#-rendered table either — area D hasn't shipped yet. If a user pulls the branch after Pre-defaults but before D3, summaries lose the action-items table entirely (regression). Plan does not flag this as a known intermediate state. Minor because the branch is unfinished work and not user-facing until merge, but worth noting for any partial-merge / cherry-pick scenarios.
  - **Mitigation:** Document in PD.2's verify line that "until D3 ships, summaries will lack an action-items section. Do not cherry-pick PD without D3." Alternatively, ship PD.2 with the action-items section still in the prompt and remove it as part of D3.

- **m9 — D2.5 catches `InvalidOperationException` from D2.4 sub-step 3 by message-typing only; collides with other InvalidOperationException sources** — Step D2.5
  - **Concern:** D2.5 says "catch `InvalidOperationException` from D2.4.3 and surface as `reply.Content = $"[Chyba: {ex.Message}]"`". `InvalidOperationException` is a base .NET type thrown by many sources (collection mutated during enumeration, task already running, etc.). Catching all of them and rendering `[Chyba: …]` will surface unrelated bugs as if they were "no chunks parsed" messages, masking real issues during development.
  - **Mitigation:** Define a dedicated exception type (e.g. `SummarizationFailedException`) for D2.4 sub-step 3 and catch only that. Or, at minimum, catch `InvalidOperationException` and re-throw if the message doesn't start with the expected prefix.

- **m10 — Step C.5b Snackbar dependency on Section E not enforced by plan ordering** — Step C.5b
  - **Concern:** C.5b's manual-edit refusal uses `WarningSnackbar.MessageQueue?.Enqueue(...)`. The note says "Snackbar comes from Section E; until then use `Debug.WriteLine`." But the plan order is Pre-defaults → Foundation → A1 → A2 → X → B → C → D1 → D2 → D3 → E. Section C lands BEFORE Section E, so Section C's commit cannot reference `WarningSnackbar` at all. Plan needs the executor to remember to use the `Debug.WriteLine` path during C, then come back during E to add the Snackbar call. Easy to miss.
  - **Mitigation:** Make the dependency explicit: C.5b's verify line should state "uses `Debug.WriteLine` for now; revisit in Section E once `WarningSnackbar` is in XAML." Or move the Snackbar element into Foundation (it's pure XAML, no logic dependency).

- **m11 — Pre-defaults PD.2 prompt-body content not specified verbatim; executor risk of divergence from M14 single-source rule** — Step PD.2
  - **Concern:** PD.2 describes the new prompt sections in prose ("Executive summary → Participants → Topics …") but does not provide the verbatim Czech text. The executor invents the wording at write time. If the wording inadvertently includes "Akční položky" or a similar table-implying section, then D2.2's `ReduceSystemPrompt` (which the user CAN customise via the settings dialog if they're brave) drifts from the canonical-table-only rule.
  - **Mitigation:** Provide the exact prompt text in PD.2 or in an inline appendix. Reference the round-1 plan's prompt body if it had a draft. At minimum, add a verification line: "confirm the new prompt does NOT contain the words 'Akční položky' or 'Action items' — D3 owns the table."

- **m12 — Step A1.7 instruction to bump `SettingsVersion` default initialiser to `CurrentSettingsVersion` makes A1.1's premise ("new-default 0") false; rereading the section is confusing** — Step A1.1 / A1.7
  - **Concern:** A1.1 establishes `SettingsVersion = 0` as the default initialiser ("so missing-field files load as `0` — existing users"). A1.7 then says to bump that initialiser to `CurrentSettingsVersion`. After A1.7 lands, A1.1's premise is overwritten in the same commit — reading the diff post-merge, the reviewer sees only `SettingsVersion = 1` and the migration logic is harder to justify. Functionally correct (JSON deserialisation of a file without the field yields the default value of `0` for a missing JSON property when using `JsonSerializer` with non-required semantics — wait, actually with default options, a missing JSON property keeps the property at its C# default initializer value, which after A1.7 would be `1`, NOT `0`). **This is a correctness issue, not just cosmetic.**
  - **Mitigation:** Verify the JSON deserialisation contract: if `JsonSerializer.Deserialize<UserSettingsData>` on a JSON without `SettingsVersion` returns an object where `SettingsVersion == 1` (post A1.7), then existing v0 users are misidentified as v1 and the migration NEVER runs for them. Either (a) keep `SettingsVersion = 0` as the initialiser and handle the "new file" path differently (e.g. set version explicitly in `Save` for new files), or (b) use `JsonRequired` attribute / custom converter to detect a missing field, or (c) write a `[JsonInclude]` private field with default 0 and a public computed property. Plan must pick one.

---

## Round 2 Verdict

- **Critical:** 0 · **Major:** 5 · **Minor:** 5
- **Plan revision required:** Yes — 5 Major findings, none acknowledged in plan.md's "Acknowledged risks" section.
- **Recommended next step:** Loop back to dev-planner

**Why loop back, not proceed:**

- **M15** is a recurrence of M14 in a different form (Reduce prompt compliance is best-effort, not guaranteed; post-process strip is missing).
- **M16** silently neuters the M2 source-quote validation for the entire Czech-language use case the project targets.
- **M17** leaves Section A2 abort behaviour undefined — executor will stall.
- **M18** depends on OllamaSharp streaming cancellation semantics that the plan has not verified.
- **M19** ships a low-visibility error surface inconsistent with the rest of the plan.
- **m12** is a correctness issue masquerading as a minor — JSON deserialisation of a missing field interacts with A1.7's initialiser bump in a way that may skip migration for existing users.

If the orchestrator decides round 3 is out of bounds, the most material risks to accept-as-known are M15 (duplicate table will reappear under prompt-non-compliance) and m12 (migration may silently skip v0 users). M16/M18 are degraded-functionality rather than data-loss. M17/M19 are executor-friction rather than user-facing.

---

## Round 4 — verification of round-3 resolutions

**Round:** 4
**Reviewed plan:** docs/task/kral-update/plan.md (round 3, 88 steps, 8 sections + Pre-defaults)
**Reviewer:** dev-challenger
**Scope:** verify M15 / M16 / M17 / M18 / M19 / m12 + new ADR-7 + Acknowledged-risks list. No new findings sought.

### Resolved since previous round

- **M15 (stray action-items table strip)** — Resolved by Step D3.4a (`RemoveActionItemsTable`) + ADR-3 update. Heading-regex matches all six variant titles (`Akční položky|Akční úkoly|Akce|Action items|Action Items|Tasks|Úkoly`) case-insensitive; finds next equal-or-higher heading via dynamic regex `^\s{0,3}#{1,level}\s+\S` and removes the byte range. Orphan-table second pass detects header rows with both task-like AND owner-like columns (diacritic-blind via `NormalizeForQuoteMatch`). Idempotent. Logs via `Debug.WriteLine` when it strips. **Two-tables case** (round-4 probe): the `while (true) { Match; if (!Success) break; }` loop in heading-pattern phase re-runs from the start of the (mutated) string after every removal, so a Reduce output with two `## Akční položky` sections is fully cleared. The orphan-table second pass iterates `Matches(...).Cast<Match>().Reverse()` so multiple orphan tables are removed back-to-front (no index drift). Order is correct.
- **M16 (diacritics + smart-quote normalisation)** — Resolved by Step D3.3 `NormalizeForQuoteMatch`. Smart-quote mapping explicit: `„`/`"`/`"`/`«`/`»` → `"`; `'`/`'`/`‚`/`‹`/`›` → `'`; en/em/minus dash → `-`. Unicode FormD + `NonSpacingMark` filter strips combining marks (Czech diacritics). Whitespace collapse + `ToLowerInvariant` + trim. Applied identically to transcript and candidate quote BEFORE substring check. Four scratch-test cases in D3.3 verify line cover: diacritic match, diacritic-stripped LLM output, smart-quote vs straight, fabricated quote. Token-overlap fallback NOT added — explicitly listed in "Out of scope" with rationale (false-negative risk on coincidental overlap). Acceptable trade-off.
- **M17 (Markdig spike pass criteria + fallback)** — Resolved by Step A2.1a (Stage 0) + A2.1c (Stage 1 Neo.Markdig.Xaml) + A2.1e (Stage 2 abort) + ADR-2. Six objective pass criteria listed (build clean, H1 ≥ FontSize 20, bullets visible, table renders as grid, bold heavier, italic slanted, no exception). Fail = ANY criterion missed. Stage 1 swap to Neo.Markdig.Xaml re-runs the same six criteria. Stage 2 = revert NuGet, leave `TextBox` DataTemplate, action-items table renders as raw Markdown pipes (acknowledged in Acknowledged risks via A2.1e's instruction). Executor stall point removed — every branch has a deterministic next step.
- **M18 (mid-stream cancel verification)** — Resolved by Step X.0 (spike measuring `Stopwatch.ElapsedMilliseconds` and last-token age) + X.0a (Stage-1 raw-HttpClient fallback with `HttpCompletionOption.ResponseHeadersRead` + line-by-line NDJSON read) + X.0b (Stage-2 acknowledged limitation) + ADR-5. Pass threshold: ≤ 1500ms elapsed AND tokens stopped within 500ms of `Cancel()`. **Duplication concern (round-4 probe):** Stage-1 raw HttpClient duplicates a thin slice of OllamaSharp (streaming `ChatAsync` only); `ShowModelAsync` and other non-streaming calls stay on OllamaSharp. ADR-5 explicitly acknowledges this trade-off ("Stage 1 raw-HttpClient fallback duplicates a thin slice of OllamaSharp; acceptable as the only path that guarantees mid-stream cancel"). Acceptable.
- **M19 (Snackbar replaces Markdown prepend for partial-warning)** — Resolved by Step D2.4 (return type changed to `Task<SummarizationResult>` where `record SummarizationResult(string Markdown, double MapSuccessRatio)`) + D2.5 (UI inspects `result.MapSuccessRatio`: `< 0.5` → `WarningSnackbar.MessageQueue?.Enqueue("Některé části přepisu se nepodařilo zpracovat (úspěšnost {ratio:P0})")`; `0.5 ≤ ratio < 1.0` → `Debug.WriteLine` informational; `1.0` → silent). Markdown body has no prepended warning marker. Wiring to `WarningSnackbar` in MainWindow.xaml is in place via Foundation step 0.7 (ADR-7). Pattern matches E.5 ctx-overflow Snackbar.
- **m12 (SettingsVersion initialiser correctness)** — Resolved by Step A1.1 (initialiser STAYS at `0` — explicit "MUST stay at `0`" wording) + Step A1.7 (replaces the round-2 default-initialiser bump with `!File.Exists(FilePath) && !File.Exists(FilePath + ".bak")` short-circuit at top of `Load()` that constructs a fresh `UserSettingsData { SettingsVersion = CurrentSettingsVersion }` and persists). ADR-4 documents the correctness rationale. A1.8 (existing v0 file without field → migrates to v1, custom prompt or legacy mapped correctly) + A1.9 (custom prompt survives) + A1.10 (already-v1 file is no-op) cover all three paths. **Race concern (round-4 probe):** the `!File.Exists` check is non-atomic relative to two concurrent app launches. The user-facing scenario is a Windows desktop single-user app (`%AppData%\MeetingMinutes\user-settings.json`); concurrent launches by the same user are not realistic. ADR-4 / A1.7 omit any file-lock or mutex. Acceptable for the user-facing scope; documenting in Acknowledged risks would be belt-and-braces but not required.

### Sanity checks on new ADR-7 + Acknowledged risks

- **ADR-7 (Snackbar centralisation)** — Step 0.7 lands the `<materialDesign:Snackbar x:Name="WarningSnackbar"/>` element in Foundation, BEFORE Sections A1 / A2 / X / B / C / D / E that consume it. Sequence is correct: Pre-defaults → Foundation (0.7) → A1 → A2 → X → B → C (C.5b consumes Snackbar) → D1 → D2 (D2.5 consumes Snackbar) → D3 → E (E.5 consumes Snackbar). Both the inline `MessageQueue="{materialDesign:MessageQueue}"` and the verbose-form fallback are spelled out, addressing m3. m10 is collapsed to "verify-only" in Section E.3.
- **Acknowledged risks list** — Final plan.md "Acknowledged risks" section enumerates M5 (Markdig stream-then-render UX cliff), M11 (Foundation ordering — partially resolved by Pre-defaults), M13 (no test project — out of scope), m3 (Snackbar markup-extension syntax — both forms in 0.7), m5 (`MIN_DUR` first-segment drop), m6 (`_systemMessage` cache post-rename), m8 round-2 (PD → D3 intermediate state). Each has a one-paragraph reason and mitigation. All Round-2 Major/Minor items that were not fully resolved are either listed here OR resolved in the revision log.

### Critical
_None._

### Major
_None._

### Minor
_None new._ Round-2 Minors are either resolved (m8, m9, m10, m11, m12) or explicitly acknowledged with rationale (m3, m5, m6, m8-round-2 carried into the acknowledged section).

## Round 4 Verdict

- **Critical:** 0 · **Major:** 0 · **Minor:** 0
- **Plan revision required:** No — all round-2 Major findings have concrete step IDs and ADR backing; m12 correctness issue fully resolved; ADR-7 centralises the Snackbar surface so forward references in C.5b / D2.5 / E.5 are valid; Acknowledged risks list is explicit and bounded.
- **Recommended next step:** Proceed to dev-executor
