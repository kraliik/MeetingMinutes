using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OllamaSharp.Models.Chat;

namespace MeetingMinutes.Services;

public class SummarizationService(ILlmService llm) : ISummarizationService
{
    private const int CharsPerToken = 4;

    private const string MapSystemPrompt =
        "Jsi asistent pro analýzu schůzek. Přečti část přepisu schůzky a vrať POUZE JSON objekt " +
        "odpovídající tomuto schématu (žádný jiný text, žádné Markdown ohrady):\n" +
        "{\n" +
        "  \"participants\": [\"jméno\"],\n" +
        "  \"topics\": [{\"title\": \"název tématu\", \"points\": [\"klíčový bod\"]}],\n" +
        "  \"decisions\": [{\"decision\": \"text rozhodnutí\"}],\n" +
        "  \"tasks\": [{\"task\": \"popis úkolu\", \"owner\": \"zodpovědná osoba nebo null\", \"due\": \"termín nebo null\"}]\n" +
        "}\n\n" +
        "Pravidla:\n" +
        "- Odpovídej POUZE platným JSON objektem. Žádná próza, žádné Markdown ohrady, žádný jiný text.\n" +
        "- Použij přesně pole: participants, topics, decisions, tasks.\n" +
        "- U topics vnořuj pole points.\n" +
        "- Pokud nejsou žádné položky dané kategorie, vrať prázdné pole.\n" +
        "- Jazyk výstupu: čeština.\n" +
        "- do participants zapisuj POUZE skutečná jména osob. " +
        "Pokud mluvčí jsou označeni jako SPEAKER_XX (anonymní), vrať prázdné pole.";

    private const string ReduceSystemPrompt =
        "Jsi asistent pro tvorbu zápisů ze schůzek. Obdržíš JSON pole výsledků z map fáze analýzy " +
        "přepisu schůzky. Vytvoř jedno konsolidované Markdown shrnutí v češtině.\n\n" +
        "Struktura shrnutí (povinné sekce v tomto pořadí):\n" +
        "1. `# Souhrn schůzky` — nadpis\n" +
        "2. Výkonný souhrn — odstavec 2–3 věty.\n" +
        "3. `## Účastníci` — odrážkový seznam skutečných jmen (SPEAKER_XX vynechej).\n" +
        "4. `## Témata` — pro každé téma `### Téma N: {název}` se třemi podsekemi:\n" +
        "   - `**Shrnutí:**` (1–2 věty)\n" +
        "   - `**Klíčové body:**` (odrážky)\n" +
        "   - `**Rozhodnutí:**` (odrážky; \"není uvedeno\" pokud žádné)\n" +
        "5. `## Otevřené otázky` — odrážkový seznam; \"není uvedeno\" pokud žádné.\n\n" +
        "KRITICKÉ OMEZENÍ: Shrnutí NESMÍ obsahovat sekci ani tabulku s akčními položkami. " +
        "NEemituj nadpisy ani tabulky obsahující slova: " +
        "\"Akční položky\", \"Akční úkoly\", \"Akce\", \"Action items\", \"Tasks\", \"Úkoly\". " +
        "Sekci s úkoly/akcemi ZCELA vynech — bude přidána samostatně.\n\n" +
        "Pravidla:\n" +
        "- Pouze čeština.\n" +
        "- Nevymýšlej informace, které nejsou v datech.\n" +
        "- Kde data chybí, piš \"není uvedeno\".\n" +
        "- Nezobecňuj — vycházej pouze z poskytnutých dat.";

    private const string ActionItemsExtractionPrompt =
        "Jsi asistent pro extrakci akčních položek ze záznamu schůzky. " +
        "Obdržíš přepis schůzky a seznam úkolů identifikovaných v map fázi. " +
        "Vrať POUZE JSON pole akčních položek odpovídající tomuto schématu (žádný jiný text, žádné Markdown ohrady):\n" +
        "[{\"task\": \"popis úkolu\", \"owner\": \"zodpovědná osoba nebo null\", " +
        "\"due\": \"termín nebo null\", " +
        "\"source_quote\": \"doslovný citát z přepisu prokazující úkol nebo null\", " +
        "\"priority\": \"vysoká|střední|nízká nebo null\"}]\n\n" +
        "Pravidla:\n" +
        "- Odpovídej POUZE platným JSON polem. Žádná próza, žádné Markdown ohrady.\n" +
        "- source_quote MUSÍ být doslovný úryvek z poskytnutého přepisu. Pokud vhodný citát nenajdeš, vrať null.\n" +
        "- Pokud nejsou žádné akční položky, vrať prázdné pole: []\n" +
        "- Jazyk výstupu: čeština.";

