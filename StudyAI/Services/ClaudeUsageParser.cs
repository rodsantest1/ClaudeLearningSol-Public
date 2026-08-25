using System.Text.Json;

namespace StudyAI.Services;

// Pure extraction of token usage from a Messages API response body. Shared by
// both GetQuestionAsync and GradeAsync since the top-level "usage" field has
// the same shape regardless of what's in "content" (text vs. a tool call).
// Returns nulls instead of throwing when the field is missing or the body
// isn't valid JSON — usage is a nice-to-have for the session stats display,
// not something that should ever break a question or a grade over it.
public static class ClaudeUsageParser
{
    public static (int? InputTokens, int? OutputTokens) Parse(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (!doc.RootElement.TryGetProperty("usage", out var usage))
                return (null, null);

            int? inputTokens = usage.TryGetProperty("input_tokens", out var input)
                ? input.GetInt32()
                : null;
            int? outputTokens = usage.TryGetProperty("output_tokens", out var output)
                ? output.GetInt32()
                : null;

            return (inputTokens, outputTokens);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}
