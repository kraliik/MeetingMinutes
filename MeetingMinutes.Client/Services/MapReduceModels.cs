using System.Text.Json.Serialization;

namespace MeetingMinutes.Services;

public record MapChunkResult(
    [property: JsonPropertyName("participants")] string[] Participants,
    [property: JsonPropertyName("topics")] MapTopic[] Topics,
    [property: JsonPropertyName("decisions")] MapDecision[] Decisions,
    [property: JsonPropertyName("tasks")] MapTask[] Tasks);

public record MapTopic(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("points")] string[] Points);

public record MapDecision(
    [property: JsonPropertyName("decision")] string Decision);

public record MapTask(
    [property: JsonPropertyName("task")] string Task,
    [property: JsonPropertyName("owner")] string? Owner,
    [property: JsonPropertyName("due")] string? Due);

public record ActionItem(
    [property: JsonPropertyName("task")] string Task,
    [property: JsonPropertyName("owner")] string? Owner,
    [property: JsonPropertyName("due")] string? Due,
    [property: JsonPropertyName("source_quote")] string? SourceQuote,
    [property: JsonPropertyName("priority")] string? Priority);
