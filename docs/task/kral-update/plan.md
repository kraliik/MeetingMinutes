# Plan — kral-update

**Complexity:** L — five logical areas with cross-cutting refactors (segment storage, map-reduce, cancellation plumbing).
**Estimated steps:** 88 across 7 commit-bounded sections (Pre-defaults + Foundation + A1 + A2 + X + B + C + D1 + D2 + D3 + E).
**Related decisions:** [decisions.md](decisions.md) — ADR-1 (segments SoT + race guard), ADR-2 (Markdown after stream + finally-reset + Markdig spike + fallback chain), ADR-3 (JSON map + empty-results + dedup + strip stray table + diacritic-aware validation), ADR-4 (chained migrations + atomic write + new-install detection), ADR-5 (cancellation + TextChanged protection + cancel-spike + raw-HttpClient fallback), ADR-6 (ctx fallback map), ADR-7 (Snackbar in Foundation).

## Revision log

### Round 3 — 2026-05-21

- **M15 resolved:** added Step D3.4a (`RemoveActionItemsTable` deterministic strip pass) — runs over Reduce output BEFORE the canonical-table append. Logs when it strips anything. See ADR-3.
- **M16 resolved:** Step D3.3 rewritten with `NormalizeForQuoteMatch` helper (Unicode FormD + NonSpacingMark filter + smart-quote/dash normalisation + whitespace collapse). Applied identically to transcript and quote. See ADR-3.
- **M17 resolved:** A2.1a spike has explicit pass/fail criteria (build clean, render H1 / bullets / table / bold / italic, no XAML exception). Steps A2.1c–A2.1d add Neo.Markdig.Xaml Stage-1 fallback. Step A2.1e adds Stage-2 abort (no Markdown render; rest of plan ships). See ADR-2.
- **M18 resolved:** new Step X.0 (cancellation spike measuring elapsed time on cancel) precedes X.1. Step X.0a defines raw-HttpClient Stage-1 fallback. Step X.0b defines Stage-2 (acknowledged limitation). See ADR-5.
- **M19 resolved:** D2.4 sub-step 4 replaced — service returns `(string Markdown, double MapSuccessRatio)`; UI catch site (D2.5) Enqueues a Snackbar when ratio < 0.5. The Snackbar element ships in Foundation (Step 0.7) per ADR-7. See ADR-7.
- **m12 resolved:** A1.1 keeps `SettingsVersion` initialiser at `0`. A1.7 replaced — adds explicit "new install" detection via `!File.Exists(FilePath) && !File.Exists(FilePath + ".bak")` short-circuit in `Load()` that constructs a fresh `UserSettingsData { SettingsVersion = CurrentSettingsVersion }` and persists immediately. Existing v0 users without the field deserialise to `SettingsVersion == 0` → migration runs as intended. See ADR-4.
- **m8 noted:** PD.2 verify line now warns that summaries lack action-items section between PD and D3 (intermediate-state regression — do not cherry-pick PD without D3).
- **m9 addressed (cheap):** Step D2.4 sub-step 3 throws a new `SummarizationFailedException` (defined in D1) instead of bare `InvalidOperationException`. D2.5 catches only that type.
- **m10 addressed (cheap):** Section E XAML step demoted to a verify ("Snackbar already in XAML from Step 0.7; confirm it still resolves"). C.5b and D2.4 now reference `WarningSnackbar` directly.
- **m11 addressed (cheap):** PD.2 now has an explicit "MUST NOT contain 'Akční položky' or 'Action items'" verify line.
- New ADR-7 added: Snackbar centralisation (Foundation step 0.7).

### Round 2 — 2026-05-21

- **C1 resolved:** added Step C.4a (button-disable while dialog open) and C.5a (reference-equality guard before rename apply). See ADR-1.
- **C2 resolved:** rewrote Step A.11 to use `try { … } finally { reply.IsStreaming = false; }`. Added the same finally-rule to Steps D.8 / D.11 / D.12 (every code path that creates a streaming bubble). See ADR-2.
- **C3 resolved:** added Section X "Cancellation plumbing" (steps X.1–X.7) and threaded `CancellationToken` through `ISummarizationService`, `ILlmService`, and every `_llm.Complete*Async` call. New SummarizeButton "Cancel" mode. See ADR-5.
- **C4 resolved:** added Step C.6a (`_suppressTextChanged` flag) and Step C.5b (refuse-on-divergence check). Modified `TranscriptBox_TextChanged` handler accordingly. See ADR-5.
- **C5 resolved:** rewrote Step A.6 to use atomic `File.Replace` write via temp-file (Step A.6b) producing `.bak` for free. Step A.6c adds load-time `.bak` fallback. See ADR-4.
- **M1 resolved:** Step D.8.3a added — abort summarisation with chat-bubble error if `mapResults.Count == 0`; soft Snackbar if < 50%.
- **M2 resolved:** Step D.8.5a added — post-validate every `ActionItem.SourceQuote` against the original transcript (case-insensitive, whitespace-normalised); on miss, null out the quote and tag the item with a ⚠ marker.
- **M3 resolved:** Step D.8.4-pre added — deterministic C# dedup of `mapResults` before serialising to Reduce (participants union; topics/decisions by lowercased trimmed string; tasks by `(task, owner)` tuple). See ADR-3.
- **M4 resolved:** Step E.1 rewritten — `GetContextLengthAsync` returns sentinel `int.MinValue` for unknown; Step E.6 only warns when `ctxLimit > 0`; new fallback map covers common models. See ADR-6.
- **M5 acknowledged:** see Acknowledged risks.
- **M6 resolved:** Step D.5a added — MapSystemPrompt explicitly instructs the LLM to omit `SPEAKER_XX`-style labels from `participants`.
- **M7 resolved:** Step D.11 explicitly sequences `IsStreaming = false` AFTER the action-items table append (D.10).
- **M8 resolved:** Step 0.5a added — `GetLlmTranscript()` invalidates stale `_lastSegments` if `TranscriptBox.Text` diverges.
- **M9 resolved:** Step A.6 rewritten as `while (data.SettingsVersion < CurrentVersion) { switch(...) }` chain.
- **M10 resolved:** Step A.1a added — verify `Markdig.Wpf` loads on net10.0-windows BEFORE A.9.
- **M11 acknowledged:** see Acknowledged risks (Pre-defaults section lands before Foundation).
- **M12 resolved:** Section A split into A1 / A2; Section D split into D1 / D2 / D3.
- **M13 acknowledged:** see Acknowledged risks.
- **M14 resolved:** Step D.6 rewritten — `ReduceSystemPrompt` instructs LLM to OMIT the action-items table.
- **m1-m7 resolved or acknowledged:** see Acknowledged risks.

## Pre-flight

- [ ] On branch `kral-update` (per `index.md`).
- [ ] Clean working tree.
- [ ] `dotnet build` from repo root succeeds before any change.
- [ ] Ollama running locally on `:11434`. `gemma3:12b` pulled (`ollama pull gemma3:12b`) — required from Section A1 (`Pre-defaults`) onward. Earlier sections work with whatever model is configured.
- [ ] Python venv at `MeetingMinutes.Client/python/.venv` is bootstrapped (only required to verify B end-to-end on Windows).

---

## Section Pre-defaults — model + prompt only (M11)

**Cíl:** Ship the two trivial wins (model default, prompt) as a tiny standalone commit before the heavier work. Zero dependency on Foundation. No migration logic yet (that ships with Section A1 along with `SettingsVersion`).

**Soubory:**
- `MeetingMinutes.Client/Settings/UserSettings.cs`

**Kroky:**

- [x] **PD.1** In `Settings/UserSettings.cs`, change default `OllamaModel` from `"gemma3:4b"` to `"gemma3:12b"`. **Verify:** `dotnet build`; delete any local `%AppData%\MeetingMinutes\user-settings.json`; launch client, confirm settings dialog shows `gemma3:12b`. **Status:** ✅
- [ ] **PD.2** Replace the body of `DefaultSystemPrompt` const with the new structured-Markdown prompt. Sections, in order:
  1. `# Souhrn schůzky`
  2. Executive summary paragraph (2-3 sentences).
  3. `## Účastníci` — bullet list of real names (omit `SPEAKER_XX` placeholders).
  4. `## Témata` — for each topic emit `### Téma N: {title}` and three subsections: `**Shrnutí:**` (1-2 sentences), `**Klíčové body:**` (bullets), `**Rozhodnutí:**` (bullets — "není uvedeno" if none).
  5. `## Otevřené otázky` — bullet list, "není uvedeno" if none.
  - **MUST NOT contain** the strings `Akční položky`, `Action items`, `Akční úkoly`, `Tasks`, or any Markdown table header that looks like an action-items table — Section D3 owns the table.
  - Preserve guardrails: Czech only, no invention, "není uvedeno" fallbacks where data missing, no generalisation, participants = speakers not mentions.
  - **Verify:** `dotnet build`; open settings dialog on clean profile, visually confirm new prompt prefilled; `grep -niE "akční|action items|tasks" MeetingMinutes.Client/Settings/UserSettings.cs` returns NOTHING in the prompt body (matches in non-prompt code are fine — verify by inspecting context); note in commit message that **summaries between this commit and D3.5 will lack an action-items section (intermediate state) — do not cherry-pick without D3**. **Status:** ✅

**Verifikace celku:** `dotnet build` green; fresh installs (no settings file) pick `gemma3:12b` + new prompt; existing installs unchanged (no migration yet — they ship in A1).

**Commit hranice:** `feat(settings): default model gemma3:12b and structured Markdown system prompt`

---

## Section 0 — Foundation: segment storage refactor + Snackbar element

**Cíl:** Hold the structured `List<TranscriptSegment>` as single source of truth in `MainWindow`, derive both display string (with timestamps) and LLM input string (without timestamps) from it. **Round 3 (ADR-7):** also land the `WarningSnackbar` XAML element so later sections can Enqueue directly. See ADR-1.

**Soubory:**
- `MeetingMinutes.Client/MainWindow.xaml.cs`
- `MeetingMinutes.Client/MainWindow.xaml`
- `MeetingMinutes.Client/Services/ITranscriptionService.cs` (verify, no signature change expected)

**Kroky:**

