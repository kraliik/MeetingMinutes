namespace MeetingMinutes.Services;

internal static class ServiceFactory
{
    public static ITranscriptionService CreateTranscriptionService() => new LocalPythonTranscriptionService();

    public static ILlmService CreateLlmService() => new OllamaLlmService();

    public static ISummarizationService CreateSummarizationService(ILlmService llm) => new SummarizationService(llm);
}
