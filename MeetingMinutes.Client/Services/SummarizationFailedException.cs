namespace MeetingMinutes.Services;

public class SummarizationFailedException : Exception
{
    public SummarizationFailedException(string message) : base(message) { }
}
