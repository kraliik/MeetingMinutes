using System.Diagnostics;
using System.Net.Http;
using System.Text;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace MeetingMinutes.Services;

public class OllamaLlmService : ILlmService
{
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

    private static readonly HttpClient _http = new()
    {
        BaseAddress = new Uri("http://localhost:11434"),
        Timeout = Timeout.InfiniteTimeSpan
    };
    private readonly OllamaApiClient _client = new(_http);

    public async Task<string> CompleteAsync(
        IReadOnlyList<LlmMessage> messages,
        string model,
        Action<string> onChunk,
        CancellationToken cancellationToken = default)
    {
        var request = new ChatRequest
        {
            Model = model,
            Messages = messages.Select(m => new Message(m.Role, m.Content)).ToList(),
            Stream = true,
            Options = new RequestOptions { Temperature = 0.3f, NumCtx = 65536 },
        };

        var sb = new StringBuilder();
        await foreach (var chunk in _client.ChatAsync(request, cancellationToken))
        {
            var content = chunk?.Message?.Content;
            if (!string.IsNullOrEmpty(content))
            {
                onChunk(content);
                sb.Append(content);
            }
        }
        return sb.ToString();
    }

    public async Task<string> CompleteJsonAsync(
        IReadOnlyList<LlmMessage> messages,
        string model,
        CancellationToken ct = default)
    {
        // Format property is System.Object — pass "json" string per OllamaSharp 5.x API
        var request = new ChatRequest
        {
            Model = model,
            Messages = messages.Select(m => new Message(m.Role, m.Content)).ToList(),
            Stream = false,
            Format = "json",
            Options = new RequestOptions { Temperature = 0.3f, NumCtx = 65536 },
        };

        var sb = new StringBuilder();
        await foreach (var chunk in _client.ChatAsync(request, ct))
        {
            var content = chunk?.Message?.Content;
            if (!string.IsNullOrEmpty(content))
                sb.Append(content);
        }
        return sb.ToString();
    }

    public async Task<int> GetContextLengthAsync(string model, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.ShowModelAsync(new ShowModelRequest { Model = model }, ct);
            var extra = response?.Info?.ExtraInfo;
            if (extra != null)
            {
                foreach (var kv in extra)
                {
                    if (kv.Key.EndsWith(".context_length", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(kv.Key, "context_length", StringComparison.OrdinalIgnoreCase))
                    {
                        if (kv.Value is System.Text.Json.JsonElement je && je.TryGetInt32(out var ctx))
                            return ctx;
                        if (int.TryParse(kv.Value?.ToString(), out var parsed))
                            return parsed;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Debug.WriteLine($"GetContextLengthAsync: ShowModelAsync failed for {model}: {ex.Message}");
        }

        foreach (var (prefix, ctx) in FallbackMap)
        {
            if (model.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return ctx;
        }

        Debug.WriteLine($"Unknown model {model}, ctx limit unavailable");
        return int.MinValue;
    }
}