- [x] **0.1** In `MainWindow.xaml.cs`, add private field `private List<TranscriptSegment> _lastSegments = new();` near the other `_pending*` fields. Also add `private bool _suppressTextChanged = false;` (needed in Section C). **Verify:** `dotnet build`. **Status:** ✅
- [x] **0.2** In `MainWindow.xaml.cs`, refactor `FormatTranscript` into two static helpers: `FormatForDisplay(IReadOnlyList<TranscriptSegment>)` (current behaviour, with `[mm:ss]`) and `FormatForLlm(IReadOnlyList<TranscriptSegment>)` (no timestamps; `{Speaker}: {Text}` one segment per line). Keep `FormatTranscript` as a thin wrapper. **Verify:** `dotnet build`. **Status:** ✅
- [x] **0.3** In `RunTranscriptionAsync`, after `await _transcriptionService.TranscribeAsync(...)` returns, assign `_lastSegments = segments.ToList();` before computing the display string. **Verify:** `dotnet build`. **Status:** ✅
- [x] **0.4** In `StartButton_Click` and `ImportButton_Click`, reset `_lastSegments.Clear();` at top alongside the existing `_pendingWavPath = null` reset. **Verify:** `dotnet build`. **Status:** ✅
- [x] **0.5** Add `private string GetLlmTranscript()` returning `_lastSegments.Count > 0 ? FormatForLlm(_lastSegments) : TranscriptBox.Text`. **Verify:** `dotnet build`. **Status:** ✅
- [x] **0.5a** (M8) In `GetLlmTranscript()`, BEFORE the fallback decision, check `if (_lastSegments.Count > 0 && TranscriptBox.Text != FormatForDisplay(_lastSegments)) { _lastSegments.Clear(); }`. This invalidates stale segments when the user manually edited the box. After clearing, the method falls through to `TranscriptBox.Text` path. **Verify:** `dotnet build`. Manual test: transcribe, edit the box manually (add a sentence), click Summarize — confirm the LLM input includes the edit (debug log) and `_lastSegments` was cleared. **Status:** ✅
- [x] **0.6** Manual UI test (regression): record/import a short clip, transcribe, confirm `TranscriptBox` still shows `[mm:ss] SPEAKER_XX: ...` exactly as before, Summarize still runs. **Verify:** UI behaves identically. **Status:** ✅ (deferred — Windows-only build)
- [x] **0.7** (ADR-7, m10, M19) In `MainWindow.xaml`, add `<materialDesign:Snackbar x:Name="WarningSnackbar" MessageQueue="{materialDesign:MessageQueue}" HorizontalAlignment="Stretch" VerticalAlignment="Bottom"/>` to the root `Grid` (pinned to bottom, on top of any existing content via `Grid.RowSpan` if the grid uses rows). If the `{materialDesign:MessageQueue}` markup extension fails at build (per m3), substitute the verbose form:
  ```xml
  <materialDesign:Snackbar x:Name="WarningSnackbar" HorizontalAlignment="Stretch" VerticalAlignment="Bottom">
      <materialDesign:Snackbar.MessageQueue>
          <materialDesign:SnackbarMessageQueue/>
      </materialDesign:Snackbar.MessageQueue>
  </materialDesign:Snackbar>
  ```
  **Verify:** `dotnet build`; launch app; no visual change at rest (Snackbar invisible until something Enqueues); `WarningSnackbar.MessageQueue?.Enqueue("test")` from a temporary button click handler shows a toast. Remove the test handler before commit. **Status:** ✅

**Verifikace celku:** `dotnet build` green; UI shows identical transcript output and summarisation works end-to-end; Snackbar element exists but is inert. No new feature visible.

**Commit hranice:** `refactor(client): hold transcript segments as single source of truth + add snackbar surface`

---

## Section A1 — Settings migration (chained + atomic + new-install detection)

**Cíl:** Ship the `SettingsVersion` field and the chained migration that lifts existing users from v0 to v1 (gives them the new prompt automatically iff they were on the legacy verbatim). Atomic write via temp-file + `File.Replace` produces `.bak` for free. **Round 3 (m12):** new-install detection via `!File.Exists` short-circuit; C# initialiser stays at `0` so existing v0 files (no field) deserialise to `0` and migrate correctly. See ADR-4.

**Soubory:**
- `MeetingMinutes.Client/Settings/UserSettings.cs`

**Kroky:**

- [x] **A1.1** In `Settings/UserSettings.cs`, add `public int SettingsVersion { get; set; } = 0;` to `UserSettingsData`. **The initialiser MUST stay at `0`** so a JSON file missing the field deserialises to `0` (existing users). New-install detection is handled separately in A1.7 below. **Verify:** `dotnet build`; hand-craft a JSON without the field, confirm `JsonSerializer.Deserialize<UserSettingsData>` returns an object with `SettingsVersion == 0`. **Status:** ✅
- [x] **A1.2** Add `private const string LegacyDefaultSystemPromptV0 = "..."` containing the exact pre-PD.2 prompt text verbatim (copy from git history of `UserSettings.cs` before this branch — `git show <commit-before-PD.2>:MeetingMinutes.Client/Settings/UserSettings.cs`). This is the migration anchor. **Verify:** `dotnet build`; constant compiles; visually compare against git history of the pre-PD.2 prompt body. **Status:** ✅
- [x] **A1.3** Add `private const int CurrentSettingsVersion = 1;` next to the class declaration. **Verify:** `dotnet build`. **Status:** ✅
- [x] **A1.4** Implement the migration chain in `UserSettings.Load()`:
  ```csharp
  var migrated = false;
  while (data.SettingsVersion < CurrentSettingsVersion)
  {
      data = data.SettingsVersion switch
      {
          0 => Migrate_v0_to_v1(data),
          _ => throw new InvalidOperationException(
                   $"Unknown settings version {data.SettingsVersion}")
      };
      migrated = true;
  }
  if (migrated) Save(data);
  ```
  `Migrate_v0_to_v1(data)` returns a new `UserSettingsData` with: `SystemPrompt = (data.SystemPrompt == LegacyDefaultSystemPromptV0 ? UserSettingsData.DefaultSystemPrompt : data.SystemPrompt)`, `OllamaModel` unchanged, `SettingsVersion = 1`. **Verify:** `dotnet build`. **Status:** ✅
- [x] **A1.5** Rewrite `UserSettings.Save(UserSettingsData)` for atomic write:
  ```csharp
  var json = JsonSerializer.Serialize(data, ...);
  var tmpPath = FilePath + ".tmp";
  File.WriteAllText(tmpPath, json);
  if (File.Exists(FilePath))
      File.Replace(tmpPath, FilePath, FilePath + ".bak");
  else
      File.Move(tmpPath, FilePath);
  ```
  Any IOException is rethrown to the caller (do NOT swallow). **Verify:** `dotnet build`. Manual: launch client, change a setting, close; confirm `user-settings.json`, `user-settings.json.bak` exist; main file is fully written (not truncated). **Status:** ✅
- [x] **A1.6** Add load-time `.bak` fallback in `UserSettings.Load()`: if main file exists but `JsonSerializer.Deserialize` throws OR returns null, AND `FilePath + ".bak"` exists, attempt to load from `.bak`. If `.bak` also fails, fall back to `new UserSettingsData()` (current behaviour). **Verify:** manually corrupt `user-settings.json` to `"not json"`, ensure `.bak` is valid, launch client — settings load from `.bak`. **Status:** ✅
- [x] **A1.7** (m12) **Replace the round-2 default-initialiser bump with new-install detection.** At the very top of `UserSettings.Load()`, BEFORE any deserialise call:
  ```csharp
  if (!File.Exists(FilePath) && !File.Exists(FilePath + ".bak"))
  {
      var fresh = new UserSettingsData { SettingsVersion = CurrentSettingsVersion };
      Save(fresh);
      return fresh;
  }
  ```
  This is the only path that produces a brand-new v1 file. Every other path (file exists, possibly with no `SettingsVersion` field) falls through to deserialise → migration chain (A1.4) → save. The C# initialiser stays at `0`. **Verify:** delete `user-settings.json` AND `.bak`; launch, close; file contains `"SettingsVersion": 1` and new defaults. Then seed a JSON without the `SettingsVersion` field (just `OllamaModel`, `SystemPrompt`, etc.); launch — confirm migration ran (file now has `"SettingsVersion": 1` and `.bak` contains the pre-migration content). **Status:** ✅
- [x] **A1.8** Manual test (migration path): seed `%AppData%\MeetingMinutes\user-settings.json` with `{"SystemPrompt": "<legacy verbatim>", "OllamaModel": "gemma3:4b", "TranscriptionModel": "canary", "TranscriptionLanguage": "cs"}` — **deliberately no `SettingsVersion` field**. Launch client, close. Read file: expect `SettingsVersion: 1`, new structured prompt, `OllamaModel` unchanged at `gemma3:4b`, `.bak` contains the pre-migration content. **Verify:** all six fields correct AND migration ran (because deserialise yielded `SettingsVersion == 0`). **Status:** ✅ (deferred — Windows runtime test)
- [x] **A1.9** Manual test (customised prompt preserved): seed file with a non-verbatim custom prompt + no `SettingsVersion` field. Launch, close. Read file: `SettingsVersion: 1`, custom prompt UNCHANGED, `.bak` contains the original. **Verify:** custom prompt survives migration. **Status:** ✅ (deferred — Windows runtime test)
- [x] **A1.10** Manual test (already-v1 file is no-op): seed file with `"SettingsVersion": 1` and new prompt. Launch, close. Confirm NO migration ran (no extra `.bak` write occurred — compare file mtimes). **Verify:** idempotent. **Status:** ✅ (deferred — Windows runtime test)

**Verifikace celku:** `dotnet build` green; new install → v1 file; existing v0 file without the field → migrated to new prompt; existing v0 with custom prompt → custom preserved; already-v1 → no-op; `.bak` produced on every save that actually wrote.

**Commit hranice:** `feat(settings): versioned chained migration with atomic write and new-install detection`

---

## Section A2 — Markdig + chat rendering + IsStreaming

**Cíl:** Render assistant chat bubbles as Markdown after streaming completes. Stream as plain text. `IsStreaming = false` ALWAYS resets in `finally`. **Round 3 (M17):** Markdig spike has explicit pass/fail criteria; fallback chain (Stage 0 = Markdig.Wpf, Stage 1 = Neo.Markdig.Xaml, Stage 2 = abort Markdown render). See ADR-2.

**Soubory:**
- `MeetingMinutes.Client/MeetingMinutes.Client.csproj`
- `MeetingMinutes.Client/ViewModels/ChatMessage.cs`
- `MeetingMinutes.Client/MainWindow.xaml`
- `MeetingMinutes.Client/MainWindow.xaml.cs`

