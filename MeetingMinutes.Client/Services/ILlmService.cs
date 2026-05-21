using OllamaSharp.Models.Chat;

namespace MeetingMinutes.Services;

public record LlmMessage(ChatRole Role, string Content);

public interface ILlmService
{
    Task<string> CompleteAsync(
        IReadOnlyList<LlmMessage> messages,
        string model,
        Action<string> onChunk,
        CancellationToken cancellationToken = default);

    Task<string> CompleteJsonAsync(
        IReadOnlyList<LlmMessage> messages,
        string model,
        CancellationToken ct = default);

    Task<int> GetContextLengthAsync(string model, CancellationToken ct = default);
}