    public async Task<SummarizationResult> SummarizeAsync(
        SummarizationRequest request,
        Action<int, int> onChunkStarted,
        Action<string> onToken,
        CancellationToken cancellationToken = default)
    {
        var chunks = request.Segments?.Count > 0
            ? ChunkTranscript(request.Segments)
            : ChunkTranscriptString(request.Transcript);

        var mapResults = new List<MapChunkResult>();

        for (int i = 0; i < chunks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            onChunkStarted(i + 1, chunks.Count);

            var messages = new List<LlmMessage>
            {
                new(ChatRole.System, MapSystemPrompt),
                new(ChatRole.User, chunks[i]),
            };

            MapChunkResult? result = null;
            try
            {
                var json = await llm.CompleteJsonAsync(messages, request.Model, cancellationToken);
                result = JsonSerializer.Deserialize<MapChunkResult>(json);
            }
            catch (JsonException)
            {
                try
                {
                    var retryMessages = new List<LlmMessage>(messages)
                    {
                        new(ChatRole.User, "respond with valid JSON only")
                    };
                    var retryJson = await llm.CompleteJsonAsync(retryMessages, request.Model, cancellationToken);
                    result = JsonSerializer.Deserialize<MapChunkResult>(retryJson);
                }
                catch (Exception retryEx)
                {
                    Debug.WriteLine($"Map chunk {i + 1}/{chunks.Count} failed after retry: {retryEx.Message}");
                }
            }

            if (result != null)
                mapResults.Add(result);
        }

        if (mapResults.Count == 0)
            throw new SummarizationFailedException("Nepodařilo se zpracovat žádný chunk transkripce");

        var merged = DedupMapResults(mapResults);

        var mergedJson = JsonSerializer.Serialize(merged);
        var reduceMessages = new List<LlmMessage>
        {
            new(ChatRole.System, ReduceSystemPrompt),
            new(ChatRole.User, mergedJson),
        };

        var reduceMarkdown = await llm.CompleteAsync(
            reduceMessages, request.Model, onToken, cancellationToken);

        // D3.2 — action-items extraction
        ActionItem[] actionItems = Array.Empty<ActionItem>();
        try
        {
            var llmTranscript = request.Segments?.Count > 0
                ? string.Join("\n", request.Segments.Select(s => $"{s.Speaker}: {s.Text}"))
                : request.Transcript;
            var extractionUserContent =
                "PŘEPIS:\n" + llmTranscript +
                "\n\nÚKOLY Z MAP FÁZE:\n" + JsonSerializer.Serialize(merged.Tasks);
            var extractionMessages = new List<LlmMessage>
            {
                new(ChatRole.System, ActionItemsExtractionPrompt),
                new(ChatRole.User, extractionUserContent),
            };
            var extractionJson = await llm.CompleteJsonAsync(extractionMessages, request.Model, cancellationToken);
            actionItems = JsonSerializer.Deserialize<ActionItem[]>(extractionJson) ?? Array.Empty<ActionItem>();
            // D3.3 — source-quote validation
            actionItems = ValidateSourceQuotes(actionItems, llmTranscript);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Action-items extraction failed: {ex.Message}");
        }

        // D3.5 — strip stray table, append canonical table
        var stripped = RemoveActionItemsTable(reduceMarkdown);
        var finalMd = stripped + "\n\n## Akční položky\n\n" + RenderActionItemsTable(actionItems);

        var ratio = (double)mapResults.Count / chunks.Count;
        return new SummarizationResult(finalMd, ratio);
    }

    private static MapChunkResult DedupMapResults(IReadOnlyList<MapChunkResult> results)
    {
        var participants = results
            .SelectMany(r => r.Participants ?? Array.Empty<string>())
            .Where(p => p is not null)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .GroupBy(p => p.ToLowerInvariant())
            .Select(g => g.First())
            .ToArray();

        var topicsByKey = new Dictionary<string, (string Title, List<string> Points)>();
        foreach (var r in results)
        {
            foreach (var t in r.Topics ?? Array.Empty<MapTopic>())
            {
                if (t is null || string.IsNullOrWhiteSpace(t.Title)) continue;
                var key = t.Title.Trim().ToLowerInvariant();
                if (!topicsByKey.TryGetValue(key, out var entry))
                {
                    entry = (t.Title.Trim(), new List<string>());
                    topicsByKey[key] = entry;
                }
                entry.Points.AddRange(t.Points ?? Array.Empty<string>());
            }
        }
        var topics = topicsByKey.Values
            .Select(e => new MapTopic(e.Title, e.Points.ToArray()))
            .ToArray();

        var decisions = results
            .SelectMany(r => r.Decisions ?? Array.Empty<MapDecision>())
            .Where(d => d is not null && !string.IsNullOrWhiteSpace(d.Decision))
            .GroupBy(d => d.Decision.Trim().ToLowerInvariant())
            .Select(g => g.First())
            .ToArray();

        var tasks = results
            .SelectMany(r => r.Tasks ?? Array.Empty<MapTask>())
            .Where(t => t is not null && !string.IsNullOrWhiteSpace(t.Task))
            .GroupBy(t => (t.Task.Trim().ToLowerInvariant(), (t.Owner ?? "").Trim().ToLowerInvariant()))
            .Select(g => g.First())
            .ToArray();

        return new MapChunkResult(participants, topics, decisions, tasks);
    }