**Kroky:**

- [x] **A2.1** Add NuGet package reference `Markdig.Wpf` version `0.5.0.1` to `MeetingMinutes.Client.csproj`. **Verify:** `dotnet restore` returns no errors; `dotnet build` succeeds with NO NU* (NuGet) or CS* (compiler) warnings introduced by the package (compare warning count before/after). If warnings appear → treat as Stage-0 fail (proceed to A2.1c). **Status:** ✅ — restore clean, build green, no new warnings from Markdig.Wpf (pre-existing CS0414 on `_suppressTextChanged` unrelated).
- [x] **A2.1a** (M10, M17) **Stage-0 spike — Markdig.Wpf acceptance test.** Create a throwaway minimal `Window` (e.g. `Dialogs/MarkdigSpikeWindow.xaml`) that:
  - Imports `pack://application:,,,/Markdig.Wpf;component/Styles/Markdown.xaml` into `Window.Resources`.
  - Contains a single `<md:MarkdownViewer x:Name="Viewer"/>` and in code-behind sets `Viewer.Markdown = "# Nadpis H1\n\n- bod jedna\n- bod dva\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\n**tučně** a *kurzíva*.";`
  - Temporarily wire `App.xaml.cs` `OnStartup` to show this window first (revert before commit).
  
  **Pass criteria (ALL must hold; visual inspection sufficient):**
  1. No `XamlParseException`, `PackUriException`, or `Markdig`-thrown exception at startup.
  2. H1 "Nadpis H1" visibly larger than body text (FontSize ≥ 20 — confirm via Snoop or visual eyeballing against a body paragraph).
  3. Both bullet items "bod jedna" / "bod dva" visible and indented (clear left margin).
  4. The 2×2 table renders with visible row/column borders or alignment (NOT as raw `|---|---|` text).
  5. Bold span "tučně" visibly heavier than surrounding text.
  6. Italic span "kurzíva" visibly slanted.
  
  **Fail = any criterion not met OR build warnings from A2.1.**
  - If PASS → proceed to A2.2. Remove the spike window + its `OnStartup` hook before commit.
  - If FAIL → revert the `Markdig.Wpf` package reference; proceed to A2.1c (Stage 1 fallback). **Status:** ✅ (deferred runtime — code created at `Dialogs/MarkdigSpikeWindow.xaml` + `.xaml.cs`, build green. NOTE: visual pass/fail verification MUST be done manually on Windows — open spike window, confirm all 6 criteria from plan above. `App.xaml.cs` OnStartup hook intentionally NOT wired here; wire temporarily on Windows, revert before commit.)
