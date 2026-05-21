namespace MeetingMinutes.Services;

public record SummarizationRequest(
    string Transcript,
    string SystemPrompt,
    string Model,
    IReadOnlyList<TranscriptSegment>? Segments = null);

public record SummarizationResult(string Markdown, double MapSuccessRatio);

public interface ISummarizationService
{
    Task<SummarizationResult> SummarizeAsync(
        SummarizationRequest request,
        Action<int, int> onChunkStarted,
        Action<string> onToken,
        CancellationToken cancellationToken = default);

    Task<string> ContinueAsync(
        IReadOnlyList<LlmMessage> messages,
        string model,
        Action<string> onToken,
        CancellationToken cancellationToken = default);
}