    // D3.3 (M16) — diacritic-blind normalisation for source-quote matching
    private static string NormalizeForQuoteMatch(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            var mapped = ch switch
            {
                '„' or '“' or '”' or '«' or '»' => '"',
                '‘' or '’' or '‚' or '‹' or '›' => '\'',
                '–' or '—' or '−' => '-',
                '…' => ' ',
                _ => ch
            };
            sb.Append(mapped);
        }

        var decomposed = sb.ToString().Normalize(NormalizationForm.FormD);
        var noDiacritics = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                noDiacritics.Append(ch);
        }

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

    // D3.4 — render action items as a Markdown table
    private static string RenderActionItemsTable(ActionItem[] items)
    {
        if (items.Length == 0)
            return "_Žádné úkoly._";

        var sb = new StringBuilder();
        sb.AppendLine("| # | Úkol | Vlastník | Termín | Zdrojová citace | Priorita |");
        sb.AppendLine("|---|---|---|---|---|---|");
        for (int i = 0; i < items.Length; i++)
        {
            var it = items[i];
            sb.AppendLine(
                $"| {i + 1} " +
                $"| {EscapeCell(it.Task)} " +
                $"| {EscapeCell(it.Owner)} " +
                $"| {EscapeCell(it.Due)} " +
                $"| {EscapeCell(it.SourceQuote)} " +
                $"| {EscapeCell(it.Priority)} |");
        }
        return sb.ToString().TrimEnd();
    }

    private static string EscapeCell(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value.Replace("|", "\\|");

    // D3.4a (M15) — strip stray action-items section/table from Reduce output
    private static string RemoveActionItemsTable(string md)
    {
        var headingPattern = new Regex(
            @"^(?<indent>\s{0,3})(?<hashes>#{1,4})\s+(?:Akční položky|Akční úkoly|Akce|Action items|Action Items|Tasks|Úkoly)\s*$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        while (true)
        {
            var m = headingPattern.Match(md);
            if (!m.Success) break;
            var level = m.Groups["hashes"].Value.Length;
            var startIdx = m.Index;
            var nextHeadingPattern = new Regex($@"^\s{{0,3}}#{{1,{level}}}\s+\S", RegexOptions.Multiline);
            var afterMatch = m.Index + m.Length;
            var next = nextHeadingPattern.Match(md, afterMatch);
            var endIdx = next.Success ? next.Index : md.Length;
            var removed = endIdx - startIdx;
            md = md.Remove(startIdx, removed);
            Debug.WriteLine($"RemoveActionItemsTable: stripped heading section ({removed} chars)");
        }

        var tablePattern = new Regex(
            @"^(?<header>\|[^\n]+\|)\s*\n\s*\|[\s\-:]+\|[\s\-:|]*\s*\n(?<body>(?:\|[^\n]*\|\s*\n?)+)",
            RegexOptions.Multiline);

        foreach (Match tm in tablePattern.Matches(md).Cast<Match>().Reverse().ToList())
        {
            var headerCells = tm.Groups["header"].Value
                .Trim('|').Split('|')
                .Select(c => NormalizeForQuoteMatch(c))
                .ToList();
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

    public Task<string> ContinueAsync(
        IReadOnlyList<LlmMessage> messages,
        string model,
        Action<string> onToken,
        CancellationToken cancellationToken = default) =>
        llm.CompleteAsync(messages, model, onToken, cancellationToken);

    private static List<string> ChunkTranscriptString(string transcript, int maxTokens = 4000)
    {
        int maxChars = maxTokens * CharsPerToken;
        var lines = transcript.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        var current = new StringBuilder();

        foreach (var line in lines)
        {
            if (current.Length > 0 && current.Length + line.Length + 1 > maxChars)
            {
                chunks.Add(current.ToString().TrimEnd());
                current.Clear();
            }
            current.AppendLine(line);
        }

        if (current.Length > 0)
            chunks.Add(current.ToString().TrimEnd());

        return chunks;
    }

    private static List<string> ChunkTranscript(
        IReadOnlyList<TranscriptSegment> segments,
        int maxTokens = 4000,
        int overlapTurns = 2)
    {
        int maxChars = maxTokens * CharsPerToken;
        var chunks = new List<string>();
        var current = new StringBuilder();
        var currentSegments = new List<TranscriptSegment>();

        foreach (var seg in segments)
        {
            var line = $"{seg.Speaker}: {seg.Text}";
            if (current.Length > 0 && current.Length + line.Length + 1 > maxChars)
            {
                chunks.Add(current.ToString().TrimEnd());
                current.Clear();
                var overlap = currentSegments.Count >= overlapTurns
                    ? currentSegments.GetRange(currentSegments.Count - overlapTurns, overlapTurns)
                    : new List<TranscriptSegment>(currentSegments);
                currentSegments.Clear();
                foreach (var o in overlap)
                {
                    var ol = $"{o.Speaker}: {o.Text}";
                    current.AppendLine(ol);
                    currentSegments.Add(o);
                }
            }
            current.AppendLine(line);
            currentSegments.Add(seg);
        }

        if (current.Length > 0)
            chunks.Add(current.ToString().TrimEnd());

        return chunks;
    }
}