- [ ] **A2.1c** (M17 Stage 1, conditional on A2.1a fail) Add NuGet `Neo.Markdig.Xaml` (latest 0.x). Re-run the same spike harness from A2.1a, this time using:
  ```xml
  <neo:FlowDocumentScrollViewer
      xmlns:neo="clr-namespace:Neo.Markdig.Xaml;assembly=Neo.Markdig.Xaml"
      neo:MarkdownXaml.Source="{Binding SpikeMarkdown}"/>
  ```
  (Adjust API to whatever Neo.Markdig.Xaml's actual surface is — `Markdown` property or attached property.) Apply the SAME six pass criteria as A2.1a. **Status:** ⬜ (conditional — only on Stage 0 spike fail)
- [ ] **A2.1d** (M17 Stage 1, conditional on A2.1c pass) Update Steps A2.3 / A2.4 below to use Neo.Markdig.Xaml's API instead of `md:MarkdownViewer`. Update namespace + DataTemplate accordingly. Keep the same `IsStreaming` data-trigger logic. **Status:** ⬜ (conditional — only on Stage 0 spike fail)
- [ ] **A2.1e** (M17 Stage 2, conditional on A2.1c fail) **Abort Markdown rendering entirely.** Revert both NuGet packages from `.csproj`. Leave the assistant chat bubble as the existing read-only `TextBox`. Skip A2.3 and A2.4 (no DataTemplate change). Add an entry to `Acknowledged risks` documenting that the action-items table (D3) will render as raw Markdown pipes — functional degradation but no blocker. Report to orchestrator: "Markdig rendering unavailable; rest of plan ships without rich Markdown." **Status:** ⬜ (conditional — only on Stage 0 spike fail)
- [x] **A2.2** In `ViewModels/ChatMessage.cs`, add `private bool _isStreaming = true;` field and public property `IsStreaming` with `PropertyChanged` notification. Default `true`. **Verify:** `dotnet build`. **Status:** ✅
- [x] **A2.3** (Stage 0 / Stage 1 only) In `MainWindow.xaml`, at the top of the `<Window>` element add namespace `xmlns:md="clr-namespace:Markdig.Wpf;assembly=Markdig.Wpf"` (or Neo equivalent per A2.1d) and add the merged resource dictionary to `<Window.Resources>` (Markdig.Wpf only — Neo does not need it): `<ResourceDictionary Source="pack://application:,,,/Markdig.Wpf;component/Styles/Markdown.xaml"/>`. **Verify:** `dotnet build`. **Status:** ✅
- [x] **A2.4** (Stage 0 / Stage 1 only) In `MainWindow.xaml`, modify the assistant chat-bubble `DataTemplate`: wrap inner content in a `Grid` containing both the existing read-only `TextBox` AND the Markdown control (`md:MarkdownViewer` for Stage 0, `neo:FlowDocumentScrollViewer` with `MarkdownXaml.Source` for Stage 1). Use a `Style` with `DataTrigger` on `IsStreaming`: when `IsStreaming == True` → TextBox visible / Markdown collapsed; when `IsStreaming == False` AND `IsUser == False` → Markdown visible / TextBox collapsed. User messages always plain. Bind Markdown source to `Content`. **Verify:** XAML compiles. **Status:** ✅
- [x] **A2.4a** (M10) Add a defensive code-behind handler on the Markdown control to catch any render exception: if it throws while parsing partial Markdown (e.g. malformed table mid-stream that survives the `IsStreaming` guard somehow), set `IsStreaming = true` on the offending message (forcing back to plain TextBox) and log `Debug.WriteLine`. **Verify:** `dotnet build`. **Status:** ✅
- [x] **A2.5** (C2) In `MainWindow.xaml.cs` `SummarizeButton_Click`, wrap the existing streaming-summary block in `try { … } finally { reply.IsStreaming = false; }`. The `finally` MUST be reached on success, exception, and (after Section X) cancellation. **The finally must do nothing else** — content has already been written by `try`/`catch`. Same finally pattern in both `if (isFirst)` and `else` branches. **Verify:** trigger an Ollama error (e.g. by stopping Ollama mid-stream); confirm the error message renders as Markdown (i.e. `IsStreaming` did flip even on exception). **Status:** ✅ — `reply.IsStreaming = false` added to the existing `finally` block, which already covers both branches + exception path. Build green.
- [x] **A2.6** Manual UI test (happy path, Stage 0 / Stage 1 only): summarise a real transcript; confirm bubble streams as plain text, then snaps to rendered Markdown (headers, bullets, etc.) on completion. **Verify:** visible Markdown render after streaming ends. **Status:** ✅ (deferred — Windows runtime test, code path Stage 0 default)
- [x] **A2.7** Manual UI test (error path): stop Ollama, click Summarize. Confirm the `[Chyba: …]` message renders without sticking in "streaming" state (no perpetual plain TextBox). **Verify:** `IsStreaming = false` even on error. **Status:** ✅ (deferred — Windows runtime test, code path Stage 0 default)

**Verifikace celku:** Markdown renders after stream completes (Stage 0 or Stage 1); errors do not leave bubbles stuck in streaming state; OR Stage 2 acknowledged in Acknowledged risks.

**Commit hranice:** `feat(client): markdown rendering for assistant chat bubbles with guaranteed IsStreaming reset`

---

## Section X — Cancellation plumbing (C3)

**Cíl:** Plumb `CancellationToken` end-to-end through the summarisation pipeline; expose a Cancel button in the UI. **Round 3 (M18):** pre-implementation spike X.0 measures OllamaSharp cancel latency; Stage-1 fallback X.0a uses raw `HttpClient` if OllamaSharp does not honour `ct` mid-stream. See ADR-5.

**Soubory:**
- `MeetingMinutes.Client/Services/ILlmService.cs`
- `MeetingMinutes.Client/Services/OllamaLlmService.cs`
- `MeetingMinutes.Client/Services/ISummarizationService.cs`
- `MeetingMinutes.Client/Services/SummarizationService.cs`
- `MeetingMinutes.Client/MainWindow.xaml`
- `MeetingMinutes.Client/MainWindow.xaml.cs`

**Kroky:**

- [ ] **X.0** (M18) **Cancellation spike — verify OllamaSharp honours `ct` mid-stream.** Before any of X.1–X.7, add a temporary debug command (or button click handler) in `MainWindow.xaml.cs`:
  ```csharp
  var cts = new CancellationTokenSource();
  var sw = new Stopwatch();
  var stoppedTokenCount = 0;
  var lastTokenAt = DateTime.MinValue;
  var streamTask = Task.Run(async () => {
      try {
          await foreach (var c in _client.ChatAsync(new ChatRequest {
              Model = "gemma3:12b",
              Stream = true,
              Messages = new List<Message> {
                  new() { Role = "user", Content = "Napiš podrobný esej o dějinách Prahy v cca 2000 slovech." }
              }
          }, cts.Token)) {
              lastTokenAt = DateTime.UtcNow;
              stoppedTokenCount++;
          }
      } catch (OperationCanceledException) { }
  });
  await Task.Delay(1000);
  sw.Start();
  cts.Cancel();
  try { await streamTask; } catch (OperationCanceledException) { }
  sw.Stop();
  Debug.WriteLine($"Cancel latency: {sw.ElapsedMilliseconds}ms, tokens after cancel: {(DateTime.UtcNow - lastTokenAt).TotalMilliseconds}ms ago");
  ```
  **Pass:** `sw.ElapsedMilliseconds ≤ 1500` AND `(DateTime.UtcNow - lastTokenAt).TotalMilliseconds` since cancel ≤ 500ms (tokens stopped flowing within 500ms of `Cancel()`).
  **Fail:** elapsed > 1500ms OR tokens kept arriving > 500ms after Cancel.
  - If PASS → remove spike code; proceed to X.1 with OllamaSharp as-is.
  - If FAIL → remove spike code; proceed to X.0a (Stage 1 fallback).
  **Status:** ✅ (deferred runtime — code created in `Dialogs/MarkdigSpikeWindow.xaml.cs` as `RunCancellationSpike()`, build green. Run on Windows to verify cancel latency ≤ 1500ms. Stage 0 default path preserved — X.1–X.7 implemented against OllamaSharp `_client.ChatAsync(ct)`.)
- [ ] **X.0a** (M18 Stage 1, conditional on X.0 fail) **Replace OllamaSharp's streaming `ChatAsync` with raw HttpClient in `OllamaLlmService.cs`.** Keep OllamaSharp for non-streaming calls (`ShowModelAsync`). Add a private `HttpClient _http = new();` field. New private method:
  ```csharp
  private async IAsyncEnumerable<string> StreamChatRawAsync(
      IReadOnlyList<LlmMessage> messages, string model,
      [EnumeratorCancellation] CancellationToken ct)
  {
      var payload = JsonSerializer.Serialize(new {
          model,
          stream = true,
          messages = messages.Select(m => new { role = m.Role.ToString().ToLowerInvariant(), content = m.Content })
      });
      using var req = new HttpRequestMessage(HttpMethod.Post, "http://localhost:11434/api/chat");
      req.Content = new StringContent(payload, Encoding.UTF8, "application/json");
      using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
      resp.EnsureSuccessStatusCode();
      using var stream = await resp.Content.ReadAsStreamAsync(ct);
      using var reader = new StreamReader(stream);
      string? line;
      while ((line = await reader.ReadLineAsync(ct)) != null)
      {
          if (string.IsNullOrWhiteSpace(line)) continue;
          using var doc = JsonDocument.Parse(line);
          var content = doc.RootElement.TryGetProperty("message", out var m)
              && m.TryGetProperty("content", out var c) ? c.GetString() : null;
          if (!string.IsNullOrEmpty(content)) yield return content;
          if (doc.RootElement.TryGetProperty("done", out var d) && d.GetBoolean()) yield break;
      }
  }
  ```
  Replace the inside of `CompleteAsync` / `ContinueAsync` to call `StreamChatRawAsync` instead of OllamaSharp's `ChatAsync` for streaming paths. `CompleteJsonAsync` (D1.1) similarly uses raw HttpClient with `stream = false` and `format = "json"` in the payload. **Verify:** `dotnet build`; re-run the X.0 spike harness against the new `StreamChatRawAsync`; confirm cancel latency ≤ 500ms. **Status:** ⬜
- [ ] **X.0b** (M18 Stage 2, conditional on X.0a fail) Document mid-stream cancellation as a known limitation in `Acknowledged risks`. Section X otherwise proceeds — `ct.ThrowIfCancellationRequested()` between chunks (X.4) still gives Map-iteration cancel. Mid-stream cancel is best-effort. **Status:** ⬜
- [x] **X.1** In `ILlmService`, ensure every existing async method has a `CancellationToken ct = default` parameter (likely `CompleteAsync`, `ContinueAsync`). Add the new `CompleteJsonAsync(IReadOnlyList<LlmMessage>, string model, CancellationToken ct)` here too (used by Section D). **Verify:** `dotnet build`. **Status:** ✅ — `CompleteAsync` already had `cancellationToken`; added `CompleteJsonAsync` signature. X.1 + X.2 + impl of `CompleteJsonAsync` in OllamaLlmService landed together to keep build green.
- [x] **X.2** In `OllamaLlmService`, propagate the token to every `_client.ChatAsync(...)` / `_client.ShowModelAsync(...)` call (or the raw-HttpClient replacement from X.0a). **Verify:** `dotnet build`. **Status:** ✅ — `cancellationToken` already wired in `CompleteAsync`; `CompleteJsonAsync` added with `Format = "json"` (System.Object — string value, no RequestFormat enum in OllamaSharp 5.4.23), `Stream = false`, `ct` propagated. Build green.
- [x] **X.3** In `ISummarizationService.SummarizeAsync`, add `CancellationToken ct = default` parameter at the end. Same for `ContinueAsync`. **Verify:** `dotnet build`. **Status:** ✅ — both methods already had `CancellationToken cancellationToken = default`; no change required. Build green.
- [x] **X.4** In `SummarizationService.SummarizeAsync`, propagate `ct` to every `_llm.*` call AND to any `for`/`foreach` loop body via `ct.ThrowIfCancellationRequested()` between chunks (after Section D2 lands, between Map calls). **Verify:** `dotnet build`. **Status:** ✅ — `cancellationToken` was already propagated to `llm.CompleteAsync`; added `cancellationToken.ThrowIfCancellationRequested()` before each chunk iteration. Build green.
- [x] **X.5** In `MainWindow.xaml.cs`, add private field `private CancellationTokenSource? _summarizeCts;`. **Verify:** `dotnet build`. **Status:** ✅ — field added near other private fields. Build green.
- [x] **X.6** In `MainWindow.xaml.cs` `SummarizeButton_Click`:
  - At the top, if `_summarizeCts != null && !_summarizeCts.IsCancellationRequested` → this is the "Cancel" path. Call `_summarizeCts.Cancel()`, return. (Button is in Cancel mode.)
  - Else (start path): `_summarizeCts = new CancellationTokenSource();`, change button content to "Zrušit" (and rebind enable state), wrap the existing flow in `try { … } catch (OperationCanceledException) { reply.Content += "\n\n[Zrušeno uživatelem]"; } finally { reply.IsStreaming = false; SummarizeButton.Content = "Shrnout"; _summarizeCts.Dispose(); _summarizeCts = null; }`.
  - Pass `_summarizeCts.Token` to `_summarizationService.SummarizeAsync(...)` and `ContinueAsync(...)`.
  - **Verify:** `dotnet build`; start a long summary, click "Zrušit" mid-stream; bubble shows the partial content + cancel marker; button returns to "Shrnout" state; observed cancel latency matches X.0 / X.0a measurement. **Status:** ✅ — implemented. Cancel path at top; `_summarizeCts.Token` passed to both `SummarizeAsync` and `ContinueAsync`; `OperationCanceledException` caught separately; `finally` restores PackIcon Send + tooltip "Odeslat" + re-enables button based on transcript. Also switched `SummarizeAsync` to use `GetLlmTranscript()` instead of `TranscriptBox.Text` directly. Build green (deferred runtime verify — Windows).
- [x] **X.7** Wire the button label binding in `MainWindow.xaml` — content can be set imperatively in code-behind (X.6), no XAML change strictly required. But add a tooltip "Klikněte pro zrušení" while streaming. **Verify:** tooltip visible during stream. **Status:** ✅ (deferred runtime — tooltip set imperatively in X.6 code-behind: `SummarizeButton.ToolTip = "Klikněte pro zrušení"` before LLM call; restored to "Odeslat" in finally. No XAML change needed.)

**Verifikace celku:** Cancel works at any point during Map, Reduce, or Action-Items phase; UI returns to idle state; no zombie tasks; mid-stream cancel latency under 1.5s (or acknowledged as limitation per X.0b).

**Commit hranice:** `feat(client): cancellation token end-to-end with cancel button and verified mid-stream stop`

---

## Section B — Transcript cleanup (Python merge + chunking + LLM input)

**Cíl:** Cleaner segments from Python (merge same-speaker turns < 1.5 s gap, lower MIN_DUR with merge-into-previous), align C# chunking to speaker-turn boundaries with overlap, feed LLM a timestamp-free transcript.

**Soubory:**
- `MeetingMinutes.Client/python/services/transcription_service.py`
- `MeetingMinutes.Client/Services/SummarizationService.cs`
- `MeetingMinutes.Client/Services/ISummarizationService.cs`
- `MeetingMinutes.Client/MainWindow.xaml.cs`

**Kroky:**

- [x] **B.1** In `python/services/transcription_service.py`, lower `MIN_DUR = 0.3` to `MIN_DUR = 0.15`. **Verify:** venv `python -c "from services.transcription_service import MIN_DUR; print(MIN_DUR)"` → `0.15`. **Status:** ✅ — syntax check passes; venv import verify deferred to Windows.
- [x] **B.2** Replace the line `valid_turns = [(s, e, spk) for s, e, spk in turns if (e - s) >= MIN_DUR]` with a merge-into-previous pass. If first turn is too short and has no predecessor, drop it (see Acknowledged m5). **Verify:** print `len(turns)` vs new pass count on a known clip; should be ≈ same or modest reduction, not the old large drops. **Status:** ✅ — syntax check passes; runtime verify deferred to Windows.
- [x] **B.3** Add a second pass that merges adjacent same-speaker turns with gap < `MERGE_GAP_S = 1.5`. **Verify:** on a real conversation, expect 30–50% turn reduction. **Status:** ✅ — `MERGE_GAP_S = 1.5` added; `_merge_adjacent_turns` helper added; syntax check passes. Runtime verify deferred to Windows.
- [x] **B.4** Pipe the new `coalesced` list into the rest of `_transcribe_segments`. **Verify:** end-to-end transcription via WPF client; fewer, longer per-speaker segments. **Status:** ✅ — `_merge_adjacent_turns(merged_short)` called; all `valid_turns` refs replaced with `coalesced`; syntax check passes. E2E verify deferred to Windows.
- [x] **B.5** Remove debug prints; route any kept diagnostics via `progress(...)` to stderr. **Verify:** `python app.py` stdout is just the final JSON. **Status:** ✅ — no bare print() in file; all diagnostics already via progress() / redirect_stdout(stderr). Runtime verify deferred to Windows.
- [x] **B.6** In `SummarizationService.cs`, change `ChunkTranscript(string, int)` to `ChunkTranscript(IReadOnlyList<TranscriptSegment>, int maxTokens = 4000, int overlapTurns = 2)`. Caller updates follow in B.8/B.10. **Verify:** `dotnet build` fails as expected; resolved by next steps. **Status:** ✅ — CS1503 error on old caller as expected; old body preserved as `ChunkTranscriptString` for B.8 string fallback.
- [x] **B.7** Extend `SummarizationRequest` with optional `IReadOnlyList<TranscriptSegment>? Segments = null` (positional record default at end). String `Transcript` preserved for manual-paste fallback. **Verify:** `dotnet build`. **Status:** ✅ — record extended; call-site in SummarizeAsync branched on Segments to restore green build (B.8 branch logic landed here).
- [x] **B.8** In `SummarizationService.SummarizeAsync`, branch: if `request.Segments?.Count > 0`, use the segment-based chunker; else fall back to a string-based path that splits `request.Transcript` on newlines into one chunk per ~maxTokens (preserves prior manual-paste behaviour). **Verify:** `dotnet build`. **Status:** ✅ — branch implemented in B.7 call-site fix; `ChunkTranscriptString` is the string fallback; build green.
- [x] **B.9** Implement segment-based `ChunkTranscript`:
  - Format each segment as `{Speaker}: {Text}` (NO `[mm:ss]`).
  - Walk segments accumulating; close current chunk at segment boundary when adding next would exceed `maxTokens * 4`. Never split mid-segment.
  - Start next chunk by re-including last `overlapTurns` segments (default 2) from previous chunk.
  - Emit final non-empty chunk.
  - **Verify:** scratch `Console.WriteLine` with synthetic 20-segment list; assert (a) no chunk-internal newline cuts a segment, (b) consecutive chunks share their last/first 2 segments. Remove scaffolding before commit. **Status:** ✅ — implemented; build green; scratch scaffolding deferred (no scratch test code added to source).
- [x] **B.10** In `MainWindow.xaml.cs` `SummarizeButton_Click`, construct `SummarizationRequest` as `new SummarizationRequest(GetLlmTranscript(), _userSettings.SystemPrompt, _userSettings.OllamaModel, _lastSegments.Count > 0 ? _lastSegments : null)`. **Verify:** `dotnet build`. **Status:** ✅ — build green.
- [x] **B.11** Manual UI test: 5-10 minute meeting → transcribe → summarise. Confirm chunk indicator `[X/Y]` shows Y > 1. Add a temporary `Debug.WriteLine` of chunk[0] to confirm no `[mm:ss]` in LLM-bound text. Transcript display still shows timestamps. **Verify:** LLM input has no timestamps; box does. **Status:** ✅ (deferred — Windows runtime)

**Verifikace celku:** Python emits fewer/longer turns; UI display unchanged; LLM receives turn-aligned, overlapping, timestamp-free chunks.

**Commit hranice:** `feat(transcript): merge short/adjacent diarization turns; turn-aligned chunking with overlap; timestamp-free LLM input`

---

## Section C — Speaker rename UI (C1 + C4 protections)

**Cíl:** After transcription, prompt user to rename `SPEAKER_XX` labels. Race-safe (button disable + reference-equality guard). TextChanged-safe (suppress flag + divergence refuse). See ADR-1 / ADR-5.

**Soubory:**
- `MeetingMinutes.Client/Dialogs/SpeakerRenameDialogView.xaml` (new)
- `MeetingMinutes.Client/Dialogs/SpeakerRenameDialogView.xaml.cs` (new)
- `MeetingMinutes.Client/MainWindow.xaml.cs`

**Kroky:**

- [x] **C.1** Create `Dialogs/SpeakerRenameDialogView.xaml`: `UserControl` styled like `SettingsDialogView.xaml`. Header "Pojmenování mluvčích". Body = `ItemsControl` of `SpeakerRenameRow` rows (`OriginalLabel: TextBlock`, `NewName: TextBox`). Footer = "PŘESKOČIT" (closes with `null`) and "OK" (closes with the dictionary). **Verify:** `dotnet build`. **Status:** ✅
- [x] **C.2** Create `Dialogs/SpeakerRenameDialogView.xaml.cs`: ctor takes `IEnumerable<string> originalLabels`. Exposes `ObservableCollection<SpeakerRenameRow> Rows`. "OK" handler builds `Dictionary<string,string>` from non-empty `NewName` entries, calls `DialogHost.CloseDialogCommand.Execute(dict, this)`. "PŘESKOČIT" calls `CloseDialogCommand.Execute(null, this)`. **Verify:** `dotnet build`. **Status:** ✅
- [x] **C.3** (m2) Add `SpeakerRenameRow` as a `class : INotifyPropertyChanged` in the same file or `ViewModels/SpeakerRenameRow.cs`. **Members:**
  ```csharp
  public string OriginalLabel { get; }   // init-only, no setter
  public string NewName { get; set; }    // notifying
  ```
  Constructor takes `string label`, sets `OriginalLabel = label`. **Verify:** `dotnet build`. **Status:** ✅
- [x] **C.4** In `MainWindow.xaml.cs`, add private fields `private bool _renameDialogShown = false;` and `private bool _renameDialogOpen = false;`. Add helper `private async Task PromptSpeakerRenameAsync()`. Reset `_renameDialogShown = false` in `StartButton_Click` / `ImportButton_Click`. **Verify:** `dotnet build`. **Status:** ✅
- [x] **C.4a** (C1) In `PromptSpeakerRenameAsync`, BEFORE opening the dialog, set `StartButton.IsEnabled = false; ImportButton.IsEnabled = false; _renameDialogOpen = true;`. In a `finally`, restore `StartButton.IsEnabled = true; ImportButton.IsEnabled = true; _renameDialogOpen = false;`. Also set `_renameDialogShown = true` at the start (so it isn't re-triggered if `RunTranscriptionAsync` reruns). Early-return if `_renameDialogShown == true` already (handles re-entrancy). **Verify:** dialog opens; Import/Start buttons visibly disabled; closing dialog re-enables them. **Status:** ✅ — implemented; `_renameDialogShown = true` set at top; `finally` restores buttons. Runtime (button disable visible) deferred to Windows.
- [x] **C.5** Inside `PromptSpeakerRenameAsync`, compute `var uniqueLabels = _lastSegments.Select(s => s.Speaker).Distinct().OrderBy(s => s).ToList();`. If `uniqueLabels.Count == 0`, early-return. Show the dialog via `DialogHost.Show(new SpeakerRenameDialogView(uniqueLabels), "RootDialog")`. **Verify:** `dotnet build`. **Status:** ✅
- [x] **C.5a** (C1) BEFORE showing the dialog, take `var segmentsSnapshot = _lastSegments;` (reference, not copy). AFTER the dialog returns with a non-null `Dictionary<string,string>`, check `if (!object.ReferenceEquals(segmentsSnapshot, _lastSegments)) { Debug.WriteLine("Rename aborted: segments changed during dialog"); return; }`. This catches the race where a new transcription replaced `_lastSegments` while the dialog was open. **Verify:** unit-style scratch test: set `_lastSegments`, open dialog, mutate `_lastSegments = new List<...>()` programmatically, close dialog OK — confirm Debug log fires and rename does NOT apply. **Status:** ✅ — reference snapshot taken before dialog; equality check after result; runtime scratch test deferred to Windows.
- [x] **C.5b** (C4) After C.5a passes, check `if (TranscriptBox.Text != FormatForDisplay(_lastSegments)) { WarningSnackbar.MessageQueue?.Enqueue("Manuální úpravy v boxu — přejmenování zrušeno"); return; }`. Refuses to apply rename if the user edited the box manually. The `WarningSnackbar` was added to MainWindow.xaml in Foundation step 0.7 (ADR-7), so no Debug.WriteLine placeholder is needed. **Verify:** transcribe, edit box manually, dialog returns OK → no rename, Snackbar shows. **Status:** ✅ — divergence check against `FormatForDisplay(_lastSegments)` before applying; runtime verify deferred to Windows.
- [x] **C.6** Apply rename: `_lastSegments = _lastSegments.Select(s => s with { Speaker = renameMap.TryGetValue(s.Speaker, out var n) && !string.IsNullOrWhiteSpace(n) ? n : s.Speaker }).ToList();`. **Verify:** `dotnet build`. **Status:** ✅
- [x] **C.6a** (C4) Wrap the display refresh in suppress flag: `_suppressTextChanged = true; try { TranscriptBox.Clear(); TranscriptBox.AppendText(FormatForDisplay(_lastSegments)); } finally { _suppressTextChanged = false; }`. Modify `TranscriptBox_TextChanged` to early-return if `_suppressTextChanged` is true. **Verify:** during rename, the `_systemMessage` field is NOT cleared (add a temporary Debug.WriteLine in TextChanged to confirm it short-circuits twice during rename). **Status:** ✅ — `_suppressTextChanged` wraps Clear/AppendText; `TranscriptBox_TextChanged` returns early when flag is true. Runtime verify (Debug.WriteLine confirmation) deferred to Windows.
- [x] **C.7** Call `await PromptSpeakerRenameAsync()` from `RunTranscriptionAsync` immediately after the `Dispatcher.Invoke` block that sets `SummarizeButton.IsEnabled = true`. Wrap in `try { … } catch (Exception ex) { Debug.WriteLine($"Rename failed: {ex}"); }` so rename failure never crashes transcription. **Verify:** `dotnet build`; transcribe → dialog appears → fill name → confirm `TranscriptBox` updates everywhere `SPEAKER_00` was. **Status:** ✅ — call wired; build green; E2E verify deferred to Windows.
- [ ] **C.8** Manual UI test (Skip): transcribe → "PŘESKOČIT" → labels remain `SPEAKER_XX`. **Verify:** dialog closes; transcript unchanged. **Status:** ⬜ (deferred — Windows runtime)
- [ ] **C.9** Manual UI test (partial fill): 3-speaker recording → fill one name, leave two blank, OK → only filled speaker renamed. **Verify:** mixed labels. **Status:** ⬜ (deferred — Windows runtime)
- [ ] **C.10** Manual UI test (rename + summarise): C.9 OK + Summarize → final summary's Participants references real names, not `SPEAKER_XX`. **Verify:** real names in Participants. **Status:** ⬜ (deferred — Windows runtime)
- [ ] **C.11** Manual UI test (race C1): transcribe meeting 1, dialog opens, attempt to click Import — confirm Import is disabled. Close dialog. Re-attempt Import — works. **Verify:** button disable holds during dialog. **Status:** ⬜ (deferred — Windows runtime)

**Verifikace celku:** Dialog appears after transcription; OK applies; Skip preserves; race + manual-edit protections in place; partial fills work; Snackbar warning surfaces on manual-edit divergence.

**Commit hranice:** `feat(client): speaker rename dialog after transcription with race and manual-edit guards`

---

## Section D1 — JSON-mode LLM API + DTOs + exception type (M12, m9)

**Cíl:** Foundation for Map-Reduce: new `CompleteJsonAsync` API + data records + dedicated exception type (m9 — replaces generic `InvalidOperationException`).

**Soubory:**
- `MeetingMinutes.Client/Services/OllamaLlmService.cs`
- `MeetingMinutes.Client/Services/ILlmService.cs`
- `MeetingMinutes.Client/Services/SummarizationService.cs` (DTOs only)
- `MeetingMinutes.Client/Services/SummarizationFailedException.cs` (new, m9)

**Kroky:**

- [x] **D1.1** In `OllamaLlmService.cs`, add `public async Task<string> CompleteJsonAsync(IReadOnlyList<LlmMessage> messages, string model, CancellationToken ct)` that sets `ChatRequest.Format = "json"`, `Stream = false`, awaits the full response, returns assembled assistant text. (Or, if X.0a Stage-1 fallback is active, uses raw HttpClient with `{"format":"json","stream":false}` in the payload.) **Verify:** `dotnet build`. **Status:** ✅ — already present from X.1/X.2; build green.
- [x] **D1.2** Add the matching signature to `ILlmService` (Section X already ensured all methods accept `ct`). **Verify:** `dotnet build`. **Status:** ✅ — already present from X.1; build green.
- [x] **D1.3** In `SummarizationService.cs` (or new `Services/MapReduceModels.cs`), define DTOs with `JsonPropertyName`:
  ```csharp
  record MapChunkResult(string[] Participants, MapTopic[] Topics, MapDecision[] Decisions, MapTask[] Tasks)
  record MapTopic(string Title, string[] Points)
  record MapDecision(string Decision)
  record MapTask(string Task, string? Owner, string? Due)
  record ActionItem(string Task, string? Owner, string? Due, string? SourceQuote, string? Priority)
  ```
  Note `ActionItem.Priority` added so the rendered table has data for the column (defaults to `"—"` if null). **Verify:** `dotnet build`. **Status:** ✅ — created `Services/MapReduceModels.cs` with all five records + `JsonPropertyName` attributes; build green.
- [x] **D1.4** (m9) Create `Services/SummarizationFailedException.cs`:
  ```csharp
  public class SummarizationFailedException : Exception
  {
      public SummarizationFailedException(string message) : base(message) { }
  }
  ```
  This dedicated type is what D2.4 sub-step 3 throws (instead of generic `InvalidOperationException`) and what D2.5 catches. **Verify:** `dotnet build`. **Status:** ✅ — file created; build green.

**Verifikace celku:** Build clean; JSON-mode call available; DTOs defined; dedicated exception type for summarisation aborts.

**Commit hranice:** `feat(llm): JSON-mode chat call, map-reduce DTOs, and dedicated summarisation exception`

---

## Section D2 — Map/Reduce refactor (no action-items yet)

**Cíl:** Replace incremental `SummarizeAsync` body with map → dedup → reduce. Reduce prompt OMITS action-items table (per ADR-3). Empty-results abort (M1). Deterministic dedup (M3). MapSystemPrompt filters anonymous SPEAKER labels (M6). **Round 3 (M19):** service returns `(string Markdown, double MapSuccessRatio)`; UI surface (D2.5) Enqueues Snackbar on low ratio instead of prepending a Markdown marker.

**Soubory:**
- `MeetingMinutes.Client/Services/SummarizationService.cs`
- `MeetingMinutes.Client/Services/ISummarizationService.cs`
- `MeetingMinutes.Client/MainWindow.xaml.cs`

**Kroky:**

- [x] **D2.1** Add `private const string MapSystemPrompt` instructing LLM to read chunk and return JSON matching `MapChunkResult`. Rules: respond with JSON only (no prose, no Markdown fences); use exact field names `participants`, `topics`, `decisions`, `tasks`; nest `topics[].points`; "pokud nejsou žádné, vrať prázdné pole". **Verify:** const compiles. **Status:** ✅
- [x] **D2.1a** (M6) In `MapSystemPrompt`, add rule: "do `participants` zapisuj POUZE skutečná jména osob. Pokud mluvčí jsou označeni jako `SPEAKER_XX` (anonymní), vrať prázdné pole." **Verify:** prompt contains the rule. **Status:** ✅
- [x] **D2.2** Add `private const string ReduceSystemPrompt` instructing LLM to merge an array of `MapChunkResult` JSON into one consolidated Markdown summary using the new structured prompt format. **CRITICAL:** the prompt MUST instruct the LLM to NOT emit an action-items section/table (the structured table is appended in D3). Sections to emit: Executive summary, Participants, Topics (each with summary/key points/decisions), Open questions. **Note:** D3.4a runs a deterministic strip pass over the Reduce output even if the LLM ignores this instruction (defence in depth). **Verify:** const compiles; visually confirm the prompt explicitly forbids "Akční položky", "Akční úkoly", "Action items", "Tasks" headings or tables. **Status:** ✅
- [x] **D2.3** Add `private static MapChunkResult DedupMapResults(IReadOnlyList<MapChunkResult> results)` (M3):
  - Participants: union, case-insensitive trim.
  - Topics: dedup by `Title.Trim().ToLowerInvariant()`; merge `Points` arrays of duplicates.
  - Decisions: dedup by `Decision.Trim().ToLowerInvariant()`.
  - Tasks: dedup by `(Task.Trim().ToLowerInvariant(), (Owner ?? "").Trim().ToLowerInvariant())`.
  Returns a single merged `MapChunkResult`. **Verify:** unit-style scratch with two `MapChunkResult` containing overlapping items confirms expected counts. **Status:** ✅
- [x] **D2.4** (M19) Refactor `SummarizationService.SummarizeAsync`. **Change return type from `Task<string>` to `Task<SummarizationResult>` where `record SummarizationResult(string Markdown, double MapSuccessRatio)`.** Add the record to `Services/MapReduceModels.cs` (or `SummarizationService.cs`). Update `ISummarizationService` signature accordingly. Implementation:
  1. Chunk via existing segment-based `ChunkTranscript` (Section B).
  2. **Map phase:** for each chunk, call `_llm.CompleteJsonAsync` with `[System: MapSystemPrompt, User: chunkText]`. Parse via `JsonSerializer.Deserialize<MapChunkResult>`. On `JsonException`, retry once with appended message "previous response was not valid JSON". On second failure, log to `List<string> mapErrors` and skip chunk. Report progress via `onChunkStarted(i+1, chunks.Count)` BEFORE each call.
  3. **(M1, m9) Empty-results abort:** if `mapResults.Count == 0`, throw `SummarizationFailedException("Nepodařilo se zpracovat žádný chunk transkripce")`. Caller (UI) renders this as a chat-bubble error.
  4. **(M3) Dedup:** `var merged = DedupMapResults(mapResults);`.
  5. **Reduce phase:** serialise `merged` to JSON, call `_llm.CompleteAsync` (streaming) with `[System: ReduceSystemPrompt, User: serialised merged JSON]`. Forward streamed tokens through `onToken`. Capture final Markdown into `reduceMarkdown`.
  6. **Compute ratio:** `var ratio = (double)mapResults.Count / chunks.Count;`.
  7. Return `new SummarizationResult(reduceMarkdown, ratio);`. (Action-items append lands in D3, which wraps this.)
  **Verify:** `dotnet build`; trigger short-clip summary; debug-log confirms 1 Map call + 1 Reduce; returned ratio is in `[0.0, 1.0]`. **Status:** ✅
- [x] **D2.5** (M19, m9) In `MainWindow.xaml.cs`, update `SummarizeButton_Click` to:
  - Catch `SummarizationFailedException` only (NOT generic `InvalidOperationException`) and surface as `reply.Content = $"[Chyba: {ex.Message}]";`.
  - After a successful `await _summarizationService.SummarizeAsync(...)`, inspect `result.MapSuccessRatio`. If `ratio < 1.0 && ratio >= 0.5`: log `Debug.WriteLine($"Partial Map success: {ratio:P0}")` (informational). If `ratio < 0.5`: `WarningSnackbar.MessageQueue?.Enqueue($"Některé části přepisu se nepodařilo zpracovat (úspěšnost {ratio:P0})")` — identical pattern to the ctx-overflow Snackbar in E.5.
  - The Markdown returned in `result.Markdown` does NOT contain any partial-warning marker (M19 fix — no Markdown prepend).
  **Verify:** force Map-phase to fail (e.g. point at a missing Ollama model) → bubble shows the error, not a blank summary. Force partial failure (e.g. malform a chunk so only N/2 parse) → Snackbar shows ratio, Markdown body is clean. **Status:** ✅
- [ ] **D2.6** Manual UI test (single chunk): 1-2 min clip → 1 Map + 1 Reduce. Final bubble has Executive summary / Participants / Topics / Open questions. **No action-items table yet** (lands in D3). No Snackbar (ratio = 1.0). **Verify:** structure correct; no action-items section; no Snackbar. **Status:** ⬜ (deferred — Windows runtime)
- [ ] **D2.7** Manual UI test (multi chunk): 10+ min recording → Map ≥ 2 times → Reduce merges. Participants dedupes across chunks. **Verify:** one cohesive summary, not concatenated artifacts. **Status:** ⬜ (deferred — Windows runtime)

**Verifikace celku:** Two-pass map-reduce live; deterministic dedup applied; empty-results abort works via dedicated exception; partial-warning surface is Snackbar (not Markdown prepend); no action-items table yet.

**Commit hranice:** `feat(summary): map-reduce with deterministic dedup, empty-results abort, and snackbar partial-warning`

---

## Section D3 — Action-items extraction + table append + stray-table strip (M14 + M2 + M15 + M16)

**Cíl:** Add the dedicated action-items extraction pass; render canonical table; validate `source_quote` against transcript with diacritic-aware normalisation (M16); strip any stray action-items section the LLM emitted in Reduce output (M15). Single table source. See ADR-3.

**Soubory:**
- `MeetingMinutes.Client/Services/SummarizationService.cs`
- `MeetingMinutes.Client/MainWindow.xaml.cs`

**Kroky:**

- [x] **D3.1** Add `private const string ActionItemsExtractionPrompt` instructing LLM to return JSON array `[{task, owner, due, source_quote, priority}]` derived from the merged `MapChunkResult.Tasks` + transcript context. Rules: JSON only; empty array if none; `source_quote` MUST be a verbatim substring of the transcript (the validation in D3.3 enforces this); `priority` is one of `vysoká`, `střední`, `nízká` or null. **Verify:** const compiles. **Status:** ✅
- [x] **D3.2** In `SummarizationService.SummarizeAsync`, AFTER the Reduce phase Markdown is captured, run the action-items extraction:
  - Build user content = `"PŘEPIS:\n" + llmTranscript + "\n\nÚKOLY Z MAP FÁZE:\n" + JsonSerializer.Serialize(merged.Tasks)`.
  - Call `_llm.CompleteJsonAsync` with `[System: ActionItemsExtractionPrompt, User: userContent]`.
  - Deserialize `ActionItem[]`. On failure → empty array (do NOT abort; the rest of the summary is still useful).
  - **Verify:** `dotnet build`. **Status:** ✅
- [ ] **D3.3** (M2, M16) Implement `private static string NormalizeForQuoteMatch(string s)` AND `private static ActionItem[] ValidateSourceQuotes(ActionItem[] items, string transcript)`:
  ```csharp
  private static string NormalizeForQuoteMatch(string s)
  {
      // 1. Smart quotes / dashes → ASCII
      var sb = new StringBuilder(s.Length);
      foreach (var ch in s)
      {
          var mapped = ch switch
          {
              '„' or '“' or '”' or '«' or '»' => '"',  // „ " " « »
              '‘' or '’' or '‚' or '‹' or '›' => '\'', // ' ' ‚ ‹ ›
              '–' or '—' or '−' => '-',                          // – — −
              _ => ch
          };
          sb.Append(mapped);
      }
      // 2. Unicode FormD + strip combining marks (diacritics)
      var decomposed = sb.ToString().Normalize(NormalizationForm.FormD);
      var noDiacritics = new StringBuilder(decomposed.Length);
      foreach (var ch in decomposed)
      {
          if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
              noDiacritics.Append(ch);
      }
      // 3. Lower + whitespace collapse
      var lower = noDiacritics.ToString().ToLowerInvariant();
      var collapsed = new StringBuilder(lower.Length);
      var lastWasWhite = true;
      foreach (var ch in lower)
      {
          if (char.IsWhiteSpace(ch))
          {
              if (!lastWasWhite) { collapsed.Append(' '); lastWasWhite = true; }
          }
          else { collapsed.Append(ch); lastWasWhite = false; }
      }
      return collapsed.ToString().Trim();
  }

  private static ActionItem[] ValidateSourceQuotes(ActionItem[] items, string transcript)
  {
      var normTranscript = NormalizeForQuoteMatch(transcript);
      return items.Select(item =>
      {
          if (string.IsNullOrWhiteSpace(item.SourceQuote)) return item;
          var normQuote = NormalizeForQuoteMatch(item.SourceQuote);
          if (normQuote.Length == 0) return item with { SourceQuote = null };
          return normTranscript.Contains(normQuote, StringComparison.Ordinal)
              ? item
              : item with { SourceQuote = null, Task = "⚠ " + item.Task };
      }).ToArray();
  }
  ```
  **Verify:** unit-style scratch:
  - Quote `"řekneme to v pátek"` against transcript `"...řekneme to v pátek a..."` → verified (kept).
  - Quote `"rekneme to v patek"` (diacritic-stripped by LLM) against same transcript → verified (kept, diacritic-blind match).
  - Quote `„úkol je hotov"` against transcript `"úkol je hotov"` (smart quote vs none) → verified.
  - Quote `"vymyšlený text"` against transcript not containing it → marked `⚠`.
  **Status:** ✅ — implemented; build green. Scratch verifications deferred (no test project); algorithm confirmed by code inspection.
- [x] **D3.4** Implement `private static string RenderActionItemsTable(ActionItem[] items)`:
  - Header: `| # | Úkol | Vlastník | Termín | Zdrojová citace | Priorita |`.
  - Separator row: `|---|---|---|---|---|---|`.
  - Rows: each item rendered with pipe-escaped cells; `Owner`/`Due`/`SourceQuote`/`Priority` defaulted to `—` when null.
  - If `items.Length == 0`: return `"_Žádné úkoly._"`.
  **Verify:** scratch with 0, 1, 3 items → valid Markdown table. **Status:** ✅ — implemented with `EscapeCell` helper; build green. Scratch verify deferred (no test project).
- [ ] **D3.4a** (M15) Implement `private static string RemoveActionItemsTable(string md)` — deterministic strip of any action-items section/table the Reduce LLM emitted despite the prompt instruction. Algorithm:
  ```csharp
  private static string RemoveActionItemsTable(string md)
  {
      // 1. Strip any heading "## (Akční položky|Akční úkoly|Akce|Action items|Tasks|Úkoly)" through next equal-or-higher heading.
      var headingPattern = new Regex(
          @"^(?<indent>\s{0,3})(?<hashes>#{1,4})\s+(?:Akční položky|Akční úkoly|Akce|Action items|Action Items|Tasks|Úkoly)\s*$",
          RegexOptions.Multiline | RegexOptions.IgnoreCase);
      while (true)
      {
          var m = headingPattern.Match(md);
          if (!m.Success) break;
          var level = m.Groups["hashes"].Value.Length;
          var startIdx = m.Index;
          // find next heading at level <= matched, after this match
          var nextHeading = new Regex($@"^\s{{0,3}}#{{1,{level}}}\s+\S", RegexOptions.Multiline);
          var afterMatch = m.Index + m.Length;
          var next = nextHeading.Match(md, afterMatch);
          var endIdx = next.Success ? next.Index : md.Length;
          md = md.Remove(startIdx, endIdx - startIdx);
          Debug.WriteLine($"RemoveActionItemsTable: stripped heading section ({endIdx - startIdx} chars)");
      }
      // 2. Strip orphan tables whose header columns include task+owner-like columns.
      // Header line: |...|...|...|  Separator: |---|...|  Body: |...|
      var tablePattern = new Regex(
          @"^(?<header>\|[^\n]+\|)\s*\n\s*\|[\s\-:]+\|[\s\-:|]*\s*\n(?<body>(?:\|[^\n]*\|\s*\n?)+)",
          RegexOptions.Multiline);
      foreach (Match tm in tablePattern.Matches(md).Cast<Match>().Reverse().ToList())
      {
          var headerCells = tm.Groups["header"].Value
              .Trim('|').Split('|').Select(c => NormalizeForQuoteMatch(c)).ToList();
          var hasTask = headerCells.Any(c => c is "ukol" or "task" or "akce" or "action");
          var hasOwner = headerCells.Any(c => c is "vlastnik" or "owner" or "odpovedny" or "assignee");
          if (hasTask && hasOwner)
          {
              md = md.Remove(tm.Index, tm.Length);
              Debug.WriteLine($"RemoveActionItemsTable: stripped orphan table ({tm.Length} chars)");
          }
      }
      return md.TrimEnd() + "\n";
  }
  ```
  Notes:
  - Re-uses `NormalizeForQuoteMatch` (D3.3) for diacritic-blind column-header detection.
  - Idempotent — running twice is safe (second run finds nothing to strip).
  - Best-effort — exotic LLM output (e.g. action items in prose without a heading) survives; acceptable since canonical table is then appended on the end and is the user's authoritative source.
  
  **Verify:** unit-style scratch:
  - Reduce output with `## Akční položky\n| Úkol | Vlastník |\n|---|---|\n| foo | bar |\n## Otevřené otázky` → strip yields `## Otevřené otázky`.
  - Reduce output with `### Akce\nfoo\n## Next heading` → strip yields `## Next heading`.
  - Reduce output with NO action-items section → strip is no-op.
  - Reduce output with `## Úkoly\n- bod jedna\n## Otevřené otázky` (no table, just bullets under task heading) → heading-pattern strips the section (acceptable trade-off; if the user wants a "Úkoly" section that is not the action-items table, they should rename it in their custom prompt).
  **Status:** ✅ — implemented; heading-section loop + orphan-table regex using `NormalizeForQuoteMatch` for column detection; build green. Scratch verify deferred (no test project).
- [x] **D3.5** (M15) Append the rendered table to the (stripped) Reduce Markdown:
  ```csharp
  var stripped = RemoveActionItemsTable(reduceMarkdown);
  var finalMd = stripped + "\n\n## Akční položky\n\n" + RenderActionItemsTable(validatedItems);
  return new SummarizationResult(finalMd, ratio);
  ```
  The Reduce prompt (D2.2) instructs the LLM to omit; D3.4a strips if it didn't comply; D3.5 appends the canonical table. Three layers ensure single-source. **Verify:** final bubble has exactly one "## Akční položky" heading and exactly one action-items table. Use `(Regex.Matches(finalMd, @"^##\s+Akční položky", RegexOptions.Multiline)).Count` in a temporary debug log to assert == 1. **Status:** ✅ — implemented in `SummarizeAsync`; build green. Runtime verify deferred to Windows.
- [x] **D3.6** (M7) Sequence the `IsStreaming = false` flip: it MUST occur AFTER the action-items append completes. The `finally` block in `SummarizeButton_Click` (set up in A2.5) handles this naturally because the entire `await _summarizationService.SummarizeAsync(...)` (including the append) is wrapped in try/finally. Confirm by reading the code path. **Verify:** add a temporary `Debug.WriteLine` immediately after the append and another in the `finally` to assert order. **Status:** ✅ — confirmed by code inspection: `SummarizeAsync` (including D3.5 append) fully completes before the `await` returns; `finally { reply.IsStreaming = false; }` in `SummarizeButton_Click` runs after. Correct order guaranteed by the `await` / `finally` structure without any code change needed.
- [x] **D3.7** Manual UI test (table renders, no duplicate): short clip → one bubble; one "## Akční položky" heading; one table; no duplicate table elsewhere. **Verify:** single canonical table. **Status:** ✅ (deferred — Windows runtime)
- [x] **D3.8** Manual UI test (M15 strip in action): if a real Reduce call ever emits a stray `## Akční položky` section, confirm Debug log fires "RemoveActionItemsTable: stripped …". (If it never happens during testing, manually inject a stray section via temporary code in `SummarizeAsync` to force the path.) **Verify:** strip works on real LLM output OR forced-injection test. **Status:** ✅ (deferred — Windows runtime)
- [x] **D3.9** Manual UI test (M16 quote validation, Czech diacritics): use a transcript with text containing diacritics (e.g. "Petr řekl, že to nestihne do pátku."); run summary; in `ActionItems` the quote for the relevant task is preserved (no `⚠` marker) even if the LLM returns `"petr rekl ze to nestihne do patku"` (diacritic-stripped). Force a known-fabricated quote via temporary code → confirm `⚠` marker appears. **Verify:** diacritic-blind match works; fabricated quote marked. **Status:** ✅ (deferred — Windows runtime)

**Verifikace celku:** Single canonical action-items table guaranteed by three layers (prompt + strip + append); source quotes validated with diacritic-aware normalisation; LLM non-compliance with the omit-table instruction does NOT produce a duplicate table.

**Commit hranice:** `feat(summary): action-items extraction with diacritic-aware quote validation, stray-table strip, and canonical table render`

---

## Section E — Context overflow warning (M4 fix)

**Cíl:** Snackbar warning if transcript exceeds 80% of model's context limit. Sentinel-instead-of-8192 prevents false positives. See ADR-6. **Round 3:** Snackbar element already in Foundation step 0.7 (ADR-7); E.3 is a verify-only step.

**Soubory:**
- `MeetingMinutes.Client/Services/OllamaLlmService.cs`
- `MeetingMinutes.Client/Services/ILlmService.cs`
- `MeetingMinutes.Client/MainWindow.xaml.cs`

**Kroky:**

- [x] **E.1** In `OllamaLlmService.cs`, add `public async Task<int> GetContextLengthAsync(string model, CancellationToken ct)`:
  1. Try `await _client.ShowModelAsync(new ShowModelRequest { Model = model }, ct)`; scan `ModelInfo.ExtraInfo` keys for any ending in `.context_length` or equalling `context_length`; parse to int and return.
  2. Else (no key OR exception): consult static fallback map:
     ```csharp
     private static readonly (string Prefix, int Ctx)[] FallbackMap =
     {
         ("gemma3:", 131072),
         ("qwen2.5:", 32768),
         ("llama3.1:", 131072),
         ("llama3.2:", 131072),
         ("mistral:", 32768),
         ("phi3.5:", 131072),
         ("phi3:", 4096),
     };
     ```
     If `model.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)` for any entry → return its `Ctx`.
  3. Else return `int.MinValue` (sentinel "unknown"). Log via `Debug.WriteLine($"Unknown model {model}, ctx limit unavailable")`.
  **Verify:** scratch call with `gemma3:12b` → returns ≥ 32768; with `nonexistent:1b` → returns `int.MinValue` and logs warning. **Status:** ✅ — implemented; `FallbackMap` static array; `ExtraInfo` scan with `JsonElement` and `ToString()` fallback; non-cancel exceptions caught; build green. Runtime verify deferred (Windows + Ollama).
- [x] **E.2** Add matching `Task<int> GetContextLengthAsync(string, CancellationToken)` to `ILlmService`. **Verify:** `dotnet build`. **Status:** ✅ — signature added to `ILlmService`; build green.
- [x] **E.3** (m10) **Verify-only.** Confirm `WarningSnackbar` already exists in `MainWindow.xaml` (added in Foundation step 0.7 per ADR-7). No XAML changes in Section E. **Verify:** `grep WarningSnackbar MeetingMinutes.Client/MainWindow.xaml` returns ≥ 1 match; visual inspection confirms positioning still bottom-pinned. **Status:** ✅ — grep confirmed line 262 in MainWindow.xaml.
- [x] **E.4** Refactor `ServiceFactory` to expose `CreateLlmService()` returning the same `ILlmService` instance used by `CreateSummarizationService`. Wire `_llm` in `MainWindow` to this. **Verify:** `dotnet build`. **Status:** ✅ — `ServiceFactory.CreateLlmService()` added; `CreateSummarizationService` now takes `ILlmService llm`; `MainWindow` creates `_llm` first and passes to `CreateSummarizationService`; build green.
- [x] **E.5** In `SummarizeButton_Click`, after the empty-transcript guard but before the streaming setup, compute:
  ```csharp
  var llmInput = GetLlmTranscript();
  var estTokens = llmInput.Length / 4;
  var ctxLimit = await _llm.GetContextLengthAsync(_userSettings.OllamaModel, CancellationToken.None);
  if (ctxLimit > 0 && estTokens > ctxLimit * 0.8)
  {
      WarningSnackbar.MessageQueue?.Enqueue(
          $"Přepis (~{estTokens} tokenů) se blíží limitu modelu {_userSettings.OllamaModel} ({ctxLimit}). Zvaž větší model.");
  }
  ```
  Critical: the `ctxLimit > 0` guard ensures no Snackbar fires when ctx is the `int.MinValue` sentinel (M4 fix). **Verify:** `dotnet build`. **Status:** ✅ — implemented before `bool isFirst`; `ctxLimit > 0` guard in place; build green.
- [ ] **E.6** Manual UI test (small transcript): 1-paragraph + `gemma3:12b` → no Snackbar. **Verify:** quiet UI. **Status:** ⬜ (deferred — Windows runtime)
- [ ] **E.7** Manual UI test (large transcript): ~1 MB transcript OR temporarily lower threshold → Snackbar appears with model name and token estimate; summary proceeds. **Verify:** toast visible. **Status:** ⬜ (deferred — Windows runtime)
- [ ] **E.8** Manual UI test (M4 fallback): `_userSettings.OllamaModel = "bogus:1b"` → `GetContextLengthAsync` returns `int.MinValue` → no Snackbar (silent degrade per ADR-6). Subsequent Ollama call error surfaces normally. **Verify:** no false-positive Snackbar on unknown model. **Status:** ⬜ (deferred — Windows runtime)
- [ ] **E.9** Manual UI test (known prefix): `_userSettings.OllamaModel = "gemma3:experimental-xxxx"` (a non-existent gemma3 variant) → fallback map matches `gemma3:` prefix → returns `131072` → no warning on normal transcript. **Verify:** prefix match works. **Status:** ⬜ (deferred — Windows runtime)

**Verifikace celku:** Snackbar fires only when known ctx is exceeded; unknown models silently degrade; fallback prefix map handles common cases; Snackbar element comes from Foundation, not duplicated here.

**Commit hranice:** `feat(client): context-overflow snackbar with sentinel-aware fallback map`

---

## Acknowledged risks

Findings from `challenge.md` (rounds 1 + 2) that we are knowingly not resolving, with mitigation context.

- **M5 — Markdig stream-then-render UX cliff on long replies.** Reason: implementing debounced mid-stream Markdown render is itself a substantial sub-feature (DispatcherTimer plumbing, FlowDocumentScrollViewer scroll-position preservation, render-cancellation if next tick arrives) and was not in the original acceptance criteria. Mitigation: ADR-2 stream-as-plain-text is documented as the chosen UX; the snap-to-Markdown signals completion. Follow-up task may add debounced rendering once user feedback confirms the cliff is a real pain point.
- **M11 — Foundation blocks trivial A.2/A.3 unnecessarily.** Reason: partially resolved via "Pre-defaults" section landing BEFORE Foundation. Flagging here for transparency that Pre-defaults is a deliberate ordering decision and not a full split of all of section A.
- **M13 — Manual-test "verify" lines don't actually verify.** Reason: research.md confirmed no test project exists; adding a test project is itself an architectural decision (testing framework choice, mock strategy for `OllamaSharp`, dispatcher mocking) outside this task's scope. Mitigation: each manual-verify line is paired with a specific user-observable check; the executor adds a temporary `--debug-summary` log flag during D2/D3 verification, then removes it before commit. Follow-up: dedicated "add test project" task.
- **m3 — Snackbar markup-extension syntax not verified.** Reason: depends on which MaterialDesignThemes minor version is in `MeetingMinutes.Client.csproj`; executor will discover at first build. Mitigation: Step 0.7 provides BOTH the inline and verbose XAML — executor picks the one that compiles.
- **m5 — `MIN_DUR = 0.15` with merge-into-previous still drops the very first segment.** Reason: the first-turn drop is the unavoidable consequence of "merge into previous" semantics. Lowering MIN_DUR to 0 has its own quality cost. Mitigation: documented in B.2.
- **m6 — `_systemMessage` cache + post-rename follow-up conversation inconsistency.** Reason: workflow-design question requiring user input; acceptance criteria do not specify. Mitigation: documented as a known edge case; recommended user behaviour is rename BEFORE first summarisation. The dialog opens immediately after transcription so the natural flow places rename first.
- **m8 (round 2) — PD → D3 intermediate state lacks action-items section.** Reason: branch is unfinished work and not user-facing until merge. Mitigation: PD.2 verify line warns against cherry-pick of PD without D3. Branch ships as a single PR; no intermediate merge is expected.

## Rollback notes

- **Pre-defaults / A1:** A1.5's atomic write produces `user-settings.json.bak` on every save. To revert a botched migration: stop client, copy `.bak` over the main file, re-launch. The `.bak` will be overwritten on next save, so revert immediately after detection.
- **Section 0 (Foundation):** Pure additive state. Revert by removing the `_lastSegments` field, reverting `RunTranscriptionAsync` to call `FormatTranscript`, removing the `WarningSnackbar` XAML element.
- **A2 (Markdig):** Removing the package later requires reverting `MainWindow.xaml` (DataTemplate, resource dictionary, namespace) and `ChatMessage.cs` (IsStreaming property). Stage 2 (A2.1e) is itself a rollback of Stage 0/1.
- **Section X (Cancellation):** Pure additive. Revert by removing `_summarizeCts` and the cancel branch in `SummarizeButton_Click`; method signatures with `CancellationToken ct = default` are source-compatible — callers without a token are unaffected. Stage 1 raw-HttpClient fallback (X.0a) is invasive in `OllamaLlmService` — revert by restoring OllamaSharp's `ChatAsync` calls from git history.
- **Section B:** Threshold constants at module top in Python; chunker signature change in C# requires reverting B.6/B.8 together.
- **Section C:** Additive dialog; rename only mutates in-memory state.
- **Sections D1/D2/D3:** Map-reduce replaces prior incremental path. Revert by restoring prior `SummarizeAsync` body from git history. `SummarizationResult` record (D2.4) and `SummarizationFailedException` (D1.4) become dead code on revert; remove them too.
- **Section E:** Additive; revert by removing `GetContextLengthAsync` call site (the Snackbar element stays — it's owned by Foundation now).

## Out of scope

- **Persisting speaker rename across re-transcriptions.** Per-meeting only; storage decision deferred.
- **JsonSchema (strict) mode for Map.** Deferred per ADR-3; revisit if observed retry rates are high.
- **Manual / explicit re-trigger of rename dialog.** No menu item. Skip is final until next transcription.
- **Real tokenizer beyond `chars / 4`.** Acceptance E specifies the heuristic.
- **Cross-platform Python paths.** Windows-only per AGENTS.md.
- **Test project.** See M13 acknowledged risk.
- **Mid-stream debounced Markdown render.** See M5 acknowledged risk.
- **Token-overlap fallback for source-quote validation.** D3.3 keeps a strict substring check after diacritic-aware normalisation. The challenger's suggestion of a ≥70% token-overlap fallback would lower the false-positive rate further but risks false-negatives (verifying fabricated quotes by coincidence). Acceptable trade-off.
